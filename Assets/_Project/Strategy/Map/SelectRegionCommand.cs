using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to select a region on the Strategy Map for inspection or base establishment.
    /// Supports Undo.
    /// </summary>
    public sealed class SelectRegionCommand : CommandBase<CampaignState>
    {
        public string TargetRegionId { get; }

        private string _previousRegionId;

        public SelectRegionCommand(string targetRegionId, string id = null) : base(id)
        {
            TargetRegionId = targetRegionId;
        }

        public override string Name => nameof(SelectRegionCommand);
        public override PlayerRole RequiredRoles => PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(TargetRegionId))
            {
                return CommandValidationResult.Fail("TargetRegionId cannot be null or whitespace.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _previousRegionId = state.SelectedRegionId;
            state.SelectedRegionId = TargetRegionId;
            return CommandExecutionResult.Succeeded($"Selected region changed to '{TargetRegionId}'.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            string current = state.SelectedRegionId;
            state.SelectedRegionId = _previousRegionId;
            return CommandExecutionResult.Succeeded($"SelectRegionCommand undone: reverted selected region from '{current}' to '{_previousRegionId}'.");
        }
    }
}
