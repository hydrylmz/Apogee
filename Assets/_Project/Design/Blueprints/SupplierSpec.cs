using System;

namespace Apogee.Design
{
    /// <summary>
    /// Supplier multiplier specification used during blueprint evaluation.
    /// In decoupled Core/Design, this represents supplier impact on a component.
    /// Complies with GDD §9.1 and §19.6.
    /// </summary>
    public sealed class SupplierSpec
    {
        public string Id { get; }
        public string Name { get; }
        public double CostMultiplier { get; }
        public double DeliveryMultiplier { get; }
        public double ReliabilityMultiplier { get; }

        public SupplierSpec(
            string id,
            string name = null,
            double costMultiplier = 1.0,
            double deliveryMultiplier = 1.0,
            double reliabilityMultiplier = 1.0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Supplier ID cannot be empty.", nameof(id));

            Id = id;
            Name = name ?? id;
            CostMultiplier = costMultiplier > 0.0 ? costMultiplier : 1.0;
            DeliveryMultiplier = deliveryMultiplier > 0.0 ? deliveryMultiplier : 1.0;
            ReliabilityMultiplier = Math.Max(0.0, reliabilityMultiplier);
        }

        public static readonly SupplierSpec Default = new SupplierSpec("default-supplier", "Default Supplier", 1.0, 1.0, 1.0);

        public override string ToString() => $"[Supplier: {Id} Cost x{CostMultiplier:0.##} Rel x{ReliabilityMultiplier:0.##}]";
    }
}
