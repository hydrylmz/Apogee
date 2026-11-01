using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;

namespace Apogee.Design.Commands
{
    /// <summary>
    /// Command to assign a supplier across multiple vehicle slots simultaneously (GDD §9.1, §21 M4-06).
    /// Used when contracting a single supplier for entire stages or payloads,
    /// triggering single-source volume discounts and concentration risks.
    /// Supports symmetrical Undo/Redo and role authorization.
    /// </summary>
    public sealed class BatchAssignSupplierCommand : CommandBase<IBlueprintHolder>
    {
        public IReadOnlyList<VehicleSlotId> TargetSlots { get; }
        public SupplierSpec NewSupplier { get; }

        private readonly Dictionary<VehicleSlotId, DesignComponent> _previousComponents =
            new Dictionary<VehicleSlotId, DesignComponent>();

        public override PlayerRole RequiredRoles =>
            PlayerRole.LeadEngineer | PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;

        public override bool CanUndo => true;

        public BatchAssignSupplierCommand(
            IEnumerable<VehicleSlotId> targetSlots,
            SupplierSpec newSupplier,
            string id = null) : base(id)
        {
            if (targetSlots == null) throw new ArgumentNullException(nameof(targetSlots));
            TargetSlots = targetSlots.Distinct().ToList();
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

            if (TargetSlots.Count == 0)
            {
                return CommandValidationResult.Fail("TargetSlots list cannot be empty.");
            }

            bool hasAtLeastOneEquipped = TargetSlots.Any(slot => state.ActiveBlueprint.GetComponentInSlot(slot) != null);
            if (!hasAtLeastOneEquipped)
            {
                return CommandValidationResult.Fail("None of the specified slots have an equipped component to assign a supplier to.");
            }

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(IBlueprintHolder state, CommandContext context)
        {
            var bp = state.ActiveBlueprint;
            _previousComponents.Clear();

            int updatedCount = 0;
            foreach (var slot in TargetSlots)
            {
                var comp = bp.GetComponentInSlot(slot);
                if (comp != null)
                {
                    _previousComponents[slot] = comp;
                    var newComp = comp.WithSupplier(NewSupplier);
                    bp = bp.WithSlotComponent(slot, newComp);
                    updatedCount++;
                }
            }

            state.ActiveBlueprint = bp;

            return CommandExecutionResult.Succeeded(
                $"Batch assigned supplier '{NewSupplier.Name}' to {updatedCount} slot(s).");
        }

        protected override CommandExecutionResult Undo(IBlueprintHolder state, CommandContext context)
        {
            if (state.ActiveBlueprint == null)
            {
                return CommandExecutionResult.Failed("Cannot undo on null ActiveBlueprint.");
            }

            var bp = state.ActiveBlueprint;
            foreach (var kvp in _previousComponents)
            {
                bp = bp.WithSlotComponent(kvp.Key, kvp.Value);
            }

            state.ActiveBlueprint = bp;

            return CommandExecutionResult.Succeeded(
                $"Undid batch supplier assignment across {_previousComponents.Count} slot(s).");
        }

        public override string ToString() =>
            $"[BatchAssignSupplierCommand: Slots={TargetSlots.Count}, Supplier={NewSupplier.Id}]";
    }
}
