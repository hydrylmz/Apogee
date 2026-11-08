using Apogee.Core;

namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Ends the campaign with the given reason, evaluates the final state, and builds the
    /// <see cref="CampaignEndReport"/>.
    ///
    /// GDD §4.1, §4.5, §21 M6-02.
    /// </summary>
    public sealed class EndCampaignCommand : CommandBase<CampaignState>
    {
        // ---------------------------------------------------------------- parameters
        /// <summary>Termination reason (must not be Active).</summary>
        public CampaignEndStatus Reason { get; }

        // ---------------------------------------------------------------- undo snapshot
        private CampaignEndStatus  _previousEndStatus;
        private CampaignEndReport  _previousEndReport;

        // ---------------------------------------------------------------- construction
        public EndCampaignCommand(CampaignEndStatus reason, string id = null) : base(id)
        {
            Reason = reason;
        }

        // ---------------------------------------------------------------- ICommand
        public override string     Name          => nameof(EndCampaignCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool       CanUndo       => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state.EndStatus != CampaignEndStatus.Active)
                return CommandValidationResult.Fail(
                    $"Campaign is already ended with status '{state.EndStatus}'. Cannot end again.");

            if (Reason == CampaignEndStatus.Active)
                return CommandValidationResult.Fail(
                    "EndCampaignCommand requires a non-Active termination reason.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            // Snapshot for undo
            _previousEndStatus = state.EndStatus;
            _previousEndReport = state.EndReport;

            // Determine the actual end status (use Reason directly; evaluator is used for
            // automated checks in AdvanceTimeCommand — here the reason is explicit).
            CampaignEndStatus finalStatus = Reason;

            // Build legacy report
            CampaignEndReport report = CampaignEndEvaluator.BuildEndReport(state, finalStatus);

            // Commit to state
            state.EndStatus = finalStatus;
            state.EndReport = report;

            return CommandExecutionResult.Succeeded(
                $"Campaign ended: {finalStatus}. {report.TotalMissionsSucceeded}/{report.TotalMissionsLaunched} missions succeeded, " +
                $"{report.MilestonesClaimedCount}/{report.TotalMilestonesCount} milestones claimed.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            state.EndStatus = _previousEndStatus;
            state.EndReport = _previousEndReport;

            return CommandExecutionResult.Succeeded(
                $"EndCampaignCommand undone: campaign restored to '{_previousEndStatus}'.");
        }
    }
}
