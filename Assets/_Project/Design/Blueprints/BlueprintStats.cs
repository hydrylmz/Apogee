using System;

namespace Apogee.Design
{
    /// <summary>
    /// Calculated aggregate engineering and programmatic stats for a <see cref="Blueprint"/>.
    /// Contains both the raw <see cref="StatCollection"/> (with full <see cref="StatBreakdown"/> support)
    /// and convenient strongly-typed summary properties.
    /// Complies with GDD §8.1, §8.2, and §8.3.
    /// </summary>
    public sealed class BlueprintStats
    {
        public StatCollection Stats { get; }
        public DeltaVCalculationResult DeltaVResult { get; }

        public double DryMass => Stats.GetValue(StatType.DryMass);
        public double WetMass => Stats.GetValue(StatType.WetMass);
        public double PropellantMass => Stats.GetValue(StatType.PropellantMass);

        public double Cost => Stats.GetValue(StatType.Cost);
        public int BuildTimeDays => (int)Math.Round(Stats.GetValue(StatType.BuildTime));
        public double Reliability => Stats.GetValue(StatType.Reliability);

        public double PowerGeneration => Stats.GetValue(StatType.PowerGeneration);
        public double PowerConsumption => Stats.GetValue(StatType.PowerConsumption);
        public double PowerMargin => Stats.GetValue(StatType.PowerMargin);
        public double BatteryCapacity => Stats.GetValue(StatType.BatteryCapacity);

        public double ThermalLoad => Stats.GetValue(StatType.ThermalLoad);
        public double ThermalDissipation => Stats.GetValue(StatType.ThermalDissipation);
        public double ThermalMargin => Stats.GetValue(StatType.ThermalMargin);

        public double DataRate => Stats.GetValue(StatType.DataRate);
        public double DataStorage => Stats.GetValue(StatType.DataStorage);
        public double DownlinkRate => Stats.GetValue(StatType.DownlinkRate);

        public double NetDeltaV => DeltaVResult != null ? DeltaVResult.NetDeltaV : 0.0;
        public double IdealDeltaV => DeltaVResult != null ? DeltaVResult.TotalIdealDeltaV : 0.0;

        public BlueprintStats(StatCollection stats, DeltaVCalculationResult deltaVResult = null)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            DeltaVResult = deltaVResult;
        }

        public StatBreakdown GetBreakdown(StatType type) => Stats.GetBreakdown(type);

        public override string ToString() =>
            $"[BlueprintStats] DryMass={DryMass:0.##}t WetMass={WetMass:0.##}t NetΔv={NetDeltaV:0.#}m/s Cost={Cost:0.##}M$ Rel={Reliability:P1}";
    }
}
