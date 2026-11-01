namespace Apogee.Strategy
{
    /// <summary>
    /// The completion status of a single research node within an active campaign.
    /// GDD §9.2, §19.7.
    /// </summary>
    public enum ResearchStatus
    {
        /// <summary>Prerequisites not met — cannot be queued yet.</summary>
        Locked = 0,

        /// <summary>Prerequisites met but not yet started.</summary>
        Available = 1,

        /// <summary>Queued or actively being researched; <see cref="ResearchNodeState.ProgressDays"/> accumulating.</summary>
        InProgress = 2,

        /// <summary>Fully completed; all effects and module unlocks applied.</summary>
        Completed = 3
    }
}
