using System;

namespace Apogee.Design
{
    /// <summary>
    /// Encodes the Δv loss budget for a specific destination / launch profile.
    /// Losses represent gravity drag, atmospheric drag, and steering losses.
    ///
    /// GDD §8.1: "Menzil ihtiyacı = fırlatma kayıpları + transfer + varış + (iniş) + (dönüş);
    ///            fırlatma enlemi avantajı düşülür."
    ///
    /// All values in m/s.  Losses are applied only to the lowest stage(s) that
    /// operate in or near the atmosphere; upper/in-space stages have no drag loss.
    /// The profile is authored per destination in the data catalog.
    /// </summary>
    public sealed class DeltaVLossProfile
    {
        /// <summary>
        /// Total Δv losses applied to Stage-1 (booster) due to gravity drag and
        /// atmospheric drag (m/s).  Typical Earth ascent: ~1 500 m/s.
        /// </summary>
        public double Stage1LossMetersPerSecond { get; }

        /// <summary>
        /// Δv latitude advantage deducted from the requirement for east-bound launches
        /// (m/s, ≥ 0).  E.g. equatorial launch saves ~465 m/s vs polar.
        /// Default 0: no advantage (polar or equatorial already factored in catalog).
        /// </summary>
        public double LatitudeAdvantageMetersPerSecond { get; }

        /// <summary>A descriptive label for this profile (e.g. "Earth LEO", "Trans-Lunar").</summary>
        public string Label { get; }

        /// <param name="label">Human-readable profile name.</param>
        /// <param name="stage1LossMetersPerSecond">Ascent losses on first stage (m/s, ≥ 0).</param>
        /// <param name="latitudeAdvantageMetersPerSecond">Launch latitude Δv bonus (m/s, ≥ 0).</param>
        public DeltaVLossProfile(
            string label,
            double stage1LossMetersPerSecond,
            double latitudeAdvantageMetersPerSecond = 0.0)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label must not be empty.", nameof(label));
            if (stage1LossMetersPerSecond < 0.0)
                throw new ArgumentOutOfRangeException(nameof(stage1LossMetersPerSecond), "Losses must be ≥ 0.");
            if (latitudeAdvantageMetersPerSecond < 0.0)
                throw new ArgumentOutOfRangeException(nameof(latitudeAdvantageMetersPerSecond), "Latitude advantage must be ≥ 0.");

            Label                              = label;
            Stage1LossMetersPerSecond          = stage1LossMetersPerSecond;
            LatitudeAdvantageMetersPerSecond   = latitudeAdvantageMetersPerSecond;
        }

        /// <summary>
        /// A zero-loss profile for in-space stages or simplified calculations
        /// where losses are already folded into the required Δv budget.
        /// </summary>
        public static readonly DeltaVLossProfile None = new DeltaVLossProfile("No Losses", 0.0, 0.0);

        /// <summary>
        /// Typical Earth-to-LEO profile with ~1 500 m/s ascent losses.
        /// Represents an equatorial launch pad (no latitude advantage applied here;
        /// adjust via latitudeAdvantageMetersPerSecond as needed).
        /// </summary>
        public static readonly DeltaVLossProfile EarthToLeo =
            new DeltaVLossProfile("Earth to LEO (typical)", stage1LossMetersPerSecond: 1500.0, 0.0);

        public override string ToString() =>
            $"{Label}: stage1Loss={Stage1LossMetersPerSecond} m/s  latAdv={LatitudeAdvantageMetersPerSecond} m/s";
    }
}
