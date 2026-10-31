using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to change the active map lens overlay on the Strategy Map.
    /// Supports Undo.
    /// Complies with GDD §6.1.
    /// </summary>
    public sealed class SetMapLensCommand : CommandBase<CampaignState>
    {
        public MapLens TargetLens { get; }

        private MapLens _previousLens;

        public SetMapLensCommand(MapLens targetLens, string id = null) : base(id)
        {
            TargetLens = targetLens;
        }

        public override string Name => nameof(SetMapLensCommand);
        public override PlayerRole RequiredRoles => PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _previousLens = state.ActiveMapLens;
            state.ActiveMapLens = TargetLens;
            return CommandExecutionResult.Succeeded($"Map lens switched from {_previousLens} to {TargetLens}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            MapLens current = state.ActiveMapLens;
            state.ActiveMapLens = _previousLens;
            return CommandExecutionResult.Succeeded($"SetMapLensCommand undone: reverted from {current} to {_previousLens}.");
        }
    }
}
