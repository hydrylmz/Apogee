using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Pure functional calculation engine that transforms a <see cref="Blueprint"/>
    /// into derived engineering and programmatic statistics (<see cref="BlueprintStats"/>).
    /// Populates <see cref="Stat"/> and <see cref="StatModifier"/> instances for full source breakdown.
    /// Complies with GDD §8.1, §8.2, and §17.3.
    /// </summary>
    public static class BlueprintEvaluator
    {
        /// <summary>
        /// Base integration assembly time added per stage in game-days.
        /// GDD §9.3 Entegrasyon.
        /// </summary>
        public const int IntegrationDaysPerStage = 5;

        /// <summary>
        /// Evaluates a blueprint and produces full derived statistics,
        /// optionally computing launch capacity against a required Delta-V.
        /// </summary>
        /// <param name="blueprint">The vehicle blueprint to evaluate.</param>
        /// <param name="lossProfile">Optional trajectory loss profile for Δv calculation.</param>
        /// <param name="requiredDeltaV">
        ///   When &gt; 0, the evaluator also runs <see cref="LaunchCapacityCalculator"/>
        ///   to populate <see cref="BlueprintStats.LaunchCapacity"/>.
        ///   Pass 0 (default) to skip the capacity search.
        /// </param>
        /// <returns>A populated, ready-to-inspect <see cref="BlueprintStats"/>.</returns>
        public static BlueprintStats Evaluate(
            Blueprint blueprint,
            DeltaVLossProfile lossProfile = null,
            double requiredDeltaV = 0.0)
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));

            var stats = new StatCollection();
            // Setup basic stat bounds
            stats.GetOrCreate(StatType.DryMass, minValue: 0.0);
            stats.GetOrCreate(StatType.WetMass, minValue: 0.0);
            stats.GetOrCreate(StatType.PropellantMass, minValue: 0.0);
            stats.GetOrCreate(StatType.Cost, minValue: 0.0);
            stats.GetOrCreate(StatType.BuildTime, minValue: 0.0);
            stats.GetOrCreate(StatType.Reliability, minValue: 0.0, maxValue: 1.0);
            stats.GetOrCreate(StatType.PowerGeneration, minValue: 0.0);
            stats.GetOrCreate(StatType.PowerConsumption, minValue: 0.0);
            stats.GetOrCreate(StatType.PowerMargin);
            stats.GetOrCreate(StatType.BatteryCapacity, minValue: 0.0);
            stats.GetOrCreate(StatType.ThermalLoad, minValue: 0.0);
            stats.GetOrCreate(StatType.ThermalDissipation, minValue: 0.0);
            stats.GetOrCreate(StatType.ThermalMargin);
            stats.GetOrCreate(StatType.DataRate, minValue: 0.0);
            stats.GetOrCreate(StatType.DataStorage, minValue: 0.0);
            stats.GetOrCreate(StatType.DownlinkRate, minValue: 0.0);
            stats.GetOrCreate(StatType.DeltaV, minValue: 0.0);
            stats.GetOrCreate(StatType.PayloadCapacity, minValue: 0.0);

            double totalDryMass = 0.0;
            double totalPropellant = 0.0;
            double totalCost = 0.0;
            int maxComponentBuildDays = 0;
            double systemReliability = 1.0;

            double powerGen = 0.0;
            double powerCon = 0.0;
            double batteryCap = 0.0;
            double thermalLoad = 0.0;
            double thermalDiss = 0.0;
            double dataRate = 0.0;
            double dataStorage = 0.0;
            double downlinkRate = 0.0;

            // 1. Process Payload
            if (blueprint.Payload != null)
            {
                foreach (var comp in blueprint.Payload.AllComponents)
                {
                    double dry = comp.EffectiveDryMass;
                    double cost = comp.EffectiveCost;
                    int days = comp.EffectiveBuildTime;
                    double rel = comp.EffectiveReliability;

                    totalDryMass += dry;
                    totalCost += cost;
                    maxComponentBuildDays = Math.Max(maxComponentBuildDays, days);
                    systemReliability *= rel;

                    powerGen += comp.PowerGeneration;
                    powerCon += comp.PowerConsumption;
                    thermalLoad += comp.ThermalLoad;
                    thermalDiss += comp.ThermalDissipation;
                    dataRate += comp.DataRate;
                    dataStorage += comp.DataStorage;
                    downlinkRate += comp.DownlinkRate;

                    stats.AddModifier(new StatModifier(
                        StatType.DryMass,
                        StatModifierType.Flat,
                        dry,
                        source: comp,
                        sourceName: $"Payload: {comp.Name}"));

                    stats.AddModifier(new StatModifier(
                        StatType.Cost,
                        StatModifierType.Flat,
                        cost,
                        source: comp,
                        sourceName: $"Payload: {comp.Name}"));
                }
            }

            // 2. Process Fairing / Adapter
            if (blueprint.FairingAdapter != null)
            {
                var fa = blueprint.FairingAdapter;
                double dry = fa.EffectiveDryMass;
                double cost = fa.EffectiveCost;
                int days = fa.EffectiveBuildTime;
                double rel = fa.EffectiveReliability;

                totalDryMass += dry;
                totalCost += cost;
                maxComponentBuildDays = Math.Max(maxComponentBuildDays, days);
                systemReliability *= rel;

                stats.AddModifier(new StatModifier(
                    StatType.DryMass,
                    StatModifierType.Flat,
                    dry,
                    source: fa,
                    sourceName: $"Fairing/Adapter: {fa.Name}"));

                stats.AddModifier(new StatModifier(
                    StatType.Cost,
                    StatModifierType.Flat,
                    cost,
                    source: fa,
                    sourceName: $"Fairing/Adapter: {fa.Name}"));
            }

            // 3. Process Rocket Stages
            var rocketStages = new List<RocketStage>(blueprint.Stages.Count);

            for (int i = 0; i < blueprint.Stages.Count; i++)
            {
                var stage = blueprint.Stages[i];
                double stageDry = stage.DryMass;
                double stageCost = stage.Cost;
                int stageDays = stage.BuildTimeDays;
                double stageRel = stage.Reliability;

                totalDryMass += stageDry;
                totalPropellant += stage.PropellantMass;
                totalCost += stageCost;
                maxComponentBuildDays = Math.Max(maxComponentBuildDays, stageDays);
                systemReliability *= stageRel;

                stats.AddModifier(new StatModifier(
                    StatType.DryMass,
                    StatModifierType.Flat,
                    stageDry,
                    source: stage,
                    sourceName: $"Stage {stage.Name}: Structure & Engines"));

                stats.AddModifier(new StatModifier(
                    StatType.PropellantMass,
                    StatModifierType.Flat,
                    stage.PropellantMass,
                    source: stage,
                    sourceName: $"Stage {stage.Name}: Propellant"));

                stats.AddModifier(new StatModifier(
                    StatType.Cost,
                    StatModifierType.Flat,
                    stageCost,
                    source: stage,
                    sourceName: $"Stage {stage.Name}: Hardware"));

                rocketStages.Add(stage.ToRocketStage());
            }

            // Set Totals on StatCollection
            stats.SetBaseValue(StatType.WetMass, 0.0);
            stats.AddModifier(new StatModifier(StatType.WetMass, StatModifierType.Flat, totalDryMass, sourceName: "Total Dry Mass"));
            stats.AddModifier(new StatModifier(StatType.WetMass, StatModifierType.Flat, totalPropellant, sourceName: "Total Propellant Mass"));

            // Assembly / integration time
            int totalIntegrationDays = blueprint.Stages.Count * IntegrationDaysPerStage;
            int totalBuildTime = maxComponentBuildDays + totalIntegrationDays;
            stats.SetBaseValue(StatType.BuildTime, maxComponentBuildDays);
            if (totalIntegrationDays > 0)
            {
                stats.AddModifier(new StatModifier(
                    StatType.BuildTime,
                    StatModifierType.Flat,
                    totalIntegrationDays,
                    sourceName: "Vehicle Assembly & Integration"));
            }

            // Reliability
            stats.SetBaseValue(StatType.Reliability, systemReliability);

            // Power
            stats.SetBaseValue(StatType.PowerGeneration, powerGen);
            stats.SetBaseValue(StatType.PowerConsumption, powerCon);
            stats.SetBaseValue(StatType.PowerMargin, powerGen - powerCon);
            stats.SetBaseValue(StatType.BatteryCapacity, batteryCap);

            // Thermal
            stats.SetBaseValue(StatType.ThermalLoad, thermalLoad);
            stats.SetBaseValue(StatType.ThermalDissipation, thermalDiss);
            stats.SetBaseValue(StatType.ThermalMargin, thermalDiss - thermalLoad);

            // Data & Comms
            stats.SetBaseValue(StatType.DataRate, dataRate);
            stats.SetBaseValue(StatType.DataStorage, dataStorage);
            stats.SetBaseValue(StatType.DownlinkRate, downlinkRate);

            // Delta-V Calculation
            DeltaVCalculationResult deltaVResult = null;
            LaunchCapacityResult launchCapacity = null;

            if (rocketStages.Count > 0)
            {
                double payloadDryMass = (blueprint.Payload != null ? blueprint.Payload.DryMass : 0.0) +
                                       (blueprint.FairingAdapter != null ? blueprint.FairingAdapter.EffectiveDryMass : 0.0);

                var profile = lossProfile ?? DeltaVLossProfile.None;
                deltaVResult = DeltaVCalculator.Calculate(rocketStages, payloadDryMass, profile);

                stats.SetBaseValue(StatType.DeltaV, deltaVResult.NetDeltaV);

                // ── Launch Capacity (M1-08) ─────────────────────────────────────────
                // When a required ΔV is provided (e.g. from a MissionProfile), compute
                // the maximum payload mass the rocket can deliver to that trajectory.
                if (requiredDeltaV > 0.0)
                {
                    launchCapacity = LaunchCapacityCalculator.Calculate(
                        stages:             rocketStages,
                        requiredDeltaV:     requiredDeltaV,
                        lossProfile:        profile,
                        actualPayloadMass:  payloadDryMass);

                    stats.SetBaseValue(StatType.PayloadCapacity, launchCapacity.MaxPayloadMass);
                }
            }

            return new BlueprintStats(stats, deltaVResult, launchCapacity);
        }

        /// <summary>
        /// Convenience overload that automatically extracts the required ΔV from a
        /// <see cref="MissionProfile"/> before evaluating launch capacity.
        /// </summary>
        public static BlueprintStats Evaluate(
            Blueprint blueprint,
            MissionProfile missionProfile,
            DeltaVLossProfile lossProfile = null)
        {
            double reqDv = missionProfile != null ? missionProfile.RequiredDeltaV : 0.0;
            return Evaluate(blueprint, lossProfile, reqDv);
        }
    }
}
