using System;
using Apogee.Core;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Executes player selection of an option for an active campaign event (GDD §6.3, §10.2, §17.3 item 2).
    /// Applies deltas to budget, reputation, research points, knowledge points, and supplier relationships.
    /// Symmetrically undoable.
    /// </summary>
    public sealed class ResolveEventOptionCommand : CommandBase<CampaignState>
    {
        public string EventId { get; }
        public string OptionId { get; }

        // ── Undo state ──────────────────────────────────────────
        private ActiveEventInstance _previousPendingEvent;
        private double _previousBudget;
        private double _previousReputation;
        private double _previousResearchPoints;
        private double _previousKnowledgePoints;
        private double _previousSupplierRel;
        private string _affectedSupplierId;
        private EventOutcomeRecord _appliedOutcome;
        private bool _isRepeatable;

        public ResolveEventOptionCommand(string eventId, string optionId, string id = null) : base(id)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            OptionId = optionId ?? throw new ArgumentNullException(nameof(optionId));
        }

        public override string Name => nameof(ResolveEventOptionCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState is null.");

            if (state.Events == null)
                return CommandValidationResult.Fail("Event manager is not initialised on CampaignState.");

            var pending = state.Events.PendingEvent;
            if (pending == null)
                return CommandValidationResult.Fail("No event is currently pending a decision.");

            if (!string.Equals(pending.EventRecord.Id, EventId, StringComparison.OrdinalIgnoreCase))
                return CommandValidationResult.Fail($"Pending event is '{pending.EventRecord.Id}', but command targeted '{EventId}'.");

            var option = pending.EventRecord.Options.Find(o => string.Equals(o.Id, OptionId, StringComparison.OrdinalIgnoreCase));
            if (option == null)
                return CommandValidationResult.Fail($"Option '{OptionId}' is not valid for event '{EventId}'.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _previousPendingEvent = state.Events.PendingEvent;
            _previousBudget = state.Budget;
            _previousReputation = state.Reputation;
            _previousResearchPoints = state.ResearchPoints;
            _previousKnowledgePoints = state.KnowledgePoints;

            var pending = state.Events.PendingEvent;
            var option = pending.EventRecord.Options.Find(o => string.Equals(o.Id, OptionId, StringComparison.OrdinalIgnoreCase));
            _appliedOutcome = option.Outcome;
            _isRepeatable = pending.EventRecord.IsRepeatable;

            // Apply budget
            if (Math.Abs(_appliedOutcome.BudgetDelta) > 0.001)
            {
                state.Budget += _appliedOutcome.BudgetDelta;
                state.Economy?.RecordTransaction(
                    state.Time.CurrentDate,
                    FinancialRecordType.EventImpact,
                    _appliedOutcome.BudgetDelta,
                    state.Budget,
                    $"Event: {pending.EventRecord.Id} ({option.Id})");
            }

            // Apply reputation
            if (Math.Abs(_appliedOutcome.ReputationDelta) > 0.001)
            {
                state.Reputation = Math.Max(0.0, Math.Min(100.0, state.Reputation + _appliedOutcome.ReputationDelta));
            }

            // Apply research points
            if (Math.Abs(_appliedOutcome.ResearchPointsDelta) > 0.001)
            {
                state.ResearchPoints = Math.Max(0.0, state.ResearchPoints + _appliedOutcome.ResearchPointsDelta);
            }

            // Apply knowledge points
            if (Math.Abs(_appliedOutcome.KnowledgePointsDelta) > 0.001)
            {
                state.KnowledgePoints = Math.Max(0.0, state.KnowledgePoints + _appliedOutcome.KnowledgePointsDelta);
            }

            // Apply supplier relationship
            _affectedSupplierId = _appliedOutcome.SupplierId;
            if (!string.IsNullOrEmpty(_affectedSupplierId) && state.Suppliers != null)
            {
                var company = state.Suppliers.GetCompany(_affectedSupplierId);
                if (company != null)
                {
                    _previousSupplierRel = company.RelationshipScore;
                    company.ModifyRelationship(_appliedOutcome.SupplierRelationshipDelta);
                }
            }

            // Record resolution in event manager
            state.Events.RecordResolution(EventId, OptionId, state.Time.CurrentDate, _appliedOutcome, _isRepeatable);

            return CommandExecutionResult.Succeeded(
                $"Event '{EventId}' resolved with option '{OptionId}'. " +
                $"Budget: {(state.Budget - _previousBudget):+0.0;-0.0;0} M$, " +
                $"Rep: {(state.Reputation - _previousReputation):+0.0;-0.0;0}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            state.Budget = _previousBudget;
            state.Reputation = _previousReputation;
            state.ResearchPoints = _previousResearchPoints;
            state.KnowledgePoints = _previousKnowledgePoints;

            if (!string.IsNullOrEmpty(_affectedSupplierId) && state.Suppliers != null)
            {
                var company = state.Suppliers.GetCompany(_affectedSupplierId);
                if (company != null)
                {
                    company.SetRelationship(_previousSupplierRel);
                }
            }

            state.Events.UndoResolution(EventId, OptionId, _isRepeatable, _previousPendingEvent);

            return CommandExecutionResult.Succeeded($"Event '{EventId}' resolution undone. State restored.");
        }
    }
}
