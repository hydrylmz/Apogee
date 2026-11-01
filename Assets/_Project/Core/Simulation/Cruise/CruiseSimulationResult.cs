using System.Collections.Generic;

namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// Complete result of a resolved cruise simulation run.
    /// Immutable after construction; produced by <see cref="CruiseSimulator"/>.
    /// GDD §10.1, §10.3, §10.5.
    /// </summary>
    public sealed class CruiseSimulationResult
    {
        // ---------------------------------------------------------------- outcome
        /// <summary>True if the spacecraft reached the destination without mission-ending failure.</summary>
        public bool MissionContinues { get; }

        /// <summary>True if a critical failure terminated the mission during cruise.</summary>
        public bool IsCruiseLoss => !MissionContinues;

        // ---------------------------------------------------------------- progress
        /// <summary>Final cruise progress [0.0 – 1.0]. 1.0 = destination reached.</summary>
        public double FinalProgress { get; }

        // ---------------------------------------------------------------- health changes
        /// <summary>
        /// Net cumulative reliability penalty applied during cruise.
        /// Ranges from 0.0 (no damage) to SpacecraftReliability (total loss).
        /// </summary>
        public double TotalReliabilityPenalty { get; }

        /// <summary>
        /// Fuel budget consumed by trajectory correction burns, expressed as a fraction of
        /// the spacecraft's propellant margin entering the cruise phase.
        /// </summary>
        public double TotalCorrectionFuelFraction { get; }

        // ---------------------------------------------------------------- events
        /// <summary>All segment events generated during the simulation (for playback and cause-chain).</summary>
        public IReadOnlyList<CruiseSegmentEvent> SegmentEvents { get; }

        /// <summary>
        /// Decision cards awaiting player resolution, in order of occurrence.
        /// Empty on a completed (non-interactive) cruise run.
        /// </summary>
        public IReadOnlyList<CruiseDecisionCard> PendingDecisions { get; }

        // ---------------------------------------------------------------- narrative
        /// <summary>Plain-language summary for the mission report (GDD §10.5, §13.2).</summary>
        public string PlainLanguageSummary { get; }

        /// <summary>Primary cause localisation key for cause-and-effect chain.</summary>
        public string PrimaryCauseKey { get; }

        // ---------------------------------------------------------------- constructor
        public CruiseSimulationResult(
            bool missionContinues,
            double finalProgress,
            double totalReliabilityPenalty,
            double totalCorrectionFuelFraction,
            IEnumerable<CruiseSegmentEvent> segmentEvents,
            IEnumerable<CruiseDecisionCard> pendingDecisions,
            string plainLanguageSummary,
            string primaryCauseKey)
        {
            MissionContinues              = missionContinues;
            FinalProgress                 = finalProgress < 0.0 ? 0.0 : finalProgress > 1.0 ? 1.0 : finalProgress;
            TotalReliabilityPenalty       = totalReliabilityPenalty < 0.0 ? 0.0 : totalReliabilityPenalty;
            TotalCorrectionFuelFraction   = totalCorrectionFuelFraction < 0.0 ? 0.0 : totalCorrectionFuelFraction;
            PlainLanguageSummary          = plainLanguageSummary ?? string.Empty;
            PrimaryCauseKey               = primaryCauseKey ?? string.Empty;

            var segs = new List<CruiseSegmentEvent>();
            if (segmentEvents != null) segs.AddRange(segmentEvents);
            SegmentEvents = segs;

            var decisions = new List<CruiseDecisionCard>();
            if (pendingDecisions != null) decisions.AddRange(pendingDecisions);
            PendingDecisions = decisions;
        }

        public override string ToString() =>
            $"[CruiseResult] Continues={MissionContinues}, Progress={FinalProgress:P0}, Segs={SegmentEvents.Count}, RelPenalty={TotalReliabilityPenalty:F3}";
    }
}
