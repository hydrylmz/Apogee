using System;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Pure C# playback clock that advances a timeline of <see cref="FlightBeat"/> objects.
    /// Receives delta time from the caller (Unity's Time.deltaTime in play mode; a fixed step in tests).
    /// No UnityEngine dependency — fully testable in EditMode. (GDD §3.4, §15.1)
    /// </summary>
    public sealed class PlaybackClock
    {
        // ── Speed presets ────────────────────────────────────────────────────
        public const float Speed1x = 1.0f;
        public const float Speed2x = 2.0f;
        public const float Speed4x = 4.0f;

        // ── State ────────────────────────────────────────────────────────────
        private FlightBeat[] _beats;
        private int   _currentIndex;
        private float _elapsedInBeat;
        private float _speed = Speed1x;
        private bool  _isPlaying;
        private bool  _isPaused;
        private bool  _isFinished;

        // ── Public observables ───────────────────────────────────────────────
        public int   CurrentBeatIndex => _currentIndex;
        public float ElapsedInBeat   => _elapsedInBeat;
        public float Speed           => _speed;
        public bool  IsPlaying       => _isPlaying && !_isPaused;
        public bool  IsPaused        => _isPaused;
        public bool  IsFinished      => _isFinished;
        public int   BeatCount       => _beats?.Length ?? 0;

        /// <summary>Current beat or null when not loaded.</summary>
        public FlightBeat CurrentBeat =>
            (_beats != null && _currentIndex >= 0 && _currentIndex < _beats.Length)
                ? _beats[_currentIndex]
                : null;

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fires each time playback advances to a new beat (new beat index, old beat index).</summary>
        public event Action<int, int> BeatAdvanced;

        /// <summary>Fires when the final beat completes naturally.</summary>
        public event Action SequenceFinished;

        /// <summary>Fires when a beat that requires a player decision is reached.</summary>
        public event Action<FlightBeat> DecisionRequired;

        // ── Load ──────────────────────────────────────────────────────────────
        public void Load(FlightBeat[] beats)
        {
            _beats        = beats ?? throw new ArgumentNullException(nameof(beats));
            _currentIndex = 0;
            _elapsedInBeat = 0f;
            _isPlaying    = false;
            _isPaused     = false;
            _isFinished   = false;
        }

        // ── Controls ──────────────────────────────────────────────────────────
        public void Play()
        {
            if (_beats == null || _beats.Length == 0) return;
            _isPlaying = true;
            _isPaused  = false;
        }

        public void Pause()  => _isPaused = !_isPaused;
        public void Unpause() => _isPaused = false;

        public void SetSpeed(float speed)
        {
            if (speed <= 0f) throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be positive.");
            _speed = speed;
        }

        /// <summary>Jump to a specific beat index (wrap in replay/log navigation).</summary>
        public void SeekTo(int beatIndex)
        {
            if (_beats == null) return;
            int prev = _currentIndex;
            _currentIndex  = Math.Clamp(beatIndex, 0, _beats.Length - 1);
            _elapsedInBeat = 0f;
            _isFinished    = false;
            if (_currentIndex != prev)
                BeatAdvanced?.Invoke(_currentIndex, prev);
        }

        /// <summary>Advance one beat (Step button — StepNextPhaseCommand semantics preserved).</summary>
        public void StepNext()
        {
            if (_beats == null || _isFinished) return;
            int prev = _currentIndex;
            if (_currentIndex < _beats.Length - 1)
            {
                _currentIndex++;
                _elapsedInBeat = 0f;
                BeatAdvanced?.Invoke(_currentIndex, prev);
                CheckDecision();
            }
            else
            {
                Finish();
            }
        }

        /// <summary>Skip all remaining beats and jump to the final state (SkipToEndCommand semantics).</summary>
        public void SkipToEnd()
        {
            if (_beats == null || _isFinished) return;
            int prev = _currentIndex;
            _currentIndex  = _beats.Length - 1;
            _elapsedInBeat = _beats[_currentIndex].DurationSeconds;
            BeatAdvanced?.Invoke(_currentIndex, prev);
            Finish();
        }

        /// <summary>Resume from a decision-required pause (called by decision resolution).</summary>
        public void ResumeFromDecision() => _isPaused = false;

        // ── Update (called each frame from MonoBehaviour / UIElement schedule) ──
        /// <summary>Advance the clock. Call once per frame with Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            if (!_isPlaying || _isPaused || _isFinished || _beats == null || _beats.Length == 0)
                return;

            _elapsedInBeat += deltaTime * _speed;

            var beat = _beats[_currentIndex];
            if (_elapsedInBeat >= beat.DurationSeconds)
            {
                // Natural advance
                if (_currentIndex < _beats.Length - 1)
                {
                    int prev = _currentIndex;
                    _currentIndex++;
                    _elapsedInBeat = 0f;
                    BeatAdvanced?.Invoke(_currentIndex, prev);
                    CheckDecision();
                }
                else
                {
                    Finish();
                }
            }
        }

        /// <summary>Interpolated progress within the current beat [0–1].</summary>
        public float BeatProgress =>
            (CurrentBeat != null && CurrentBeat.DurationSeconds > 0f)
                ? Math.Clamp(_elapsedInBeat / CurrentBeat.DurationSeconds, 0f, 1f)
                : 1f;

        // ── Private ───────────────────────────────────────────────────────────
        private void Finish()
        {
            _isFinished = true;
            _isPlaying  = false;
            SequenceFinished?.Invoke();
        }

        private void CheckDecision()
        {
            var beat = CurrentBeat;
            if (beat != null && beat.RequiresDecision)
            {
                _isPaused = true;
                DecisionRequired?.Invoke(beat);
            }
        }
    }
}
