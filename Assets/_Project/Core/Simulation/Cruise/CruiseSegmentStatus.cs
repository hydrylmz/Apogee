namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// Status of a cruise segment or event during interplanetary/orbital transit.
    /// GDD §10.1, §10.3.
    /// </summary>
    public enum CruiseSegmentStatus
    {
        /// <summary>Everything nominal, no intervention required.</summary>
        Nominal,

        /// <summary>Minor anomaly resolved autonomously; no campaign impact.</summary>
        MinorAnomaly,

        /// <summary>Requires player decision card (GDD §10.2).</summary>
        DecisionRequired,

        /// <summary>Trajectory correction manoeuvre performed; small fuel cost.</summary>
        CorrectionBurn,

        /// <summary>Partial subsystem degradation; science or reliability reduced.</summary>
        Degraded,

        /// <summary>Mission-ending failure during cruise phase.</summary>
        CriticalFailure
    }
}
