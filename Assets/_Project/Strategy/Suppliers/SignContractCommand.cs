using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to sign a procurement contract with a supplier company (GDD §9.1, §12.2, §21 M4-06).
    /// Deducts contract cost from agency budget and schedules delivery on the campaign calendar.
    /// Supports symmetrical Undo/Redo and role authorization (Procurement & Production Director / Agency Director).
    /// </summary>
    public sealed class SignContractCommand : CommandBase<CampaignState>
    {
        public string ContractId { get; }
        public string SupplierId { get; }
        public string ItemName { get; }
        public string Category { get; }
        public double CostMUSD { get; }
        public int DeliveryDays { get; }
        public string ComponentId { get; }
        public double ReliabilityMultiplier { get; }
        public double AppliedSingleSourceDiscount { get; }

        public override string Name => nameof(SignContractCommand);
        public override PlayerRole RequiredRoles => PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public SignContractCommand(
            string contractId,
            string supplierId,
            string itemName,
            string category,
            double costMUSD,
            int deliveryDays,
            string componentId = null,
            double reliabilityMultiplier = 1.0,
            double appliedSingleSourceDiscount = 0.0,
            string id = null) : base(id)
        {
            ContractId = !string.IsNullOrWhiteSpace(contractId) ? contractId : throw new ArgumentException("ContractId cannot be empty.", nameof(contractId));
            SupplierId = !string.IsNullOrWhiteSpace(supplierId) ? supplierId : throw new ArgumentException("SupplierId cannot be empty.", nameof(supplierId));
            ItemName = !string.IsNullOrWhiteSpace(itemName) ? itemName : "Bileşen";
            Category = !string.IsNullOrWhiteSpace(category) ? category : "generic";
            CostMUSD = Math.Max(0.0, costMUSD);
            DeliveryDays = Math.Max(1, deliveryDays);
            ComponentId = componentId;
            ReliabilityMultiplier = reliabilityMultiplier > 0.0 ? reliabilityMultiplier : 1.0;
            AppliedSingleSourceDiscount = Math.Max(0.0, appliedSingleSourceDiscount);
        }

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState cannot be null.");

            if (state.Suppliers == null)
                return CommandValidationResult.Fail("SupplierManager is not initialised on CampaignState.");

            if (state.Suppliers.GetCompany(SupplierId) == null)
                return CommandValidationResult.Fail($"Supplier company '{SupplierId}' not found in supplier catalog.");

            if (state.Suppliers.GetContract(ContractId) != null)
                return CommandValidationResult.Fail($"Contract with ID '{ContractId}' already exists.");

            if (state.Budget < CostMUSD)
                return CommandValidationResult.Fail($"Insufficient budget to sign contract: Requires {CostMUSD:F1}M$, available {state.Budget:F1}M$.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            state.Budget -= CostMUSD;

            var contract = new SupplierContract(
                id: ContractId,
                supplierId: SupplierId,
                itemName: ItemName,
                category: Category,
                signedDate: state.Time.CurrentDate,
                deliveryDays: DeliveryDays,
                costMUSD: CostMUSD,
                componentId: ComponentId,
                reliabilityMultiplier: ReliabilityMultiplier,
                appliedSingleSourceDiscount: AppliedSingleSourceDiscount);

            state.Suppliers.AddContract(contract);

            return CommandExecutionResult.Succeeded(
                $"Signed contract {ContractId} with {SupplierId} for '{ItemName}'. Cost: {CostMUSD:F1}M$, Delivery scheduled: {contract.DeliveryDate}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            state.Budget += CostMUSD;
            bool removed = state.Suppliers.RemoveContract(ContractId);

            if (!removed)
            {
                return CommandExecutionResult.Failed($"Undo failed: Contract {ContractId} could not be found to remove.");
            }

            return CommandExecutionResult.Succeeded(
                $"Undid contract {ContractId}. Refunded {CostMUSD:F1}M$.");
        }

        public override string ToString() =>
            $"[SignContractCommand: {ContractId} ({SupplierId}) '{ItemName}' {CostMUSD:F1}M$]";
    }
}
