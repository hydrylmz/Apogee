using System;
using Apogee.Core.Audio;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Central audio director for the Flight Theatre (GDD §3.4, §15.4, ADR-061).
    ///
    /// Responsibilities:
    /// 1. Beat-driven SFX dispatch: routes FlightBeat.AudioEventId -> IAudioService.PlaySfx().
    ///   2. Phase-driven music transition: switches ambient music cue when the ScenePhase changes.
    ///   3. ReduceMotion aware: when active, suppresses all SFX amplitude modulation and
    ///      music cross-fade (plays or stops music immediately, no fade overhead).
    ///   4. PerformanceBudget telemetry: increments the shared repaint counter on each
    ///      audio trigger so the budget tracker can correlate audio load with repaint load.
    ///
    /// Design rules:
    ///   - Pure C# — no UnityEngine types. IAudioService is injected by the caller.
    ///   - Thread-safe single-threaded usage (Unity main thread only).
    ///   - Zero heap allocation per beat (no LINQ, no boxing of primitives).
    ///   - NullAudioService is a valid injection for EditMode tests.
    ///
    /// GDD §3.4, §15.4; ADR-061 (Phase 7).
    /// </summary>
    public sealed class FlightAudioDirector
    {
        // ── Dependencies ─────────────────────────────────────────────────────
        private readonly IAudioService _audio;
        private readonly FlightPerformanceBudget _budget;

        // ── State ─────────────────────────────────────────────────────────────
        private FlightStageView.ScenePhase _currentPhase = FlightStageView.ScenePhase.Pad;
        private bool _reduceMotion;
        private bool _isMusicActive;
        private AudioEvent _currentMusicCue = AudioEvent.None;

        // Music fade duration (seconds). Zero when ReduceMotion is on.
        private const float FadeNormal  = 1.5f;
        private const float FadeInstant = 0.0f;

        // ── Construction ──────────────────────────────────────────────────────
        /// <param name="audioService">IAudioService implementation (NullAudioService in tests).</param>
        /// <param name="budget">Shared performance budget tracker (can be null in tests).</param>
        /// <param name="reduceMotion">ReduceMotion preference from GameSettingsData.</param>
        public FlightAudioDirector(
            IAudioService audioService,
            FlightPerformanceBudget budget = null,
            bool reduceMotion = false)
        {
            _audio        = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _budget       = budget;
            _reduceMotion = reduceMotion;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Update ReduceMotion preference at runtime (synced from SettingsViewModel).
        /// </summary>
        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        /// <summary>
        /// Called by the screen controller when a new FlightBeat starts.
        /// Dispatches the beat's AudioEventId as an SFX and handles music transitions.
        /// </summary>
        public void OnBeatStarted(FlightBeat beat)
        {
            if (beat == null) return;

            // SFX dispatch (always; ReduceMotion only affects visual animation, not sound)
            if (beat.AudioEventId != AudioEvent.None)
            {
                TriggerSfx(beat.AudioEventId);
            }

            // Phase transition -> music cue change
            var newPhase = ResolvePhase(beat.PhaseId);
            if (newPhase != _currentPhase)
            {
                _currentPhase = newPhase;
                TransitionMusic(PhaseToMusicCue(newPhase));
            }

            // Record audio trigger in budget (zero-alloc, only if budget is wired)
            _budget?.RecordAudioTrigger();
        }

        /// <summary>
        /// Called at the terminal beat (success / failure outcome) to trigger the
        /// appropriate musical sting and stop ambient music.
        /// </summary>
        public void OnSequenceFinished(bool success)
        {
            StopMusic();
            TriggerSfx(success ? AudioEvent.MissionSuccessJingle : AudioEvent.MissionFailureJingle);
        }

        /// <summary>
        /// Explicitly stop any playing music (called when the screen is hidden).
        /// </summary>
        public void StopMusic()
        {
            if (!_isMusicActive) return;
            float fade = _reduceMotion ? FadeInstant : FadeNormal * 0.5f;
            _audio.StopMusic(fade);
            _isMusicActive = false;
            _currentMusicCue = AudioEvent.None;
        }

        /// <summary>
        /// Trigger a one-shot SFX without changing phase or music.
        /// (Useful for button feedback in phase footers.)
        /// </summary>
        public void TriggerSfx(AudioEvent evt)
        {
            if (evt == AudioEvent.None) return;
            _audio.PlaySfx(evt);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void TransitionMusic(AudioEvent newCue)
        {
            if (newCue == _currentMusicCue) return;

            if (newCue == AudioEvent.None)
            {
                StopMusic();
                return;
            }

            float fade = _reduceMotion ? FadeInstant : FadeNormal;
            _audio.PlayMusic(newCue, loop: true, fadeDurationSeconds: fade);
            _isMusicActive   = true;
            _currentMusicCue = newCue;
        }

        // Maps a FlightBeat.PhaseId prefix -> ScenePhase
        private static FlightStageView.ScenePhase ResolvePhase(string phaseId)
        {
            if (string.IsNullOrEmpty(phaseId)) return FlightStageView.ScenePhase.Pad;

            // Cruise / EDL / Science prefixes (FlightTimeline.FromCruise/Edl/Science)
            if (phaseId.StartsWith("Cruise", StringComparison.OrdinalIgnoreCase))
                return FlightStageView.ScenePhase.Space;

            if (phaseId.StartsWith("EDL", StringComparison.OrdinalIgnoreCase))
                return FlightStageView.ScenePhase.Landing;

            if (phaseId.StartsWith("Science", StringComparison.OrdinalIgnoreCase))
                return FlightStageView.ScenePhase.Landing;

            // Terminal beats -> phase after the sequence's last event
            if (phaseId.IndexOf("Terminal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return phaseId.StartsWith("Launch", StringComparison.OrdinalIgnoreCase)
                    ? FlightStageView.ScenePhase.Space
                    : FlightStageView.ScenePhase.Landing;
            }

            // Launch phases (LaunchPhaseType names, e.g. "Ignition", "MaxQ", "Stage1Burn")
            if (phaseId.Contains("Orbital", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Insertion", StringComparison.OrdinalIgnoreCase))
            {
                return FlightStageView.ScenePhase.Space;
            }

            if (phaseId.Contains("Ignition", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Liftoff", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("MaxQ", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Stage", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Burn", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Fairing", StringComparison.OrdinalIgnoreCase) ||
                phaseId.Contains("Separation", StringComparison.OrdinalIgnoreCase))
            {
                return FlightStageView.ScenePhase.Ascent;
            }

            return FlightStageView.ScenePhase.Pad;
        }

        // Maps ScenePhase -> ambient music cue
        private static AudioEvent PhaseToMusicCue(FlightStageView.ScenePhase phase) => phase switch
        {
            FlightStageView.ScenePhase.Pad     => AudioEvent.MusicLaunchTension,
            FlightStageView.ScenePhase.Ascent  => AudioEvent.MusicLaunchTension,
            FlightStageView.ScenePhase.Space   => AudioEvent.MusicCruiseAmbient,
            FlightStageView.ScenePhase.Landing => AudioEvent.MusicEdlTension,
            _                                  => AudioEvent.None
        };
    }
}
