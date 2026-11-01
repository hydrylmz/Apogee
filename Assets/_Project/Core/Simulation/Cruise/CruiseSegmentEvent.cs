namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// A single time-stamped event during the cruise phase.
    /// Produced by <see cref="CruiseSimulator"/> and consumed by the presentation layer
    /// and the campaign state (GDD §10.1, §10.3).
    /// </summary>
    public sealed class CruiseSegmentEvent
    {
        /// <summary>Zero-based index of the cruise segment this event belongs to.</summary>
        public int SegmentIndex { get; }

        /// <summary>Elapsed cruise progress at this event [0.0 – 1.0].</summary>
        public double Progress { get; }

        /// <summary>Status of this segment event.</summary>
        public CruiseSegmentStatus Status { get; }

        /// <summary>Localisation key for the event title.</summary>
        public string TitleKey { get; }

        /// <summary>Localisation key for the plain-language narrative (GDD §8.3, §13.2).</summary>
        public string NarrativeKey { get; }

        /// <summary>
        /// Technical details for developers and the "why?" tooltip chain.
        /// Not shown raw to the player.
        /// </summary>
        public string TechnicalDetails { get; }

        /// <summary>
        /// If not null, this event carries a decision card that must be resolved by the player
        /// before the cruise can continue (GDD §10.2).
        /// </summary>
        public CruiseDecisionCard DecisionCard { get; }

        /// <summary>True when <see cref="DecisionCard"/> is populated and awaiting resolution.</summary>
        public bool RequiresDecision => DecisionCard != null;

        /// <summary>
        /// Fuel cost of any trajectory correction manoeuvre performed at this segment, in percent of
        /// the spacecraft's remaining propellant budget.  Zero when no burn occurred.
        /// </summary>
        public double CorrectionBurnFuelPercent { get; }

        /// <summary>
        /// Reliability penalty applied to spacecraft systems as a result of this event [0.0 – 1.0].
        /// Positive value = degradation.
        /// </summary>
        public double ReliabilityPenalty { get; }

        public CruiseSegmentEvent(
            int segmentIndex,
            double progress,
            CruiseSegmentStatus status,
            string titleKey,
            string narrativeKey,
            string technicalDetails = null,
            CruiseDecisionCard decisionCard = null,
            double correctionBurnFuelPercent = 0.0,
            double reliabilityPenalty = 0.0)
        {
            SegmentIndex               = segmentIndex;
            Progress                   = progress;
            Status                     = status;
            TitleKey                   = titleKey ?? string.Empty;
            NarrativeKey               = narrativeKey ?? string.Empty;
            TechnicalDetails           = technicalDetails ?? string.Empty;
            DecisionCard               = decisionCard;
            CorrectionBurnFuelPercent  = correctionBurnFuelPercent;
            ReliabilityPenalty         = reliabilityPenalty;
        }

        public override string ToString() =>
            $"[CruiseSeg {SegmentIndex} @{Progress:P0}] {Status}: {TitleKey}";
    }
}
