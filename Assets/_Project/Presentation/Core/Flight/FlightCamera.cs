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
            _targetPan   = pan;
            _targetScale = scale;
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

        /// <summary>Reset to identity transform.</summary>
        public void Reset()
        {
            _targetPan   = Vector2.zero;
            _targetScale = 1.0f;
            if (_reduceMotion)
            {
                _pan   = _targetPan;
                _scale = _targetScale;
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
            _pan   = Vector2.Lerp(_pan,   _targetPan,   t);
            _scale = Mathf.Lerp(_scale, _targetScale, t);
            CommitTransform();
        }

        // ── USS Transform Application ─────────────────────────────────────────────
        private void CommitTransform()
        {
            if (_target == null) return;
            _target.transform.position = new Vector3(_pan.x, _pan.y, 0);
            _target.transform.scale    = new Vector3(_scale, _scale, 1f);
        }

        // ── Preset Table ──────────────────────────────────────────────────────────
        /// <returns>(targetPan, targetScale, lerpSpeed)</returns>
        private static (Vector2 pan, float scale, float speed) ResolvePreset(string key)
        {
            return key switch
            {
                "cam_launch_ground"   => (new Vector2(0,    20f),  1.05f, 1.5f),
                "cam_launch_ascent"   => (new Vector2(0,   -40f),  1.10f, 0.8f),
                "cam_launch_maxq"     => (new Vector2(0,   -80f),  1.20f, 1.0f),
                "cam_launch_orbit"    => (new Vector2(0,    0f),   0.85f, 0.5f),
                "cam_launch_abort"    => (new Vector2(20f,  0f),   1.15f, 2.5f),
                "cam_cruise_arc"      => (new Vector2(-20f, 0f),   0.90f, 0.3f),
                "cam_cruise_decision" => (new Vector2(0,    0f),   1.00f, 1.0f),
                "cam_edl_entry"       => (new Vector2(0,   30f),   1.30f, 1.2f),
                "cam_edl_heating"     => (new Vector2(10f, 20f),   1.40f, 0.9f),
                "cam_edl_parachute"   => (new Vector2(0,   40f),   1.25f, 1.0f),
                "cam_edl_landing"     => (new Vector2(0,   60f),   1.50f, 1.5f),
                "cam_edl_crash"       => (new Vector2(30f, 50f),   1.60f, 3.0f),
                "cam_science_surface" => (new Vector2(-10f, 10f),  1.10f, 0.4f),
                "cam_generic"         => (Vector2.zero,            1.00f, 1.0f),
                _                     => (Vector2.zero,            1.00f, 1.0f),
            };
        }
    }
}
