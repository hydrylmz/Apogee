using System;
using System.Collections.Generic;
using Apogee.Core.Audio;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Runtime beat-override table loaded from Resources/Catalog/Json/flight_choreography.json.
    /// Provides camera key, visual-effect key, audio event name and duration overrides for any
    /// PhaseId × StatusId combination. Falls back to FlightTimeline's built-in defaults when a
    /// key is absent — no exceptions thrown for unknown combinations. (GDD §3.3)
    ///
    /// Pure C# — no UnityEngine dependency. JSON parsing happens in the Unity layer
    /// (FlightChoreographyLoader MonoBehaviour) which calls <see cref="Apply"/>.
    /// </summary>
    public sealed class FlightChoreography
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static FlightChoreography s_instance;
        public static FlightChoreography Instance => s_instance ??= new FlightChoreography();

        // ── Beat override table ───────────────────────────────────────────────
        // Key: "PhaseId/StatusId"  e.g. "Launch.Ignition/Nominal"
        private readonly Dictionary<string, BeatOverride> _overrides
            = new Dictionary<string, BeatOverride>(StringComparer.OrdinalIgnoreCase);

        // ── Limits ────────────────────────────────────────────────────────────
        public FlightTimelineLimits Limits { get; private set; } = FlightTimelineLimits.Default;

        // ─────────────────────────────────────────────────────────────────────
        // Data model (matches flight_choreography.json structure)
        // ─────────────────────────────────────────────────────────────────────

        [Serializable]
        public sealed class BeatOverride
        {
            public string CameraMotionKey;
            public string VisualEffectKey;
            public string AudioEventName;   // AudioEvent enum name string
            public float  DurationSeconds;  // 0 = use timeline default
        }

        [Serializable]
        public sealed class ChoreographyData
        {
            /// <summary>Key format: "PhaseId/StatusId"</summary>
            public Dictionary<string, BeatOverride> Beats;

            public float LaunchMaxSeconds       = FlightTimelineLimits.Default.LaunchMaxSeconds;
            public float CruiseSegmentMaxSeconds= FlightTimelineLimits.Default.CruiseSegmentMaxSeconds;
            public float EdlMaxSeconds          = FlightTimelineLimits.Default.EdlMaxSeconds;
            public float ScienceMaxSeconds      = FlightTimelineLimits.Default.ScienceMaxSeconds;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Load API (called by Unity-side loader; pure-C# testable via Apply)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply a parsed <see cref="ChoreographyData"/> to the singleton.
        /// Safe to call multiple times (last write wins per key).
        /// </summary>
        public void Apply(ChoreographyData data)
        {
            if (data == null) return;

            if (data.Beats != null)
            {
                foreach (var kvp in data.Beats)
                    _overrides[kvp.Key] = kvp.Value;
            }

            Limits = new FlightTimelineLimits
            {
                LaunchMaxSeconds        = data.LaunchMaxSeconds > 0 ? data.LaunchMaxSeconds : FlightTimelineLimits.Default.LaunchMaxSeconds,
                CruiseSegmentMaxSeconds = data.CruiseSegmentMaxSeconds > 0 ? data.CruiseSegmentMaxSeconds : FlightTimelineLimits.Default.CruiseSegmentMaxSeconds,
                EdlMaxSeconds           = data.EdlMaxSeconds > 0 ? data.EdlMaxSeconds : FlightTimelineLimits.Default.EdlMaxSeconds,
                ScienceMaxSeconds       = data.ScienceMaxSeconds > 0 ? data.ScienceMaxSeconds : FlightTimelineLimits.Default.ScienceMaxSeconds
            };
        }

        /// <summary>Reset to defaults (useful in tests).</summary>
        public void Reset()
        {
            _overrides.Clear();
            Limits = new FlightTimelineLimits();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Query API — used by FlightTimeline / FlightStageView
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Try to get a beat override for the given phase+status key.
        /// Returns false and sets override = null when no entry exists.
        /// </summary>
        public bool TryGetOverride(string phaseId, string statusId, out BeatOverride beatOverride)
        {
            string key = phaseId + "/" + statusId;
            return _overrides.TryGetValue(key, out beatOverride);
        }

        /// <summary>
        /// Apply any override fields (non-zero / non-null) on top of an existing beat.
        /// Returns the original beat unchanged when no override is registered.
        /// </summary>
        public FlightBeat ApplyOverride(FlightBeat beat)
        {
            if (beat == null) return null;
            if (!TryGetOverride(beat.PhaseId, beat.StatusId, out var ov) || ov == null)
                return beat;

            AudioEvent audio = beat.AudioEventId;
            if (!string.IsNullOrEmpty(ov.AudioEventName) &&
                Enum.TryParse<AudioEvent>(ov.AudioEventName, out var parsedAudio))
                audio = parsedAudio;

            return new FlightBeat(
                phaseId:            beat.PhaseId,
                statusId:           beat.StatusId,
                titleKey:           beat.TitleKey,
                narrativeKey:       beat.NarrativeKey,
                technicalDetails:   beat.TechnicalDetails,
                timestampSeconds:   beat.TimestampSeconds,
                altitudeKm:         beat.AltitudeKm,
                velocityMps:        beat.VelocityMps,
                heatShieldIntegrity:beat.HeatShieldIntegrity,
                cruiseProgress:     beat.CruiseProgress,
                cameraMotionKey:    !string.IsNullOrEmpty(ov.CameraMotionKey) ? ov.CameraMotionKey : beat.CameraMotionKey,
                visualEffectKey:    !string.IsNullOrEmpty(ov.VisualEffectKey) ? ov.VisualEffectKey : beat.VisualEffectKey,
                audioEventId:       audio,
                durationSeconds:    ov.DurationSeconds > 0f ? ov.DurationSeconds : beat.DurationSeconds,
                requiresDecision:   beat.RequiresDecision,
                isTerminal:         beat.IsTerminal,
                outcomeTag:         beat.OutcomeTag);
        }
    }
}
