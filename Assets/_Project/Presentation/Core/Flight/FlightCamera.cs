using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// CSS/USS transition-based camera simulation for the Flight Theatre.
    ///
    /// "Camera" here means 2D pan + scale applied to a VisualElement via USS transitions.
    /// No Unity Camera component — purely UI Toolkit transform animation. (GDD §3.4, §15.1)
    ///
    /// Motion presets are keyed by <see cref="FlightBeat.CameraMotionKey"/>:
    ///   "cam_launch_ground"   — static, wide field
    ///   "cam_launch_ascent"   — slow pan up
    ///   "cam_launch_orbit"    — zoom out to space
    ///   "cam_cruise_arc"      — slow pan left-to-right across space scene
    ///   "cam_edl_entry"       — zoom in on descending vehicle
    ///   "cam_edl_landing"     — ground-level tight crop
    ///   "cam_science_surface" — slow pan along surface
    ///   "cam_generic"         — no motion (default)
    /// </summary>
    public sealed class FlightCamera
    {
        // ── Camera State ──────────────────────────────────────────────────────────
        private Vector2 _pan         = Vector2.zero;    // px offset
        private float   _scale       = 1.0f;
        private Vector2 _targetPan   = Vector2.zero;
        private float   _targetScale = 1.0f;
        private float   _lerpSpeed   = 3.0f;            // units/sec in Tick

        // User zoom layer (multiplies the current preset scale).
        // Lets the player zoom in/out on top of the choreographed camera.
        public const float MinUserZoom = 0.55f;
        public const float MaxUserZoom = 1.70f;
        public const float ZoomStep    = 0.08f;
        private float _userZoom    = 1.0f;
        private float _presetScale = 1.0f;

        // Gravity-turn body tilt (degrees, Z rotation) + coupled lateral drift.
        private float _rotation;
        private float _targetRotation;
        private float _drift;                       // px, applied on top of preset pan
        private const float MaxTiltDegrees  = 28f;
        private const float DriftPerDegree  = 1.1f;

        // Camera shake (launch rumble / Max-Q / staging) — suppressed by ReduceMotion.
        private float _shakeAmplitude;
        private float _shakeRemaining;
        private float _shakeDuration;

        private readonly VisualElement _target;         // element whose transform is driven
        private bool   _reduceMotion;

        // ── Construction ──────────────────────────────────────────────────────────
        /// <param name="target">VisualElement whose scale/translate will be animated.</param>
        public FlightCamera(VisualElement target, bool reduceMotion = false)
        {
            _target       = target ?? throw new ArgumentNullException(nameof(target));
            _reduceMotion = reduceMotion;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Apply a motion preset by its CameraMotionKey string.</summary>
        public void ApplyMotionKey(string key)
        {
            (Vector2 pan, float scale, float speed) = ResolvePreset(key ?? "cam_generic");
            _presetScale = scale;
            _targetPan   = pan;
            _targetScale = scale * _userZoom;
            _lerpSpeed   = speed;

            if (_reduceMotion)
            {
                // Snap immediately — no interpolation
                _pan   = _targetPan;
                _scale = _targetScale;
                CommitTransform();
            }
        }

        /// <summary>Toggle ReduceMotion. When enabled, future transitions snap instantly.</summary>
        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        /// <summary>
        /// Zoom in/out on top of the current choreography preset.
        /// Positive delta zooms in, negative zooms out (clamped to
        /// [MinUserZoom, MaxUserZoom] as a multiplier of the preset scale).
        /// </summary>
        public void AdjustZoom(float delta)
        {
            _userZoom    = Mathf.Clamp(_userZoom + delta, MinUserZoom, MaxUserZoom);
            _targetScale = _presetScale * _userZoom;
            if (_reduceMotion)
            {
                _scale = _targetScale;
                CommitTransform();
            }
        }

        /// <summary>Reset the user zoom layer back to 1.0 (keep current preset).</summary>
        public void ResetZoom()
        {
            _userZoom    = 1.0f;
            _targetScale = _presetScale;
            if (_reduceMotion)
            {
                _scale = _targetScale;
                CommitTransform();
            }
        }

        public float UserZoom => _userZoom;

        /// <summary>
        /// Player drag-pan: offsets the choreographed pan by <paramref name="delta"/> px,
        /// clamped so the vehicle element stays inside the theatre's safe area.
        /// </summary>
        public void PanBy(Vector2 delta, Vector2 maxPan)
        {
            _targetPan = new Vector2(
                Mathf.Clamp(_targetPan.x + delta.x, -maxPan.x, maxPan.x),
                Mathf.Clamp(_targetPan.y + delta.y, -maxPan.y, maxPan.y));
            if (_reduceMotion)
            {
                _pan = _targetPan;
                CommitTransform();
            }
        }

        /// <summary>
        /// Gravity-turn body tilt in degrees (positive = lean right). A small
        /// lateral drift is coupled to the tilt so the ascent reads as a smooth
        /// gravity turn instead of a teleport.
        /// </summary>
        public void SetTilt(float degrees)
        {
            _targetRotation = Mathf.Clamp(degrees, -MaxTiltDegrees, MaxTiltDegrees);
            _drift          = _targetRotation * DriftPerDegree;
            if (_reduceMotion)
            {
                _rotation = _targetRotation;
                CommitTransform();
            }
        }

        /// <summary>
        /// Trigger a camera shake (launch rumble, Max-Q buffet, staging kick).
        /// Ignored while ReduceMotion is active.
        /// </summary>
        public void Shake(float amplitude, float durationSeconds)
        {
            if (_reduceMotion || amplitude <= 0f || durationSeconds <= 0f) return;
            _shakeAmplitude = amplitude;
            _shakeDuration  = durationSeconds;
            _shakeRemaining = durationSeconds;
        }

        /// <summary>
        /// Snap instantly to the current targets (no lerp). Used when a new
        /// sequence is loaded so the previous sequence's last frame never shows.
        /// </summary>
        public void Snap()
        {
            _pan      = _targetPan;
            _scale    = _targetScale;
            _rotation = _targetRotation;
            CommitTransform();
        }

        /// <summary>Reset to identity transform.</summary>
        public void Reset()
        {
            _targetPan      = Vector2.zero;
            _targetScale    = 1.0f;
            _targetRotation = 0f;
            _drift          = 0f;
            _shakeRemaining = 0f;
            if (_reduceMotion)
            {
                _pan      = _targetPan;
                _scale    = _targetScale;
                _rotation = _targetRotation;
                CommitTransform();
            }
        }

        /// <summary>
        /// Advance the camera lerp. Call from FlightStageView's animation tick.
        /// <paramref name="dt"/> = seconds since last call.
        /// </summary>
        public void Tick(float dt)
        {
            if (_reduceMotion) return;

            float t = Mathf.Clamp01(_lerpSpeed * dt);
            _pan      = Vector2.Lerp(_pan,   _targetPan,   t);
            _scale    = Mathf.Lerp(_scale, _targetScale, t);
            _rotation = Mathf.Lerp(_rotation, _targetRotation, t);
            if (_shakeRemaining > 0f)
                _shakeRemaining -= dt;
            CommitTransform();
        }

        // ── USS Transform Application ─────────────────────────────────────────────
        private void CommitTransform()
        {
            if (_target == null) return;

            Vector2 offset = _pan + new Vector2(_drift, 0f);
            if (_shakeRemaining > 0f && !_reduceMotion)
            {
                float falloff = Mathf.Clamp01(_shakeRemaining / Mathf.Max(0.01f, _shakeDuration));
                float a       = _shakeAmplitude * falloff;
                offset += new Vector2(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a));
            }

            _target.transform.position = new Vector3(offset.x, offset.y, 0);
            _target.transform.scale    = new Vector3(_scale, _scale, 1f);
            _target.transform.rotation = Quaternion.Euler(0f, 0f, _rotation);
        }

        // ── Preset Table ──────────────────────────────────────────────────────────
        /// <returns>(targetPan, targetScale, lerpSpeed)</returns>
        private static (Vector2 pan, float scale, float speed) ResolvePreset(string key)
        {
            return key switch
            {
                // Launch — pans gain horizontal drift so the frame breathes left/right
                "cam_launch_ground"   or "launch_pad_close"   => (new Vector2(0,    10f),  1.00f, 1.5f),
                "cam_launch_ascent"   or "launch_liftoff"     => (new Vector2(22f, -30f),  1.05f, 0.8f),
                "cam_launch_maxq"     or "launch_maxq"        => (new Vector2(16f, -48f),  1.12f, 1.0f),
                "cam_launch_orbit"    or "launch_orbit"       => (new Vector2(-8f,  0f),   0.85f, 0.5f),
                "cam_launch_abort"    or "launch_abort"       => (new Vector2(-18f, 0f),   1.10f, 2.5f),
                "launch_staging"                              => (new Vector2(26f, -30f),  1.06f, 1.0f),
                "launch_fairing"                              => (new Vector2(28f, -40f),  1.08f, 0.9f),
                "launch_ascent_high"                          => (new Vector2(36f, -50f),  1.02f, 0.7f),

                // Cruise
                "cam_cruise_arc"      or "cruise_transit"     => (new Vector2(-24f, 0f),   0.90f, 0.3f),
                "cam_cruise_decision" or "cruise_decision"    => (new Vector2(0,    0f),   1.00f, 1.0f),
                "cruise_burn"         or "cam_transfer_injection" => (new Vector2(12f, 0f),0.95f, 0.8f),
                "cruise_signal_loss"                          => (new Vector2(28f,  0f),   1.10f, 2.0f),
                // Arrival capture: wide shot showing destination + capture ring
                "cam_arrival_capture" or "arrival_capture"    => (new Vector2(0,  -10f),   0.72f, 0.5f),

                // EDL (Entry, Descent, Landing)
                "cam_edl_deorbit"     or "edl_deorbit"        => (new Vector2(-8f, 10f),   1.10f, 0.8f),
                "cam_edl_entry"       or "edl_entry"          => (new Vector2(8f,  15f),   1.20f, 1.2f),
                "cam_edl_heating"     or "edl_heating"        => (new Vector2(10f, 12f),   1.28f, 0.9f),
                "cam_edl_parachute"   or "edl_chute"          => (new Vector2(-8f, 22f),   1.15f, 1.0f),
                "cam_edl_landing"     or "edl_descent"        => (new Vector2(6f,  30f),   1.28f, 1.2f),
                "cam_edl_touchdown"   or "edl_touchdown"      => (new Vector2(0,   35f),   1.35f, 1.5f),
                "cam_edl_crash"                               => (new Vector2(20f, 30f),   1.40f, 3.0f),

                // Science Surface Operations
                "cam_science_surface" or "science_generic"    => (new Vector2(-14f, 15f),  1.15f, 0.5f),
                "science_scan"                                => (new Vector2(-18f, 20f),  1.25f, 0.8f),
                "science_downlink"                            => (new Vector2(0,   -10f),  1.05f, 0.6f),

                "cam_generic"         or "generic"            => (Vector2.zero,            1.00f, 1.0f),
                _                                             => (Vector2.zero,            1.00f, 1.0f),
            };
        }
    }
}
