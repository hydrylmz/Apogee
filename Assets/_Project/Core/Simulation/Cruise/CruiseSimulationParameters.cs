using System.Collections.Generic;

namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// Input parameters for the deterministic cruise simulation.
    /// All values are engine-free; Unity layer is not referenced.
    /// GDD §8.1, §8.4, §10.1, §19.12.
    /// </summary>
    public sealed class CruiseSimulationParameters
    {
        // ---------------------------------------------------------------- identity
        /// <summary>Mission identifier (used in determinism / save keys).</summary>
        public string MissionId { get; }

        /// <summary>Human-readable vehicle name.</summary>
        public string VehicleName { get; }

        // ---------------------------------------------------------------- trajectory
        /// <summary>Destination identifier (e.g. "moon", "mars") — used for event weighting.</summary>
        public string DestinationId { get; }

        /// <summary>Total one-way cruise duration in game days (from MissionProfile).</summary>
        public int CruiseDurationDays { get; }

        /// <summary>
        /// Number of discrete simulation segments (resolution).
        /// Each segment is a potential event slot.
        /// Default 10; must be ≥ 3.
        /// </summary>
        public int SegmentCount { get; }

        // ---------------------------------------------------------------- spacecraft health
        /// <summary>
        /// Overall spacecraft reliability going into the cruise phase [0.0 – 1.0].
        /// Derived from BlueprintStats.Reliability × test bonus.
        /// </summary>
        public double SpacecraftReliability { get; }

        /// <summary>
        /// Power margin ratio [0.0 – 1.0]: (generated – consumed) / generated.
        /// Negative values indicate a power deficit entering the cruise.
        /// </summary>
        public double PowerMarginRatio { get; }

        /// <summary>
        /// Thermal margin ratio [0.0 – 1.0].
        /// </summary>
        public double ThermalMarginRatio { get; }

        /// <summary>
        /// Data / comms downlink daily capacity vs daily production ratio.
        /// Values &lt; 1.0 indicate a data bottleneck.
        /// </summary>
        public double CommsCapacityRatio { get; }

        // ---------------------------------------------------------------- hazards
        /// <summary>
        /// Destination-specific radiation hazard level [0.0 – 1.0].
        /// Drives radiation event probability (higher near Jupiter/Europa).
        /// </summary>
        public double RadiationHazard { get; }

        /// <summary>
        /// Dust / debris encounter probability per segment [0.0 – 0.5].
        /// Relevant for Mars (dust storms), asteroid belt routes.
        /// </summary>
        public double DustHazard { get; }

        // ---------------------------------------------------------------- RNG
        /// <summary>Deterministic seed for the cruise simulation.</summary>
        public ulong Seed { get; }

        // ---------------------------------------------------------------- constructor
        public CruiseSimulationParameters(
            string missionId,
            string vehicleName,
            string destinationId,
            int cruiseDurationDays,
            double spacecraftReliability,
            double powerMarginRatio      = 0.20,
            double thermalMarginRatio    = 0.20,
            double commsCapacityRatio    = 1.0,
            double radiationHazard       = 0.10,
            double dustHazard            = 0.05,
            int segmentCount             = 10,
            ulong seed                   = 0)
        {
            MissionId              = missionId ?? string.Empty;
            VehicleName            = vehicleName ?? string.Empty;
            DestinationId          = destinationId ?? string.Empty;
            CruiseDurationDays     = cruiseDurationDays < 1 ? 1 : cruiseDurationDays;
            SpacecraftReliability  = Clamp(spacecraftReliability, 0.0, 1.0);
            PowerMarginRatio       = powerMarginRatio;
            ThermalMarginRatio     = thermalMarginRatio;
            CommsCapacityRatio     = commsCapacityRatio;
            RadiationHazard        = Clamp(radiationHazard, 0.0, 1.0);
            DustHazard             = Clamp(dustHazard, 0.0, 0.5);
            SegmentCount           = segmentCount < 3 ? 3 : segmentCount;
            Seed                   = seed;
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : v > max ? max : v;

        public override string ToString() =>
            $"[CruiseParams] {MissionId} -> {DestinationId}, {CruiseDurationDays}d, Rel={SpacecraftReliability:P0}, Segs={SegmentCount}";
    }
}
