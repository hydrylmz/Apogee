using System;
using Apogee.Core;
using Apogee.Strategy.Campaign;

namespace Apogee.Strategy
{
    /// <summary>
    /// Advances the campaign clock by a specified number of game-days.
    /// Deterministic: same tick + same days → same resulting date.
    /// Progresses supplier contracts, manufacturing jobs, and test programs (M4-06, M4-07).
    /// Supports Undo (reverts date, tick, contract, and production states to prior values).
    /// Complies with GDD §6.3, §9.1, §9.3, §17.3 item 3.
    /// </summary>
    public sealed class AdvanceTimeCommand : CommandBase<CampaignState>
    {
        // ---------------------------------------------------------------- parameters
        /// <summary>Number of game-days to advance. Must be ≥ 1.</summary>
        public int Days { get; }

        // ---------------------------------------------------------------- undo snapshot
        private GameDate _previousDate;
        private long     _previousTick;
        private ActiveEventInstance _previousPendingEvent;
        // end-condition snapshot (M6-02)
        private CampaignEndStatus _previousEndStatus;
        private CampaignEndReport _previousEndReport;

        // ---------------------------------------------------------------- construction
        public AdvanceTimeCommand(int days, string id = null) : base(id)
        {
            Days = days;
        }

        // ---------------------------------------------------------------- ICommand
        public override string Name         => nameof(AdvanceTimeCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool CanUndo        => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (Days <= 0)
                return CommandValidationResult.Fail($"AdvanceTimeCommand: Days must be ≥ 1, got {Days}.");

            if (state.Time.IsExpired)
                return CommandValidationResult.Fail("Campaign has already reached its end date (2061-01-01). No further time can pass.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _previousDate = state.Time.CurrentDate;
            _previousTick = state.Time.Tick;
            _previousPendingEvent = state.Events?.PendingEvent;
            _previousEndStatus = state.EndStatus;
            _previousEndReport = state.EndReport;

            GameDate newDate = state.Time.AdvanceDays(Days);
            var fulfilled = state.Suppliers?.AdvanceDays(Days, newDate);
            var completedJobs = state.Production?.AdvanceDays(Days, newDate, state);
            var economyResult = state.Economy?.AdvanceDays(Days, _previousDate, newDate, state);
            var triggeredEvent = state.Events?.AdvanceDays(Days, newDate, state);

            string notices = string.Empty;
            if (fulfilled != null && fulfilled.Count > 0)
                notices += $" [{fulfilled.Count} contract(s) fulfilled]";
            if (completedJobs != null && completedJobs.Count > 0)
                notices += $" [{completedJobs.Count} job(s) completed]";
            if (economyResult.HasValue && economyResult.Value.newRecords.Count > 0)
                notices += $" [{economyResult.Value.newRecords.Count} financial event(s), Net {(economyResult.Value.netBudgetDelta >= 0 ? "+" : "")}{economyResult.Value.netBudgetDelta:F1} M$]";
            if (triggeredEvent != null)
                notices += $" [Event triggered: {triggeredEvent.EventRecord.Id}]";

            // Auto end-condition check (M6-02)
            ApplyEndConditionIfTriggered(state);
            if (state.EndStatus != CampaignEndStatus.Active)
                notices += $" [Campaign ended: {state.EndStatus}]";

            return CommandExecutionResult.Succeeded(
                $"Time advanced by {Days} day(s): {_previousDate} → {newDate} (tick {state.Time.Tick}){notices}.");
        }

        // ---------------------------------------------------------------- auto end-condition check (M6-02)
        private static void ApplyEndConditionIfTriggered(CampaignState state)
        {
            if (state.EndStatus != CampaignEndStatus.Active)
                return; // already ended

            CampaignEndStatus status = CampaignEndEvaluator.EvaluateEndCondition(state);
            if (status == CampaignEndStatus.Active)
                return;

            // Write end-state directly (Undo will restore _previousEndStatus/_previousEndReport snapshots).
            state.EndStatus = status;
            state.EndReport = CampaignEndEvaluator.BuildEndReport(state, status);
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            // Restore internal state directly — CampaignTime exposes ctor for restore,
            // but since we need field-level mutation we use a package-internal approach.
            // Strategy layer owns CampaignTime so this is within the same assembly.
            GameDate dateBeforeUndo = state.Time.CurrentDate;

            state.Economy?.UndoAdvance(_previousDate, state);
            state.Production?.UndoAdvance(_previousDate);
            state.Suppliers?.UndoAdvance(_previousDate);
            state.Events?.UndoAdvance(_previousPendingEvent);
            // Re-create time state from snapshot; CampaignTime is internal-mutable.
            state.Time.UndoAdvance(_previousDate, _previousTick);

            // Restore end-condition state (M6-02)
            state.EndStatus = _previousEndStatus;
            state.EndReport = _previousEndReport;

            return CommandExecutionResult.Succeeded(
                $"AdvanceTimeCommand undone: {dateBeforeUndo} → {state.Time.CurrentDate} (tick {state.Time.Tick}).");
        }
    }
}
