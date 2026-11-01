using System;

namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Parameters required to deterministically resolve an EDL / arrival phase.
    /// Complies with GDD §7.7, §8.1, §10.1, §16.1, and §19.12.
    /// </summary>
    public sealed class EdlSimulationParameters
    {
        public string MissionId { get; }
        public string DestinationId { get; }
        public bool HasAtmosphere { get; }
        public double AtmospherePressureKpa { get; }
        public bool HasHeatShield { get; }
        public string HeatShieldTier { get; } // "none", "mk1", "mk2"
        public bool HasParachute { get; }
        public bool HasSkycraneOrRetro { get; }
        public double AvailableLandingDv { get; }
        public double RequiredLandingDv { get; }
        public double SpacecraftReliability { get; }
        public double EdlSystemReliability { get; }
        public bool IsCrewed { get; }
        public ulong Seed { get; }

        public EdlSimulationParameters(
            string missionId,
            string destinationId,
            bool hasAtmosphere,
            double atmospherePressureKpa,
            bool hasHeatShield,
            string heatShieldTier,
            bool hasParachute,
            bool hasSkycraneOrRetro,
            double availableLandingDv,
            double requiredLandingDv,
            double spacecraftReliability,
            double edlSystemReliability,
            bool isCrewed = false,
            ulong seed = 0)
        {
            MissionId = !string.IsNullOrWhiteSpace(missionId) ? missionId : "mission_default";
            DestinationId = !string.IsNullOrWhiteSpace(destinationId) ? destinationId : "moon";
            HasAtmosphere = hasAtmosphere;
            AtmospherePressureKpa = Math.Max(0.0, atmospherePressureKpa);
            HasHeatShield = hasHeatShield;
            HeatShieldTier = heatShieldTier ?? (hasHeatShield ? "mk1" : "none");
            HasParachute = hasParachute;
            HasSkycraneOrRetro = hasSkycraneOrRetro;
            AvailableLandingDv = Math.Max(0.0, availableLandingDv);
            RequiredLandingDv = Math.Max(0.0, requiredLandingDv);
            SpacecraftReliability = Math.Max(0.01, Math.Min(1.0, spacecraftReliability));
            EdlSystemReliability = Math.Max(0.01, Math.Min(1.0, edlSystemReliability));
            IsCrewed = isCrewed;
            Seed = seed;
        }
    }
}
