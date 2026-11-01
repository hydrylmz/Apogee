using System;
using Apogee.Core;

namespace Apogee.Design.Commands
{
    /// <summary>
    /// Command to assign or change the supplier company for a specific vehicle slot (GDD §7.3, §9.1, §21 M4-06).
    /// Mutates the component's supplier spec, immediately updating effective cost, delivery time, and reliability stats.
    /// Supports symmetrical Undo/Redo and role authorization.
    /// </summary>
    public sealed class SetSlotSupplierCommand : CommandBase<IBlueprintHolder>
    {
        public VehicleSlotId SlotId { get; }
        public SupplierSpec NewSupplier { get; }

        public DesignComponent PreviousComponent { get; private set; }
        public DesignComponent NewComponent { get; private set; }

        public override PlayerRole RequiredRoles =>
            PlayerRole.LeadEngineer | PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;

        public override bool CanUndo => true;

        public SetSlotSupplierCommand(VehicleSlotId slotId, SupplierSpec newSupplier, string id = null)
            : base(id)
        {
            SlotId = slotId;
            NewSupplier = newSupplier ?? throw new ArgumentNullException(nameof(newSupplier));
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

            var currentComponent = state.ActiveBlueprint.GetComponentInSlot(SlotId);
            if (currentComponent == null)
            {
                return CommandValidationResult.Fail($"Cannot assign supplier: Slot {SlotId} does not currently have an equipped component.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(IBlueprintHolder state, CommandContext context)
        {
            var currentBlueprint = state.ActiveBlueprint;
            PreviousComponent = currentBlueprint.GetComponentInSlot(SlotId);

            NewComponent = PreviousComponent.WithSupplier(NewSupplier);
            state.ActiveBlueprint = currentBlueprint.WithSlotComponent(SlotId, NewComponent);

            return CommandExecutionResult.Succeeded(
                $"Assigned supplier '{NewSupplier.Name}' to slot {SlotId}.");
        }

        protected override CommandExecutionResult Undo(IBlueprintHolder state, CommandContext context)
        {
            if (state.ActiveBlueprint == null)
            {
                return CommandExecutionResult.Failed("Cannot undo on null ActiveBlueprint.");
            }

            state.ActiveBlueprint = state.ActiveBlueprint.WithSlotComponent(SlotId, PreviousComponent);

            return CommandExecutionResult.Succeeded(
                $"Undid supplier assignment on slot {SlotId}. Restored previous component.");
        }

        public override string ToString() =>
            $"[SetSlotSupplierCommand: Slot={SlotId}, Supplier={NewSupplier.Id}]";
    }
}
