using System;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Represents an active or historical procurement contract between the agency and a supplier company.
    /// Complies with GDD §9.1 ("Sözleşme: Şirket × bileşen. Fiyat, teslim süresi, gecikme riski, güvenilirlik çarpanı")
    /// and §19.12 / §19.13.
    /// </summary>
    public sealed class SupplierContract
    {
        public string Id { get; }
        public string SupplierId { get; }
        public string ItemName { get; }
        public string ComponentId { get; }
        public string Category { get; }

        public GameDate SignedDate { get; }
        public int DeliveryDays { get; }
        public GameDate DeliveryDate { get; }

        public double CostMUSD { get; }
        public ContractStatus Status { get; private set; }
        public GameDate? FulfilledDate { get; private set; }

        public double ReliabilityMultiplier { get; }
        public double AppliedSingleSourceDiscount { get; }

        public bool IsPending => Status == ContractStatus.PendingDelivery;
        public bool IsFulfilled => Status == ContractStatus.Fulfilled;
        public bool IsCancelled => Status == ContractStatus.Cancelled;

        public SupplierContract(
            string id,
            string supplierId,
            string itemName,
            string category,
            GameDate signedDate,
            int deliveryDays,
            double costMUSD,
            string componentId = null,
            double reliabilityMultiplier = 1.0,
            double appliedSingleSourceDiscount = 0.0,
            ContractStatus status = ContractStatus.PendingDelivery)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Contract ID cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(supplierId))
                throw new ArgumentException("Supplier ID cannot be empty.", nameof(supplierId));

            Id = id;
            SupplierId = supplierId;
            ItemName = !string.IsNullOrWhiteSpace(itemName) ? itemName : "Bileşen";
            Category = !string.IsNullOrWhiteSpace(category) ? category : "generic";
            ComponentId = componentId;

            SignedDate = signedDate;
            DeliveryDays = Math.Max(1, deliveryDays);
            DeliveryDate = signedDate.AddDays(DeliveryDays);

            CostMUSD = Math.Max(0.0, costMUSD);
            Status = status;
            ReliabilityMultiplier = Math.Max(0.1, reliabilityMultiplier);
            AppliedSingleSourceDiscount = Math.Max(0.0, appliedSingleSourceDiscount);
        }

        public void MarkFulfilled(GameDate fulfillmentDate)
        {
            Status = ContractStatus.Fulfilled;
            FulfilledDate = fulfillmentDate;
        }

        public void MarkCancelled()
        {
            Status = ContractStatus.Cancelled;
            FulfilledDate = null;
        }

        /// <summary>
        /// Restores contract status during command Undo.
        /// </summary>
        public void RevertState(ContractStatus previousStatus, GameDate? previousFulfilledDate)
        {
            Status = previousStatus;
            FulfilledDate = previousFulfilledDate;
        }

        public override string ToString() =>
            $"[Contract {Id} ({SupplierId}) Item='{ItemName}' Cost={CostMUSD:F1}M$ Deliv={DeliveryDate} ({Status})]";
    }
}
