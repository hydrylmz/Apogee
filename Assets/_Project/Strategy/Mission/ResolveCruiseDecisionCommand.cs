using System;
using Apogee.Core;
using Apogee.Core.Simulation.Cruise;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Resolves an active decision card for a mission paused in cruise phase.
    /// Applies chosen option effects (budget cost, reliability hit, science impact)
    /// and transitions mission status back to InCruise.
    /// Supports symmetrical Undo/Redo.
    /// GDD §10.2, §12.2, §17.3.
    /// </summary>
    public sealed class ResolveCruiseDecisionCommand : CommandBase
    {
        public override string Name => "ResolveCruiseDecisionCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector;

        public override bool CanUndo => true;

        public CampaignState Campaign { get; }
        public string MissionId { get; }
        public string OptionId { get; }

        private double _previousBudget;
        private double _previousReputation;
        private double _previousRelPenalty;
        private MissionStatus _previousStatus;
        private CruiseDecisionCard _previousPendingCard;
        private bool _hasExecuted;

        public ResolveCruiseDecisionCommand(
            CampaignState campaign,
            string missionId,
            string optionId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
            OptionId = optionId ?? throw new ArgumentNullException(nameof(optionId));
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) karar çözümleme yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");

            if (mission.Status != MissionStatus.DecisionPending || mission.PendingDecisionCard == null)
                return CommandValidationResult.Fail("Misyon için bekleyen bir karar kartı bulunmuyor.");

            bool optionExists = false;
            foreach (var opt in mission.PendingDecisionCard.Options)
            {
                if (string.Equals(opt.OptionId, OptionId, StringComparison.OrdinalIgnoreCase))
                {
                    optionExists = true;
                    break;
                }
            }

            if (!optionExists)
                return CommandValidationResult.Fail($"Geçersiz seçenek ID'si: '{OptionId}'.");

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
                return CommandExecutionResult.Failed(validation.Reason);

            var mission = Campaign.Missions[MissionId];
            var card = mission.PendingDecisionCard;

            _previousBudget = Campaign.Budget;
            _previousReputation = Campaign.Reputation;
            _previousRelPenalty = mission.AccumulatedReliabilityPenalty;
            _previousStatus = mission.Status;
            _previousPendingCard = card;

            CruiseDecisionOption chosenOption = null;
            foreach (var opt in card.Options)
            {
                if (string.Equals(opt.OptionId, OptionId, StringComparison.OrdinalIgnoreCase))
                {
                    chosenOption = opt;
                    break;
                }
            }

            // Apply effects based on tag (GDD §10.2)
            switch (chosenOption?.EffectTag)
            {
                case "EXTRA_COST":
                    Campaign.Budget = Math.Max(0.0, Campaign.Budget - 2.5); // 2.5 M$ backup comms
                    break;
                case "RISK_HARDWARE":
                    mission.AccumulatedReliabilityPenalty += 0.04;
                    break;
                case "PANEL_REORIENT":
                    mission.AccumulatedCorrectionFuel += 0.03;
                    break;
                case "RISKY_REPAIR":
                    mission.AccumulatedReliabilityPenalty += 0.02;
                    break;
                case "SAFE_MODE":
                case "SCIENCE_PRIORITY":
                case "COMPRESS":
                case "INSTRUMENT_OFF":
                case "DATA_GAP":
                case "SCOPE_REDUCTION":
                default:
                    // Operational trade-offs handled in science delivery / mission scoring
                    break;
            }

            mission.ResolvedDecisions[card.DecisionId] = OptionId;
            mission.PendingDecisionCard = null;
            mission.Status = MissionStatus.InCruise;

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded($"Karar uygulandı: {chosenOption?.LabelKey}");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak bir karar çözümü bulunmuyor.");

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandExecutionResult.Failed($"Misyon bulunamadı: '{MissionId}'.");

            Campaign.Budget = _previousBudget;
            Campaign.Reputation = _previousReputation;
            mission.AccumulatedReliabilityPenalty = _previousRelPenalty;
            mission.PendingDecisionCard = _previousPendingCard;
            mission.Status = _previousStatus;

            if (_previousPendingCard != null)
            {
                mission.ResolvedDecisions.Remove(_previousPendingCard.DecisionId);
            }

            _hasExecuted = false;
            return CommandExecutionResult.Succeeded("Karar seçimi geri alındı.");
        }
    }
}
