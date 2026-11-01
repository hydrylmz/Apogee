using System;
using System.Collections.Generic;

namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Comprehensive parameters required to resolve a launch attempt deterministically.
    /// Complies with GDD §8.1, §9.1, §10.1, and §19.12.
    /// </summary>
    public sealed class LaunchSimulationParameters
    {
        public string MissionId { get; }
        public string VehicleName { get; }
        public int StageCount { get; }
        public IReadOnlyList<double> StageReliabilities { get; }
        public double OverallReliability { get; }
        public double LaunchPadWeatherRisk { get; }
        public double EquatorialBonusDv { get; }
        public bool IsCrewed { get; }
        public bool HasAbortSystem { get; }
        public double AbortSystemReliability { get; }
        public ulong Seed { get; }

        public LaunchSimulationParameters(
            string missionId,
            string vehicleName,
            int stageCount,
            IEnumerable<double> stageReliabilities,
            double overallReliability,
            double launchPadWeatherRisk,
            double equatorialBonusDv = 0.0,
            bool isCrewed = false,
            bool hasAbortSystem = false,
            double abortSystemReliability = 0.95,
            ulong seed = 0)
        {
            MissionId = !string.IsNullOrWhiteSpace(missionId) ? missionId : "mission_default";
            VehicleName = !string.IsNullOrWhiteSpace(vehicleName) ? vehicleName : "LaunchVehicle";
            StageCount = Math.Max(1, Math.Min(3, stageCount));

            var stageList = new List<double>();
            if (stageReliabilities != null)
            {
                foreach (var r in stageReliabilities)
                {
                    stageList.Add(Math.Max(0.1, Math.Min(1.0, r)));
                }
            }
            while (stageList.Count < StageCount)
            {
                stageList.Add(0.95);
            }
            StageReliabilities = stageList;

            OverallReliability = Math.Max(0.01, Math.Min(1.0, overallReliability));
            LaunchPadWeatherRisk = Math.Max(0.0, Math.Min(1.0, launchPadWeatherRisk));
            EquatorialBonusDv = Math.Max(0.0, equatorialBonusDv);
            IsCrewed = isCrewed;
            HasAbortSystem = hasAbortSystem;
            AbortSystemReliability = Math.Max(0.1, Math.Min(1.0, abortSystemReliability));
            Seed = seed;
        }
    }
}
