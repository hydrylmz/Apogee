using System;
using System.Collections.Generic;

namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// Pure functional, deterministic cruise-phase simulation engine.
    ///
    /// Simulates the interplanetary or orbital transit between launch insertion and
    /// arrival at the destination.  Generates segment events that may be:
    ///   • Nominal (no player action needed)
    ///   • Minor anomaly (auto-resolved)
    ///   • Decision card (player must choose an option before proceeding)
    ///   • Trajectory correction burn (small fuel cost)
    ///   • Degraded subsystem (reliability penalty)
    ///   • Critical failure (mission ends mid-cruise)
    ///
    /// Complies with GDD §8.1 (cruise model), §8.4 (determinism), §10.1 (cruise phase),
    /// §10.2 (decision cards), §10.3 (event pool principles), §19.12.
    ///
    /// Event probability weights are derived from spacecraft health parameters so that
    /// a weaker design produces harder cruises (GDD §10.3: "tasarımdaki zayıflığa göre ağırlıklanır").
    /// </summary>
    public static class CruiseSimulator
    {
        // ---------------------------------------------------------------- constants
        /// <summary>
        /// Minimum reliability penalty per critical-failure segment [0–1].
        /// Reflects realistic compounding risk.
        /// </summary>
        private const double CriticalFailureReliabilityHit = 0.40;

        /// <summary>Fuel consumed by a correction burn as fraction of propellant margin.</summary>
        private const double CorrectionBurnFuelCost = 0.06;

        /// <summary>Reliability penalty for a degraded subsystem event.</summary>
        private const double DegradedReliabilityPenalty = 0.05;

        // ---------------------------------------------------------------- public API
        /// <summary>
        /// Run a full deterministic cruise simulation and return the complete result.
        /// No interactivity — all decision cards are recorded in PendingDecisions but
        /// not resolved (the campaign layer decides when to pause and ask the player).
        /// </summary>
        public static CruiseSimulationResult Simulate(CruiseSimulationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var rng    = new SeededRng(parameters.Seed);
            return Simulate(parameters, rng);
        }

        /// <summary>
        /// Overload accepting an explicit <see cref="IRandom"/> for injected testing.
        /// </summary>
        public static CruiseSimulationResult Simulate(CruiseSimulationParameters parameters, IRandom rng)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (rng == null)        throw new ArgumentNullException(nameof(rng));

            var segmentEvents    = new List<CruiseSegmentEvent>();
            var pendingDecisions = new List<CruiseDecisionCard>();

            double reliability          = parameters.SpacecraftReliability;
            double totalRelPenalty      = 0.0;
            double totalCorrectionFuel  = 0.0;

            for (int i = 0; i < parameters.SegmentCount; i++)
            {
                double progress = (i + 1.0) / parameters.SegmentCount;

                // Compute event probability for this segment based on design weaknesses
                double eventProb = ComputeSegmentEventProbability(parameters, reliability, i);

                double roll = rng.NextDouble();

                if (roll > eventProb)
                {
                    // Nominal segment
                    segmentEvents.Add(new CruiseSegmentEvent(
                        segmentIndex:              i,
                        progress:                  progress,
                        status:                    CruiseSegmentStatus.Nominal,
                        titleKey:                  "CRUISE_SEG_NOMINAL_TITLE",
                        narrativeKey:              "CRUISE_SEG_NOMINAL_DESC",
                        technicalDetails:          $"Seg {i}: All systems nominal (Roll={roll:F3}, Threshold={eventProb:F3})."
                    ));
                    continue;
                }

                // An event fires — determine type
                double typeRoll = rng.NextDouble();
                CruiseSegmentEvent evt = SelectEvent(parameters, i, progress, typeRoll, rng,
                    ref reliability, ref totalRelPenalty, ref totalCorrectionFuel,
                    pendingDecisions);

                segmentEvents.Add(evt);

                // Critical failure ends the cruise immediately
                if (evt.Status == CruiseSegmentStatus.CriticalFailure)
                {
                    return new CruiseSimulationResult(
                        missionContinues:            false,
                        finalProgress:               progress,
                        totalReliabilityPenalty:     totalRelPenalty,
                        totalCorrectionFuelFraction: totalCorrectionFuel,
                        segmentEvents:               segmentEvents,
                        pendingDecisions:            pendingDecisions,
                        plainLanguageSummary:
                            "Seyir sırasında kritik bir sistem arızası oluştu. Araç yeniden toparlanamadı ve görev kaybedildi.",
                        primaryCauseKey: "CAUSE_CRUISE_CRITICAL_FAILURE"
                    );
                }
            }

            // Full cruise completed
            return new CruiseSimulationResult(
                missionContinues:            true,
                finalProgress:               1.0,
                totalReliabilityPenalty:     totalRelPenalty,
                totalCorrectionFuelFraction: totalCorrectionFuel,
                segmentEvents:               segmentEvents,
                pendingDecisions:            pendingDecisions,
                plainLanguageSummary:
                    $"Seyir fazı tamamlandı. Araç {parameters.DestinationId} hedefine ulaştı.",
                primaryCauseKey: "CAUSE_CRUISE_NOMINAL"
            );
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Computes the probability that a random event fires in this segment.
        /// Influenced by spacecraft reliability, power/thermal margins, and hazards.
        /// GDD §10.3: "tasarımdaki zayıflığa göre ağırlıklanır".
        /// </summary>
        private static double ComputeSegmentEventProbability(
            CruiseSimulationParameters p,
            double currentReliability,
            int segmentIndex)
        {
            // Base probability per segment — inversely proportional to reliability
            double baseProb = 1.0 - currentReliability;

            // Power deficit multiplier: low power margin → more anomalies
            double powerPenalty = p.PowerMarginRatio < 0.10
                ? 0.20
                : p.PowerMarginRatio < 0.20 ? 0.08 : 0.0;

            // Thermal stress multiplier
            double thermalPenalty = p.ThermalMarginRatio < 0.10
                ? 0.15
                : p.ThermalMarginRatio < 0.20 ? 0.06 : 0.0;

            // Radiation hazard (scales directly)
            double radPenalty = p.RadiationHazard * 0.20;

            // Dust / debris
            double dustPenalty = p.DustHazard * 0.15;

            // Slight increase over time: cumulative wear (0–10% ramp across segments)
            double wearFactor = 0.10 * ((double)segmentIndex / Math.Max(1, p.SegmentCount - 1));

            double totalProb = baseProb + powerPenalty + thermalPenalty + radPenalty + dustPenalty + wearFactor;
            return Clamp(totalProb, 0.02, 0.75); // never fully safe, never certain failure
        }

        /// <summary>
        /// Selects which type of event fires and builds the segment event record.
        /// </summary>
        private static CruiseSegmentEvent SelectEvent(
            CruiseSimulationParameters p,
            int segIdx,
            double progress,
            double typeRoll,
            IRandom rng,
            ref double reliability,
            ref double totalRelPenalty,
            ref double totalFuel,
            List<CruiseDecisionCard> pendingDecisions)
        {
            // Thresholds for event type selection (cumulative):
            // [0.00–0.40) → Minor anomaly (auto-resolved)
            // [0.40–0.65) → Trajectory correction burn
            // [0.65–0.80) → Degraded subsystem
            // [0.80–0.95) → Decision card required
            // [0.95–1.00) → Critical failure (weighted by how low reliability already is)

            // Adjust critical failure threshold: if reliability < 0.7 it becomes more likely
            double critThreshold = 0.95 - (1.0 - reliability) * 0.20;

            if (typeRoll < 0.40)
                return BuildMinorAnomalyEvent(segIdx, progress, rng);

            if (typeRoll < 0.65)
                return BuildCorrectionBurnEvent(segIdx, progress, ref totalFuel);

            if (typeRoll < 0.80)
                return BuildDegradedEvent(segIdx, progress, ref reliability, ref totalRelPenalty);

            if (typeRoll < critThreshold)
                return BuildDecisionCardEvent(segIdx, progress, p, rng, pendingDecisions);

            // Critical failure
            reliability -= CriticalFailureReliabilityHit;
            totalRelPenalty += CriticalFailureReliabilityHit;
            return new CruiseSegmentEvent(
                segmentIndex:    segIdx,
                progress:        progress,
                status:          CruiseSegmentStatus.CriticalFailure,
                titleKey:        "CRUISE_SEG_CRITICAL_FAILURE_TITLE",
                narrativeKey:    "CRUISE_SEG_CRITICAL_FAILURE_DESC",
                technicalDetails: $"Seg {segIdx}: Cascading subsystem failure detected. Roll={typeRoll:F3}.",
                reliabilityPenalty: CriticalFailureReliabilityHit
            );
        }

        private static CruiseSegmentEvent BuildMinorAnomalyEvent(int segIdx, double progress, IRandom rng)
        {
            // Pick from a small pool of minor anomaly subtypes
            double subRoll = rng.NextDouble();

            string titleKey;
            string narrativeKey;
            string techDetail;

            if (subRoll < 0.33)
            {
                titleKey     = "CRUISE_MINOR_SENSOR_GLITCH_TITLE";
                narrativeKey = "CRUISE_MINOR_SENSOR_GLITCH_DESC";
                techDetail   = "Onboard attitude sensor reported transient spike; watchdog reset nominal.";
            }
            else if (subRoll < 0.66)
            {
                titleKey     = "CRUISE_MINOR_COMMS_DROPOUT_TITLE";
                narrativeKey = "CRUISE_MINOR_COMMS_DROPOUT_DESC";
                techDetail   = "Brief comms dropout caused by high-gain antenna pointing error; recovered autonomously.";
            }
            else
            {
                titleKey     = "CRUISE_MINOR_SOLAR_FLARE_TITLE";
                narrativeKey = "CRUISE_MINOR_SOLAR_FLARE_DESC";
                techDetail   = "Minor solar particle event; radiation counters elevated temporarily, within tolerance.";
            }

            return new CruiseSegmentEvent(
                segmentIndex:  segIdx,
                progress:      progress,
                status:        CruiseSegmentStatus.MinorAnomaly,
                titleKey:      titleKey,
                narrativeKey:  narrativeKey,
                technicalDetails: techDetail
            );
        }

        private static CruiseSegmentEvent BuildCorrectionBurnEvent(
            int segIdx, double progress, ref double totalFuel)
        {
            totalFuel += CorrectionBurnFuelCost;
            return new CruiseSegmentEvent(
                segmentIndex:             segIdx,
                progress:                 progress,
                status:                   CruiseSegmentStatus.CorrectionBurn,
                titleKey:                 "CRUISE_SEG_CORRECTION_BURN_TITLE",
                narrativeKey:             "CRUISE_SEG_CORRECTION_BURN_DESC",
                technicalDetails:         $"Trajectory correction manoeuvre executed. Δv ≈ small, fuel cost ≈{CorrectionBurnFuelCost:P0} of margin.",
                correctionBurnFuelPercent: CorrectionBurnFuelCost
            );
        }

        private static CruiseSegmentEvent BuildDegradedEvent(
            int segIdx, double progress, ref double reliability, ref double totalRelPenalty)
        {
            reliability    -= DegradedReliabilityPenalty;
            totalRelPenalty += DegradedReliabilityPenalty;

            return new CruiseSegmentEvent(
                segmentIndex:      segIdx,
                progress:          progress,
                status:            CruiseSegmentStatus.Degraded,
                titleKey:          "CRUISE_SEG_DEGRADED_TITLE",
                narrativeKey:      "CRUISE_SEG_DEGRADED_DESC",
                technicalDetails:  $"Partial subsystem degradation detected. Reliability reduced by {DegradedReliabilityPenalty:P0}.",
                reliabilityPenalty: DegradedReliabilityPenalty
            );
        }

        private static CruiseSegmentEvent BuildDecisionCardEvent(
            int segIdx,
            double progress,
            CruiseSimulationParameters p,
            IRandom rng,
            List<CruiseDecisionCard> pendingDecisions)
        {
            // Pick a decision event type from the pool (GDD §10.2 examples)
            double subRoll = rng.NextDouble();
            CruiseDecisionCard card;

            if (subRoll < 0.30)
                card = BuildSolarStormDecision(segIdx);
            else if (subRoll < 0.55)
                card = BuildDataBottleneckDecision(segIdx);
            else if (subRoll < 0.75)
                card = BuildBatteryLowDecision(segIdx);
            else if (subRoll < 0.90)
                card = BuildGroundStationFaultDecision(segIdx);
            else
                card = BuildPropellantLeakDecision(segIdx);

            pendingDecisions.Add(card);

            return new CruiseSegmentEvent(
                segmentIndex:    segIdx,
                progress:        progress,
                status:          CruiseSegmentStatus.DecisionRequired,
                titleKey:        card.TitleKey,
                narrativeKey:    card.NarrativeKey,
                technicalDetails: $"Seg {segIdx}: Decision card spawned ({card.DecisionId}).",
                decisionCard:    card
            );
        }

        // ---------------------------------------------------------------- decision card builders

        private static CruiseDecisionCard BuildSolarStormDecision(int segIdx)
        {
            return new CruiseDecisionCard(
                decisionId:   "DECISION_SOLAR_STORM",
                titleKey:     "CRUISE_DECISION_SOLAR_STORM_TITLE",
                narrativeKey: "CRUISE_DECISION_SOLAR_STORM_DESC",
                segmentIndex: segIdx,
                options: new[]
                {
                    new CruiseDecisionOption(
                        "SAFE_MODE",
                        "OPTION_ENTER_SAFE_MODE_LABEL",
                        "OPTION_ENTER_SAFE_MODE_CONSEQUENCE",
                        "SAFE_MODE"       // → science loss, hardware safe
                    ),
                    new CruiseDecisionOption(
                        "RISK_HARDWARE",
                        "OPTION_RISK_HARDWARE_LABEL",
                        "OPTION_RISK_HARDWARE_CONSEQUENCE",
                        "RISK_HARDWARE"   // → possible subsystem damage, no science loss
                    )
                }
            );
        }

        private static CruiseDecisionCard BuildDataBottleneckDecision(int segIdx)
        {
            return new CruiseDecisionCard(
                decisionId:   "DECISION_DATA_BOTTLENECK",
                titleKey:     "CRUISE_DECISION_DATA_BOTTLENECK_TITLE",
                narrativeKey: "CRUISE_DECISION_DATA_BOTTLENECK_DESC",
                segmentIndex: segIdx,
                options: new[]
                {
                    new CruiseDecisionOption(
                        "PRIORITIZE_SCIENCE",
                        "OPTION_PRIORITIZE_SCIENCE_LABEL",
                        "OPTION_PRIORITIZE_SCIENCE_CONSEQUENCE",
                        "SCIENCE_PRIORITY"   // → science data retained, housekeeping logs lost
                    ),
                    new CruiseDecisionOption(
                        "BUFFER_COMPRESS",
                        "OPTION_COMPRESS_BUFFER_LABEL",
                        "OPTION_COMPRESS_BUFFER_CONSEQUENCE",
                        "COMPRESS"           // → slight science quality loss, nothing dropped
                    )
                }
            );
        }

        private static CruiseDecisionCard BuildBatteryLowDecision(int segIdx)
        {
            return new CruiseDecisionCard(
                decisionId:   "DECISION_BATTERY_LOW",
                titleKey:     "CRUISE_DECISION_BATTERY_LOW_TITLE",
                narrativeKey: "CRUISE_DECISION_BATTERY_LOW_DESC",
                segmentIndex: segIdx,
                options: new[]
                {
                    new CruiseDecisionOption(
                        "SHUTDOWN_INSTRUMENT",
                        "OPTION_SHUTDOWN_INSTRUMENT_LABEL",
                        "OPTION_SHUTDOWN_INSTRUMENT_CONSEQUENCE",
                        "INSTRUMENT_OFF"   // → science loss, power margin restored
                    ),
                    new CruiseDecisionOption(
                        "REORIENT_PANEL",
                        "OPTION_REORIENT_PANEL_LABEL",
                        "OPTION_REORIENT_PANEL_CONSEQUENCE",
                        "PANEL_REORIENT"   // → correction burn cost, science maintained
                    )
                }
            );
        }

        private static CruiseDecisionCard BuildGroundStationFaultDecision(int segIdx)
        {
            return new CruiseDecisionCard(
                decisionId:   "DECISION_GROUND_STATION_FAULT",
                titleKey:     "CRUISE_DECISION_GROUND_FAULT_TITLE",
                narrativeKey: "CRUISE_DECISION_GROUND_FAULT_DESC",
                segmentIndex: segIdx,
                options: new[]
                {
                    new CruiseDecisionOption(
                        "BACKUP_STATION",
                        "OPTION_BACKUP_STATION_LABEL",
                        "OPTION_BACKUP_STATION_CONSEQUENCE",
                        "EXTRA_COST"     // → budget cost, no data gap
                    ),
                    new CruiseDecisionOption(
                        "WAIT_STATION",
                        "OPTION_WAIT_STATION_LABEL",
                        "OPTION_WAIT_STATION_CONSEQUENCE",
                        "DATA_GAP"       // → data gap this segment, no cost
                    )
                }
            );
        }

        private static CruiseDecisionCard BuildPropellantLeakDecision(int segIdx)
        {
            return new CruiseDecisionCard(
                decisionId:   "DECISION_PROPELLANT_LEAK",
                titleKey:     "CRUISE_DECISION_PROPELLANT_LEAK_TITLE",
                narrativeKey: "CRUISE_DECISION_PROPELLANT_LEAK_DESC",
                segmentIndex: segIdx,
                options: new[]
                {
                    new CruiseDecisionOption(
                        "REDUCE_SCOPE",
                        "OPTION_REDUCE_SCOPE_LABEL",
                        "OPTION_REDUCE_SCOPE_CONSEQUENCE",
                        "SCOPE_REDUCTION" // → fewer destination objectives, mission continues
                    ),
                    new CruiseDecisionOption(
                        "RISKY_REPAIR",
                        "OPTION_RISKY_REPAIR_LABEL",
                        "OPTION_RISKY_REPAIR_CONSEQUENCE",
                        "RISKY_REPAIR"    // → chance of further damage, full scope maintained
                    )
                }
            );
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : v > max ? max : v;
    }
}
