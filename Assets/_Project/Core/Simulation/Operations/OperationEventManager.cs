using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Core.Simulation.Operations
{
    /// <summary>
    /// Snapshot of spacecraft health parameters used to weight operation events.
    /// Values are derived from BlueprintStats + accumulated penalties at event-check time.
    /// GDD §10.3: "olaylar tasarımdaki zayıflığa göre ağırlıklanır".
    /// </summary>
    public sealed class OperationHealthSnapshot
    {
        /// <summary>Overall reliability [0.0 – 1.0] after accumulated penalties.</summary>
        public double Reliability { get; }

        /// <summary>Power margin ratio: (generated – consumed) / generated [0.0+].</summary>
        public double PowerMarginRatio { get; }

        /// <summary>Thermal margin ratio [0.0+].</summary>
        public double ThermalMarginRatio { get; }

        /// <summary>Comms downlink capacity vs daily data production ratio [0.0+].</summary>
        public double CommsCapacityRatio { get; }

        /// <summary>Radiation hazard level for this destination [0.0 – 1.0].</summary>
        public double RadiationHazard { get; }

        /// <summary>Dust / debris hazard for this destination [0.0 – 0.5].</summary>
        public double DustHazard { get; }

        public OperationHealthSnapshot(
            double reliability,
            double powerMarginRatio,
            double thermalMarginRatio,
            double commsCapacityRatio,
            double radiationHazard,
            double dustHazard)
        {
            Reliability        = Clamp(reliability,        0.0, 1.0);
            PowerMarginRatio   = Math.Max(0.0, powerMarginRatio);
            ThermalMarginRatio = Math.Max(0.0, thermalMarginRatio);
            CommsCapacityRatio = Math.Max(0.0, commsCapacityRatio);
            RadiationHazard    = Clamp(radiationHazard,   0.0, 1.0);
            DustHazard         = Clamp(dustHazard,        0.0, 0.5);
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// A resolved or pending operation event associated with a mission.
    /// </summary>
    public sealed class ActiveOperationEvent
    {
        /// <summary>Unique instance ID for this event occurrence.</summary>
        public string InstanceId { get; }

        /// <summary>Source event definition.</summary>
        public OperationEventRecord Record { get; }

        /// <summary>Phase the event fired in.</summary>
        public OperationEventPhase Phase { get; }

        /// <summary>Player-chosen option ID; null if still pending.</summary>
        public string ChosenOptionId { get; set; }

        /// <summary>Turn or day number within the mission at which the event was triggered.</summary>
        public int TriggerTurn { get; }

        public ActiveOperationEvent(
            string instanceId,
            OperationEventRecord record,
            OperationEventPhase phase,
            int triggerTurn)
        {
            InstanceId  = instanceId  ?? $"OPEVT-{Guid.NewGuid().ToString().Substring(0, 8)}";
            Record      = record      ?? throw new ArgumentNullException(nameof(record));
            Phase       = phase;
            TriggerTurn = triggerTurn;
        }

        public bool IsPending => string.IsNullOrEmpty(ChosenOptionId);

        public override string ToString() =>
            $"[ActiveOpEvent:{InstanceId}] {Record.EventId} Phase={Phase} Pending={IsPending}";
    }

    /// <summary>
    /// Pure functional deterministic operation event manager.
    /// Builds the canonical event catalog, selects weighted events based on
    /// spacecraft health, and exposes the pending event interface.
    ///
    /// GDD §10.2 (Karar Kartları), §10.3 (Olay Havuzu İlkeleri), §19.12.
    /// </summary>
    public static class OperationEventManager
    {
        // ─── Catalog ────────────────────────────────────────────────────────
        /// <summary>Returns the full built-in catalog of operation events.</summary>
        public static IReadOnlyList<OperationEventRecord> BuildCatalog()
        {
            return new List<OperationEventRecord>
            {
                // ── Cruise Phase Events ─────────────────────────────────────

                new OperationEventRecord(
                    "op_solar_storm",
                    "OPEVT_SOLAR_STORM_TITLE",
                    "OPEVT_SOLAR_STORM_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 18.0,
                    isRepeatable: true,
                    weaknessWeightMultiplier: 2.5,
                    powerWeaknessTriggerBelow: 0.15,
                    options: new[]
                    {
                        new OperationEventOption("safe_mode",      "OPEVT_SAFE_MODE_LABEL",      "OPEVT_SAFE_MODE_CONSEQ",
                            "SAFE_MODE",        scienceDataDeltaGb: -0.5, reputationDelta: 0.0),
                        new OperationEventOption("risk_hardware",  "OPEVT_RISK_HARDWARE_LABEL",  "OPEVT_RISK_HARDWARE_CONSEQ",
                            "RISK_HARDWARE",    reliabilityPenalty: 0.05),
                    }),

                new OperationEventRecord(
                    "op_comms_blackout",
                    "OPEVT_COMMS_BLACKOUT_TITLE",
                    "OPEVT_COMMS_BLACKOUT_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 14.0,
                    isRepeatable: true,
                    commsWeaknessTriggerBelow: 0.80,
                    weaknessWeightMultiplier: 2.0,
                    options: new[]
                    {
                        new OperationEventOption("backup_link",    "OPEVT_BACKUP_LINK_LABEL",    "OPEVT_BACKUP_LINK_CONSEQ",
                            "EXTRA_COST",       budgetDelta: -3.0),
                        new OperationEventOption("wait_blackout",  "OPEVT_WAIT_BLACKOUT_LABEL",  "OPEVT_WAIT_BLACKOUT_CONSEQ",
                            "DATA_GAP",         scienceDataDeltaGb: -0.3),
                    }),

                new OperationEventRecord(
                    "op_battery_low",
                    "OPEVT_BATTERY_LOW_TITLE",
                    "OPEVT_BATTERY_LOW_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 16.0,
                    isRepeatable: true,
                    powerWeaknessTriggerBelow: 0.10,
                    weaknessWeightMultiplier: 3.0,
                    options: new[]
                    {
                        new OperationEventOption("shutdown_instrument", "OPEVT_SHUTDOWN_INST_LABEL",  "OPEVT_SHUTDOWN_INST_CONSEQ",
                            "INSTRUMENT_OFF",   scienceDataDeltaGb: -0.4),
                        new OperationEventOption("reorient_panel",      "OPEVT_REORIENT_PANEL_LABEL", "OPEVT_REORIENT_PANEL_CONSEQ",
                            "PANEL_REORIENT",   reliabilityPenalty: 0.02),
                    }),

                new OperationEventRecord(
                    "op_micrometeorite_impact",
                    "OPEVT_MICROMETEORITE_TITLE",
                    "OPEVT_MICROMETEORITE_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 10.0,
                    isRepeatable: true,
                    dustWeaknessTriggerBelow: 0.0,  // always heavier if dust hazard > 0
                    weaknessWeightMultiplier: 2.0,
                    options: new[]
                    {
                        new OperationEventOption("patch_thermal",  "OPEVT_PATCH_THERMAL_LABEL",  "OPEVT_PATCH_THERMAL_CONSEQ",
                            "REPAIR",           budgetDelta: -2.0, reliabilityPenalty: 0.01),
                        new OperationEventOption("accept_damage",  "OPEVT_ACCEPT_DAMAGE_LABEL",  "OPEVT_ACCEPT_DAMAGE_CONSEQ",
                            "RELIABILITY_HIT",  reliabilityPenalty: 0.06),
                    }),

                new OperationEventRecord(
                    "op_propellant_leak",
                    "OPEVT_PROPELLANT_LEAK_TITLE",
                    "OPEVT_PROPELLANT_LEAK_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 8.0,
                    isRepeatable: false,
                    options: new[]
                    {
                        new OperationEventOption("reduce_scope",  "OPEVT_REDUCE_SCOPE_LABEL",   "OPEVT_REDUCE_SCOPE_CONSEQ",
                            "SCOPE_REDUCTION",  scienceDataDeltaGb: -1.0, reputationDelta: -1.0),
                        new OperationEventOption("risky_repair",  "OPEVT_RISKY_REPAIR_LABEL",   "OPEVT_RISKY_REPAIR_CONSEQ",
                            "RISKY_REPAIR",     reliabilityPenalty: 0.03, reputationDelta: 1.0),
                    }),

                new OperationEventRecord(
                    "op_ground_station_fault",
                    "OPEVT_GROUND_FAULT_TITLE",
                    "OPEVT_GROUND_FAULT_DESC",
                    OperationEventPhase.Cruise,
                    baseWeight: 12.0,
                    isRepeatable: true,
                    commsWeaknessTriggerBelow: 0.60,
                    weaknessWeightMultiplier: 2.5,
                    options: new[]
                    {
                        new OperationEventOption("rent_backup_station", "OPEVT_RENT_BACKUP_LABEL",  "OPEVT_RENT_BACKUP_CONSEQ",
                            "EXTRA_COST",       budgetDelta: -4.0),
                        new OperationEventOption("wait_primary",        "OPEVT_WAIT_PRIMARY_LABEL", "OPEVT_WAIT_PRIMARY_CONSEQ",
                            "DATA_GAP",         scienceDataDeltaGb: -0.5, reputationDelta: -0.5),
                    }),

                // ── Science Phase Events ────────────────────────────────────

                new OperationEventRecord(
                    "op_instrument_calibration_drift",
                    "OPEVT_CALIB_DRIFT_TITLE",
                    "OPEVT_CALIB_DRIFT_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 20.0,
                    isRepeatable: true,
                    options: new[]
                    {
                        new OperationEventOption("recalibrate_now", "OPEVT_RECALIBRATE_LABEL",   "OPEVT_RECALIBRATE_CONSEQ",
                            "RECALIBRATE",     scienceDataDeltaGb: -0.2, researchPointsDelta: 5.0),
                        new OperationEventOption("skip_calib",      "OPEVT_SKIP_CALIB_LABEL",   "OPEVT_SKIP_CALIB_CONSEQ",
                            "SKIP_CALIB",      reliabilityPenalty: 0.03),
                    }),

                new OperationEventRecord(
                    "op_thermal_spike",
                    "OPEVT_THERMAL_SPIKE_TITLE",
                    "OPEVT_THERMAL_SPIKE_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 15.0,
                    isRepeatable: true,
                    thermalWeaknessTriggerBelow: 0.15,
                    weaknessWeightMultiplier: 3.0,
                    options: new[]
                    {
                        new OperationEventOption("reduce_duty_cycle", "OPEVT_REDUCE_DUTY_LABEL",  "OPEVT_REDUCE_DUTY_CONSEQ",
                            "SAFE_MODE",        scienceDataDeltaGb: -0.6),
                        new OperationEventOption("cool_with_maneuver","OPEVT_COOL_MAN_LABEL",    "OPEVT_COOL_MAN_CONSEQ",
                            "RELIABILITY_HIT",  reliabilityPenalty: 0.02, budgetDelta: -1.0),
                    }),

                new OperationEventRecord(
                    "op_data_buffer_overflow",
                    "OPEVT_BUFFER_OVERFLOW_TITLE",
                    "OPEVT_BUFFER_OVERFLOW_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 18.0,
                    isRepeatable: true,
                    commsWeaknessTriggerBelow: 0.70,
                    weaknessWeightMultiplier: 2.5,
                    options: new[]
                    {
                        new OperationEventOption("prioritize_science", "OPEVT_PRIO_SCIENCE_LABEL","OPEVT_PRIO_SCIENCE_CONSEQ",
                            "SCIENCE_PRIORITY",  scienceDataDeltaGb: 0.0,  researchPointsDelta: 2.0),
                        new OperationEventOption("compress_buffer",    "OPEVT_COMPRESS_LABEL",   "OPEVT_COMPRESS_CONSEQ",
                            "COMPRESS",          scienceDataDeltaGb: -0.1, reputationDelta: 0.0),
                    }),

                new OperationEventRecord(
                    "op_unexpected_discovery",
                    "OPEVT_DISCOVERY_TITLE",
                    "OPEVT_DISCOVERY_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 8.0,
                    isRepeatable: false,
                    options: new[]
                    {
                        new OperationEventOption("extend_observation", "OPEVT_EXTEND_OBS_LABEL",  "OPEVT_EXTEND_OBS_CONSEQ",
                            "BONUS_SCIENCE",     scienceDataDeltaGb: 1.5, researchPointsDelta: 20.0, reputationDelta: 3.0),
                        new OperationEventOption("transmit_immediately","OPEVT_TRANSMIT_LABEL",   "OPEVT_TRANSMIT_CONSEQ",
                            "BONUS_SCIENCE",     scienceDataDeltaGb: 0.8, researchPointsDelta: 10.0, reputationDelta: 5.0),
                    }),

                new OperationEventRecord(
                    "op_power_brownout",
                    "OPEVT_BROWNOUT_TITLE",
                    "OPEVT_BROWNOUT_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 14.0,
                    isRepeatable: true,
                    powerWeaknessTriggerBelow: 0.05,
                    weaknessWeightMultiplier: 4.0,
                    options: new[]
                    {
                        new OperationEventOption("shut_nonessential",  "OPEVT_SHUT_NONES_LABEL",  "OPEVT_SHUT_NONES_CONSEQ",
                            "INSTRUMENT_OFF",   scienceDataDeltaGb: -0.5),
                        new OperationEventOption("draw_from_battery",  "OPEVT_BATTERY_DRAW_LABEL","OPEVT_BATTERY_DRAW_CONSEQ",
                            "RISK_HARDWARE",    reliabilityPenalty: 0.03),
                    }),

                new OperationEventRecord(
                    "op_radiation_hit",
                    "OPEVT_RADIATION_HIT_TITLE",
                    "OPEVT_RADIATION_HIT_DESC",
                    OperationEventPhase.Science,
                    baseWeight: 12.0,
                    isRepeatable: true,
                    weaknessWeightMultiplier: 2.0,
                    options: new[]
                    {
                        new OperationEventOption("radiation_safe_mode","OPEVT_RAD_SAFE_LABEL",    "OPEVT_RAD_SAFE_CONSEQ",
                            "SAFE_MODE",        scienceDataDeltaGb: -0.4, reliabilityPenalty: 0.0),
                        new OperationEventOption("continue_risk",      "OPEVT_CONTINUE_RISK_LABEL","OPEVT_CONTINUE_RISK_CONSEQ",
                            "RISK_HARDWARE",    reliabilityPenalty: 0.07),
                    }),

                // ── Any Phase Events ────────────────────────────────────────

                new OperationEventRecord(
                    "op_attitude_control_glitch",
                    "OPEVT_ADCS_GLITCH_TITLE",
                    "OPEVT_ADCS_GLITCH_DESC",
                    OperationEventPhase.Any,
                    baseWeight: 11.0,
                    isRepeatable: true,
                    options: new[]
                    {
                        new OperationEventOption("auto_reset",      "OPEVT_AUTO_RESET_LABEL",   "OPEVT_AUTO_RESET_CONSEQ",
                            "REPAIR",           reliabilityPenalty: 0.01),
                        new OperationEventOption("manual_override", "OPEVT_MANUAL_OVR_LABEL",   "OPEVT_MANUAL_OVR_CONSEQ",
                            "RISK_HARDWARE",    reliabilityPenalty: 0.04, budgetDelta: -1.0),
                    }),

                new OperationEventRecord(
                    "op_spare_part_shortage",
                    "OPEVT_SPARE_SHORTAGE_TITLE",
                    "OPEVT_SPARE_SHORTAGE_DESC",
                    OperationEventPhase.Any,
                    baseWeight: 9.0,
                    isRepeatable: false,
                    options: new[]
                    {
                        new OperationEventOption("emergency_supply",  "OPEVT_EMERGENCY_SUPPLY_LABEL","OPEVT_EMERGENCY_SUPPLY_CONSEQ",
                            "EXTRA_COST",       budgetDelta: -8.0, reliabilityPenalty: 0.0),
                        new OperationEventOption("improvise_repair",  "OPEVT_IMPROVISE_LABEL",   "OPEVT_IMPROVISE_CONSEQ",
                            "RELIABILITY_HIT",  reliabilityPenalty: 0.05, budgetDelta: -1.0),
                    }),

                new OperationEventRecord(
                    "op_science_instrument_failure",
                    "OPEVT_INST_FAIL_TITLE",
                    "OPEVT_INST_FAIL_DESC",
                    OperationEventPhase.Any,
                    baseWeight: 7.0,
                    isRepeatable: false,
                    options: new[]
                    {
                        new OperationEventOption("bypass_failed_inst","OPEVT_BYPASS_INST_LABEL", "OPEVT_BYPASS_INST_CONSEQ",
                            "SCOPE_REDUCTION",  scienceDataDeltaGb: -1.0, reputationDelta: -2.0),
                        new OperationEventOption("attempt_restart",   "OPEVT_RESTART_INST_LABEL","OPEVT_RESTART_INST_CONSEQ",
                            "RISKY_REPAIR",     reliabilityPenalty: 0.04, scienceDataDeltaGb: -0.3),
                    }),
            };
        }

        // ─── Event Selection ────────────────────────────────────────────────

        /// <summary>
        /// Selects one event from the catalog that matches <paramref name="phase"/>
        /// using deterministic weighted random selection based on spacecraft health.
        ///
        /// Returns null if no eligible event passes the selection roll or the catalog is empty.
        ///
        /// GDD §10.3: event pool weighted by design weakness.
        /// </summary>
        public static OperationEventRecord SelectEvent(
            IReadOnlyList<OperationEventRecord> catalog,
            OperationEventPhase phase,
            OperationHealthSnapshot health,
            IRandom rng,
            IReadOnlyCollection<string> usedNonRepeatableEventIds)
        {
            if (catalog == null || rng == null) return null;

            // Build weighted pool
            var pool   = new List<OperationEventRecord>();
            var weights = new List<double>();
            double totalWeight = 0.0;

            foreach (var record in catalog)
            {
                // Phase filter
                if (record.Phase != OperationEventPhase.Any && record.Phase != phase)
                    continue;

                // Non-repeatable filter
                if (!record.IsRepeatable &&
                    usedNonRepeatableEventIds != null &&
                    usedNonRepeatableEventIds.Contains(record.EventId))
                    continue;

                double w = ComputeEffectiveWeight(record, health);
                if (w <= 0.0) continue;

                pool.Add(record);
                weights.Add(w);
                totalWeight += w;
            }

            if (pool.Count == 0 || totalWeight <= 0.0) return null;

            // Weighted random selection
            double pick = rng.NextDouble() * totalWeight;
            double accumulate = 0.0;
            for (int i = 0; i < pool.Count; i++)
            {
                accumulate += weights[i];
                if (pick <= accumulate)
                    return pool[i];
            }

            return pool[pool.Count - 1]; // fallback (rounding)
        }

        /// <summary>
        /// Checks whether an operation event roll should fire for the current turn.
        /// Base probability is modulated by reliability and time-cumulative wear.
        /// </summary>
        public static bool ShouldFireEvent(
            OperationHealthSnapshot health,
            OperationEventPhase phase,
            int turnIndex,
            int totalTurns,
            IRandom rng,
            double baseEventChance = 0.30)
        {
            if (rng == null) return false;

            // Lower reliability -> higher event chance
            double relPenalty = (1.0 - health.Reliability) * 0.25;

            // Cumulative wear (up to +10% over the mission)
            double wearBonus = totalTurns > 1
                ? 0.10 * ((double)turnIndex / (totalTurns - 1))
                : 0.0;

            // Phase-specific modifiers
            double phaseBonus = phase == OperationEventPhase.Science ? 0.05 : 0.0;

            double probability = baseEventChance + relPenalty + wearBonus + phaseBonus;
            probability = probability < 0.05 ? 0.05 : probability > 0.75 ? 0.75 : probability;

            return rng.NextDouble() < probability;
        }

        // ─── Private Helpers ─────────────────────────────────────────────────

        private static double ComputeEffectiveWeight(
            OperationEventRecord record,
            OperationHealthSnapshot health)
        {
            double w = record.BaseWeight;

            // Power weakness amplifier
            if (health.PowerMarginRatio < record.PowerWeaknessTriggerBelow)
                w *= record.WeaknessWeightMultiplier;

            // Thermal weakness amplifier
            if (health.ThermalMarginRatio < record.ThermalWeaknessTriggerBelow)
                w *= record.WeaknessWeightMultiplier;

            // Comms weakness amplifier
            if (health.CommsCapacityRatio < record.CommsWeaknessTriggerBelow)
                w *= record.WeaknessWeightMultiplier;

            // Radiation-driven amplifier for solar storm / radiation hit
            if (health.RadiationHazard > 0.20 &&
                (record.EventId == "op_solar_storm" || record.EventId == "op_radiation_hit"))
            {
                w *= 1.0 + health.RadiationHazard * 2.0;
            }

            // Dust hazard amplifier for micrometeorite
            if (health.DustHazard > 0.10 && record.EventId == "op_micrometeorite_impact")
            {
                w *= 1.0 + health.DustHazard * 3.0;
            }

            return w;
        }
    }
}
