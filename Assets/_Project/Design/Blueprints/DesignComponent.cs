using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents an installed module/component within a rocket stage or spacecraft payload.
    /// Encapsulates base engineering specs combined with chosen supplier and material.
    /// Complies with GDD §7.2, §7.3, §8.1, and §19.4.
    /// </summary>
    public sealed class DesignComponent
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }

        public double BaseDryMass { get; }
        public double BaseCost { get; }
        public int BaseBuildTime { get; }
        public double BaseReliability { get; }

        public double PowerGeneration { get; }
        public double PowerConsumption { get; }
        public double ThermalLoad { get; }
        public double ThermalDissipation { get; }
        public double DataRate { get; }
        public double DataStorage { get; }
        public double DownlinkRate { get; }

        // Propulsion specific
        public double Isp { get; }
        public double Thrust { get; }

        // Associated supplier and material options
        public SupplierSpec Supplier { get; }
        public MaterialSpec Material { get; }

        public double EffectiveDryMass
        {
            get
            {
                double mass = BaseDryMass;
                if (Material != null)
                {
                    mass *= Material.MassMultiplier;
                }
                return mass;
            }
        }

        public double EffectiveCost
        {
            get
            {
                double cost = BaseCost;
                if (Supplier != null)
                {
                    cost *= Supplier.CostMultiplier;
                }
                if (Material != null)
                {
                    cost *= Material.CostMultiplier;
                }
                return cost;
            }
        }

        public int EffectiveBuildTime
        {
            get
            {
                double days = BaseBuildTime;
                if (Supplier != null)
                {
                    days *= Supplier.DeliveryMultiplier;
                }
                if (Material != null)
                {
                    days *= Material.BuildTimeMultiplier;
                }
                return (int)Math.Ceiling(days);
            }
        }

        public double EffectiveReliability
        {
            get
            {
                double rel = BaseReliability;
                if (Supplier != null)
                {
                    rel *= Supplier.ReliabilityMultiplier;
                }
                return Math.Max(0.0, Math.Min(1.0, rel));
            }
        }

        public DesignComponent(
            string id,
            string category,
            string name = null,
            double dryMass = 0.0,
            double cost = 0.0,
            int buildTime = 0,
            double reliability = 1.0,
            double powerGeneration = 0.0,
            double powerConsumption = 0.0,
            double thermalLoad = 0.0,
            double thermalDissipation = 0.0,
            double dataRate = 0.0,
            double dataStorage = 0.0,
            double downlinkRate = 0.0,
            double isp = 0.0,
            double thrust = 0.0,
            SupplierSpec supplier = null,
            MaterialSpec material = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Component ID cannot be empty.", nameof(id));

            Id = id;
            Category = category ?? "generic";
            Name = name ?? id;

            BaseDryMass = Math.Max(0.0, dryMass);
            BaseCost = Math.Max(0.0, cost);
            BaseBuildTime = Math.Max(0, buildTime);
            BaseReliability = Math.Max(0.0, Math.Min(1.0, reliability));

            PowerGeneration = Math.Max(0.0, powerGeneration);
            PowerConsumption = Math.Max(0.0, powerConsumption);
            ThermalLoad = Math.Max(0.0, thermalLoad);
            ThermalDissipation = Math.Max(0.0, thermalDissipation);
            DataRate = Math.Max(0.0, dataRate);
            DataStorage = Math.Max(0.0, dataStorage);
            DownlinkRate = Math.Max(0.0, downlinkRate);

            Isp = Math.Max(0.0, isp);
            Thrust = Math.Max(0.0, thrust);

            Supplier = supplier ?? SupplierSpec.Default;
            Material = material;
        }

        public override string ToString() => $"[{Category}] {Id} (Mass: {EffectiveDryMass:0.##}t, Cost: {EffectiveCost:0.##}M$)";
    }
}
