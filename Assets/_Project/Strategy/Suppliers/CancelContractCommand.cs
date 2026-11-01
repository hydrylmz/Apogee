using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to cancel a pending supplier contract (GDD §9.1, §12.2).
    /// Refunds a partial amount of the contract fee and applies a relationship penalty with the supplier.
    /// Supports symmetrical Undo/Redo.
    /// </summary>
    public sealed class CancelContractCommand : CommandBase<CampaignState>
    {
        public string ContractId { get; }
        public double RefundRatio { get; }
        public double RelationshipPenalty { get; }

        private double _refundedAmount;
        private double _previousRelationshipScore;
        private ContractStatus _previousStatus;
        private GameDate? _previousFulfilledDate;

        public override string Name => nameof(CancelContractCommand);
        public override PlayerRole RequiredRoles => PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public CancelContractCommand(
            string contractId,
            double refundRatio = 0.5,
            double relationshipPenalty = 0.05,
            string id = null) : base(id)
        {
            ContractId = !string.IsNullOrWhiteSpace(contractId) ? contractId : throw new ArgumentException("ContractId cannot be empty.", nameof(contractId));
            RefundRatio = Math.Max(0.0, Math.Min(1.0, refundRatio));
            RelationshipPenalty = Math.Max(0.0, Math.Min(1.0, relationshipPenalty));
        }

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState cannot be null.");

            if (state.Suppliers == null)
                return CommandValidationResult.Fail("SupplierManager is not initialised on CampaignState.");

            var contract = state.Suppliers.GetContract(ContractId);
            if (contract == null)
                return CommandValidationResult.Fail($"Contract with ID '{ContractId}' not found.");

            if (contract.Status != ContractStatus.PendingDelivery)
                return CommandValidationResult.Fail($"Contract '{ContractId}' cannot be cancelled because its status is {contract.Status} (only PendingDelivery contracts can be cancelled).");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            var contract = state.Suppliers.GetContract(ContractId);
            _previousStatus = contract.Status;
            _previousFulfilledDate = contract.FulfilledDate;

            var company = state.Suppliers.GetCompany(contract.SupplierId);
            _previousRelationshipScore = company != null ? company.RelationshipScore : 0.5;

            _refundedAmount = contract.CostMUSD * RefundRatio;
            state.Budget += _refundedAmount;

            state.Suppliers.CancelContract(ContractId, RelationshipPenalty);

            return CommandExecutionResult.Succeeded(
                $"Cancelled contract {ContractId}. Refunded {_refundedAmount:F1}M$ ({RefundRatio:P0}). Relationship penalty applied (-{RelationshipPenalty:F2}).");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            if (state.Budget < _refundedAmount)
            {
                return CommandExecutionResult.Failed($"Undo failed: Insufficient budget to reverse refund of {_refundedAmount:F1}M$. Available: {state.Budget:F1}M$.");
            }

            state.Budget -= _refundedAmount;

            var contract = state.Suppliers.GetContract(ContractId);
            contract?.RevertState(_previousStatus, _previousFulfilledDate);

            if (contract != null)
            {
                var company = state.Suppliers.GetCompany(contract.SupplierId);
                company?.SetRelationship(_previousRelationshipScore);
            }

            return CommandExecutionResult.Succeeded(
                $"Undid cancellation of contract {ContractId}. Re-deducted {_refundedAmount:F1}M$, restored status and relationship.");
        }

        public override string ToString() =>
            $"[CancelContractCommand: {ContractId} Refund={RefundRatio:P0}]";
    }
}
