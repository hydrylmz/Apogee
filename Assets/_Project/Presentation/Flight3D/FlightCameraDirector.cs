using UnityEngine;
using Apogee.UI.Core.Flight;

// Nested-type aliases — the shared enums live inside FlightStageView (2D stage).
using ScenePhase = Apogee.UI.Core.Flight.FlightStageView.ScenePhase;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// 3D camera choreography director for the Flight Theatre.
    /// Maps the existing <see cref="FlightBeat.CameraMotionKey"/> strings (shared with
    /// the 2D <see cref="FlightCamera"/> preset table) to world-space camera poses:
    /// an offset from the focus point, a look height offset, FOV and lerp speed.
    ///
    /// Parity with the 2D stage:
    ///   - Player zoom (wheel / +/- / Z) multiplies distance the same way as the 2D
    ///     userZoom layer (ADR-061), clamped to [MinUserZoom, MaxUserZoom].
    ///   - Player drag-pan offsets the frame in camera-local X/Y.
    ///   - Shake on high-energy beats, suppressed by ReduceMotion.
    /// (3D stage plan, Faz 1)
    /// </summary>
    public sealed class FlightCameraDirector
    {
        // ── Player zoom constants (mirror FlightCamera) ───────────────────────────
        public const float MinUserZoom = 0.55f;
        public const float MaxUserZoom = 1.70f;
        public const float ZoomStep    = 0.08f;

        /// <summary>Camera pose preset (offset from focus point, look height, FOV, lerp speed).</summary>
        private readonly struct CameraPreset
        {
            public readonly Vector3 Offset;
            public readonly Vector3 LookOffset;
            public readonly float   Fov;
            public readonly float   Speed;

            public CameraPreset(Vector3 offset, Vector3 lookOffset, float fov, float speed)
            {
                Offset     = offset;
                LookOffset = lookOffset;
                Fov        = fov;
                Speed      = speed;
            }
        }

        // ── Interpolated camera state ─────────────────────────────────────────────
        private Vector3 _offset    = new Vector3(4.5f, 4f, 11f);
        private Vector3 _lookOff   = Vector3.zero;
        private float   _fov       = 46f;
        private float   _speed     = 1.0f;

        private Vector3 _targetOffset;
        private Vector3 _targetLookOff;
        private float   _targetFov;

        private Vector3 _focus = Vector3.up;          // set per tick from the vehicle rig

        // Player layers (2D parity)
        private float   _userZoom  = 1.0f;
        private Vector2 _pan       = Vector2.zero;    // accumulated px
        private Vector2 _targetPan = Vector2.zero;

        // Shake state
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeRemaining;

        // Context
        private ScenePhase _phase        = ScenePhase.Pad;
        private float      _altProgress  = 0f;
        private double     _cruiseProgress = 0.0;

        private readonly Camera _camera;
        private bool _reduceMotion;

        // ── Construction ──────────────────────────────────────────────────────────
        public FlightCameraDirector(Camera camera, bool reduceMotion = false)
        {
            _camera       = camera;
            _reduceMotion = reduceMotion;
            (_targetOffset, _targetLookOff, _targetFov, _) = ResolvePreset("generic");
            _offset  = _targetOffset;
            _lookOff = _targetLookOff;
            _fov     = _targetFov;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Apply a motion preset by its CameraMotionKey string.</summary>
        public void ApplyMotionKey(string key)
        {
            (Vector3 offset, Vector3 lookOff, float fov, float speed) = ResolvePreset(key ?? "generic");
            _targetOffset  = offset;
            _targetLookOff = lookOff;
            _targetFov     = fov;
            _speed         = speed;

            if (_reduceMotion) Snap();
        }

        /// <summary>Feed the current focus point (vehicle position) — called every tick.</summary>
        public void SetFocus(Vector3 focus) => _focus = focus;

        /// <summary>Scene context used for framing adjustments (altitude pull-back, cruise framing).</summary>
        public void SetContext(ScenePhase phase, float altProgress, double cruiseProgress)
        {
            _phase          = phase;
            _altProgress    = altProgress;
            _cruiseProgress = cruiseProgress;
        }

        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        /// <summary>Zoom in/out on top of the current preset (positive delta zooms in).</summary>
        public void AdjustZoom(float delta)
        {
            _userZoom = Mathf.Clamp(_userZoom + delta, MinUserZoom, MaxUserZoom);
            if (_reduceMotion) Snap();
        }

        /// <summary>Reset the user zoom layer back to 1.0.</summary>
        public void ResetZoom()
        {
            _userZoom = 1.0f;
            if (_reduceMotion) Snap();
        }

        public float UserZoom => _userZoom;

        /// <summary>Player drag-pan: offsets the frame by delta px (clamped to the safe area).</summary>
        public void PanBy(Vector2 delta, Vector2 maxPan)
        {
            _targetPan = new Vector2(
                Mathf.Clamp(_targetPan.x + delta.x, -maxPan.x, maxPan.x),
                Mathf.Clamp(_targetPan.y + delta.y, -maxPan.y, maxPan.y));
            if (_reduceMotion) { _pan = _targetPan; }
        }

        /// <summary>Trigger a camera shake (launch rumble, Max-Q buffet, staging kick).</summary>
        public void Shake(float amplitude, float durationSeconds)
        {
            if (_reduceMotion || amplitude <= 0f || durationSeconds <= 0f) return;
            _shakeAmplitude = amplitude;
            _shakeDuration  = durationSeconds;
            _shakeRemaining = durationSeconds;
        }

        /// <summary>Snap instantly to the current targets (no lerp).</summary>
        public void Snap()
        {
            _offset  = ResolveTargetOffset();
            _lookOff = _targetLookOff;
            _fov     = _targetFov;
            _pan     = _targetPan;
            Commit();
        }

        /// <summary>Reset to the identity pose (sequence handoff).</summary>
        public void Reset()
        {
            (_targetOffset, _targetLookOff, _targetFov, _) = ResolvePreset("generic");
            _offset         = _targetOffset;
            _lookOff        = _targetLookOff;
            _fov            = _targetFov;
            _pan            = Vector2.zero;
            _targetPan      = Vector2.zero;
            _userZoom       = 1.0f;
            _shakeRemaining = 0f;
            Commit();
        }

        /// <summary>Advance the camera lerp. <paramref name="dt"/> = seconds since last call.</summary>
        public void Tick(float dt)
        {
            float t = Mathf.Clamp01(_speed * dt);
            _offset  = Vector3.Lerp(_offset,  ResolveTargetOffset(), t);
            _lookOff = Vector3.Lerp(_lookOff, _targetLookOff,         t);
            _fov     = Mathf.Lerp(_fov,       _targetFov,             t);
            _pan     = Vector2.Lerp(_pan,     _targetPan,             t);
            if (_shakeRemaining > 0f)
                _shakeRemaining -= dt;
            Commit();
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        /// <summary>Context-adjusted target offset (altitude pull-back, arrival framing).</summary>
        private Vector3 ResolveTargetOffset()
        {
            Vector3 offset = _targetOffset;
            switch (_phase)
            {
                case ScenePhase.Ascent:
                    // Pull back as the rocket climbs so the frame breathes out
                    offset *= 1f + 0.5f * _altProgress;
                    break;
                case ScenePhase.Space:
                    // Slide the frame toward the destination as the craft approaches
                    offset.z += 4f + 6f * (float)_cruiseProgress;
                    break;
                case ScenePhase.Landing:
                    offset *= 1f - 0.15f * Mathf.Clamp01(_altProgress);
                    break;
            }
            return offset;
        }

        private void Commit()
        {
            if (_camera == null) return;

            Vector3 focus = _focus + _lookOff;

            // Player zoom: distance factor — zoom in (>1) moves the camera closer.
            Vector3 dir = _offset.sqrMagnitude > 0.0001f ? _offset.normalized : Vector3.back;
            float distance = _offset.magnitude / Mathf.Max(0.05f, _userZoom);
            Vector3 position = focus + dir * distance;

            // Player pan: px → camera-local world units (≈0.005 u/px at default framing)
            Vector3 right = _camera.transform.right;
            Vector3 up    = _camera.transform.up;
            position += (right * _pan.x + up * _pan.y) * 0.005f;
            focus    += (right * _pan.x + up * _pan.y) * 0.005f;

            // Shake: decaying random jitter (suppressed entirely by ReduceMotion)
            if (_shakeRemaining > 0f && !_reduceMotion)
            {
                float falloff = Mathf.Clamp01(_shakeRemaining / Mathf.Max(0.01f, _shakeDuration));
                float a       = _shakeAmplitude * falloff;
                position += new Vector3(
                    UnityEngine.Random.Range(-a, a),
                    UnityEngine.Random.Range(-a, a),
                    UnityEngine.Random.Range(-a, a) * 0.3f);
            }

            _camera.transform.position = position;
            _camera.transform.LookAt(focus, Vector3.up);
            _camera.fieldOfView = _fov;
        }

        // ── Preset Table ──────────────────────────────────────────────────────────
        // Keys mirror FlightCamera.ResolvePreset (2D) so flight_choreography.json
        // overrides drive both renderers identically. (3D stage plan, Faz 1)
        private static (Vector3 offset, Vector3 lookOff, float fov, float speed) ResolvePreset(string key)
        {
            return key switch
            {
                // ── Launch ──
                "cam_launch_ground"  or "launch_pad_close"   => Preset(new Vector3( 4.2f,  2.5f,  8.5f), new Vector3(0f, 3.5f, 0f), 42f, 1.5f),
                "cam_launch_ascent"  or "launch_liftoff"     => Preset(new Vector3( 7.0f,  5.0f, 12.0f), new Vector3(0f, 4.0f, 0f), 46f, 0.8f),
                "cam_launch_maxq"    or "launch_maxq"        => Preset(new Vector3( 5.5f,  7.0f, 10.0f), new Vector3(0f, 6.0f, 0f), 44f, 1.0f),
                "cam_launch_orbit"   or "launch_orbit"       => Preset(new Vector3( 0.0f,  8.0f, 26.0f), new Vector3(0f, 3.0f, 0f), 50f, 0.5f),
                "cam_launch_abort"   or "launch_abort"       => Preset(new Vector3(-7.0f,  5.0f, 10.0f), new Vector3(0f, 5.0f, 0f), 46f, 2.5f),
                "launch_staging"                             => Preset(new Vector3( 4.5f,  5.5f,  8.0f), new Vector3(0f, 5.0f, 0f), 44f, 1.0f),
                "launch_fairing"                             => Preset(new Vector3( 3.0f,  7.5f,  6.0f), new Vector3(0f, 7.5f, 0f), 40f, 0.9f),
                "launch_ascent_high"                         => Preset(new Vector3(11.0f,  9.0f, 20.0f), new Vector3(0f, 4.0f, 0f), 50f, 0.7f),

                // ── Cruise ──
                "cam_cruise_arc"     or "cruise_transit"     => Preset(new Vector3(-9.0f,  3.0f, 16.0f), Vector3.zero,               48f, 0.3f),
                "cam_cruise_decision" or "cruise_decision"   => Preset(new Vector3( 4.0f,  1.5f,  8.0f), Vector3.zero,               42f, 1.0f),
                "cruise_burn" or "cam_transfer_injection"    => Preset(new Vector3( 6.0f,  2.0f, 10.0f), new Vector3(0f, -0.5f, 0f), 44f, 0.8f),
                "cruise_signal_loss"                         => Preset(new Vector3( 7.0f,  2.0f,  9.0f), Vector3.zero,               44f, 2.0f),
                "cam_arrival_capture" or "arrival_capture"  => Preset(new Vector3( 0.0f,  6.0f, 34.0f), new Vector3(6f, 0f, 0f),    52f, 0.5f),

                // ── EDL ──
                "cam_edl_deorbit" or "edl_deorbit"           => Preset(new Vector3(-5.0f,  6.0f, 14.0f), Vector3.zero,               48f, 0.8f),
                "cam_edl_entry"   or "edl_entry"             => Preset(new Vector3( 5.5f,  4.0f, 10.0f), Vector3.zero,               46f, 1.2f),
                "cam_edl_heating" or "edl_heating"           => Preset(new Vector3( 4.5f,  3.0f,  8.0f), Vector3.zero,               44f, 0.9f),
                "cam_edl_parachute" or "edl_chute"           => Preset(new Vector3(-6.0f,  6.0f, 12.0f), Vector3.zero,               46f, 1.0f),
                "cam_edl_landing" or "edl_descent"           => Preset(new Vector3( 4.0f,  4.5f,  9.0f), Vector3.zero,               44f, 1.2f),
                "cam_edl_touchdown" or "edl_touchdown"       => Preset(new Vector3( 0.0f,  2.2f,  7.0f), new Vector3(0f, 0.6f, 0f),  40f, 1.5f),
                "cam_edl_crash"                              => Preset(new Vector3( 7.0f,  3.0f,  9.0f), Vector3.zero,               46f, 3.0f),

                // ── Science Surface Operations ──
                "cam_science_surface" or "science_generic"   => Preset(new Vector3(-4.5f,  2.2f,  7.5f), new Vector3(0f, 0.8f, 0f),  44f, 0.5f),
                "science_scan"                               => Preset(new Vector3(-3.5f,  1.8f,  6.0f), new Vector3(0f, 0.6f, 0f),  40f, 0.8f),
                "science_downlink"                           => Preset(new Vector3( 0.0f,  4.0f, 12.0f), Vector3.zero,               46f, 0.6f),

                "cam_generic" or "generic"                   => Preset(new Vector3( 5.0f,  4.0f, 11.0f), Vector3.zero,               46f, 1.0f),
                _                                            => Preset(new Vector3( 5.0f,  4.0f, 11.0f), Vector3.zero,               46f, 1.0f),
            };
        }

        private static (Vector3, Vector3, float, float) Preset(Vector3 offset, Vector3 lookOff, float fov, float speed)
            => (offset, lookOff, fov, speed);
    }
}
