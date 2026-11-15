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
        private double _previousCorrectionFuel;
        private double _previousScienceLoss;
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
            _previousCorrectionFuel = mission.AccumulatedCorrectionFuel;
            _previousScienceLoss = mission.PendingScienceLossGb;
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

            // Apply effects based on tag (GDD §10.2). Every choice must carry a real,
            // asymmetric cost — there is no free option: safe choices sacrifice science
            // data, cheap choices sacrifice hardware margin, greedy choices gamble.
            switch (chosenOption?.EffectTag)
            {
                case "EXTRA_COST":
                    // Paid backup ground-station time: keeps the data link alive
                    Campaign.Budget = Math.Max(0.0, Campaign.Budget - 4.0);
                    Campaign.Economy?.RecordTransaction(
                        Campaign.Time.CurrentDate,
                        FinancialRecordType.EventImpact,
                        -4.0,
                        Campaign.Budget,
                        $"DecisionCard: {card.DecisionId} ({OptionId})");
                    break;

                case "RISK_HARDWARE":
                    // Keep everything running through the hazard; hardware eats the stress
                    mission.AccumulatedReliabilityPenalty =
                        Math.Min(1.0, mission.AccumulatedReliabilityPenalty + 0.05);
                    break;

                case "PANEL_REORIENT":
                    // Re-orienting for power costs a small attitude-correction burn
                    mission.AccumulatedCorrectionFuel += 0.04;
                    break;

                case "SCIENCE_PRIORITY":
                    // Science instrument kept at full duty; unattended housekeeping
                    // subsystems slowly degrade (logs lost = deferred maintenance)
                    mission.AccumulatedReliabilityPenalty =
                        Math.Min(1.0, mission.AccumulatedReliabilityPenalty + 0.03);
                    break;

                case "COMPRESS":
                    // Lossy compression: slight science quality loss, nothing dropped
                    mission.PendingScienceLossGb += 0.2;
                    break;

                case "SAFE_MODE":
                    // Spacecraft protected, but science ops are suspended
                    mission.PendingScienceLossGb += 0.6;
                    break;

                case "INSTRUMENT_OFF":
                    // Power margin restored by shutting the instrument down
                    mission.PendingScienceLossGb += 0.5;
                    break;

                case "DATA_GAP":
                    // Wait for the primary station: data lost + agency embarrassment
                    mission.PendingScienceLossGb += 0.5;
                    Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - 0.5);
                    break;

                case "SCOPE_REDUCTION":
                    // Cut objectives to save propellant: reduced science + public letdown
                    mission.PendingScienceLossGb += 1.0;
                    Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - 1.0);
                    break;

                case "RISKY_REPAIR":
                    ApplyRiskyRepairGamble(mission, card, chosenOption);
                    break;

                default:
                    // Unrecognized tag — no effect (logged via the resolved record)
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
            mission.AccumulatedCorrectionFuel = _previousCorrectionFuel;
            mission.PendingScienceLossGb = _previousScienceLoss;
            mission.PendingDecisionCard = _previousPendingCard;
            mission.Status = _previousStatus;

            if (_previousPendingCard != null)
            {
                mission.ResolvedDecisions.Remove(_previousPendingCard.DecisionId);
            }

            _hasExecuted = false;
            return CommandExecutionResult.Succeeded("Karar seçimi geri alındı.");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Riskli tamir kumarı: %55 başarı şansı. Deterministik — sonuç mission seed +
        /// karar kimliğinden türetilen RNG akışının saf fonksiyonudur (GDD §8.4), böylece
        /// Undo/Redo aynı sonucu yeniden üretir.
        ///
        /// Başarıda mürettebatın hızlı müdahalesi itibar kazandırır; arızada uzay aracı
        /// ek hasar alır ve ajans kamoyunda kötü görünür. Gerçek bir ikilem: garantici
        /// "kapsam küçült" seçeneğine karşı bir bahise girme.
        /// </summary>
        private void ApplyRiskyRepairGamble(
            MissionInstance mission,
            CruiseDecisionCard card,
            CruiseDecisionOption chosenOption)
        {
            var rng = new SeededRng(DeriveGambleSeed(mission, card, chosenOption));
            bool repairSucceeded = rng.NextDouble() < 0.55;

            if (repairSucceeded)
            {
                // Leak patched — quick thinking becomes a small PR win
                Campaign.Reputation = Math.Min(100.0, Campaign.Reputation + 1.0);
            }
            else
            {
                // Repair attempt backfires: additional hardware damage + public criticism
                mission.AccumulatedReliabilityPenalty =
                    Math.Min(1.0, mission.AccumulatedReliabilityPenalty + 0.10);
                Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - 1.0);
            }
        }

        /// <summary>
        /// Gamble seed: mission seed XOR FNV-1a hash of "decisionId/optionId".
        /// Pure function of immutable mission identity → deterministic across undo/redo.
        /// </summary>
        private ulong DeriveGambleSeed(
            MissionInstance mission,
            CruiseDecisionCard card,
            CruiseDecisionOption chosenOption)
        {
            string key = $"{card.DecisionId}/{chosenOption?.OptionId ?? OptionId}";
            ulong hash = 14695981039346656037UL; // FNV-1a 64-bit offset basis
            foreach (char c in key)
            {
                hash ^= c;
                hash *= 1099511628211UL;        // FNV-1a 64-bit prime
            }
            return mission.MissionSeed ^ hash;
        }
    }
}
