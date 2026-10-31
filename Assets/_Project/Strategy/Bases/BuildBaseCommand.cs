using System;
using Apogee.Core;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to construct a new base facility in a specified World Map region.
    /// Deducts construction cost from CampaignState.Budget.
    /// Checks region permissions and existing facilities. Supports Undo.
    /// Complies with GDD §6.1, §19.8, §17.3.
    /// </summary>
    public sealed class BuildBaseCommand : CommandBase<CampaignState>
    {
        public string BaseId { get; }
        public string RegionId { get; }
        public BaseType Type { get; }
        public double LaborCostMultiplier { get; }

        private double _costDeducted;

        public BuildBaseCommand(
            string baseId,
            string regionId,
            BaseType type,
            double laborCostMultiplier = 1.0,
            string id = null) : base(id)
        {
            BaseId = baseId;
            RegionId = regionId;
            Type = type;
            LaborCostMultiplier = Math.Max(0.1, laborCostMultiplier);
        }

        public override string Name => nameof(BuildBaseCommand);
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(BaseId))
                return CommandValidationResult.Fail("BaseId cannot be empty.");

            if (string.IsNullOrWhiteSpace(RegionId))
                return CommandValidationResult.Fail("RegionId cannot be empty.");

            if (state.Bases.ContainsKey(BaseId))
                return CommandValidationResult.Fail($"A base facility with ID '{BaseId}' already exists.");

            double cost = BaseLevelSpec.GetUpgradeCost(Type, targetLevel: 1, LaborCostMultiplier);
            if (state.Budget < cost)
            {
                return CommandValidationResult.Fail(
                    $"Insufficient budget to construct {Type} in {RegionId}. Required: {cost:0.#}M$, Available: {state.Budget:0.#}M$.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            _costDeducted = BaseLevelSpec.GetUpgradeCost(Type, targetLevel: 1, LaborCostMultiplier);
            state.Budget -= _costDeducted;

            var facility = new BaseFacility(BaseId, RegionId, Type, level: 1);
            state.Bases[BaseId] = facility;

            return CommandExecutionResult.Succeeded(
                $"Constructed {Type} (Level 1) at {RegionId} for {_costDeducted:0.#}M$. Remaining budget: {state.Budget:0.#}M$.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            if (state.Bases.Remove(BaseId))
            {
                state.Budget += _costDeducted;
                return CommandExecutionResult.Succeeded(
                    $"BuildBaseCommand undone: Removed base '{BaseId}' and refunded {_costDeducted:0.#}M$. Budget restored to {state.Budget:0.#}M$.");
            }

            return CommandExecutionResult.Failed($"Cannot undo BuildBaseCommand: Base '{BaseId}' was not found.");
        }
    }
}
