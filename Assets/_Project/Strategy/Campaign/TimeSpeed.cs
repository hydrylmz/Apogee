namespace Apogee.Strategy
{
    /// <summary>
    /// Controls how fast the in-game clock runs relative to real time.
    /// The presentation layer maps each speed to a tick-per-second rate.
    /// Complies with GDD §6.3.
    /// </summary>
    public enum TimeSpeed
    {
        /// <summary>Clock is stopped. No ticks advance.</summary>
        Paused = 0,

        /// <summary>Normal pace: 1 game-day per real-time unit.</summary>
        Normal = 1,

        /// <summary>Faster: 2 game-days per real-time unit.</summary>
        Fast = 2,

        /// <summary>Fastest: 3 game-days per real-time unit.</summary>
        VeryFast = 3,
    }
}
