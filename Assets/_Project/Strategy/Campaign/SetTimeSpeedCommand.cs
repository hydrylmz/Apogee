using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Changes the campaign time speed (Paused / Normal / Fast / VeryFast).
    /// Not undoable (speed is a UI-level preference, not a meaningful game state mutation).
    /// Complies with GDD §6.3.
    /// </summary>
    public sealed class SetTimeSpeedCommand : CommandBase<CampaignState>
    {
        public TimeSpeed TargetSpeed { get; }

        private TimeSpeed _previousSpeed;

        public SetTimeSpeedCommand(TimeSpeed targetSpeed, string id = null) : base(id)
        {
            TargetSpeed = targetSpeed;
        }

        public override string Name          => nameof(SetTimeSpeedCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool CanUndo         => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            // Always valid — speed can be set even when expired (e.g. to pause at the end screen).
            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _previousSpeed = state.Time.Speed;
            state.Time.SetSpeed(TargetSpeed);
            return CommandExecutionResult.Succeeded(
                $"Time speed changed: {_previousSpeed} → {TargetSpeed}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            TimeSpeed current = state.Time.Speed;
            state.Time.SetSpeed(_previousSpeed);
            return CommandExecutionResult.Succeeded(
                $"SetTimeSpeedCommand undone: {current} → {state.Time.Speed}.");
        }
    }
}
