namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Text display mode for the Flight Theatre caption layer. (GDD §3.5)
    /// Persisted in GameSettingsData.CaptionMode.
    /// </summary>
    public enum FlightCaptionMode
    {
        /// <summary>No captions — minimal HUD only (T+ clock, altitude, velocity, phase name).</summary>
        Off = 0,

        /// <summary>Default: current beat title + narrative as a fading subtitle bar.</summary>
        Subtitle = 1,

        /// <summary>Full scrollable event log panel with NarrativeKey + TechnicalDetails.</summary>
        FullLog = 2
    }
}
