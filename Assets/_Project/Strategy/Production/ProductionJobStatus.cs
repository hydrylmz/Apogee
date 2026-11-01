namespace Apogee.Strategy
{
    /// <summary>
    /// Lifecycle states for production, assembly, and testing queue jobs (GDD §9.3, §19.13).
    /// </summary>
    public enum ProductionJobStatus
    {
        /// <summary>Waiting in queue for factory or test facility capacity to become available.</summary>
        Queued = 0,

        /// <summary>Currently active and receiving daily production progress.</summary>
        InProgress = 1,

        /// <summary>Temporarily suspended due to resource bottlenecks or player intervention.</summary>
        Paused = 2,

        /// <summary>Successfully completed, assembled, or certified.</summary>
        Completed = 3,

        /// <summary>Aborted early with partial budget refund.</summary>
        Cancelled = 4
    }
}
