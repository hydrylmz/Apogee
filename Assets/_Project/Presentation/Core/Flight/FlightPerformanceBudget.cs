using System;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Lightweight performance budget tracker for the Flight Theatre (ADR-061, GDD §3.4).
    ///
    /// Tracks:
    ///   - Repaint count per second (target: ≤30 Hz, i.e. ≤30 repaints/sec)
    ///   - Audio trigger count per second (target: ≤4 triggers/sec average)
    ///   - Budget violation flag (repaint rate exceeded TargetRepaintHz)
    ///
    /// Design rules:
    ///   - Pure C# — no UnityEngine types. Tick(dt) is called by the screen controller.
    ///   - Zero heap allocation per frame (no List resize, no boxing).
    ///   - Thread-safe single-threaded usage (Unity main thread only).
    ///   - In WebGL the repaint schedule is set to 33 ms (≤30 Hz); this tracker
    ///     asserts that the actual delivered rate stays within budget.
    ///
    /// GDD §17.3, §17.4; ADR-061 (Phase 7).
    /// </summary>
    public sealed class FlightPerformanceBudget
    {
        // ── Constants ────────────────────────────────────────────────────────
        public const float TargetRepaintHz  = 30f;    // ≤30 repaints/sec
        public const float MaxAudioHz       = 4f;     // ≤4 audio triggers/sec average
        public const float WindowSeconds    = 1.0f;   // measurement window

        // ── Counters (reset every WindowSeconds) ─────────────────────────────
        private int   _repaintsInWindow;
        private int   _audioTriggersInWindow;
        private float _windowElapsed;

        // ── Published snapshot (last completed window) ────────────────────────
        public int   LastRepaintsPerSecond       { get; private set; }
        public int   LastAudioTriggersPerSecond  { get; private set; }
        public bool  IsRepaintBudgetExceeded     { get; private set; }
        public bool  IsAudioBudgetExceeded       { get; private set; }

        /// <summary>
        /// Total repaint count since the director was created (never resets — for lifetime audit).
        /// </summary>
        public long TotalRepaints     { get; private set; }
        public long TotalAudioEvents  { get; private set; }

        // ── Tick API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Advance the measurement window. Called by the screen controller's
        /// animation tick (same schedule as the repaint interval, ~33 ms).
        /// dt = seconds since last call.
        /// </summary>
        public void Tick(float dt)
        {
            _windowElapsed += dt;
            if (_windowElapsed >= WindowSeconds)
            {
                // Snapshot
                LastRepaintsPerSecond      = _repaintsInWindow;
                LastAudioTriggersPerSecond = _audioTriggersInWindow;
                IsRepaintBudgetExceeded    = _repaintsInWindow  > TargetRepaintHz;
                IsAudioBudgetExceeded      = _audioTriggersInWindow > MaxAudioHz;

                // Reset counters
                _repaintsInWindow     = 0;
                _audioTriggersInWindow = 0;
                _windowElapsed         = 0f;
            }
        }

        /// <summary>
        /// Called by FlightStageView.OnAnimationTick() (the repaint callback).
        /// </summary>
        public void RecordRepaint()
        {
            _repaintsInWindow++;
            TotalRepaints++;
        }

        /// <summary>
        /// Called by FlightAudioDirector.OnBeatStarted() when an audio event fires.
        /// </summary>
        public void RecordAudioTrigger()
        {
            _audioTriggersInWindow++;
            TotalAudioEvents++;
        }

        /// <summary>
        /// Returns a one-line budget summary suitable for the HUD developer overlay.
        /// </summary>
        public string GetSummary()
        {
            string repaintStatus = IsRepaintBudgetExceeded ? "!!" : "OK";
            string audioStatus   = IsAudioBudgetExceeded   ? "!!" : "OK";
            return $"Repaint: {LastRepaintsPerSecond}/s [{repaintStatus}] " +
                   $"| Audio: {LastAudioTriggersPerSecond}/s [{audioStatus}] " +
                   $"| Total: {TotalRepaints}";
        }

        /// <summary>
        /// Reset all counters (useful for unit tests to start fresh).
        /// </summary>
        public void Reset()
        {
            _repaintsInWindow      = 0;
            _audioTriggersInWindow = 0;
            _windowElapsed         = 0f;
            LastRepaintsPerSecond       = 0;
            LastAudioTriggersPerSecond  = 0;
            IsRepaintBudgetExceeded     = false;
            IsAudioBudgetExceeded       = false;
            TotalRepaints    = 0;
            TotalAudioEvents = 0;
        }
    }
}
