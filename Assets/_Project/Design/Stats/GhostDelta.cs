using System;
using System.Text;

namespace Apogee.Design
{
    /// <summary>
    /// Computes and holds differential telemetry ("hayalet delta") when hovering over candidate modules (GDD §7.3, §13.2, §14.4).
    /// Pure functional and non-mutating.
    /// </summary>
    public sealed class GhostDelta
    {
        public BlueprintStats BaseStats { get; }
        public BlueprintStats PreviewStats { get; }

        public double DeltaDeltaV { get; }
        public double DeltaDryMassTons { get; }
        public double DeltaWetMassTons { get; }
        public double DeltaCostMillions { get; }
        public double DeltaReliability { get; }
        public double DeltaPowerMarginKw { get; }
        public double DeltaThermalMarginKw { get; }
        public int DeltaBuildTimeDays { get; }

        public bool HasChanges =>
            Math.Abs(DeltaDeltaV) > 0.1 ||
            Math.Abs(DeltaDryMassTons) > 0.001 ||
            Math.Abs(DeltaWetMassTons) > 0.001 ||
            Math.Abs(DeltaCostMillions) > 0.01 ||
            Math.Abs(DeltaReliability) > 0.0001 ||
            Math.Abs(DeltaPowerMarginKw) > 0.01 ||
            Math.Abs(DeltaThermalMarginKw) > 0.01 ||
            DeltaBuildTimeDays != 0;

        public GhostDelta(BlueprintStats baseStats, BlueprintStats previewStats)
        {
            BaseStats = baseStats ?? throw new ArgumentNullException(nameof(baseStats));
            PreviewStats = previewStats ?? throw new ArgumentNullException(nameof(previewStats));

            DeltaDeltaV = previewStats.NetDeltaV - baseStats.NetDeltaV;
            DeltaDryMassTons = previewStats.DryMass - baseStats.DryMass;
            DeltaWetMassTons = previewStats.WetMass - baseStats.WetMass;
            DeltaCostMillions = previewStats.Cost - baseStats.Cost;
            DeltaReliability = previewStats.Reliability - baseStats.Reliability;
            DeltaPowerMarginKw = previewStats.PowerMargin - baseStats.PowerMargin;
            DeltaThermalMarginKw = previewStats.ThermalMargin - baseStats.ThermalMargin;
            DeltaBuildTimeDays = previewStats.BuildTimeDays - baseStats.BuildTimeDays;
        }

        /// <summary>
        /// Functional factory to compute ghost delta for a prospective component swap without mutating the current blueprint.
        /// </summary>
        public static GhostDelta Compute(
            Blueprint currentBlueprint,
            VehicleSlotId slotId,
            DesignComponent candidateComponent,
            DeltaVLossProfile lossProfile = null,
            double requiredDeltaV = 0.0)
        {
            if (currentBlueprint == null)
                throw new ArgumentNullException(nameof(currentBlueprint));

            var baseStats = BlueprintEvaluator.Evaluate(currentBlueprint, lossProfile, requiredDeltaV);
            var previewBlueprint = currentBlueprint.WithSlotComponent(slotId, candidateComponent);
            var previewStats = BlueprintEvaluator.Evaluate(previewBlueprint, lossProfile, requiredDeltaV);

            return new GhostDelta(baseStats, previewStats);
        }

        /// <summary>
        /// Generates a concise, human-readable summary in plain Turkish (GDD §8.3 & §13.3).
        /// e.g. "+450 m/s menzil, -0.4 t kütle, +$1.5M maliyet"
        /// </summary>
        public string GetSummaryText()
        {
            if (!HasChanges)
            {
                return "Değişim yok";
            }

            var sb = new StringBuilder();

            if (Math.Abs(DeltaDeltaV) >= 1.0)
            {
                sb.Append(DeltaDeltaV > 0 ? $"+{DeltaDeltaV:N0} m/s menzil" : $"{DeltaDeltaV:N0} m/s menzil");
            }

            if (Math.Abs(DeltaWetMassTons) >= 0.01)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DeltaWetMassTons > 0 ? $"+{DeltaWetMassTons:F2} t kütle" : $"{DeltaWetMassTons:F2} t kütle");
            }

            if (Math.Abs(DeltaCostMillions) >= 0.05)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DeltaCostMillions > 0 ? $"+${DeltaCostMillions:F1}M maliyet" : $"-${Math.Abs(DeltaCostMillions):F1}M maliyet");
            }

            if (Math.Abs(DeltaReliability) >= 0.005)
            {
                if (sb.Length > 0) sb.Append(", ");
                double relPct = DeltaReliability * 100.0;
                sb.Append(relPct > 0 ? $"+%{relPct:F1} güvenilirlik" : $"-%{Math.Abs(relPct):F1} güvenilirlik");
            }

            if (Math.Abs(DeltaPowerMarginKw) >= 0.05)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DeltaPowerMarginKw > 0 ? $"+{DeltaPowerMarginKw:F1} kW güç" : $"{DeltaPowerMarginKw:F1} kW güç");
            }

            return sb.ToString();
        }

        public override string ToString() => $"[GhostDelta: {GetSummaryText()}]";
    }
}
