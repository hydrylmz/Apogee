using System;
using System.Collections.Generic;
using UnityEngine;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Vehicle;

// Nested-type aliases — the shared enums live inside FlightStageView (2D stage).
using ScenePhase = Apogee.UI.Core.Flight.FlightStageView.ScenePhase;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// Runtime-built modular vehicle rig for the 3D Flight Theatre.
    ///
    /// Placeholder geometry (Unity primitives) laid out with the same part names an
    /// FBX/GLB drop-in will use later (Stage1/Stage2/Stage3/Fairing/Capsule/Rover…),
    /// so swapping in authored models requires no code change (3D stage plan, Faz 2/6).
    ///
    /// Beat-driven behaviour:
    ///   - Launch.*Separation  — detaches the matching stage into world-space debris
    ///   - Launch.FairingJettison — hides the fairing, ejects two halves
    ///   - Ignition (sequence start / rewatch) — resets the full stack
    ///   - Cruise — capsule bus travels a bezier arc (CruiseProgress-driven)
    ///   - EDL — payload descends with altitude; touchdown locks to the surface
    ///   - Science — payload type shape (rover / lander / orbiter) drives forward
    /// </summary>
    public sealed class FlightVehicleRig
    {
        // ── Layout constants (metres; total stack ≈ 10 u) ─────────────────────────
        private const float Stage1H   = 4.0f;
        private const float Stage2H   = 2.2f;
        private const float Stage3H   = 1.5f;
        private const float FairingH  = 1.8f;
        private const float PadBaseY  = 0.4f;

        private static readonly Vector3 CruiseP0 = new Vector3(-45f, 10f, -12f);
        private static readonly Vector3 CruiseP1 = new Vector3(  0f, 32f,   6f);
        private static readonly Vector3 CruiseP2 = new Vector3( 45f, 12f, -18f);
        private const float SurfaceZ = 90f;          // Surface environment world offset (FlightSceneRig)

        // ── Parts ─────────────────────────────────────────────────────────────────
        public GameObject Root { get; }

        private readonly GameObject _stackGroup;      // full launch stack
        private readonly GameObject _stage1;
        private readonly GameObject _stage2;
        private readonly GameObject _stage3;
        private readonly GameObject _fairing;
        private readonly GameObject _payloadGroup;    // capsule / rover / lander / orbiter
        private readonly GameObject _capsule;
        private readonly GameObject _rover;
        private readonly GameObject _lander;
        private readonly GameObject _orbiter;
        private readonly GameObject _debrisGroup;     // world-space (sibling of Root)

        // Exhaust anchors (stack local space)
        private readonly Transform _anchorStage1;
        private readonly Transform _anchorStage2;
        private readonly Transform _anchorStage3;
        private readonly Transform _anchorBus;
        private readonly Transform _anchorPayloadBase;
        private readonly Transform _noseAnchor;

        private struct Debris
        {
            public GameObject Go;
            public Vector3    Velocity;
            public Vector3    Spin;
            public float      Remaining;
        }

        private readonly List<Debris> _debris = new List<Debris>();

        // ── State ─────────────────────────────────────────────────────────────────
        private ScenePhase _phase = ScenePhase.Pad;
        private VehicleSilhouetteView.PayloadType _payloadType = VehicleSilhouetteView.PayloadType.Orbiter;

        private int  _currentStage = 1;               // active engine stage (exhaust anchor)
        private bool _scienceActive;
        private bool _reduceMotion;
        private float _scienceDriveX;

        private static readonly System.Random Rng = new System.Random(42);

        // ── Construction ──────────────────────────────────────────────────────────
        public FlightVehicleRig(Transform parent)
        {
            var rootGo = new GameObject("Vehicle");
            Root = rootGo;
            Root.transform.SetParent(parent, false);

            var debrisGo = new GameObject("VehicleDebris");
            _debrisGroup = debrisGo;
            _debrisGroup.transform.SetParent(parent, false);

            Material hull    = Flight3DMaterials.Lit(new Color(0.82f, 0.84f, 0.88f));
            Material hullDim = Flight3DMaterials.Lit(new Color(0.35f, 0.40f, 0.48f));
            Material accent  = Flight3DMaterials.Lit(new Color(0.024f, 0.714f, 0.831f));
            Material dark    = Flight3DMaterials.Lit(new Color(0.16f, 0.18f, 0.22f));

            // ── Launch stack ──
            _stackGroup = new GameObject("Stack");
            _stackGroup.transform.SetParent(Root.transform, false);

            // Stage 1: booster body + engine skirt
            _stage1 = new GameObject("Stage1");
            _stage1.transform.SetParent(_stackGroup.transform, false);
            CreatePrimitive(PrimitiveType.Cylinder, "S1Body", _stage1.transform,
                new Vector3(0f, Stage1H * 0.5f, 0f), new Vector3(1.0f, Stage1H * 0.5f, 1.0f), hull);
            CreatePrimitive(PrimitiveType.Cylinder, "S1Skirt", _stage1.transform,
                new Vector3(0f, 0.12f, 0f), new Vector3(1.08f, 0.12f, 1.08f), hullDim);
            CreatePrimitive(PrimitiveType.Cylinder, "S1Nozzle", _stage1.transform,
                new Vector3(0f, 0.28f, 0f), new Vector3(0.5f, 0.28f, 0.5f), dark);

            // Stage 2 (slimmer, upper)
            _stage2 = new GameObject("Stage2");
            _stage2.transform.SetParent(_stackGroup.transform, false);
            float s2Base = Stage1H;
            CreatePrimitive(PrimitiveType.Cylinder, "S2Body", _stage2.transform,
                new Vector3(0f, s2Base + Stage2H * 0.5f, 0f), new Vector3(0.9f, Stage2H * 0.5f, 0.9f), hull);
            CreatePrimitive(PrimitiveType.Cylinder, "S2Nozzle", _stage2.transform,
                new Vector3(0f, s2Base + 0.18f, 0f), new Vector3(0.42f, 0.18f, 0.42f), dark);

            // Stage 3
            _stage3 = new GameObject("Stage3");
            _stage3.transform.SetParent(_stackGroup.transform, false);
            float s3Base = Stage1H + Stage2H;
            CreatePrimitive(PrimitiveType.Cylinder, "S3Body", _stage3.transform,
                new Vector3(0f, s3Base + Stage3H * 0.5f, 0f), new Vector3(0.8f, Stage3H * 0.5f, 0.8f), hull);
            CreatePrimitive(PrimitiveType.Cylinder, "S3Nozzle", _stage3.transform,
                new Vector3(0f, s3Base + 0.16f, 0f), new Vector3(0.36f, 0.16f, 0.36f), dark);

            // Fairing (capsule shape on top)
            float fairBase = Stage1H + Stage2H + Stage3H;
            _fairing = new GameObject("Fairing");
            _fairing.transform.SetParent(_stackGroup.transform, false);
            CreatePrimitive(PrimitiveType.Sphere, "FairingBulge", _fairing.transform,
                new Vector3(0f, fairBase + FairingH * 0.35f, 0f), new Vector3(1.5f, 1.2f, 1.5f), accent);
            CreatePrimitive(PrimitiveType.Cylinder, "FairingCone", _fairing.transform,
                new Vector3(0f, fairBase + FairingH * 0.8f, 0f), new Vector3(0.55f, FairingH * 0.35f, 0.55f), hull);

            // ── Payload variants ──
            _payloadGroup = new GameObject("Payload");
            _payloadGroup.transform.SetParent(Root.transform, false);

            // Cruise capsule bus (solar panels)
            _capsule = new GameObject("Capsule");
            _capsule.transform.SetParent(_payloadGroup.transform, false);
            CreatePrimitive(PrimitiveType.Capsule, "Bus", _capsule.transform,
                Vector3.zero, new Vector3(0.9f, 0.7f, 0.9f), hull);
            CreatePrimitive(PrimitiveType.Cube, "PanelL", _capsule.transform,
                new Vector3(-1.2f, 0f, 0f), new Vector3(1.4f, 0.04f, 0.7f), dark);
            CreatePrimitive(PrimitiveType.Cube, "PanelR", _capsule.transform,
                new Vector3( 1.2f, 0f, 0f), new Vector3(1.4f, 0.04f, 0.7f), dark);
            CreatePrimitive(PrimitiveType.Cylinder, "Nozzle", _capsule.transform,
                new Vector3(0f, -0.7f, 0f), new Vector3(0.3f, 0.18f, 0.3f), dark);

            // Rover (wheels + mast)
            _rover = new GameObject("Rover");
            _rover.transform.SetParent(_payloadGroup.transform, false);
            CreatePrimitive(PrimitiveType.Cube, "Body", _rover.transform,
                new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.5f, 1.1f), hull);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.8f : 0.8f;
                float z = (i < 2) ? -0.55f : 0.55f;
                CreatePrimitive(PrimitiveType.Cylinder, $"Wheel{i}", _rover.transform,
                    new Vector3(x, 0.2f, z), new Vector3(0.28f, 0.06f, 0.28f), dark, 90f, 0f, 0f);
            }
            CreatePrimitive(PrimitiveType.Cylinder, "Mast", _rover.transform,
                new Vector3(0.5f, 0.9f, 0f), new Vector3(0.05f, 0.45f, 0.05f), hullDim);
            CreatePrimitive(PrimitiveType.Cube, "CamHead", _rover.transform,
                new Vector3(0.5f, 1.35f, 0f), new Vector3(0.3f, 0.2f, 0.2f), dark);

            // Lander (platform + legs)
            _lander = new GameObject("Lander");
            _lander.transform.SetParent(_payloadGroup.transform, false);
            CreatePrimitive(PrimitiveType.Cylinder, "Deck", _lander.transform,
                new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.12f, 0.9f), hull);
            CreatePrimitive(PrimitiveType.Cube, "Habitat", _lander.transform,
                new Vector3(0f, 1.25f, 0f), new Vector3(0.7f, 0.7f, 0.7f), accent);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var legPos = new Vector3(Mathf.Cos(a) * 0.75f, 0.4f, Mathf.Sin(a) * 0.75f);
                var leg = CreatePrimitive(PrimitiveType.Cylinder, $"Leg{i}", _lander.transform,
                    legPos, new Vector3(0.07f, 0.4f, 0.07f), hullDim);
                leg.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(a) * 25f, 0f, -Mathf.Cos(a) * 25f);
            }

            // Orbiter (bus + dish + panels)
            _orbiter = new GameObject("Orbiter");
            _orbiter.transform.SetParent(_payloadGroup.transform, false);
            CreatePrimitive(PrimitiveType.Cube, "Body", _orbiter.transform,
                Vector3.zero, new Vector3(1.0f, 0.5f, 1.0f), hull);
            CreatePrimitive(PrimitiveType.Sphere, "Dish", _orbiter.transform,
                new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.4f, 0.9f), accent);
            CreatePrimitive(PrimitiveType.Cube, "PanelL", _orbiter.transform,
                new Vector3(-1.3f, 0f, 0f), new Vector3(1.6f, 0.03f, 0.8f), dark);
            CreatePrimitive(PrimitiveType.Cube, "PanelR", _orbiter.transform,
                new Vector3( 1.3f, 0f, 0f), new Vector3(1.6f, 0.03f, 0.8f), dark);

            // ── Exhaust anchors (stack local space) ──
            _anchorStage1 = CreateAnchor("AnchorStage1", new Vector3(0f, 0.35f, 0f), _stackGroup.transform);
            _anchorStage2 = CreateAnchor("AnchorStage2", new Vector3(0f, Stage1H + 0.2f, 0f), _stackGroup.transform);
            _anchorStage3 = CreateAnchor("AnchorStage3", new Vector3(0f, Stage1H + Stage2H + 0.18f, 0f), _stackGroup.transform);
            _anchorBus    = CreateAnchor("AnchorBus",    new Vector3(0f, -0.7f, 0f), _capsule.transform);
            _anchorPayloadBase = CreateAnchor("AnchorPayloadBase", new Vector3(0f, 0.35f, 0f), _payloadGroup.transform);
            _noseAnchor   = CreateAnchor("AnchorNose",   new Vector3(0f, fairBase + FairingH, 0f), _stackGroup.transform);

            ApplyLayout();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public Vector3 Position => Root.transform.position;

        /// <summary>Anchor of the currently burning engine (drives exhaust FX).</summary>
        public Transform ActiveExhaustAnchor
        {
            get
            {
                // Payload phases burn through the bus nozzle, not the (hidden) stack
                if (_phase == ScenePhase.Space)     return _anchorBus;
                if (_phase == ScenePhase.Landing)   return _anchorPayloadBase;
                return _currentStage switch
                {
                    2 => _anchorStage2,
                    3 => _anchorStage3,
                    _ => _anchorStage1,
                };
            }
        }

        /// <summary>Nose anchor — Max-Q cone / fairing FX attach point (stack space).</summary>
        public Transform NoseAnchor => _noseAnchor;

        /// <summary>Payload bus anchor — cruise burn FX attach point (capsule local).</summary>
        public Transform BusAnchor => _anchorBus;

        /// <summary>Payload group root — always valid in Space/Landing phases.</summary>
        public Transform PayloadAnchor => _payloadGroup.transform;

        /// <summary>Payload base — retro plume / chute FX attach point (payload local).</summary>
        public Transform PayloadBaseAnchor => _anchorPayloadBase;

        /// <summary>Whether the stack (vs bare payload) is currently shown.</summary>
        public bool StackVisible => _stackGroup.activeSelf;

        public void SetPayloadType(VehicleSilhouetteView.PayloadType type) => _payloadType = type;

        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        /// <summary>
        /// Apply a beat: phase transitions, staging events, exhaust state and
        /// science/retro flags. Safe against arbitrary seeks (idempotent staging).
        /// </summary>
        public void ApplyBeat(FlightBeat beat, ScenePhase phase)
        {
            if (beat == null) return;
            _phase = phase;
            string id = beat.PhaseId ?? string.Empty;

            // Sequence start (also handles Rewatch / SeekTo(0))
            if (id.Contains("Ignition", StringComparison.OrdinalIgnoreCase) &&
                !id.Contains("Stage2", StringComparison.OrdinalIgnoreCase) &&
                !id.Contains("Stage3", StringComparison.OrdinalIgnoreCase))
            {
                ResetStack();
            }

            // Staging (idempotent — safe when seeking mid-sequence)
            if (id.Contains("Stage1Separation", StringComparison.OrdinalIgnoreCase) ||
                (id.Contains("Stage2", StringComparison.OrdinalIgnoreCase) && id.Contains("Ignition", StringComparison.OrdinalIgnoreCase)))
                DetachStage(1);
            if (id.Contains("Stage2Separation", StringComparison.OrdinalIgnoreCase) ||
                (id.Contains("Stage3", StringComparison.OrdinalIgnoreCase) && id.Contains("Ignition", StringComparison.OrdinalIgnoreCase)))
                DetachStage(2);
            if (id.Contains("FairingJettison", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("Fairing", StringComparison.OrdinalIgnoreCase) && id.Contains("Jettison", StringComparison.OrdinalIgnoreCase))
                JettisonFairing();
            if (id.Contains("ORBIT", StringComparison.OrdinalIgnoreCase) || id.Contains("Insertion", StringComparison.OrdinalIgnoreCase))
            {
                DetachStage(1);
                DetachStage(2);
                JettisonFairing();
            }

            // Exhaust anchor follows the burning stage
            if (id.Contains("Stage3", StringComparison.OrdinalIgnoreCase)) _currentStage = 3;
            else if (id.Contains("Stage2", StringComparison.OrdinalIgnoreCase)) _currentStage = 2;
            else if (id.Contains("Ignition", StringComparison.OrdinalIgnoreCase) || id.Contains("Liftoff", StringComparison.OrdinalIgnoreCase) || id.Contains("MaxQ", StringComparison.OrdinalIgnoreCase))
                _currentStage = 1;

            // Visual flags (mirror the 2D stage logic)
            _scienceActive = id.Contains("SCIENCE", StringComparison.OrdinalIgnoreCase) ||
                             (beat.VisualEffectKey?.Contains("science", StringComparison.OrdinalIgnoreCase) ?? false);

            ApplyLayout();
            ApplyPosition(beat);
        }

        /// <summary>Advance debris physics + science drive. <paramref name="dt"/> in seconds.</summary>
        public void Tick(float dt)
        {
            // Debris tumble
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                if (d.Go == null) { _debris.RemoveAt(i); continue; }
                if (!_reduceMotion)
                {
                    d.Velocity += Vector3.down * 4f * dt;                      // light gravity
                    d.Go.transform.position += d.Velocity * dt;
                    d.Go.transform.Rotate(d.Spin * dt, Space.Self);
                }
                d.Remaining -= dt;
                if (d.Remaining <= 0f)
                {
                    UnityEngine.Object.Destroy(d.Go);
                    _debris.RemoveAt(i);
                }
                else
                {
                    _debris[i] = d;
                }
            }

            // Science surface drive (slow forward roll)
            if (_phase == ScenePhase.Landing && _scienceActive && !_reduceMotion)
            {
                _scienceDriveX += dt * 0.4f;
                var p = Root.transform.position;
                p.x = 0.5f + _scienceDriveX % 8f;
                Root.transform.position = p;
            }
        }

        // ── Layout / position ─────────────────────────────────────────────────────

        /// <summary>Show the correct group for the current phase + payload type.</summary>
        private void ApplyLayout()
        {
            switch (_phase)
            {
                case ScenePhase.Space:
                    _stackGroup.SetActive(false);
                    _payloadGroup.SetActive(true);
                    SetPayloadVariant(showCapsule: true);
                    break;
                case ScenePhase.Landing:
                    _stackGroup.SetActive(false);
                    _payloadGroup.SetActive(true);
                    SetPayloadVariant(showCapsule: false);
                    break;
                default: // Pad / Ascent
                    _stackGroup.SetActive(true);
                    _payloadGroup.SetActive(false);
                    break;
            }
        }

        private void SetPayloadVariant(bool showCapsule)
        {
            if (_capsule != null) _capsule.SetActive(showCapsule);
            if (_rover  != null)  _rover.SetActive(!showCapsule && _payloadType == VehicleSilhouetteView.PayloadType.Rover);
            if (_lander != null)  _lander.SetActive(!showCapsule && _payloadType == VehicleSilhouetteView.PayloadType.Lander);
            if (_orbiter != null) _orbiter.SetActive(!showCapsule && _payloadType == VehicleSilhouetteView.PayloadType.Orbiter);
        }

        /// <summary>Compute the root world position + orientation for the current beat.</summary>
        private void ApplyPosition(FlightBeat beat)
        {
            float altProgress = Mathf.Clamp01((float)Math.Clamp(beat.AltitudeKm / 400.0, 0.0, 1.0));

            switch (_phase)
            {
                case ScenePhase.Pad:
                    Root.transform.SetPositionAndRotation(new Vector3(0f, PadBaseY, 0f), Quaternion.identity);
                    break;

                case ScenePhase.Ascent:
                {
                    // Climb up to y ≈ 70; gravity-turn tilt builds 0.06 → 0.55 then relaxes.
                    float y = PadBaseY + 70f * altProgress;
                    float turnT = Mathf.InverseLerp(0.06f, 0.55f, altProgress);
                    float tilt  = Mathf.SmoothStep(0f, 12f, turnT);
                    var pos = new Vector3(Mathf.Sin(tilt * Mathf.Deg2Rad) * 2f, y, 0f);
                    Root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, -tilt));
                    break;
                }

                case ScenePhase.Space:
                {
                    float t = Mathf.Clamp01((float)(beat.CruiseProgress is > 0 ? beat.CruiseProgress : 0.0));
                    Vector3 pos = Bezier(CruiseP0, CruiseP1, CruiseP2, t);
                    Vector3 tangent = BezierTangent(CruiseP0, CruiseP1, CruiseP2, t);
                    Root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent, Vector3.up));
                    break;
                }

                case ScenePhase.Landing:
                {
                    bool isSurface = beat.PhaseId.Contains("SCIENCE", StringComparison.OrdinalIgnoreCase) ||
                                     beat.PhaseId.Contains("TOUCHDOWN", StringComparison.OrdinalIgnoreCase) ||
                                     beat.AltitudeKm <= 0.05;
                    float descentT = isSurface ? 0f : Mathf.Clamp01((float)(beat.AltitudeKm / 60.0));
                    float y = 0.6f + 35f * descentT;
                    Root.transform.SetPositionAndRotation(
                        new Vector3(_scienceDriveX % 8f, y, SurfaceZ),
                        Quaternion.identity);
                    break;
                }
            }
        }

        // ── Staging ───────────────────────────────────────────────────────────────

        private void ResetStack()
        {
            _currentStage = 1;
            _scienceDriveX = 0f;

            // Clear debris
            foreach (var d in _debris)
                if (d.Go != null) UnityEngine.Object.Destroy(d.Go);
            _debris.Clear();

            // Restore stack
            if (_stage1  != null) _stage1.SetActive(true);
            if (_stage2  != null) _stage2.SetActive(true);
            if (_stage3  != null) _stage3.SetActive(true);
            if (_fairing != null) _fairing.SetActive(true);
            ApplyLayout();
        }

        private void DetachStage(int stage)
        {
            GameObject part = stage switch { 1 => _stage1, 2 => _stage2, _ => _stage3 };
            if (part == null || !part.activeSelf) return;

            // Stage2 separation implies stage1 already gone (idempotent ordering)
            if (stage >= 2) DetachStage(1);

            part.SetActive(false);
            SpawnDebris(part.name, part.transform.position, Quaternion.identity,
                new Vector3((float)(Rng.NextDouble() - 0.5) * 1.5f, -2.5f, (float)(Rng.NextDouble() - 0.5)),
                new Vector3(30f, 10f, 45f), hullSizeFor(stage));
        }

        private void JettisonFairing()
        {
            if (_fairing == null || !_fairing.activeSelf) return;
            _fairing.SetActive(false);
            var nose = _noseAnchor != null ? _noseAnchor.position : Root.transform.position;
            SpawnDebris("FairingHalves", nose + new Vector3(-0.3f, 0f, 0f), Quaternion.identity,
                new Vector3(-2.5f, -0.5f, 0.4f), new Vector3(60f, 20f, 30f), Vector3.one * 0.5f);
            SpawnDebris("FairingHalves", nose + new Vector3( 0.3f, 0f, 0f), Quaternion.identity,
                new Vector3( 2.5f, -0.5f, -0.4f), new Vector3(-60f, 20f, 30f), Vector3.one * 0.5f);
        }

        private void SpawnDebris(string name, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 spin, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name + "_Debris";
            go.GetComponent<Renderer>().sharedMaterial =
                Flight3DMaterials.Lit(new Color(0.45f, 0.48f, 0.54f));
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = new Vector3(scale.x * 0.8f, scale.y * 0.6f, scale.z * 0.8f);
            go.transform.SetParent(_debrisGroup.transform, true);
            _debris.Add(new Debris { Go = go, Velocity = vel, Spin = spin, Remaining = 8f });
        }

        private static Vector3 hullSizeFor(int stage) => stage switch
        {
            1 => new Vector3(1.0f, Stage1H, 1.0f),
            2 => new Vector3(0.9f, Stage2H, 0.9f),
            _ => new Vector3(0.8f, Stage3H, 0.8f),
        };

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Transform CreateAnchor(string name, Vector3 localPos, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Material material,
            float eulerX = 0f, float eulerY = 0f, float eulerZ = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(eulerX, eulerY, eulerZ);
            go.transform.localScale    = localScale;
            // Cylinder/Capsule primitives are 2 units tall by default — localScale.y
            // is expressed in half-heights already accounted for by callers.
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }
    }
}
