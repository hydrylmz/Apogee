using System;
using Apogee.Core;
using Apogee.Core.Simulation.Operations;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Resolves an active operation event card for a mission in any operational phase
    /// (Cruise, EDL, Science, or Any).
    ///
    /// Applies the chosen option's effects to:
    ///   • Campaign budget and reputation
    ///   • Campaign research points
    ///   • Mission accumulated reliability penalty
    ///   • Mission accumulated science data (bonus or loss)
    ///
    /// Moves the pending event to the operation event log and clears the pending slot.
    /// Fully supports symmetric Undo/Redo.
    ///
    /// GDD §10.2 (Karar Kartları), §10.3, §17.3, §21 M5-05.
    /// </summary>
    public sealed class ResolveOperationEventCommand : CommandBase
    {
        public override string Name => "ResolveOperationEventCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector | PlayerRole.LeadEngineer;

        public override bool CanUndo => true;

        // ─── Inputs ──────────────────────────────────────────────────────────
        public CampaignState Campaign   { get; }
        public string MissionId         { get; }
        public string OptionId          { get; }

        // ─── Undo State ───────────────────────────────────────────────────────
        private double _prevBudget;
        private double _prevReputation;
        private double _prevResearchPoints;
        private double _prevReliabilityPenalty;
        private double _prevDownlinkDataGb;
        private ActiveOperationEvent _prevPendingEvent;
        private bool _hasExecuted;

        public ResolveOperationEventCommand(
            CampaignState campaign,
            string missionId,
            string optionId)
        {
            Campaign  = campaign  ?? throw new ArgumentNullException(nameof(campaign));
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
            OptionId  = optionId  ?? throw new ArgumentNullException(nameof(optionId));
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) operasyon kararını çözümleme yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");

            if (mission.PendingOperationEvent == null)
                return CommandValidationResult.Fail("Mevcut misyon için bekleyen operasyon olayı bulunmuyor.");

            bool optionExists = false;
            foreach (var opt in mission.PendingOperationEvent.Record.Options)
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
            var pending = mission.PendingOperationEvent;

            // Capture undo state
            _prevBudget             = Campaign.Budget;
            _prevReputation         = Campaign.Reputation;
            _prevResearchPoints     = Campaign.ResearchPoints;
            _prevReliabilityPenalty = mission.AccumulatedReliabilityPenalty;
            _prevDownlinkDataGb     = mission.AccumulatedDownlinkDataGb;
            _prevPendingEvent       = pending;

            // Find chosen option
            OperationEventOption chosenOption = null;
            foreach (var opt in pending.Record.Options)
            {
                if (string.Equals(opt.OptionId, OptionId, StringComparison.OrdinalIgnoreCase))
                {
                    chosenOption = opt;
                    break;
                }
            }

            if (chosenOption == null)
                return CommandExecutionResult.Failed($"Seçenek bulunamadı: '{OptionId}'.");

            // Apply effects
            if (Math.Abs(chosenOption.BudgetDelta) > 0.001)
            {
                Campaign.Budget = Math.Max(0.0, Campaign.Budget + chosenOption.BudgetDelta);
                Campaign.Economy?.RecordTransaction(
                    Campaign.Time.CurrentDate,
                    FinancialRecordType.EventImpact,
                    chosenOption.BudgetDelta,
                    Campaign.Budget,
                    $"OperationEvent: {pending.Record.EventId} ({OptionId})");
            }

            if (Math.Abs(chosenOption.ReputationDelta) > 0.001)
            {
                Campaign.Reputation = Math.Max(0.0,
                    Math.Min(100.0, Campaign.Reputation + chosenOption.ReputationDelta));
            }

            if (Math.Abs(chosenOption.ResearchPointsDelta) > 0.001)
            {
                Campaign.ResearchPoints = Math.Max(0.0,
                    Campaign.ResearchPoints + chosenOption.ResearchPointsDelta);
            }

            if (chosenOption.ReliabilityPenalty > 0.0)
            {
                mission.AccumulatedReliabilityPenalty =
                    Math.Min(1.0, mission.AccumulatedReliabilityPenalty + chosenOption.ReliabilityPenalty);
            }

            if (Math.Abs(chosenOption.ScienceDataDeltaGb) > 0.001)
            {
                // Positive delta = bonus data delivered; negative = data lost
                mission.AccumulatedDownlinkDataGb = Math.Max(0.0,
                    mission.AccumulatedDownlinkDataGb + chosenOption.ScienceDataDeltaGb);
            }

            // Record the choice and move to log
            pending.ChosenOptionId = OptionId;
            if (!pending.Record.IsRepeatable)
                mission.UsedNonRepeatableOperationEvents.Add(pending.Record.EventId);

            mission.OperationEventLog.Add(pending);
            mission.PendingOperationEvent = null;

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded(
                $"Operasyon olayı çözümlendi: '{pending.Record.EventId}' -> '{OptionId}'.");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak operasyon kararı yok.");

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandExecutionResult.Failed($"Misyon bulunamadı: '{MissionId}'.");

            // Restore campaign financials
            Campaign.Budget         = _prevBudget;
            Campaign.Reputation     = _prevReputation;
            Campaign.ResearchPoints = _prevResearchPoints;

            // Restore mission fields
            mission.AccumulatedReliabilityPenalty = _prevReliabilityPenalty;
            mission.AccumulatedDownlinkDataGb     = _prevDownlinkDataGb;

            // Remove from log and restore pending
            if (_prevPendingEvent != null)
            {
                mission.OperationEventLog.Remove(_prevPendingEvent);
                _prevPendingEvent.ChosenOptionId = null;

                if (!_prevPendingEvent.Record.IsRepeatable)
                    mission.UsedNonRepeatableOperationEvents.Remove(_prevPendingEvent.Record.EventId);
            }
            mission.PendingOperationEvent = _prevPendingEvent;

            _hasExecuted = false;
            return CommandExecutionResult.Succeeded("Operasyon kararı geri alındı.");
        }
    }
}
