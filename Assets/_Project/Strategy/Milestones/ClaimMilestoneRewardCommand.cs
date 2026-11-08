using System;
using Apogee.Core;

namespace Apogee.Strategy.Milestones
{
    /// <summary>
    /// Command to claim rewards for an achieved campaign milestone.
    /// Grants budget, reputation, and knowledge points, and unlocks subsequent milestones.
    /// Fully supports symmetric Undo/Redo.
    /// GDD §4.4, §12.2, §19.14, §21 M6-01.
    /// </summary>
    public sealed class ClaimMilestoneRewardCommand : CommandBase
    {
        public override string Name => "ClaimMilestoneRewardCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.LeadEngineer | PlayerRole.ScienceDirector;

        public override bool CanUndo => true;

        public CampaignState Campaign { get; }
        public string MilestoneId { get; }

        private MilestoneStatus _previousStatus;
        private double _previousBudget;
        private double _previousReputation;
        private double _previousKnowledgePoints;
        private bool _hasExecuted;

        public ClaimMilestoneRewardCommand(CampaignState campaign, string milestoneId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MilestoneId = milestoneId ?? throw new ArgumentNullException(nameof(milestoneId));
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) kilometre taşı ödülü talep etme yetkisine sahip değil. Gerekli: {RequiredRoles}.");
            }

            if (Campaign.Milestones == null)
            {
                return CommandValidationResult.Fail("Kampanyada kilometre taşı yöneticisi (Milestones) başlatılmamış.");
            }

            if (!Campaign.Milestones.TryGetMilestone(MilestoneId, out var progress))
            {
                return CommandValidationResult.Fail($"Kilometre taşı bulunamadı: '{MilestoneId}'.");
            }

            if (progress.Status != MilestoneStatus.CompletedPendingClaim)
            {
                return CommandValidationResult.Fail(
                    $"Kilometre taşı '{progress.Record.Title}' ödül almaya hazır değil (Mevcut durum: {progress.Status}).");
            }

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            if (!Campaign.Milestones.TryGetMilestone(MilestoneId, out var progress))
            {
                return CommandExecutionResult.Failed($"Kilometre taşı bulunamadı: '{MilestoneId}'.");
            }

            _previousStatus = progress.Status;
            _previousBudget = Campaign.Budget;
            _previousReputation = Campaign.Reputation;
            _previousKnowledgePoints = Campaign.KnowledgePoints;

            // Apply rewards
            Campaign.Budget += progress.Record.RewardBudget;
            Campaign.Reputation = Math.Min(100.0, Campaign.Reputation + progress.Record.RewardReputation);
            Campaign.KnowledgePoints += progress.Record.RewardKnowledgePoints;

            // Mark claimed
            Campaign.Milestones.MarkClaimed(MilestoneId);
            _hasExecuted = true;

            // Record to ledger if economy manager exists
            if (Campaign.Economy != null && progress.Record.RewardBudget > 0)
            {
                Campaign.Economy.RecordTransaction(
                    date: Campaign.Time?.CurrentDate ?? new GameDate(0),
                    type: FinancialRecordType.MissionReward,
                    amount: progress.Record.RewardBudget,
                    resultingBudget: Campaign.Budget,
                    description: $"Kilometre taşı ödülü: {progress.Record.Title}");
            }

            return CommandExecutionResult.Succeeded(
                $"'{progress.Record.Title}' kilometre taşı ödülü alındı: +{progress.Record.RewardBudget:F0} M$ Bütçe, +{progress.Record.RewardReputation:F0} İtibar, +{progress.Record.RewardKnowledgePoints:F0} Bilgi Puanı.");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
            {
                return CommandExecutionResult.Failed("Komut henüz çalıştırılmadığı için geri alınamaz.");
            }

            Campaign.Budget = _previousBudget;
            Campaign.Reputation = _previousReputation;
            Campaign.KnowledgePoints = _previousKnowledgePoints;

            Campaign.Milestones.RevertClaim(MilestoneId, _previousStatus);
            _hasExecuted = false;

            return CommandExecutionResult.Succeeded(
                $"'{MilestoneId}' kilometre taşı ödülü geri alındı; bütçe, itibar ve bilgi puanı eski seviyesine döndürüldü.");
        }
    }
}
