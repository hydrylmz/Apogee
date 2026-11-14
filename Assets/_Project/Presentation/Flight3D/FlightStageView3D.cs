using System;
using UnityEngine;
using UnityEngine.UIElements;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Vehicle;

// Nested-type aliases — the shared enums/structs live inside FlightStageView (2D stage).
using ScenePhase = Apogee.UI.Core.Flight.FlightStageView.ScenePhase;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// 3D Flight Theatre stage: a UI Toolkit <see cref="VisualElement"/> that hosts a
    /// runtime-built URP scene rendered into a <see cref="RenderTexture"/> and displayed
    /// via an <see cref="Image"/> child. Implements the same public surface as the 2D
    /// <see cref="FlightStageView"/> (through <see cref="IFlightStage"/>) so the
    /// LaunchSequenceScreenController can swap renderers without touching the
    /// beat-driven orchestration (GDD §3.4, §15.1 — 3D stage plan).
    ///
    /// Architecture:
    ///   - GameObjects live ONLY while the stage is animating (StartAnimation →
    ///     StopAnimation fully tears down camera + scene + RenderTexture), so the
    ///     single bootstrap scene stays clean and WebGL memory stays bounded.
    ///   - <see cref="FlightSceneRig"/>  : pad / space / stars / surface environments
    ///   - <see cref="FlightVehicleRig"/>: modular placeholder rocket + staging
    ///   - <see cref="FlightFxRig"/>     : exhaust / steam / EDL / science FX
    ///   - <see cref="FlightCameraDirector"/>: CameraMotionKey presets + player zoom/pan
    ///   - <see cref="FlightPerformanceBudget"/>: repaint accounting (ADR-061)
    /// </summary>
    public sealed class FlightStageView3D : VisualElement, IFlightStage
    {
        private const int TickIntervalMs = 16; // ~60 Hz director tick
        private const int MinRTWidth = 320, MaxRTWidth = 1920;
        private const int MinRTHeight = 240, MaxRTHeight = 1080;

        // ── UI ──
        private readonly Image _image;

        // ── 3D host ──
        private RenderTexture _rt;
        private GameObject    _cameraGo;
        private GameObject    _rigRoot;
        private Camera        _camera;
        private FlightSceneRig      _scene;
        private FlightVehicleRig    _vehicle;
        private FlightFxRig         _fx;
        private FlightCameraDirector _director;

        // ── State ──
        private FlightPerformanceBudget _budget;
        private FlightBeat _currentBeat;
        private ScenePhase _phase = ScenePhase.Pad;
        private FlightStageView.DestinationProfile _destination;
        private VehicleSilhouetteView.PayloadType _payloadType = VehicleSilhouetteView.PayloadType.Orbiter;
        private bool _reduceMotion;
        private bool _animRunning;
        private float _altitudeProgress;
        private IVisualElementScheduledItem _tickSchedule;

        // ── Construction ──────────────────────────────────────────────────────────
        public FlightStageView3D()
        {
            _destination = new FlightStageView.DestinationProfile
            {
                BodyColor       = new Color(0.10f, 0.35f, 0.60f),
                AtmosphereColor = new Color(0.063f, 0.361f, 0.573f, 0.5f),
                OrbitRingColor  = new Color(0.024f, 0.714f, 0.831f),
                HasAtmosphere   = true,
                HasRings        = false,
                BodyScale       = 1.0f,
                GlowIntensity   = 0.3f,
                BackgroundKey   = "bg_earth_low_orbit",
            };

            style.position = Position.Absolute;
            style.left   = 0; style.top    = 0;
            style.right  = 0; style.bottom = 0;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(0.05f, 0.07f, 0.12f);

            // RT display surface — ignores picking so pointer drags reach this element
            // (parity with the 2D stage's pan/zoom interaction contract).
            _image = new Image
            {
                image     = null,
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore,
            };
            _image.style.position = Position.Absolute;
            _image.style.left   = 0; _image.style.top    = 0;
            _image.style.right  = 0; _image.style.bottom = 0;
            Add(_image);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        // ── Public API (IFlightStage) ─────────────────────────────────────────────

        /// <summary>Apply a new beat — phase, environment, vehicle, camera and FX.</summary>
        public void ApplyBeat(FlightBeat beat)
        {
            if (beat == null) return;
            _currentBeat = beat;

            ScenePhase newPhase = ResolvePhase(beat);
            _phase = newPhase;

            _altitudeProgress = Mathf.Clamp01((float)Math.Clamp(beat.AltitudeKm / 400.0, 0.0, 1.0));

            if (!IsBuilt) return;

            // Environment switch + altitude-driven star visibility
            SyncEnvironment();

            // Camera choreography (same keys as the 2D stage)
            _director.ApplyMotionKey(beat.CameraMotionKey);

            // Cinematic shake on high-energy beats (suppressed by ReduceMotion)
            if (!_reduceMotion)
            {
                string camKey = beat.CameraMotionKey ?? string.Empty;
                if (camKey.Contains("liftoff", StringComparison.OrdinalIgnoreCase) ||
                    camKey.Contains("ground", StringComparison.OrdinalIgnoreCase) ||
                    camKey.Contains("pad_close", StringComparison.OrdinalIgnoreCase))
                    _director.Shake(0.09f, 1.6f);
                else if (camKey.Contains("maxq", StringComparison.OrdinalIgnoreCase))
                    _director.Shake(0.06f, 1.0f);
                else if (camKey.Contains("staging", StringComparison.OrdinalIgnoreCase))
                    _director.Shake(0.05f, 0.8f);
            }

            // Vehicle (staging events, positioning, exhaust anchor selection)
            _vehicle.ApplyBeat(beat, _phase);

            // FX reset + per-beat triggers (mirrors the 2D SpawnParticlesForBeat table)
            _fx.ResetTransient();
            ApplyFxForBeat(beat);

            // Exhaust only while engines actually fire (MECO/SECO/coast = no flame,
            // playtest fix #2 parity)
            string titleKey = beat.TitleKey ?? string.Empty;
            bool coastBeat = titleKey.Contains("MECO", StringComparison.OrdinalIgnoreCase) ||
                             titleKey.Contains("SECO", StringComparison.OrdinalIgnoreCase) ||
                             titleKey.Contains("CUTOFF", StringComparison.OrdinalIgnoreCase) ||
                             beat.PhaseId.Contains("Separation", StringComparison.OrdinalIgnoreCase) ||
                             beat.PhaseId.Contains("Fairing", StringComparison.OrdinalIgnoreCase) ||
                             _phase == ScenePhase.Space && !IsBurnBeat(beat);
            bool burning = !coastBeat && !_reduceMotion &&
                           (_phase == ScenePhase.Ascent || IsBurnBeat(beat));
            _fx.SetExhaust(burning, _vehicle.ActiveExhaustAnchor);

            // Retro plume (EDL descent)
            bool isRetro = beat.PhaseId.Contains("DESCENT", StringComparison.OrdinalIgnoreCase) ||
                           (beat.VisualEffectKey?.Contains("retro", StringComparison.OrdinalIgnoreCase) ?? false);
            _fx.SetRetro(isRetro && !_reduceMotion, _vehicle.PayloadBaseAnchor);

            // Background (sky) colour per phase/altitude
            _camera.backgroundColor = _scene.ResolveBackground(_phase, _altitudeProgress, _destination);

            // Camera context (framing adjustments)
            _director.SetContext(_phase, _altitudeProgress, beat.CruiseProgress);
        }

        /// <summary>Set the payload variant (Rover, Lander, Orbiter).</summary>
        public void SetPayloadType(VehicleSilhouetteView.PayloadType type)
        {
            _payloadType = type;
            _vehicle?.SetPayloadType(type);
        }

        /// <summary>Set the destination visual profile (flight_visuals.json).</summary>
        public void SetDestinationProfile(FlightStageView.DestinationProfile profile)
        {
            _destination = profile;
            _scene?.ApplyDestinationProfile(profile);
            if (IsBuilt && _camera != null)
                _camera.backgroundColor = _scene.ResolveBackground(_phase, _altitudeProgress, _destination);
        }

        /// <summary>Toggle ReduceMotion at runtime (Ayarlar binding).</summary>
        public void SetReduceMotion(bool reduce)
        {
            _reduceMotion = reduce;
            _director?.SetReduceMotion(reduce);
            _vehicle?.SetReduceMotion(reduce);
            _fx?.SetReduceMotion(reduce);
            _scene?.SetReduceMotion(reduce);
        }

        /// <summary>Attach the shared FlightPerformanceBudget (ADR-061).</summary>
        public void SetPerformanceBudget(FlightPerformanceBudget budget) => _budget = budget;

        /// <summary>Zoom the camera in/out on top of the current preset (player control).</summary>
        public void AdjustCameraZoom(float delta) => _director?.AdjustZoom(delta);

        /// <summary>Reset the player zoom layer back to the choreographed default.</summary>
        public void ResetCameraZoom() => _director?.ResetZoom();

        /// <summary>Current player zoom multiplier (1.0 = choreographed default).</summary>
        public float CameraZoom => _director?.UserZoom ?? 1.0f;

        /// <summary>Player drag-pan: offsets the frame by delta pixels.</summary>
        public void PanCameraBy(Vector2 delta)
        {
            if (_director == null) return;
            var maxPan = new Vector2(contentRect.width * 0.30f, contentRect.height * 0.25f);
            _director.PanBy(delta, maxPan);
        }

        /// <summary>Snap the camera instantly to its current targets (sequence handoff).</summary>
        public void SnapCamera() => _director?.Snap();

        /// <summary>Build the 3D rig and start the per-frame tick (call after AddToHierarchy).</summary>
        public void StartAnimation()
        {
            if (_animRunning) return;
            _animRunning = true;

            EnsureBuilt();

            if (_currentBeat != null)
            {
                // Re-apply the last beat so a rebuild (e.g. after StopAnimation)
                // resumes in the correct phase rather than snapping back to the pad.
                SyncEnvironment();
                ApplyBeat(_currentBeat);
            }

            _tickSchedule?.Pause();
            _tickSchedule = schedule.Execute(OnTick).Every(TickIntervalMs);
        }

        /// <summary>Stop the tick and fully tear down the 3D rig (screen hidden).</summary>
        public void StopAnimation()
        {
            if (!_animRunning && !IsBuilt) return;
            _animRunning = false;
            _tickSchedule?.Pause();
            _tickSchedule = null;
            Teardown();
        }

        // ── Build / teardown ──────────────────────────────────────────────────────

        private bool IsBuilt => _scene != null && _camera != null;

        private void EnsureBuilt()
        {
            if (IsBuilt) return;

            // RenderTexture sized to the element rect (clamped)
            int w = Mathf.Clamp(Mathf.RoundToInt(contentRect.width),  MinRTWidth,  MaxRTWidth);
            int h = Mathf.Clamp(Mathf.RoundToInt(contentRect.height), MinRTHeight, MaxRTHeight);
            if (contentRect.width < 10 || contentRect.height < 10)
            {
                w = Mathf.Max(w, 1280);
                h = Mathf.Max(h, 720);
            }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);

            // Camera GameObject (renders to RT only — never touches the main display)
            _cameraGo = new GameObject("FlightTheatre3DCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.clearFlags      = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            _camera.fieldOfView     = 46f;
            _camera.nearClipPlane   = 0.1f;
            _camera.farClipPlane    = 800f;
            _camera.allowHDR        = false;
            _camera.targetTexture   = _rt;
            _camera.enabled         = true; // auto-renders to the RT each frame

            // One rig root owns environment + vehicle + FX; destroyed together.
            _rigRoot = new GameObject("FlightTheatre3DRoot");
            _scene   = new FlightSceneRig(_rigRoot.transform);
            _scene.ApplyDestinationProfile(_destination);
            _vehicle = new FlightVehicleRig(_rigRoot.transform);
            _fx      = new FlightFxRig(_rigRoot.transform);

            _director = new FlightCameraDirector(_camera, _reduceMotion);
            _vehicle.SetPayloadType(_payloadType);
            _vehicle.SetReduceMotion(_reduceMotion);
            _fx.SetReduceMotion(_reduceMotion);
            _scene.SetReduceMotion(_reduceMotion);

            _image.image = _rt;
        }

        private void Teardown()
        {
            _image.image = null;

            if (_camera != null)
            {
                _camera.targetTexture = null;
                _camera.enabled = false;
            }
            if (_rt != null)
            {
                _rt.Release();
                UnityEngine.Object.Destroy(_rt);
                _rt = null;
            }
            if (_scene != null)
            {
                _scene.Teardown();
                _scene = null;
            }
            if (_rigRoot != null)
            {
                UnityEngine.Object.Destroy(_rigRoot);
                _rigRoot = null;
            }
            if (_cameraGo != null)
            {
                UnityEngine.Object.Destroy(_cameraGo);
                _cameraGo = null;
            }
            _camera   = null;
            _vehicle  = null;
            _fx       = null;
            _director = null;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (!IsBuilt || !_animRunning) return;
            var oldRect = evt.oldRect;
            var newRect = evt.newRect;
            if (Mathf.Abs(oldRect.width - newRect.width) < 4f &&
                Mathf.Abs(oldRect.height - newRect.height) < 4f) return;

            // Recreate the RT at the new size (RenderTexture is immutable once created)
            int w = Mathf.Clamp(Mathf.RoundToInt(newRect.width),  MinRTWidth,  MaxRTWidth);
            int h = Mathf.Clamp(Mathf.RoundToInt(newRect.height), MinRTHeight, MaxRTHeight);
            if (_rt != null && w == _rt.width && h == _rt.height) return;

            _camera.targetTexture = null;
            _rt.Release();
            UnityEngine.Object.Destroy(_rt);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _rt;
            _image.image = _rt;
        }

        // ── Tick ──────────────────────────────────────────────────────────────────
        private void SyncEnvironment()
        {
            bool stars = _phase == ScenePhase.Space ||
                         (_phase == ScenePhase.Ascent && _altitudeProgress > 0.45f) ||
                         (_phase == ScenePhase.Landing && !_destination.HasAtmosphere);
            _scene.SetPhase(_phase, stars);
        }

        private void OnTick()
        {
            if (!IsBuilt) return;
            float dt = TickIntervalMs / 1000f;
            _budget?.RecordRepaint();

            _director.SetFocus(_vehicle.Position);
            _vehicle.Tick(dt);
            _scene.Tick(dt);
            _fx.Tick(dt);
            _director.Tick(dt);
        }

        // ── FX triggers (mirror of the 2D SpawnParticlesForBeat table) ────────────
        private void ApplyFxForBeat(FlightBeat beat)
        {
            string fx = beat.VisualEffectKey ?? "none";
            Vector3 vehiclePos = _vehicle.Position;

            switch (fx)
            {
                case "fx_launch_exhaust" or "fx_ignition":
                    // Exhaust handled by the burn logic; pad steam near the ground
                    if (_altitudeProgress < 0.05f)
                        _fx.SetSteam(true, new Vector3(0f, 0.6f, 0f));
                    break;

                case "fx_liftoff_steam":
                    _fx.SetSteam(true, new Vector3(0f, 0.6f, 0f));
                    break;

                case "fx_stage_separation" or "fx_stage_sep":
                    _fx.Burst(vehiclePos + Vector3.down * 1.5f, 30,
                              new Color(0.8f, 0.82f, 0.86f, 0.9f), size: 0.7f, speed: 7f);
                    break;

                case "fx_stage_ignite":
                    _fx.Burst(_vehicle.ActiveExhaustAnchor.position, 16,
                              ExhaustColor, size: 0.6f, speed: 8f);
                    break;

                case "fx_fairing":
                    _fx.Burst(vehiclePos + Vector3.up * 4f, 24,
                              new Color(0.9f, 0.92f, 0.95f, 0.9f), size: 0.5f, speed: 6f);
                    break;

                case "fx_max_q" or "fx_maxq_cone":
                    _fx.SetVapourCone(true, _vehicle.NoseAnchor);
                    break;

                case "fx_abort" or "fx_explosion":
                    _fx.Burst(vehiclePos, 60, new Color(1.0f, 0.5f, 0.1f), size: 1.2f, speed: 10f);
                    _fx.Burst(vehiclePos, 40, new Color(0.9f, 0.9f, 0.9f, 0.9f), size: 0.8f, speed: 8f);
                    break;

                case "fx_orbit_ring" or "fx_orbit_partial":
                {
                    // Orbital insertion celebration — cyan sparkle at the vehicle
                    Color ring = _destination.OrbitRingColor;
                    int count = fx == "fx_orbit_ring" ? 40 : 22;
                    _fx.Burst(vehiclePos, count, new Color(ring.r, ring.g, ring.b, 0.9f),
                              size: 0.55f, speed: 5f);
                    break;
                }

                case "fx_weather_scrub":
                    // Scrubbed on the pad — white vapour puff rolls off the stack
                    _fx.SetSteam(true, new Vector3(0f, 1.2f, 0f));
                    _fx.Burst(vehiclePos, 18, new Color(0.85f, 0.88f, 0.92f, 0.8f),
                              size: 0.8f, speed: 4f);
                    break;

                // Cruise — telemetry-only beats stay silent in 3D (2D parity);
                // fx_transfer_burn / fx_correction_burn drive the exhaust logic above.
                case "fx_transfer_burn":
                case "fx_correction_burn":
                case "fx_cruise_nominal":
                case "fx_decision_card":
                case "fx_warning_blink":
                case "fx_signal_static":
                    break;

                // EDL
                case "fx_entry_heating" or "fx_peak_heating" or "fx_edl_heating":
                    _fx.SetHeatGlow(true, new Vector3(0f, 0.4f, 0f), _vehicle.PayloadAnchor,
                                    (float)beat.HeatShieldIntegrity);
                    break;

                case "fx_chute_deploy":
                    _fx.SetChute(true, _vehicle.PayloadAnchor);
                    break;

                case "fx_touchdown_dust" or "fx_landing_dust":
                    _fx.TouchdownDust(new Vector3(vehiclePos.x, 0.2f, FlightSceneRig.SurfaceZ), 50);
                    break;

                case "fx_edl_fail":
                    _fx.Burst(vehiclePos, 60, new Color(1.0f, 0.5f, 0.1f), size: 1.2f, speed: 10f);
                    _fx.Burst(new Vector3(vehiclePos.x, 0.2f, FlightSceneRig.SurfaceZ), 30, DustColor, size: 1.0f, speed: 6f);
                    break;

                // Science Surface Operations
                case "fx_science_data_collect" or "fx_science_progress":
                {
                    var p = vehiclePos;
                    _fx.SetScanBeam(true,
                        new Vector3(p.x + 0.4f, p.y + 1.0f, p.z),
                        new Vector3(p.x + 1.8f, 0.2f, p.z));
                    break;
                }

                case "fx_science_downlink" or "fx_science_contact":
                    _fx.SetDownlink(true, vehiclePos + Vector3.up * 0.8f);
                    break;
            }
        }

        private static readonly Color ExhaustColor = new Color(0.961f, 0.620f, 0.043f);
        private static readonly Color DustColor    = new Color(0.55f, 0.50f, 0.42f);

        private static bool IsBurnBeat(FlightBeat beat)
        {
            string id = beat.PhaseId ?? string.Empty;
            return id.Contains("Ignition", StringComparison.OrdinalIgnoreCase) ||
                   id.Contains("Liftoff", StringComparison.OrdinalIgnoreCase) ||
                   id.Contains("Burn", StringComparison.OrdinalIgnoreCase);
        }

        // ── Phase resolution (mirror of the 2D stage) ─────────────────────────────
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
    }
}
