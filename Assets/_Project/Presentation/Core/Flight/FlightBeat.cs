using Apogee.Core.Audio;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Discrete visual/audio unit in the Flight Timeline replay sequence.
    /// A beat maps one simulation event to a camera move, visual effect, audio cue, and caption.
    /// Pure C# — no UnityEngine dependency so it can be tested in EditMode. (GDD §15.1, §10.1)
    /// </summary>
    public sealed class FlightBeat
    {
        // ── Identity ─────────────────────────────────────────────────────────
        /// <summary>Simulation phase identifier string, e.g. "Launch.Ignition" or "Cruise.CorrectionBurn".</summary>
        public string PhaseId { get; }

        /// <summary>Status string as produced by the simulator, e.g. "Nominal" or "CriticalFailure".</summary>
        public string StatusId { get; }

        // ── Localisation ─────────────────────────────────────────────────────
        /// <summary>Localisation key for the caption title shown in subtitle / log modes.</summary>
        public string TitleKey { get; }

        /// <summary>Localisation key for the narrative text (NarrativeKey / *_DESC).</summary>
        public string NarrativeKey { get; }

        /// <summary>Raw technical details string (developer info, tooltip chain).</summary>
        public string TechnicalDetails { get; }

        // ── Telemetry snapshot ────────────────────────────────────────────────
        public double AltitudeKm { get; }
        public double VelocityMps { get; }

        /// <summary>Elapsed mission time in seconds (T+ for launch, cruise days for cruise, etc.).</summary>
        public double TimestampSeconds { get; }

        /// <summary>EDL heat-shield integrity [0–1]. Zero for non-EDL beats.</summary>
        public double HeatShieldIntegrity { get; }

        /// <summary>Cruise progress [0–1]. Zero for non-cruise beats.</summary>
        public double CruiseProgress { get; }

        // ── Choreography ─────────────────────────────────────────────────────
        /// <summary>Camera motion preset key, looked up in flight_choreography.json.</summary>
        public string CameraMotionKey { get; }

        /// <summary>Visual effect preset key (particle, flash, shader).</summary>
        public string VisualEffectKey { get; }

        /// <summary>AudioEvent to fire when this beat starts. None = silent.</summary>
        public AudioEvent AudioEventId { get; }

        /// <summary>Wall-clock duration of this beat in seconds at 1× speed.</summary>
        public float DurationSeconds { get; }

        // ── Flags ─────────────────────────────────────────────────────────────
        /// <summary>When true, playback pauses and waits for player decision resolution.</summary>
        public bool RequiresDecision { get; }

        /// <summary>When true, this is a terminal beat (success, failure, or EDL outcome).</summary>
        public bool IsTerminal { get; }

        /// <summary>LaunchOutcome / EdlOutcome / cruise-end tag for final-state routing.</summary>
        public string OutcomeTag { get; }

        public FlightBeat(
            string phaseId,
            string statusId,
            string titleKey,
            string narrativeKey,
            string technicalDetails,
            double timestampSeconds,
            double altitudeKm,
            double velocityMps,
            double heatShieldIntegrity,
            double cruiseProgress,
            string cameraMotionKey,
            string visualEffectKey,
            AudioEvent audioEventId,
            float durationSeconds,
            bool requiresDecision = false,
            bool isTerminal = false,
            string outcomeTag = null)
        {
            PhaseId             = phaseId ?? string.Empty;
            StatusId            = statusId ?? string.Empty;
            TitleKey            = titleKey ?? string.Empty;
            NarrativeKey        = narrativeKey ?? string.Empty;
            TechnicalDetails    = technicalDetails ?? string.Empty;
            TimestampSeconds    = timestampSeconds;
            AltitudeKm          = altitudeKm;
            VelocityMps         = velocityMps;
            HeatShieldIntegrity = heatShieldIntegrity;
            CruiseProgress      = cruiseProgress;
            CameraMotionKey     = cameraMotionKey ?? "generic";
            VisualEffectKey     = visualEffectKey ?? "none";
            AudioEventId        = audioEventId;
            DurationSeconds     = durationSeconds > 0f ? durationSeconds : 1f;
            RequiresDecision    = requiresDecision;
            IsTerminal          = isTerminal;
            OutcomeTag          = outcomeTag ?? string.Empty;
        }

        public override string ToString() =>
            $"[Beat] {PhaseId}/{StatusId} T={TimestampSeconds:0}s Alt={AltitudeKm:0.#}km " +
            $"dur={DurationSeconds:0.#}s terminal={IsTerminal}";
    }
}
