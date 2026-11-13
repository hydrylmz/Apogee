namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Playback duration upper limits (1× speed) for each flight segment type.
    /// Values are loaded from flight_choreography.json at runtime; these constants
    /// are the compile-time defaults (decided in §3.4). Never hardcode elsewhere.
    /// Pure C# — no UnityEngine dependency. (GDD §3.4)
    /// </summary>
    public sealed class FlightTimelineLimits
    {
        // ── §3.4 decided values ───────────────────────────────────────────────
        /// <summary>Total launch sequence max duration at 1× speed (seconds).</summary>
        public float LaunchMaxSeconds { get; set; } = 75f;

        /// <summary>Max duration per cruise segment at 1× speed (seconds).</summary>
        public float CruiseSegmentMaxSeconds { get; set; } = 6f;

        /// <summary>Total EDL sequence max duration at 1× speed (seconds).</summary>
        public float EdlMaxSeconds { get; set; } = 45f;

        /// <summary>Total science operation animation max duration at 1× speed (seconds).</summary>
        public float ScienceMaxSeconds { get; set; } = 20f;

        // ── Singleton default ─────────────────────────────────────────────────
        private static readonly FlightTimelineLimits s_default = new FlightTimelineLimits();

        /// <summary>Compile-time defaults. Replaced at runtime by FlightChoreography.Load().</summary>
        public static FlightTimelineLimits Default => s_default;
    }
}
