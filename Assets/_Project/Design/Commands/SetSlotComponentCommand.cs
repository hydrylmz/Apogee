using System;
using Apogee.Core;

namespace Apogee.Design.Commands
{
    /// <summary>
    /// Command to equip, replace, or clear a component in a vehicle blueprint slot (GDD §7.3 & §21 M2-02).
    /// Dispatched via CommandBus, supports symmetric Undo/Redo and role authorization.
    /// </summary>
    public sealed class SetSlotComponentCommand : CommandBase<IBlueprintHolder>
    {
        public VehicleSlotId SlotId { get; }
        public DesignComponent NewComponent { get; }
        public DesignComponent PreviousComponent { get; private set; }

        public override PlayerRole RequiredRoles => PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public SetSlotComponentCommand(VehicleSlotId slotId, DesignComponent newComponent, string id = null)
            : base(id)
        {
            SlotId = slotId;
            NewComponent = newComponent;
        }

        protected override CommandValidationResult CanExecute(IBlueprintHolder state, CommandContext context)
        {
            if (state == null)
            {
                return CommandValidationResult.Fail("Blueprint container cannot be null.");
            }

            if (state.ActiveBlueprint == null)
            {
                return CommandValidationResult.Fail("ActiveBlueprint cannot be null.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(IBlueprintHolder state, CommandContext context)
        {
            var currentBlueprint = state.ActiveBlueprint;
            PreviousComponent = currentBlueprint.GetComponentInSlot(SlotId);

            state.ActiveBlueprint = currentBlueprint.WithSlotComponent(SlotId, NewComponent);

            return CommandExecutionResult.Succeeded();
        }

        protected override CommandExecutionResult Undo(IBlueprintHolder state, CommandContext context)
        {
            if (state.ActiveBlueprint == null)
            {
                return CommandExecutionResult.Failed("Cannot undo on null ActiveBlueprint.");
            }

            state.ActiveBlueprint = state.ActiveBlueprint.WithSlotComponent(SlotId, PreviousComponent);

            return CommandExecutionResult.Succeeded();
        }

        public override string ToString() =>
            $"[SetSlotComponentCommand: Slot={SlotId}, New={NewComponent?.Id ?? "(none)"}, Prev={PreviousComponent?.Id ?? "(none)"}]";
    }
}
