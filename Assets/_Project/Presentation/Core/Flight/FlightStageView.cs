using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Vehicle;
using Apogee.UI.Flight3D;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Full-screen animated scene rendered via a single Painter2D pass at ≤30 Hz.
    /// Hosts 4 scene layers driven by a <see cref="FlightBeat"/>:
    ///   1. Pad/Background  — sky gradient, terrain silhouette, stars
    ///   2. Effects         — clouds, vapour cone (Max-Q), atmosphere glow
    ///   3. Vehicle         — <see cref="VehicleSilhouetteView"/> composite
    ///   4. Trajectory      — cruise arc / EDL descent vector
    ///
    /// Design rules:
    ///   - Single Painter2D pass per repaint — no nested canvases.
    ///   - Repaint capped to ≤30 Hz via schedule (33 ms interval).
    ///   - Particles are pooled via <see cref="ParticlePool"/>; zero per-frame allocation.
    ///   - ReduceMotion: when true, suppresses all flash and animation (transitions only).
    ///   - No UnityEngine types in Core/Flight layer — this file lives in Presentation.
    ///   GDD §3.4, §15.1.
    /// </summary>
    public sealed class FlightStageView : VisualElement, IFlightStage
    {
        // ── Scene phase ───────────────────────────────────────────────────────────
        public enum ScenePhase { Pad, Ascent, Space, Landing }

        // ── Destination visual profile (populated from flight_visuals.json via caller) ──
        public struct DestinationProfile
        {
            public Color  BodyColor;
            public Color  AtmosphereColor;
            public bool   HasAtmosphere;
            public Color  OrbitRingColor;
            public bool   HasRings;
            public float  BodyScale;         // relative [0–1]
            public float  GlowIntensity;
            public string BackgroundKey;
        }

        // ── State ─────────────────────────────────────────────────────────────────
        private FlightBeat         _currentBeat;
        private ScenePhase         _phase = ScenePhase.Pad;
        private DestinationProfile _destination;
        private bool               _reduceMotion;
        private FlightPerformanceBudget _budget;

        // Sky gradient lerp
        private Color _skyTop    = new Color(0.027f, 0.043f, 0.078f);
        private Color _skyBottom = new Color(0.054f, 0.086f, 0.157f);

        // Altitude-based progress [0–1] within current phase
        private float _altitudeProgress;

        // Animation
        private float _starTwinklePhase;
        private float _cloudDriftX;
        private bool  _animRunning;
        private IVisualElementScheduledItem _sceneSchedule;

        // Sub-views
        private readonly VehicleSilhouetteView _vehicle;
        private readonly ParticlePool          _particles;
        private readonly FlightCamera          _camera;

        // Transition
        private const int RepaintIntervalMs = 33; // ≤30 Hz
        private const float TransitionSeconds = 0.3f;

        // ── Pad / liftoff scene constants ─────────────────────────────────────────
        // Ascent altitude progress at which the pad ground has fully slid off-screen.
        // Below this threshold the launch pad ground + tower remain visible and
        // continuously slide down / fade out as the rocket climbs (no hard scene cut).
        internal const float GroundFadeEndProgress = 0.15f;

        // ── Vehicle framing constants (% of stage) ────────────────────────────────
        private const float VehicleWidthPct     = 30f;  // element width
        private const float VehicleHeightPct    = 35f;  // fixed element height (~35% of frame)
        private const float VehicleTopPadPct    = 41f;  // Pad/Ascent start (base rests on pad line)
        private const float VehicleTopAscentPct = 22f;  // late-ascent (rocket climbs in frame)
        private const float VehicleTopSpacePct  = 30f;  // cruise — near the trajectory arc
        private const float VehicleTopEntryPct  = 8f;   // EDL entry (high altitude)
        // Touchdown top ≈ pad top: base lands exactly on the surface line (h*0.72).

        // ── Colour constants (match Variables.uss tokens) ─────────────────────────
        private static readonly Color SkyGroundColor   = new Color(0.102f, 0.102f, 0.180f);
        private static readonly Color SkyHighColor     = new Color(0.027f, 0.043f, 0.078f);
        private static readonly Color SkyAtmosphere    = new Color(0.063f, 0.361f, 0.573f, 0.6f);
        private static readonly Color StarColor        = new Color(0.90f, 0.95f, 1.00f);
        private static readonly Color TrajectoryColor  = new Color(0.024f, 0.714f, 0.831f, 0.6f);
        private static readonly Color HeatGlowColor    = new Color(0.961f, 0.620f, 0.043f, 0.7f);
        private static readonly Color ScienceLaserColor= new Color(0.024f, 0.95f, 0.85f, 0.75f);
        private static readonly Color DownlinkWaveColor= new Color(0.20f, 0.75f, 1.00f, 0.45f);

        // ── Construction ──────────────────────────────────────────────────────────
        public FlightStageView(bool reduceMotion = false)
        {
            _reduceMotion = reduceMotion;
            style.position = Position.Absolute;
            style.left   = 0; style.top    = 0;
            style.right  = 0; style.bottom = 0;
            style.overflow = Overflow.Hidden;

            // Painter2D pass
            generateVisualContent += OnGenerateVisualContent;

            // Vehicle silhouette (child VisualElement, composited on top of Painter2D layer).
            // Fixed footprint ≈35% of the frame height (playtest fix #2: the rocket
            // used to fill ~85% of the screen). Height stays constant — only the
            // vertical position is choreographed, so the element never deforms.
            _vehicle = new VehicleSilhouetteView();
            _vehicle.style.position = Position.Absolute;
            _vehicle.style.left     = Length.Percent(35f);
            _vehicle.style.width    = Length.Percent(30f);
            _vehicle.style.top      = Length.Percent(VehicleTopPadPct);
            _vehicle.style.height   = Length.Percent(VehicleHeightPct);
            _vehicle.pickingMode    = PickingMode.Ignore; // let pointer drags reach the stage
            Add(_vehicle);

            // Particle pool (20 slots; repaint-budget-aware)
            _particles = new ParticlePool(20, this);

            // USS transform camera simulator
            _camera = new FlightCamera(_vehicle, _reduceMotion);

            // Set initial sky
            SetSkyForPhase(ScenePhase.Pad, immediate: true);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Apply a new beat — updates scene phase, sky, vehicle, camera and particles.</summary>
        public void ApplyBeat(FlightBeat beat)
        {
            if (beat == null) return;
            _currentBeat = beat;

            var newPhase = ResolvePhase(beat);
            if (newPhase != _phase)
            {
                _phase = newPhase;
                SetSkyForPhase(_phase, immediate: _reduceMotion);
                UpdateVehicleLevel();
            }

            // Altitude progress for sub-phase positioning
            _altitudeProgress = ResolveAltitudeProgress(beat);

            // Camera motion key integration
            if (!string.IsNullOrEmpty(beat.CameraMotionKey))
            {
                _camera.ApplyMotionKey(beat.CameraMotionKey);
            }

            // Gravity turn: body tilt + coupled lateral drift build continuously
            // with altitude during ascent, then relax to vertical in space/EDL.
            if (_phase == ScenePhase.Ascent && !_reduceMotion)
            {
                float turnT = Mathf.InverseLerp(0.06f, 0.55f, _altitudeProgress);
                _camera.SetTilt(Mathf.SmoothStep(0f, 24f, turnT));
            }
            else
            {
                _camera.SetTilt(0f);
            }

            // Cinematic shake on high-energy beats (suppressed by ReduceMotion).
            if (!_reduceMotion)
            {
                string camKey = beat.CameraMotionKey ?? string.Empty;
                if (camKey.Contains("liftoff", StringComparison.OrdinalIgnoreCase) ||
                    camKey.Contains("ground", StringComparison.OrdinalIgnoreCase) ||
                    camKey.Contains("pad_close", StringComparison.OrdinalIgnoreCase))
                    _camera.Shake(2.2f, 1.6f);
                else if (camKey.Contains("maxq", StringComparison.OrdinalIgnoreCase))
                    _camera.Shake(1.6f, 1.0f);
                else if (camKey.Contains("staging", StringComparison.OrdinalIgnoreCase))
                    _camera.Shake(1.2f, 0.8f);
            }

            // Vehicle altitude and vertical positioning
            UpdateVehicleVerticalPosition(beat);

            // Landing-phase visibility guarantee: the vehicle must always be on
            // screen during EDL/Science sub-phases (playtest fix #3).
            if (_phase == ScenePhase.Landing)
            {
                _vehicle.style.display = DisplayStyle.Flex;
                _vehicle.style.opacity = 1f;
            }

            // Vehicle state flags (Science and Retro descent firing)
            bool isSci = beat.PhaseId.Contains("SCIENCE", StringComparison.OrdinalIgnoreCase) ||
                         (beat.VisualEffectKey != null && beat.VisualEffectKey.Contains("science", StringComparison.OrdinalIgnoreCase));
            bool isRetro = beat.PhaseId.Contains("DESCENT", StringComparison.OrdinalIgnoreCase) ||
                           (beat.VisualEffectKey != null && beat.VisualEffectKey.Contains("retro", StringComparison.OrdinalIgnoreCase));
            _vehicle.SetScienceActive(isSci);
            _vehicle.SetRetroFiring(isRetro);

            // Particles
            if (!_reduceMotion)
                SpawnParticlesForBeat(beat);

            // Exhaust animation only while engines are actually firing.
            // MECO/SECO/cutoff and coasting beats (stage separation, fairing
            // jettison) must not show a flame (playtest fix #2).
            string titleKey = beat.TitleKey ?? string.Empty;
            bool coastBeat = titleKey.Contains("MECO", StringComparison.OrdinalIgnoreCase) ||
                             titleKey.Contains("SECO", StringComparison.OrdinalIgnoreCase) ||
                             titleKey.Contains("CUTOFF", StringComparison.OrdinalIgnoreCase) ||
                             beat.PhaseId.Contains("Separation", StringComparison.OrdinalIgnoreCase) ||
                             beat.PhaseId.Contains("Fairing", StringComparison.OrdinalIgnoreCase);
            if (_phase == ScenePhase.Ascent && !coastBeat && !_reduceMotion)
                _vehicle.StartExhaustAnimation();
            else
                _vehicle.StopExhaustAnimation();

            MarkDirtyRepaint();
        }

        private void UpdateVehicleVerticalPosition(FlightBeat beat)
        {
            // Height is fixed (VehicleHeightPct); only `top` is choreographed so
            // the element never deforms (playtest fix #8).
            if (_phase == ScenePhase.Landing)
            {
                bool isSurface = beat.PhaseId.Contains("SCIENCE", StringComparison.OrdinalIgnoreCase) ||
                                 beat.PhaseId.Contains("TOUCHDOWN", StringComparison.OrdinalIgnoreCase) ||
                                 beat.AltitudeKm <= 0.05;
                if (isSurface)
                {
                    // Surface: base sits exactly on the landing ground line (h * 0.72).
                    _vehicle.style.top = Length.Percent(VehicleTopPadPct);
                }
                else
                {
                    // Descend from entry (top 8%) to the surface line based on
                    // normalized altitude — the vehicle is guaranteed on-screen
                    // for the whole EDL profile.
                    float descentT = Mathf.Clamp01((float)(beat.AltitudeKm / 60.0));
                    float topPct   = Mathf.Lerp(VehicleTopEntryPct, VehicleTopPadPct, descentT);
                    _vehicle.style.top = Length.Percent(topPct);
                }
            }
            else if (_phase == ScenePhase.Space)
            {
                _vehicle.style.top = Length.Percent(VehicleTopSpacePct);
            }
            else
            {
                // Pad / Ascent: start with the base resting on the pad ground
                // line (see TryGetPadGroundLine) and let the rocket climb slowly
                // in-frame as altitude builds. Liftoff motion is carried by the
                // ground sliding away + camera pan (playtest fix #1).
                float climbT = Mathf.Clamp01(_altitudeProgress / Mathf.Max(0.01f, GroundFadeEndProgress * 2f));
                _vehicle.style.top = Length.Percent(Mathf.Lerp(VehicleTopPadPct, VehicleTopAscentPct, climbT));
            }
        }

        /// <summary>Access the underlying vehicle silhouette view.</summary>
        public VehicleSilhouetteView Vehicle => _vehicle;

        /// <summary>Set the payload silhouette type (Rover, Lander, Orbiter).</summary>
        public void SetPayloadType(VehicleSilhouetteView.PayloadType type)
        {
            _vehicle?.SetPayloadType(type);
        }

        /// <summary>Set the destination visual profile (from flight_visuals.json).</summary>
        public void SetDestinationProfile(DestinationProfile profile)
        {
            _destination = profile;
            _vehicle.SetAccentColor(profile.OrbitRingColor);
            MarkDirtyRepaint();
        }

        /// <summary>Toggle ReduceMotion at runtime (Ayarlar binding).</summary>
        public void SetReduceMotion(bool reduce)
        {
            _reduceMotion = reduce;
            _camera.SetReduceMotion(reduce);
            if (reduce)
            {
                _vehicle.StopExhaustAnimation();
                _particles.Clear();
            }
        }

        /// <summary>
        /// Attach the shared FlightPerformanceBudget so every repaint tick is
        /// counted (ADR-061 WebGL audit). Optional — null disables tracking.
        /// </summary>
        public void SetPerformanceBudget(FlightPerformanceBudget budget)
        {
            _budget = budget;
        }

        /// <summary>Zoom the flight camera in/out on top of the current preset (player control).</summary>
        public void AdjustCameraZoom(float delta) => _camera.AdjustZoom(delta);

        /// <summary>Reset the player zoom layer back to the choreographed default.</summary>
        public void ResetCameraZoom() => _camera.ResetZoom();

        /// <summary>Current player zoom multiplier (1.0 = choreographed default).</summary>
        public float CameraZoom => _camera.UserZoom;

        /// <summary>
        /// Player drag-pan: offsets the camera by <paramref name="delta"/> pixels
        /// (from PointerMove deltas), clamped so the vehicle stays in the safe area.
        /// </summary>
        public void PanCameraBy(Vector2 delta)
        {
            var maxPan = new Vector2(contentRect.width * 0.30f, contentRect.height * 0.25f);
            _camera.PanBy(delta, maxPan);
        }

        /// <summary>Snap the camera instantly to its current targets (sequence handoff).</summary>
        public void SnapCamera() => _camera.Snap();

        /// <summary>Start the per-frame repaint schedule (call after AddToHierarchy).</summary>
        public void StartAnimation()
        {
            if (_animRunning) return;
            _animRunning = true;
            _sceneSchedule = schedule.Execute(OnAnimationTick).Every(RepaintIntervalMs);
        }

        /// <summary>Stop the repaint schedule (call when screen is hidden).</summary>
        public void StopAnimation()
        {
            if (!_animRunning) return;
            _animRunning = false;
            _sceneSchedule?.Pause();
            _sceneSchedule = null;
            _vehicle.StopExhaustAnimation();
            _camera.Reset();
        }

        // ── Painter2D Scene Draw ──────────────────────────────────────────────────
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 10 || h < 10) return;

            switch (_phase)
            {
                case ScenePhase.Pad:     DrawPadScene(p, w, h);     break;
                case ScenePhase.Ascent:  DrawAscentScene(p, w, h);  break;
                case ScenePhase.Space:   DrawSpaceScene(p, w, h);   break;
                case ScenePhase.Landing: DrawLandingScene(p, w, h); break;
            }

            // Particles always on top of background
            _particles.Draw(p, w, h);
        }

        // ── Scene Layers ──────────────────────────────────────────────────────────

        private void DrawPadScene(Painter2D p, float w, float h)
        {
            // Sky gradient (pre-dawn deep blue -> lighter at horizon)
            DrawSkyGradient(p, w, h, SkyGroundColor, new Color(0.09f, 0.14f, 0.25f));

            // Ground / concrete pad — aligned to the vehicle's base so the
            // rocket visibly STANDS on the pad (playtest fix #1: no floating start).
            float groundY = TryGetPadGroundLine(h, out float baseY) ? baseY : h * 0.78f;
            DrawPadGroundLayer(p, w, h, groundY, 1f);

            // Stars
            DrawStars(p, w, h, groundY * 0.9f);
        }

        /// <summary>
        /// Draws the launch pad ground plane + tower at the given line, fading by
        /// <paramref name="alpha"/> (0–1). Shared by the Pad scene and the
        /// altitude-dependent ascent ground layer.
        /// </summary>
        private static void DrawPadGroundLayer(Painter2D p, float w, float h, float groundY, float alpha)
        {
            groundY = Mathf.Clamp(groundY, h * 0.35f, h + 200f);

            // Concrete pad quad (extends past the stage bottom; clipped naturally)
            p.fillColor = new Color(0.08f, 0.10f, 0.14f, alpha);
            p.BeginPath();
            p.MoveTo(new Vector2(0, groundY));
            p.LineTo(new Vector2(w, groundY));
            p.LineTo(new Vector2(w, h + 200f));
            p.LineTo(new Vector2(0, h + 200f));
            p.ClosePath();
            p.Fill();

            // Darker surface strip on the pad line for depth
            p.fillColor = new Color(0.05f, 0.07f, 0.10f, alpha);
            p.BeginPath();
            p.MoveTo(new Vector2(0, groundY));
            p.LineTo(new Vector2(w, groundY));
            p.LineTo(new Vector2(w, groundY + 6f));
            p.LineTo(new Vector2(0, groundY + 6f));
            p.ClosePath();
            p.Fill();

            // Launch tower silhouette
            DrawLaunchTower(p, w * 0.68f, groundY, alpha);
        }

        private void DrawAscentScene(Painter2D p, float w, float h)
        {
            // Sky transitions from deep blue (ground) to black (space) based on altitude
            Color skyT = Color.Lerp(new Color(0.09f, 0.14f, 0.25f), SkyHighColor, _altitudeProgress);
            Color skyB = Color.Lerp(SkyAtmosphere, new Color(0.027f, 0.043f, 0.078f), _altitudeProgress);
            DrawSkyGradient(p, w, h, skyT, skyB);

            // Atmosphere glow at horizon (fades with altitude)
            if (_altitudeProgress < 0.5f)
            {
                float alpha = (0.5f - _altitudeProgress) * 1.2f;
                DrawAtmosphereBand(p, w, h, new Color(SkyAtmosphere.r, SkyAtmosphere.g, SkyAtmosphere.b, alpha * 0.6f));
            }

            // Altitude-dependent pad ground layer: the launch pad + tower stay
            // visible in the early ascent and continuously slide down / fade out
            // as the rocket climbs (continuous transition — no hard scene change).
            if (_altitudeProgress < GroundFadeEndProgress)
            {
                float slide = _altitudeProgress / GroundFadeEndProgress; // 0 on pad → 1 gone
                float alpha = 1f - slide;
                if (TryGetPadGroundLine(h, out float padY))
                {
                    // Slide the whole ground plane down past the stage bottom.
                    float groundY = padY + slide * (h - padY + 220f);
                    DrawPadGroundLayer(p, w, h, groundY, alpha);
                }
            }

            // Max-Q vapour cone (around 12-15 km)
            if (_altitudeProgress is > 0.12f and < 0.22f)
            {
                float coneAlpha = 1f - Mathf.Abs(_altitudeProgress - 0.17f) / 0.05f;
                DrawVapourCone(p, w * 0.5f, h * 0.35f, w * 0.08f, h * 0.12f,
                               new Color(1f, 1f, 1f, coneAlpha * 0.35f));
            }

            // Earth curvature hint when >100 km
            if (_altitudeProgress > 0.7f)
                DrawEarthCurve(p, w, h, _altitudeProgress);

            // Stars appear as altitude climbs
            if (_altitudeProgress > 0.4f)
                DrawStars(p, w, h, h, _altitudeProgress);
        }

        private void DrawSpaceScene(Painter2D p, float w, float h)
        {
            // Deep space background
            DrawSkyGradient(p, w, h, SkyHighColor, new Color(0.01f, 0.01f, 0.03f));

            // Stars (full field)
            DrawStars(p, w, h, h);

            float progress = Mathf.Clamp01((float)(_currentBeat?.CruiseProgress ?? 0.0));

            // Earth (departure body) on the left - scales down slightly as craft travels further away
            float earthScale = Mathf.Lerp(1.0f, 0.65f, progress);
            DrawPlanetBody(p, w * 0.12f, h * 0.35f, w * 0.09f * earthScale,
                           new Color(0.10f, 0.35f, 0.60f), new Color(0.063f, 0.361f, 0.573f, 0.5f));

            // Destination body on the right - scales up as craft approaches
            if (_destination.BodyScale > 0.01f)
            {
                float baseRadius = w * 0.07f * _destination.BodyScale;
                float destScale = Mathf.Lerp(0.6f, 1.25f, progress);
                float radius = baseRadius * destScale;
                DrawPlanetBody(p, w * 0.88f, h * 0.3f, radius,
                               _destination.BodyColor, _destination.AtmosphereColor);
                if (_destination.HasRings)
                    DrawRings(p, w * 0.88f, h * 0.3f, radius);

                // Orbital capture ring when approaching arrival (progress >= 0.85)
                if (progress >= 0.85f)
                {
                    DrawArrivalCaptureRing(p, w * 0.88f, h * 0.3f, radius * 1.45f, _destination.OrbitRingColor);
                }
            }

            // Trajectory arc
            DrawTrajectoryArc(p, w, h);
        }

        private void DrawLandingScene(Painter2D p, float w, float h)
        {
            // Destination sky (atmosphere or space)
            Color skyTop = _destination.HasAtmosphere
                ? Color.Lerp(_destination.AtmosphereColor, new Color(0.04f, 0.06f, 0.12f), 0.6f)
                : SkyHighColor;
            Color skyBot = _destination.HasAtmosphere
                ? _destination.AtmosphereColor
                : new Color(0.02f, 0.03f, 0.06f);

            DrawSkyGradient(p, w, h, skyTop, skyBot);
            if (!_destination.HasAtmosphere)
                DrawStars(p, w, h, h);

            // Surface
            float groundY = h * 0.72f;
            Color groundColor = Color.Lerp(_destination.BodyColor, Color.black, 0.6f);
            p.fillColor = groundColor;
            p.BeginPath();
            p.MoveTo(new Vector2(0, groundY));
            p.LineTo(new Vector2(w, groundY));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0, h));
            p.ClosePath();
            p.Fill();

            // Destination-specific terrain features (craters, dunes, fissures)
            DrawPlanetSpecificTerrain(p, w, groundY, _destination);

            // Dynamic EDL and Science Visual Elements
            if (_currentBeat != null)
            {
                string phaseId = _currentBeat.PhaseId ?? "";
                string fxKey   = _currentBeat.VisualEffectKey ?? "";

                // 1. Atmospheric Entry & Peak Heating Shockwave
                if (phaseId.Contains("ENTRY", StringComparison.OrdinalIgnoreCase) ||
                    phaseId.Contains("HEATING", StringComparison.OrdinalIgnoreCase) ||
                    fxKey.Contains("heating", StringComparison.OrdinalIgnoreCase))
                {
                    float glowAlpha = Mathf.Clamp01((float)_currentBeat.HeatShieldIntegrity);
                    if (glowAlpha <= 0.01f) glowAlpha = 0.85f;
                    DrawHeatShockwave(p, w * 0.5f, h * 0.35f, w * 0.18f, glowAlpha);
                }

                // 2. Parachute Deployment Canopy & Lines
                if (phaseId.Contains("PARACHUTE", StringComparison.OrdinalIgnoreCase) ||
                    fxKey == "fx_chute_deploy")
                {
                    DrawParachute(p, w * 0.5f, h * 0.18f, w * 0.16f, h * 0.10f);
                }

                // 3. Retro Descent Engine Plume — anchored to the vehicle's live
                //    nozzle position; never drawn when the vehicle is not laid out.
                if (phaseId.Contains("DESCENT", StringComparison.OrdinalIgnoreCase) ||
                    fxKey.Contains("retro", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetVehicleLocalAnchor(0.55f, out var nozzle))
                        DrawRetroPlume(p, nozzle.x, nozzle.y + 6f, groundY);
                }

                // 4. Touchdown Shock Ring
                if (phaseId.Contains("TOUCHDOWN", StringComparison.OrdinalIgnoreCase) ||
                    fxKey.Contains("touchdown", StringComparison.OrdinalIgnoreCase))
                {
                    DrawTouchdownShockRing(p, w * 0.5f, groundY + 12f, w * 0.14f);
                }

                // 5. Crash Site (EDL Failure)
                if (fxKey == "fx_edl_fail" || _currentBeat.StatusId.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    DrawCrashSite(p, w * 0.5f, groundY + 10f, w * 0.15f);
                }

                // 6. Surface Science Operations
                bool isScience = phaseId.Contains("SCIENCE", StringComparison.OrdinalIgnoreCase) ||
                                 fxKey.Contains("science", StringComparison.OrdinalIgnoreCase);
                if (isScience)
                {
                    // Surface tracks
                    DrawSurfaceTracks(p, w * 0.5f, groundY + 14f, w * 0.22f);

                    // Sensor scan beam
                    DrawScienceScanBeam(p, w * 0.46f, groundY - 30f, groundY + 12f);

                    // Downlink radio waves
                    if (fxKey.Contains("downlink", StringComparison.OrdinalIgnoreCase) ||
                        fxKey.Contains("contact", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawDownlinkWaves(p, w * 0.50f, groundY - 60f);
                    }
                }
            }
        }

        // ── Draw Primitives ───────────────────────────────────────────────────────

        private static void DrawSkyGradient(Painter2D p, float w, float h, Color top, Color bottom)
        {
            // Painter2D has no native gradient fill — approximate with layered quads
            int bands = 6;
            for (int i = 0; i < bands; i++)
            {
                float t0 = (float)i / bands;
                float t1 = (float)(i + 1) / bands;
                float y0 = h * t0;
                float y1 = h * t1;
                p.fillColor = Color.Lerp(top, bottom, (t0 + t1) * 0.5f);
                p.BeginPath();
                p.MoveTo(new Vector2(0, y0));
                p.LineTo(new Vector2(w, y0));
                p.LineTo(new Vector2(w, y1));
                p.LineTo(new Vector2(0, y1));
                p.ClosePath();
                p.Fill();
            }
        }

        private void DrawStars(Painter2D p, float w, float h, float maxY, float density = 1f)
        {
            // Deterministic pseudo-random star field
            var rng = new System.Random(42);
            int count = (int)(60 * density);
            for (int i = 0; i < count; i++)
            {
                float sx = (float)rng.NextDouble() * w;
                float sy = (float)rng.NextDouble() * maxY;
                float brightness = 0.4f + (float)rng.NextDouble() * 0.6f;
                // Twinkle: vary alpha slightly
                float twinkle = _reduceMotion ? brightness :
                    brightness * (0.75f + 0.25f * Mathf.Sin(_starTwinklePhase + i * 1.37f));
                p.fillColor = new Color(StarColor.r, StarColor.g, StarColor.b, twinkle);
                float radius = 0.5f + (float)rng.NextDouble() * 1.2f;
                p.BeginPath();
                p.Arc(new Vector2(sx, sy), radius, 0, 360, ArcDirection.Clockwise);
                p.Fill();
            }
        }

        private static void DrawLaunchTower(Painter2D p, float x, float groundY, float alpha = 1f)
        {
            float towerH = groundY * 0.72f;
            float towerW = 12f;
            p.fillColor = new Color(0.12f, 0.17f, 0.24f, alpha);
            // Main mast
            p.BeginPath();
            p.MoveTo(new Vector2(x, groundY - towerH));
            p.LineTo(new Vector2(x + towerW, groundY - towerH));
            p.LineTo(new Vector2(x + towerW, groundY));
            p.LineTo(new Vector2(x, groundY));
            p.ClosePath();
            p.Fill();
            // Cross arms
            p.strokeColor = new Color(0.15f, 0.22f, 0.32f, alpha);
            p.lineWidth   = 3f;
            for (int i = 1; i <= 4; i++)
            {
                float armY = groundY - towerH * i / 4.5f;
                p.BeginPath();
                p.MoveTo(new Vector2(x, armY));
                p.LineTo(new Vector2(x - 24f, armY - 8f));
                p.Stroke();
            }
        }

        private static void DrawAtmosphereBand(Painter2D p, float w, float h, Color color)
        {
            float bandH = h * 0.18f;
            float bandY = h * 0.72f;
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(0, bandY));
            p.LineTo(new Vector2(w, bandY));
            p.LineTo(new Vector2(w, bandY + bandH));
            p.LineTo(new Vector2(0, bandY + bandH));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawVapourCone(Painter2D p, float cx, float cy,
                                            float coneW, float coneH, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - coneH * 0.5f));
            p.LineTo(new Vector2(cx + coneW, cy + coneH * 0.5f));
            p.LineTo(new Vector2(cx - coneW, cy + coneH * 0.5f));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawEarthCurve(Painter2D p, float w, float h, float alt01)
        {
            float curveAlpha = (alt01 - 0.7f) / 0.3f;
            p.strokeColor = new Color(0.063f, 0.361f, 0.573f, curveAlpha * 0.5f);
            p.lineWidth   = 3f;
            float radius  = w * 2.8f;
            float arcCy   = h + radius * 0.78f;
            p.BeginPath();
            p.Arc(new Vector2(w * 0.5f, arcCy), radius,
                  180f + 20f * (1f - curveAlpha), 360f - 20f * (1f - curveAlpha),
                  ArcDirection.Clockwise);
            p.Stroke();
        }

        private static void DrawPlanetBody(Painter2D p, float cx, float cy,
                                            float radius, Color body, Color atmosphere)
        {
            // Atmosphere halo
            if (atmosphere.a > 0.01f)
            {
                p.fillColor = new Color(atmosphere.r, atmosphere.g, atmosphere.b, atmosphere.a * 0.4f);
                p.BeginPath();
                p.Arc(new Vector2(cx, cy), radius * 1.3f, 0, 360, ArcDirection.Clockwise);
                p.Fill();
            }
            // Body
            p.fillColor = body;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Fill();
            // Terminator shading
            p.fillColor = new Color(0, 0, 0, 0.35f);
            p.BeginPath();
            p.Arc(new Vector2(cx + radius * 0.2f, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        private static void DrawRings(Painter2D p, float cx, float cy, float radius)
        {
            p.strokeColor = new Color(0.8f, 0.75f, 0.5f, 0.4f);
            p.lineWidth   = 4f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius * 1.7f, 160, 380, ArcDirection.Clockwise);
            p.Stroke();
        }

        private static void DrawArrivalCaptureRing(Painter2D p, float cx, float cy, float radius, Color ringColor)
        {
            p.strokeColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0.75f);
            p.lineWidth   = 2.5f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Stroke();
        }

        private void DrawTrajectoryArc(Painter2D p, float w, float h)
        {
            // Bezier arc from Earth (left) to Destination (right), vehicle progresses along it
            float progress = _currentBeat?.CruiseProgress is > 0 ? (float)_currentBeat.CruiseProgress : 0.5f;

            Vector2 start  = new Vector2(w * 0.18f, h * 0.42f);
            Vector2 end    = new Vector2(w * 0.82f, h * 0.32f);
            Vector2 ctrl   = new Vector2(w * 0.5f,  h * 0.08f);

            p.strokeColor = TrajectoryColor;
            p.lineWidth   = 1.5f;
            p.BeginPath();
            p.MoveTo(start);
            p.BezierCurveTo(ctrl, ctrl, end);
            p.Stroke();

            // Vehicle position dot on the arc
            Vector2 vPos = BezierPoint(start, ctrl, end, progress);
            p.fillColor = new Color(TrajectoryColor.r, TrajectoryColor.g, TrajectoryColor.b, 1f);
            p.BeginPath();
            p.Arc(vPos, 5f, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        private static void DrawTerrainSilhouette(Painter2D p, float w, float groundY, Color groundColor)
        {
            // Simple bumpy horizon
            var rng = new System.Random(7);
            p.fillColor = Color.Lerp(groundColor, Color.black, 0.3f);
            p.BeginPath();
            p.MoveTo(new Vector2(0, groundY));
            float step = w / 12f;
            for (int i = 0; i <= 12; i++)
            {
                float bx = i * step;
                float by = groundY - (float)rng.NextDouble() * 20f;
                if (i == 0) p.MoveTo(new Vector2(bx, by));
                else        p.LineTo(new Vector2(bx, by));
            }
            p.LineTo(new Vector2(w, groundY + 40f));
            p.LineTo(new Vector2(0, groundY + 40f));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawHeatGlow(Painter2D p, float cx, float cy, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        // ── Phase 6: EDL and Science Cinematic Primitives ─────────────────────────

        private static void DrawPlanetSpecificTerrain(Painter2D p, float w, float groundY, DestinationProfile dest)
        {
            // Base bumpy horizon
            DrawTerrainSilhouette(p, w, groundY, dest.BodyColor);

            // Planetary surface details (craters, dunes, fissures)
            if (!dest.HasAtmosphere)
            {
                // Lunar / vacuum crater formations
                p.strokeColor = new Color(0.25f, 0.28f, 0.35f, 0.6f);
                p.lineWidth   = 2f;
                p.BeginPath();
                p.Arc(new Vector2(w * 0.25f, groundY + 18f), 22f, 0, 360, ArcDirection.Clockwise);
                p.Stroke();
                p.BeginPath();
                p.Arc(new Vector2(w * 0.78f, groundY + 30f), 35f, 0, 360, ArcDirection.Clockwise);
                p.Stroke();
            }
            else if (dest.AtmosphereColor.r > 0.4f)
            {
                // Mars reddish sand dune ripples and rocky outcrops
                p.strokeColor = new Color(0.65f, 0.28f, 0.15f, 0.5f);
                p.lineWidth   = 1.5f;
                for (int i = 0; i < 4; i++)
                {
                    float y = groundY + 12f + i * 8f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(w * 0.1f + i * 40f, y));
                    p.LineTo(new Vector2(w * 0.4f + i * 50f, y + 4f));
                    p.Stroke();
                }
            }
            else
            {
                // Venusian / icy fissure lines
                p.strokeColor = new Color(dest.OrbitRingColor.r, dest.OrbitRingColor.g, dest.OrbitRingColor.b, 0.35f);
                p.lineWidth   = 1.5f;
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.2f, groundY + 8f));
                p.LineTo(new Vector2(w * 0.35f, groundY + 28f));
                p.LineTo(new Vector2(w * 0.5f, groundY + 16f));
                p.Stroke();
            }
        }

        private static void DrawHeatShockwave(Painter2D p, float cx, float cy, float radius, float intensity)
        {
            // Curved plasma bow shock ahead of the descending vehicle
            Color shockCol = new Color(1.0f, 0.45f, 0.08f, Mathf.Clamp01(intensity * 0.75f));
            p.strokeColor = shockCol;
            p.lineWidth   = 6f;
            p.BeginPath();
            // Upward concave arc simulating bow shock compression
            p.Arc(new Vector2(cx, cy + radius * 0.4f), radius, 200, 340, ArcDirection.Clockwise);
            p.Stroke();

            // Inner hot core
            p.strokeColor = new Color(1.0f, 0.92f, 0.60f, Mathf.Clamp01(intensity * 0.90f));
            p.lineWidth   = 2.5f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy + radius * 0.4f), radius * 0.92f, 220, 320, ArcDirection.Clockwise);
            p.Stroke();
        }

        private static void DrawParachute(Painter2D p, float cx, float cy, float chuteW, float chuteH)
        {
            // Suspension cords from chute to vehicle connection
            p.strokeColor = new Color(0.85f, 0.90f, 0.95f, 0.75f);
            p.lineWidth   = 1.5f;
            Vector2 vehicleAnchor = new Vector2(cx, cy + chuteH * 1.8f);

            p.BeginPath();
            p.MoveTo(new Vector2(cx - chuteW * 0.5f, cy + chuteH * 0.3f));
            p.LineTo(vehicleAnchor);
            p.MoveTo(new Vector2(cx - chuteW * 0.2f, cy + chuteH * 0.4f));
            p.LineTo(vehicleAnchor);
            p.MoveTo(new Vector2(cx + chuteW * 0.2f, cy + chuteH * 0.4f));
            p.LineTo(vehicleAnchor);
            p.MoveTo(new Vector2(cx + chuteW * 0.5f, cy + chuteH * 0.3f));
            p.LineTo(vehicleAnchor);
            p.Stroke();

            // Parachute canopy dome (NASA orange and white alternating bands)
            p.fillColor = new Color(0.92f, 0.35f, 0.08f); // Apollo orange
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), chuteW * 0.5f, 180, 360, ArcDirection.Clockwise);
            p.ClosePath();
            p.Fill();

            // White center stripe
            p.fillColor = new Color(0.95f, 0.95f, 0.98f);
            p.BeginPath();
            p.MoveTo(new Vector2(cx - chuteW * 0.15f, cy));
            p.LineTo(new Vector2(cx + chuteW * 0.15f, cy));
            p.LineTo(new Vector2(cx + chuteW * 0.08f, cy - chuteW * 0.5f));
            p.LineTo(new Vector2(cx - chuteW * 0.08f, cy - chuteW * 0.5f));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawRetroPlume(Painter2D p, float cx, float engineY, float groundY)
        {
            float flameH = Mathf.Min(groundY - engineY, 70f);
            if (flameH <= 5f) return;

            // Dual retro thrust flames
            foreach (var sign in new[] { -18f, 18f })
            {
                float fx = cx + sign;
                p.fillColor = new Color(0.96f, 0.62f, 0.05f, 0.85f);
                p.BeginPath();
                p.MoveTo(new Vector2(fx - 6f, engineY));
                p.LineTo(new Vector2(fx, engineY + flameH));
                p.LineTo(new Vector2(fx + 6f, engineY));
                p.ClosePath();
                p.Fill();

                // Inner core
                p.fillColor = new Color(0.40f, 0.85f, 1.0f, 0.95f);
                p.BeginPath();
                p.MoveTo(new Vector2(fx - 2.5f, engineY));
                p.LineTo(new Vector2(fx, engineY + flameH * 0.55f));
                p.LineTo(new Vector2(fx + 2.5f, engineY));
                p.ClosePath();
                p.Fill();
            }
        }

        private static void DrawTouchdownShockRing(Painter2D p, float cx, float cy, float radius)
        {
            p.strokeColor = new Color(0.85f, 0.70f, 0.50f, 0.65f);
            p.lineWidth   = 2.5f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Stroke();

            p.strokeColor = new Color(0.95f, 0.85f, 0.65f, 0.35f);
            p.lineWidth   = 1.5f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius * 1.55f, 0, 360, ArcDirection.Clockwise);
            p.Stroke();
        }

        private static void DrawCrashSite(Painter2D p, float cx, float cy, float radius)
        {
            // Dark burn crater on surface
            p.fillColor = new Color(0.08f, 0.06f, 0.05f, 0.90f);
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius, 0, 360, ArcDirection.Clockwise);
            p.Fill();

            p.strokeColor = new Color(0.75f, 0.20f, 0.05f, 0.8f);
            p.lineWidth   = 2.5f;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), radius * 0.7f, 0, 360, ArcDirection.Clockwise);
            p.Stroke();
        }

        private static void DrawSurfaceTracks(Painter2D p, float cx, float cy, float length)
        {
            // Parallel rover wheel tracks on the ground
            p.strokeColor = new Color(0.06f, 0.08f, 0.12f, 0.55f);
            p.lineWidth   = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - length, cy - 3f));
            p.LineTo(new Vector2(cx + length * 0.4f, cy - 3f));
            p.MoveTo(new Vector2(cx - length, cy + 6f));
            p.LineTo(new Vector2(cx + length * 0.4f, cy + 6f));
            p.Stroke();
        }

        private void DrawScienceScanBeam(Painter2D p, float originX, float originY, float targetY)
        {
            float sweepAngle = Mathf.Sin(_starTwinklePhase * 2.5f) * 20f;
            float targetX = originX - 35f + sweepAngle;

            // Semi-transparent laser cone
            p.fillColor = new Color(ScienceLaserColor.r, ScienceLaserColor.g, ScienceLaserColor.b, 0.20f);
            p.BeginPath();
            p.MoveTo(new Vector2(originX, originY));
            p.LineTo(new Vector2(targetX - 18f, targetY));
            p.LineTo(new Vector2(targetX + 18f, targetY));
            p.ClosePath();
            p.Fill();

            // Intense laser sweep line
            p.strokeColor = ScienceLaserColor;
            p.lineWidth   = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(originX, originY));
            p.LineTo(new Vector2(targetX, targetY));
            p.Stroke();

            // Ground analysis hot spot
            p.fillColor = ScienceLaserColor;
            p.BeginPath();
            p.Arc(new Vector2(targetX, targetY), 5f, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        private void DrawDownlinkWaves(Painter2D p, float cx, float cy)
        {
            float wavePhase = (_starTwinklePhase * 2f) % 1f;
            p.strokeColor = DownlinkWaveColor;
            p.lineWidth   = 2f;

            for (int i = 0; i < 3; i++)
            {
                float t = (wavePhase + i * 0.33f) % 1f;
                float r = 10f + t * 45f;
                float y = cy - t * 35f;
                float alpha = (1f - t) * 0.75f;
                p.strokeColor = new Color(DownlinkWaveColor.r, DownlinkWaveColor.g, DownlinkWaveColor.b, alpha);
                p.BeginPath();
                p.Arc(new Vector2(cx, y), r, 220, 320, ArcDirection.Clockwise);
                p.Stroke();
            }
        }

        // ── Helper Maths ──────────────────────────────────────────────────────────
        private static Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u  = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        // ── Phase Resolution ──────────────────────────────────────────────────────
        private static ScenePhase ResolvePhase(FlightBeat beat)
        {
            string phase = beat.PhaseId.ToUpperInvariant();
            if (phase.Contains("IGNITION") || phase.Contains("LIFTOFF") || phase.Contains("MAXQ") ||
                phase.Contains("STAGE") || phase.Contains("FAIRING") || phase.Contains("ORBITAL"))
                return ScenePhase.Ascent;
            if (phase.Contains("CRUISE"))
                return ScenePhase.Space;
            if (phase.Contains("DEORBIT") || phase.Contains("ENTRY") || phase.Contains("HEATING") ||
                phase.Contains("PARACHUTE") || phase.Contains("DESCENT") || phase.Contains("TOUCHDOWN"))
                return ScenePhase.Landing;
            if (phase.Contains("SCIENCE") || phase.Contains("ARRIVAL"))
                return ScenePhase.Landing;
            return ScenePhase.Pad;
        }

        private static float ResolveAltitudeProgress(FlightBeat beat)
        {
            if (beat == null) return 0f;
            // Normalise vs 400 km (LEO reference) for ascent; cap at 1
            return (float)Math.Clamp(beat.AltitudeKm / 400.0, 0.0, 1.0);
        }

        // ── Vehicle Level Sync ────────────────────────────────────────────────────
        private void UpdateVehicleLevel()
        {
            var level = _phase switch
            {
                ScenePhase.Ascent  => VehicleSilhouetteView.SilhouetteLevel.Launch,
                ScenePhase.Space   => VehicleSilhouetteView.SilhouetteLevel.Cruise,
                ScenePhase.Landing => VehicleSilhouetteView.SilhouetteLevel.Landing,
                _                  => VehicleSilhouetteView.SilhouetteLevel.Launch,
            };
            _vehicle.SetLevel(level);
        }

        // ── Sky Transition ────────────────────────────────────────────────────────
        private void SetSkyForPhase(ScenePhase phase, bool immediate)
        {
            // Sky colours are expressed inline per phase in the draw methods
            // (gradient changes smoothly per altitudeProgress) — no discrete targets needed.
            // ReduceMotion: immediate = instant, otherwise scheduled via animation tick.
            _ = immediate; // used by caller as a hint; gradient logic handles smoothness
        }

        // ── Particles ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the vehicle's current on-screen position as normalized canvas
        /// coordinates, accounting for layout, the camera pan/scale transform and
        /// EDL altitude positioning. <paramref name="verticalT"/> interpolates
        /// between the vehicle bottom (0, nozzles) and its visual centre (1, body).
        /// Returns false if the vehicle is not laid out yet.
        /// </summary>
        private bool TryGetVehicleAnchor(float verticalT, out Vector2 anchor)
        {
            anchor = new Vector2(0.5f, 0.5f);
            var vb = _vehicle.worldBound;
            var stage = worldBound;
            if (vb.width < 1 || vb.height < 1 || stage.width < 1 || stage.height < 1)
                return false;

            float y = Mathf.Lerp(vb.yMax, vb.center.y, Mathf.Clamp01(verticalT));
            var p = new Vector2(vb.center.x, y);
            anchor = new Vector2(
                Mathf.Clamp01((p.x - stage.xMin) / stage.width),
                Mathf.Clamp01((p.y - stage.yMin) / stage.height));
            return true;
        }

        /// <summary>
        /// Vehicle anchor in this element's LOCAL pixel space (for Painter2D drawing).
        /// <paramref name="verticalT"/> interpolates bottom (0) -> visual centre (1).
        /// Returns false if the vehicle is not laid out.
        /// </summary>
        private bool TryGetVehicleLocalAnchor(float verticalT, out Vector2 anchor)
        {
            anchor = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.5f);
            var vb = _vehicle.worldBound;
            var stage = worldBound;
            if (vb.width < 1 || vb.height < 1 || stage.width < 1 || stage.height < 1)
                return false;

            float y = Mathf.Lerp(vb.yMax, vb.center.y, Mathf.Clamp01(verticalT));
            anchor = new Vector2(
                vb.center.x - stage.xMin,
                y - stage.yMin);
            return true;
        }

        /// <summary>
        /// Y coordinate (local px) of the pad ground line, aligned so the launch
        /// rocket's base (drawn at 88% of the vehicle element height, see
        /// <see cref="VehicleSilhouetteView.DrawLaunchRocket"/>) rests exactly ON
        /// the ground. Uses the vehicle element's resolved layout, so it stays
        /// correct if the element geometry changes. Returns false while the
        /// vehicle element is not laid out yet (caller falls back to a default).
        /// </summary>
        private bool TryGetPadGroundLine(float stageHeight, out float groundY)
        {
            groundY = stageHeight * 0.85f;
            var layout = _vehicle.layout;
            if (_phase == ScenePhase.Landing || layout.width < 1 || layout.height < 1)
                return false;

            // Vehicle is a direct child: layout coordinates are already in
            // this element's local space. Ignoring the camera transform is
            // intentional — the ground plane is part of the untransformed scene.
            groundY = layout.y + layout.height * 0.88f;
            return groundY > 0f && groundY < stageHeight;
        }

        /// <summary>Emit particles at a position relative to the vehicle anchor.</summary>
        private void EmitRelative(int count, ParticlePool.Kind kind, float verticalT, float dx, float dy)
        {
            if (!TryGetVehicleAnchor(verticalT, out var anchor)) return;
            var origin = new Vector2(
                Mathf.Clamp01(anchor.x + dx),
                Mathf.Clamp01(anchor.y + dy));
            _particles.Emit(count, kind, origin);
        }

        private void SpawnParticlesForBeat(FlightBeat beat)
        {
            string fx = beat.VisualEffectKey ?? "none";
            switch (fx)
            {
                // Launch — exhaust spawns at the nozzles (vehicle bottom edge).
                // While still on/near the pad, also vent pad steam at ground level.
                case "fx_launch_exhaust" or "fx_ignition":
                    EmitRelative(8, ParticlePool.Kind.Exhaust, 0f, 0f, 0.01f);
                    if (_altitudeProgress < 0.05f)
                        EmitRelative(6, ParticlePool.Kind.Vapour, 0f, 0f, 0.07f);
                    break;
                case "fx_liftoff_steam":
                    EmitRelative(12, ParticlePool.Kind.Vapour, 0f, 0f, 0.08f);
                    if (_altitudeProgress < 0.05f)
                        EmitRelative(8, ParticlePool.Kind.Vapour, 0f, 0.02f, 0.09f);
                    break;
                case "fx_stage_separation" or "fx_stage_sep" or "fx_fairing":
                    EmitRelative(12, ParticlePool.Kind.Debris, 0.5f, 0f, -0.02f);
                    break;
                case "fx_stage_ignite":
                    EmitRelative(8, ParticlePool.Kind.Exhaust, 0f, 0f, 0.01f);
                    break;
                case "fx_max_q" or "fx_maxq_cone":
                    EmitRelative(6, ParticlePool.Kind.Vapour, 1f, 0f, -0.06f);
                    break;
                case "fx_abort" or "fx_explosion":
                    EmitRelative(16, ParticlePool.Kind.Debris, 0.5f, 0f, -0.03f);
                    EmitRelative(10, ParticlePool.Kind.Heat, 0.5f, 0f, -0.03f);
                    break;

                // Cruise — spacecraft bus centre
                case "fx_transfer_burn":
                    EmitRelative(14, ParticlePool.Kind.Exhaust, 1f, 0f, 0.02f);
                    break;
                case "fx_correction_burn":
                    EmitRelative(8, ParticlePool.Kind.Exhaust, 1f, 0f, 0.02f);
                    break;

                // EDL (Entry, Descent, Landing)
                case "fx_entry_heating" or "fx_peak_heating" or "fx_edl_heating":
                    EmitRelative(12, ParticlePool.Kind.Heat, 1f, 0f, -0.05f);
                    break;
                case "fx_chute_deploy":
                    EmitRelative(10, ParticlePool.Kind.Vapour, 1f, 0f, -0.10f);
                    break;
                case "fx_retro_burn" or "fx_retro_ignite" or "fx_edl_retro_ignite" or "fx_edl_descent_vac":
                    EmitRelative(12, ParticlePool.Kind.Exhaust, 0f, 0f, 0.03f);
                    break;
                case "fx_touchdown_dust" or "fx_landing_dust":
                    EmitRelative(16, ParticlePool.Kind.Dust, 0f, 0f, 0.06f);
                    break;
                case "fx_edl_fail":
                    EmitRelative(16, ParticlePool.Kind.Debris, 0.5f, 0f, 0.02f);
                    EmitRelative(12, ParticlePool.Kind.Heat, 0.5f, 0f, 0.02f);
                    break;

                // Science Surface Operations
                case "fx_science_data_collect" or "fx_science_progress":
                    EmitRelative(6, ParticlePool.Kind.Vapour, 1f, -0.07f, 0f);
                    break;
                case "fx_science_downlink" or "fx_science_contact":
                    EmitRelative(8, ParticlePool.Kind.Vapour, 1f, 0f, -0.12f);
                    break;
                case "fx_science_fail":
                    EmitRelative(10, ParticlePool.Kind.Debris, 1f, 0f, 0.02f);
                    break;

                // "none" and unrecognised keys: no particles
            }
        }

        // ── Animation Tick ────────────────────────────────────────────────────────
        private void OnAnimationTick()
        {
            float dt = RepaintIntervalMs / 1000f;
            _budget?.RecordRepaint();
            if (!_reduceMotion)
            {
                _camera.Tick(dt);
                _starTwinklePhase += dt * 1.1f;
                _cloudDriftX      += dt * 8f;
                _particles.Tick(dt);
            }
            MarkDirtyRepaint();
        }
    }
}
