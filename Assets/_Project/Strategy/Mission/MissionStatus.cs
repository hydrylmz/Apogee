namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Lifecycle state of a campaign mission instance.
    /// GDD §19.12.
    /// </summary>
    public enum MissionStatus
    {
        /// <summary>Mission is being planned; blueprint not yet finalised.</summary>
        Planning,

        /// <summary>Vehicle is in the production / integration queue.</summary>
        InProduction,

        /// <summary>Vehicle is ready; waiting for the next launch window.</summary>
        AwaitingWindow,

        /// <summary>Vehicle launched; cruise phase underway.</summary>
        InCruise,

        /// <summary>
        /// Cruise paused: a decision card is waiting for player resolution.
        /// </summary>
        DecisionPending,

        /// <summary>Vehicle arrived; EDL / arrival phase in progress (M5-03).</summary>
        Arrival,

        /// <summary>Mission at destination performing science / operations (M5-04).</summary>
        Operating,

        /// <summary>Mission completed successfully.</summary>
        Completed,

        /// <summary>Mission ended in failure (vehicle lost or objectives unachievable).</summary>
        Failed
    }
}
