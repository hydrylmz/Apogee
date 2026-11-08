using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.Operations;
using Apogee.Design;

namespace Apogee.Core.Simulation.Report
{
    /// <summary>
    /// Pure functional deterministic What-If simulator.
    ///
    /// Applies a single counterfactual decision change to an already-completed mission's
    /// data and re-evaluates the report using <see cref="MissionReportEvaluator"/>. Because
    /// the simulation is fully deterministic, this is cheap: no replay of the full sequence
    /// is required — only the deltas from the alternative event option are applied on top of
    /// the accumulated mission data before re-evaluation.
    ///
    /// Design principle: does NOT mutate any campaign or mission state. All inputs are read-only;
    /// the result is a fresh <see cref="WhatIfComparison"/> object.
    ///
    /// GDD §10.5 ("deterministik olduğu için ucuz"), §13.2, §21 M5-07.
    /// </summary>
    public static class WhatIfSimulator
    {
        /// <summary>
        /// Builds a list of operation events eligible for what-if re-analysis from a mission log.
        /// An event is eligible when it is resolved (not still pending) and has at least one alternative option.
        /// </summary>
        public static IReadOnlyList<ActiveOperationEvent> GetEligibleEvents(
            IReadOnlyList<ActiveOperationEvent> operationEventLog)
        {
            if (operationEventLog == null || operationEventLog.Count == 0)
                return Array.Empty<ActiveOperationEvent>();

            return operationEventLog
                .Where(e => !e.IsPending && e.Record?.Options?.Count > 1)
                .ToList();
        }

        /// <summary>
        /// For a given resolved operation event, returns the alternative options the player did NOT choose.
        /// </summary>
        public static IReadOnlyList<OperationEventOption> GetAlternativeOptions(
            ActiveOperationEvent resolvedEvent)
        {
            if (resolvedEvent?.Record?.Options == null)
                return Array.Empty<OperationEventOption>();

            return resolvedEvent.Record.Options
                .Where(o => o.OptionId != resolvedEvent.ChosenOptionId)
                .ToList();
        }

        /// <summary>
        /// Runs a What-If analysis: swaps the chosen option for <paramref name="alternativeOptionId"/>
        /// in <paramref name="targetEvent"/> and re-evaluates the mission report.
        ///
        /// The delta between the original and alternative options is applied to the accumulated mission
        /// data before re-evaluation (e.g., science data delta, reliability penalty).
        /// All other mission data (blueprint, launch, cruise, EDL) stays identical to the original.
        /// </summary>
        /// <param name="missionId">Mission identifier.</param>
        /// <param name="missionName">Mission display name.</param>
        /// <param name="destinationId">Destination catalogue ID.</param>
        /// <param name="profile">Mission profile used for success criteria.</param>
        /// <param name="blueprint">Spacecraft blueprint (unchanged).</param>
        /// <param name="launchResult">Launch simulation result (unchanged).</param>
        /// <param name="edlResult">EDL simulation result (unchanged).</param>
        /// <param name="downlinkedGb">Actual accumulated downlinked data in GB.</param>
        /// <param name="rawGeneratedGb">Actual accumulated raw generated data in GB.</param>
        /// <param name="droppedGb">Actual accumulated dropped data in GB.</param>
        /// <param name="scienceDaysCompleted">Actual science days completed.</param>
        /// <param name="accumulatedReliabilityPenalty">Actual accumulated reliability penalty.</param>
        /// <param name="accumulatedCorrectionFuel">Actual accumulated correction fuel fraction.</param>
        /// <param name="operationEventLog">Full resolved operation event log for the mission.</param>
        /// <param name="targetEvent">The specific event whose decision is being changed.</param>
        /// <param name="alternativeOptionId">The option ID to apply instead of the original choice.</param>
        /// <param name="originalReport">The original MissionReport to compare against.</param>
        /// <param name="isMissionFailedEarly">Whether the mission ended early.</param>
        /// <param name="earlyFailureReason">Reason for early termination if applicable.</param>
        /// <returns>A <see cref="WhatIfComparison"/> with the original and counterfactual reports.</returns>
        public static WhatIfComparison Simulate(
            string missionId,
            string missionName,
            string destinationId,
            MissionProfile profile,
            Blueprint blueprint,
            Apogee.Core.Simulation.Launch.LaunchSimulationResult launchResult,
            Apogee.Core.Simulation.EDL.EdlSimulationResult edlResult,
            double downlinkedGb,
            double rawGeneratedGb,
            double droppedGb,
            int scienceDaysCompleted,
            double accumulatedReliabilityPenalty,
            double accumulatedCorrectionFuel,
            IReadOnlyList<ActiveOperationEvent> operationEventLog,
            ActiveOperationEvent targetEvent,
            string alternativeOptionId,
            MissionReport originalReport,
            bool isMissionFailedEarly = false,
            string earlyFailureReason = null)
        {
            if (targetEvent == null)
                throw new ArgumentNullException(nameof(targetEvent));
            if (string.IsNullOrEmpty(alternativeOptionId))
                throw new ArgumentNullException(nameof(alternativeOptionId));
            if (originalReport == null)
                throw new ArgumentNullException(nameof(originalReport));

            // Find the original option that was chosen
            var originalOption = targetEvent.Record?.Options?.FirstOrDefault(o => o.OptionId == targetEvent.ChosenOptionId);
            // Find the alternative option
            var alternativeOption = targetEvent.Record?.Options?.FirstOrDefault(o => o.OptionId == alternativeOptionId);

            if (alternativeOption == null)
                throw new ArgumentException(
                    $"Alternative option '{alternativeOptionId}' not found in event '{targetEvent.Record?.EventId}'.",
                    nameof(alternativeOptionId));

            // ── Compute the net delta between original and alternative options ──
            double scienceDataDeltaNet = alternativeOption.ScienceDataDeltaGb
                                       - (originalOption?.ScienceDataDeltaGb ?? 0.0);
            double reliabilityPenaltyDeltaNet = alternativeOption.ReliabilityPenalty
                                              - (originalOption?.ReliabilityPenalty ?? 0.0);

            // ── Apply deltas to accumulated mission data ──
            double whatIfDownlinkedGb = Math.Max(0.0, downlinkedGb + scienceDataDeltaNet);
            double whatIfRawGeneratedGb = Math.Max(0.0, rawGeneratedGb + scienceDataDeltaNet);
            // If the alternative improved science, reduce dropped data proportionally; otherwise increase it
            double whatIfDroppedGb = Math.Max(0.0, droppedGb - scienceDataDeltaNet);
            double whatIfReliabilityPenalty = Math.Max(0.0, Math.Min(1.0, accumulatedReliabilityPenalty + reliabilityPenaltyDeltaNet));

            // ── Build a patched event log replacing the target event's choice ──
            var patchedEventLog = BuildPatchedEventLog(operationEventLog, targetEvent.InstanceId, alternativeOptionId);

            // ── Re-evaluate the report with what-if inputs ──
            MissionReport whatIfMissionReport = MissionReportEvaluator.Evaluate(
                missionId: missionId,
                missionName: missionName,
                destinationId: destinationId,
                profile: profile,
                blueprint: blueprint,
                launchResult: launchResult,
                edlResult: edlResult,
                downlinkedGb: whatIfDownlinkedGb,
                rawGeneratedGb: whatIfRawGeneratedGb,
                droppedGb: whatIfDroppedGb,
                scienceDaysCompleted: scienceDaysCompleted,
                accumulatedReliabilityPenalty: whatIfReliabilityPenalty,
                accumulatedCorrectionFuel: accumulatedCorrectionFuel,
                operationEvents: patchedEventLog,
                isMissionFailedEarly: isMissionFailedEarly,
                earlyFailureReason: earlyFailureReason
            );

            var change = new WhatIfChange(
                eventInstanceId:        targetEvent.InstanceId,
                eventTitle:             targetEvent.Record?.TitleKey ?? targetEvent.Record?.EventId ?? "Bilinmeyen Olay",
                originalOptionId:       targetEvent.ChosenOptionId ?? string.Empty,
                originalOptionLabel:    originalOption?.LabelKey   ?? "(Orijinal Seçim)",
                alternativeOptionId:    alternativeOptionId,
                alternativeOptionLabel: alternativeOption.LabelKey ?? alternativeOptionId
            );

            return new WhatIfComparison(change, originalReport, whatIfMissionReport);
        }

        // ---------------------------------------------------------------- helpers

        private static IReadOnlyList<ActiveOperationEvent> BuildPatchedEventLog(
            IReadOnlyList<ActiveOperationEvent> log,
            string targetInstanceId,
            string newOptionId)
        {
            if (log == null) return Array.Empty<ActiveOperationEvent>();

            var patched = new List<ActiveOperationEvent>(log.Count);
            foreach (var ev in log)
            {
                if (ev.InstanceId == targetInstanceId)
                {
                    // Create a shallow clone of the event with the alternative choice applied
                    var clone = new ActiveOperationEvent(ev.InstanceId, ev.Record, ev.Phase, ev.TriggerTurn)
                    {
                        ChosenOptionId = newOptionId
                    };
                    patched.Add(clone);
                }
                else
                {
                    patched.Add(ev);
                }
            }
            return patched;
        }
    }
}
