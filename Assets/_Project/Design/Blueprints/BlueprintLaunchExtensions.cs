using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Launch;

namespace Apogee.Design
{
    /// <summary>
    /// Extension bridge to construct <see cref="LaunchSimulationParameters"/> directly
    /// from a <see cref="Blueprint"/>, <see cref="BlueprintStats"/>, and mission parameters.
    /// Complies with GDD §7.6, §8.1, §10.1, and §19.11.
    /// </summary>
    public static class BlueprintLaunchExtensions
    {
        public static LaunchSimulationParameters ToLaunchParameters(
            this Blueprint blueprint,
            BlueprintStats stats,
            double padWeatherRisk,
            double equatorialBonusDv = 0.0,
            bool isCrewed = false,
            ulong seed = 0)
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));

            var stageReliabilities = new List<double>();
            foreach (var stage in blueprint.Stages)
            {
                stageReliabilities.Add(stage.Reliability);
            }

            double overallRel = stats != null ? stats.Reliability : 0.95;
            bool hasAbort = blueprint.Payload != null && blueprint.Payload.AbortSystem != null;
            double abortRel = hasAbort ? blueprint.Payload.AbortSystem.EffectiveReliability : 0.95;

            return new LaunchSimulationParameters(
                missionId: blueprint.Id,
                vehicleName: blueprint.Name,
                stageCount: Math.Max(1, blueprint.Stages.Count),
                stageReliabilities: stageReliabilities,
                overallReliability: overallRel,
                launchPadWeatherRisk: padWeatherRisk,
                equatorialBonusDv: equatorialBonusDv,
                isCrewed: isCrewed,
                hasAbortSystem: hasAbort,
                abortSystemReliability: abortRel,
                seed: seed
            );
        }
    }
}
