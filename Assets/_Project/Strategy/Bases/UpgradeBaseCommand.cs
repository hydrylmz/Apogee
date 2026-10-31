using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to upgrade an existing base facility to the next tier level (up to Level 5).
    /// Deducts upgrade cost from CampaignState.Budget and increases the base level.
    /// Supports Undo. Complies with GDD §6.1, §19.8, §16.7.
    /// </summary>
    public sealed class UpgradeBaseCommand : CommandBase<CampaignState>
    {
        public string BaseId { get; }
        public double LaborCostMultiplier { get; }

        private double _costDeducted;
        private int _previousLevel;

        public UpgradeBaseCommand(string baseId, double laborCostMultiplier = 1.0, string id = null) : base(id)
        {
            BaseId = baseId;
            LaborCostMultiplier = Math.Max(0.1, laborCostMultiplier);
        }

        public override string Name => nameof(UpgradeBaseCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(BaseId))
                return CommandValidationResult.Fail("BaseId cannot be empty.");

            if (!state.Bases.TryGetValue(BaseId, out BaseFacility facility))
                return CommandValidationResult.Fail($"Base facility with ID '{BaseId}' does not exist.");

            if (!facility.CanUpgrade)
                return CommandValidationResult.Fail($"Base facility '{BaseId}' is already at maximum level ({facility.Level}).");

            int targetLevel = facility.Level + 1;
            double cost = BaseLevelSpec.GetUpgradeCost(facility.Type, targetLevel, LaborCostMultiplier);
            if (state.Budget < cost)
            {
                return CommandValidationResult.Fail(
                    $"Insufficient budget to upgrade '{BaseId}' to Level {targetLevel}. Required: {cost:0.#}M$, Available: {state.Budget:0.#}M$.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            var facility = state.Bases[BaseId];
            _previousLevel = facility.Level;
            int nextLevel = facility.Level + 1;

            _costDeducted = BaseLevelSpec.GetUpgradeCost(facility.Type, nextLevel, LaborCostMultiplier);
            state.Budget -= _costDeducted;
            facility.SetLevel(nextLevel);

            return CommandExecutionResult.Succeeded(
                $"Upgraded '{BaseId}' ({facility.Type}) to Level {nextLevel} for {_costDeducted:0.#}M$. Remaining budget: {state.Budget:0.#}M$.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            if (state.Bases.TryGetValue(BaseId, out BaseFacility facility))
            {
                facility.SetLevel(_previousLevel);
                state.Budget += _costDeducted;
                return CommandExecutionResult.Succeeded(
                    $"UpgradeBaseCommand undone: Reverted '{BaseId}' to Level {_previousLevel} and refunded {_costDeducted:0.#}M$. Budget restored to {state.Budget:0.#}M$.");
            }

            return CommandExecutionResult.Failed($"Cannot undo UpgradeBaseCommand: Base '{BaseId}' was not found.");
        }
    }
}
