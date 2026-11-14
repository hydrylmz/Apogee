using UnityEngine;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// Runtime-built effect rig for the 3D Flight Theatre (3D stage plan, Faz 4).
    /// Maps the <see cref="FlightBeat.VisualEffectKey"/> strings from
    /// flight_choreography.json to 3D FX:
    ///
    ///   Continuous (toggled):  exhaust flame, retro plume, pad steam,
    ///                          EDL heat glow, parachute, Max-Q vapour cone,
    ///                          science scan beam, downlink rings
    ///   One-shot bursts:       stage separation puffs, touchdown dust,
    ///                          abort/explosion debris
    ///
    /// Colour constants match the 2D stage (FlightStageView) for visual continuity.
    /// All particle systems are pooled once at construction — zero per-beat allocation.
    /// </summary>
    public sealed class FlightFxRig
    {
        // ── Colour constants (match FlightStageView) ──────────────────────────────
        private static readonly Color ExhaustColor = new Color(0.961f, 0.620f, 0.043f); // amber
        private static readonly Color ExhaustCore  = new Color(1.00f, 0.85f, 0.45f);
        private static readonly Color RetroColor   = new Color(0.90f, 0.95f, 1.00f);
        private static readonly Color HeatGlowColor= new Color(0.961f, 0.620f, 0.043f);
        private static readonly Color ChuteColor   = new Color(0.92f, 0.35f, 0.08f);   // Apollo orange
        private static readonly Color ScanColor    = new Color(0.024f, 0.95f, 0.85f, 0.75f);
        private static readonly Color DownlinkColor= new Color(0.20f, 0.75f, 1.00f, 0.45f);
        private static readonly Color DustColor    = new Color(0.55f, 0.50f, 0.42f);

        // ── Continuous systems ────────────────────────────────────────────────────
        private readonly ParticleSystem _exhaust;      // main engine flame (re-anchored per stage)
        private readonly ParticleSystem _retro;        // EDL retro plume
        private readonly ParticleSystem _steam;        // pad steam / liftoff clouds
        private readonly GameObject     _heatGlow;     // translucent plasma shell
        private readonly GameObject     _chute;        // parachute canopy + cords
        private readonly GameObject     _vapourCone;   // Max-Q condensation cone (placeholder)
        private readonly LineRenderer   _scanBeam;     // science sensor beam
        private readonly LineRenderer   _downlinkRings;// expanding radio rings (3)
        private readonly ParticleSystem _burst;        // shared one-shot burst emitter
        private readonly ParticleSystem _dustBurst;    // touchdown dust

        private Transform _exhaustTransform;
        private Transform _retroTransform;
        private Vector3   _steamPosition = Vector3.zero;
        private Material  _scanMaterial;
        private Material  _downlinkMaterial;
        private float     _downlinkPhase;
        private bool      _downlinkActive;
        private bool      _reduceMotion;
        private bool      _exhaustActive;
        private bool      _retroActive;
        private bool      _steamActive;

        public FlightFxRig(Transform parent)
        {
            var root = new GameObject("FX");
            root.transform.SetParent(parent, false);

            // ── Exhaust flame (cone-shaped, downward) ──
            _exhaust = BuildConeSystem("Exhaust", ExhaustColor, ExhaustCore,
                lifetime: 0.45f, speed: 9f, size: 1.1f, rate: 90f, spreadAngle: 14f);
            _exhaustTransform = _exhaust.transform;
            _exhaust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // ── Retro plume (white, smaller) ──
            _retro = BuildConeSystem("Retro", RetroColor, Color.white,
                lifetime: 0.3f, speed: 7f, size: 0.5f, rate: 60f, spreadAngle: 20f);
            _retroTransform = _retro.transform;
            _retro.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // ── Pad steam (billowing ground clouds) ──
            _steam = BuildConeSystem("Steam", new Color(0.85f, 0.88f, 0.92f, 0.7f), Color.white,
                lifetime: 2.2f, speed: 5f, size: 2.6f, rate: 40f, spreadAngle: 55f);
            _steam.transform.rotation = Quaternion.Euler(180f, 0f, 0f);   // billow upward
            _steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // ── Shared one-shot burst emitter ──
            _burst = BuildConeSystem("Burst", Color.white, Color.white,
                lifetime: 1.1f, speed: 6f, size: 0.5f, rate: 0f, spreadAngle: 75f);
            _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // ── Touchdown dust burst ──
            _dustBurst = BuildConeSystem("DustBurst", DustColor, DustColor,
                lifetime: 1.6f, speed: 4f, size: 1.2f, rate: 0f, spreadAngle: 85f);
            _dustBurst.transform.rotation = Quaternion.Euler(180f, 0f, 0f); // kick upward/outward
            _dustBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // ── Heat glow shell (EDL plasma) ──
            _heatGlow = new GameObject("HeatGlow");
            var heatMesh = _heatGlow.AddComponent<MeshFilter>();
            heatMesh.sharedMesh = PrimitiveSphereMesh();
            var heatRenderer = _heatGlow.AddComponent<MeshRenderer>();
            heatRenderer.sharedMaterial = Flight3DMaterials.Transparent(
                new Color(HeatGlowColor.r, HeatGlowColor.g, HeatGlowColor.b, 0.55f));
            _heatGlow.transform.localScale = Vector3.one * 2.4f;
            _heatGlow.SetActive(false);

            // ── Parachute (canopy + 4 cords, local to payload anchor) ──
            _chute = new GameObject("Chute");
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(canopy.GetComponent<Collider>());
            canopy.name = "Canopy";
            canopy.transform.SetParent(_chute.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            canopy.transform.localScale = new Vector3(3.2f, 1.4f, 3.2f);
            canopy.GetComponent<Renderer>().sharedMaterial = Flight3DMaterials.Transparent(
                new Color(ChuteColor.r, ChuteColor.g, ChuteColor.b, 0.85f));
            var cordMat = Flight3DMaterials.Line();
            var cords = new GameObject("Cords").AddComponent<LineRenderer>();
            cords.transform.SetParent(_chute.transform, false);
            cords.useWorldSpace = false;
            cords.positionCount = 8;
            cords.startWidth = 0.04f;
            cords.endWidth   = 0.04f;
            cords.material   = cordMat;
            cords.startColor = new Color(0.85f, 0.90f, 0.95f, 0.8f);
            cords.endColor   = new Color(0.85f, 0.90f, 0.95f, 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                float x = Mathf.Cos(a) * 1.4f;
                float z = Mathf.Sin(a) * 1.4f;
                cords.SetPosition(i * 2,     new Vector3(x, 2.4f, z));
                cords.SetPosition(i * 2 + 1, new Vector3(0f, 0.4f, 0f));
            }
            _chute.SetActive(false);

            // ── Max-Q vapour cone (translucent shell ahead of the nose) ──
            _vapourCone = new GameObject("VapourCone");
            var coneMesh = _vapourCone.AddComponent<MeshFilter>();
            coneMesh.sharedMesh = PrimitiveSphereMesh();
            var coneRenderer = _vapourCone.AddComponent<MeshRenderer>();
            coneRenderer.sharedMaterial = Flight3DMaterials.Transparent(new Color(1f, 1f, 1f, 0.22f));
            _vapourCone.transform.localScale = new Vector3(1.6f, 3.4f, 1.6f);
            _vapourCone.SetActive(false);

            // ── Science scan beam ──
            _scanMaterial = Flight3DMaterials.Line();
            _scanBeam = new GameObject("ScanBeam").AddComponent<LineRenderer>();
            _scanBeam.transform.SetParent(root.transform, false);
            _scanBeam.useWorldSpace = true;
            _scanBeam.positionCount = 2;
            _scanBeam.startWidth = 0.08f;
            _scanBeam.endWidth   = 0.35f;
            _scanBeam.material   = _scanMaterial;
            _scanBeam.startColor = ScanColor;
            _scanBeam.endColor   = new Color(ScanColor.r, ScanColor.g, ScanColor.b, 0.15f);
            _scanBeam.gameObject.SetActive(false);

            // ── Downlink rings (3 expanding circles) ──
            _downlinkMaterial = Flight3DMaterials.Line();
            _downlinkRings = new GameObject("DownlinkRings").AddComponent<LineRenderer>();
            _downlinkRings.transform.SetParent(root.transform, false);
            _downlinkRings.useWorldSpace = true;
            _downlinkRings.loop = true;
            _downlinkRings.positionCount = 32;
            _downlinkRings.startWidth = 0.06f;
            _downlinkRings.endWidth   = 0.06f;
            _downlinkRings.material   = _downlinkMaterial;
            _downlinkRings.gameObject.SetActive(false);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Attach/toggle the main engine flame at the given exhaust anchor.</summary>
        public void SetExhaust(bool on, Transform anchor)
        {
            _exhaustActive = on;
            if (on && anchor != null)
            {
                if (_exhaustTransform.parent != anchor)
                    _exhaustTransform.SetParent(anchor, false);
                _exhaustTransform.localPosition = Vector3.zero;
                if (!_exhaust.isEmitting) _exhaust.Play();
            }
            else
            {
                _exhaust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>Attach/toggle the EDL retro plume at the payload base.</summary>
        public void SetRetro(bool on, Transform anchor)
        {
            _retroActive = on;
            if (on && anchor != null)
            {
                if (_retroTransform.parent != anchor)
                    _retroTransform.SetParent(anchor, false);
                _retroTransform.localPosition = Vector3.zero;
                if (!_retro.isEmitting) _retro.Play();
            }
            else
            {
                _retro.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>Toggle pad steam at a world position (ignition / liftoff).</summary>
        public void SetSteam(bool on, Vector3 worldPos)
        {
            _steamActive = on;
            if (on)
            {
                _steamPosition = worldPos;
                _steam.transform.position = worldPos;
                if (!_steam.isEmitting) _steam.Play();
            }
            else
            {
                _steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>One-shot debris/smoke burst at a world position (staging, fairing, explosion).</summary>
        public void Burst(Vector3 pos, int count, Color color, float size = 0.5f, float speed = 6f)
        {
            if (_reduceMotion) return;
            _burst.transform.position = pos;
            var main = _burst.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            main.startSize  = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.3f);
            _burst.Play();
            _burst.Emit(count);
        }

        /// <summary>One-shot touchdown dust at a world position.</summary>
        public void TouchdownDust(Vector3 pos, int count = 40)
        {
            if (_reduceMotion) return;
            _dustBurst.transform.position = pos;
            _dustBurst.Play();
            _dustBurst.Emit(count);
        }

        /// <summary>Toggle the EDL plasma heat glow at a world-space point; intensity from heat shield integrity.</summary>
        public void SetHeatGlow(bool on, Vector3 localOffset, Transform attach, float intensity)
        {
            _heatGlow.SetActive(on);
            if (on && attach != null)
            {
                _heatGlow.transform.SetParent(attach, false);
                _heatGlow.transform.localPosition = localOffset;
                float a = Mathf.Clamp01(intensity <= 0.01f ? 0.85f : intensity) * 0.55f + 0.15f;
                _heatGlow.GetComponent<Renderer>().sharedMaterial.color =
                    new Color(HeatGlowColor.r, HeatGlowColor.g, HeatGlowColor.b, a);
            }
        }

        /// <summary>Toggle the parachute at a local anchor (payload space).</summary>
        public void SetChute(bool on, Transform attach)
        {
            _chute.SetActive(on);
            if (on && attach != null)
            {
                _chute.transform.SetParent(attach, false);
                _chute.transform.localPosition = Vector3.zero;
                _chute.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Toggle the Max-Q vapour cone at the nose anchor.</summary>
        public void SetVapourCone(bool on, Transform noseAnchor)
        {
            _vapourCone.SetActive(on);
            if (on && noseAnchor != null)
            {
                _vapourCone.transform.SetParent(noseAnchor, false);
                _vapourCone.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                _vapourCone.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Toggle the science scan beam (world-space line rover → surface).</summary>
        public void SetScanBeam(bool on, Vector3 from, Vector3 to)
        {
            _scanBeam.gameObject.SetActive(on);
            if (on)
            {
                _scanBeam.SetPosition(0, from);
                _scanBeam.SetPosition(1, to);
            }
        }

        /// <summary>Toggle expanding downlink radio rings at a world position.</summary>
        public void SetDownlink(bool on, Vector3 origin)
        {
            _downlinkActive = on;
            _downlinkRings.gameObject.SetActive(on);
            _downlinkRings.transform.position = origin;
            _downlinkPhase = 0f;
        }

        /// <summary>Reset all transient (beat-driven) toggles — called before each ApplyBeat.</summary>
        public void ResetTransient()
        {
            SetSteam(false, Vector3.zero);
            SetHeatGlow(false, Vector3.zero, null, 0f);
            SetChute(false, null);
            SetVapourCone(false, null);
            SetScanBeam(false, Vector3.zero, Vector3.zero);
            SetDownlink(false, Vector3.zero);
        }

        /// <summary>Stop everything (ReduceMotion / screen hidden).</summary>
        public void StopAll()
        {
            SetExhaust(false, null);
            SetRetro(false, null);
            ResetTransient();
        }

        /// <summary>Advance downlink ring animation; also keeps steam following the pad.</summary>
        public void Tick(float dt)
        {
            if (_reduceMotion) return;

            if (_downlinkActive)
            {
                _downlinkPhase += dt * 0.8f;
                const float maxRadius = 4f;
                // Single ring looping 0 → maxRadius with fading alpha
                float t = _downlinkPhase % 1f;
                float r = 0.3f + t * maxRadius;
                for (int i = 0; i < 32; i++)
                {
                    float a = i / 32f * Mathf.PI * 2f;
                    _downlinkRings.SetPosition(i, new Vector3(Mathf.Cos(a) * r, t * 0.8f, Mathf.Sin(a) * r));
                }
                _downlinkMaterial.color = new Color(
                    DownlinkColor.r, DownlinkColor.g, DownlinkColor.b, (1f - t) * 0.8f);
            }
        }

        public void SetReduceMotion(bool reduce)
        {
            _reduceMotion = reduce;
            if (reduce) StopAll();
        }

        // ── Builder ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Cone-shaped particle system emitting along -Y (rocket exhaust orientation),
        /// with an additive-looking bright core via color over lifetime.
        /// </summary>
        private static ParticleSystem BuildConeSystem(
            string name, Color color, Color coreColor,
            float lifetime, float speed, float size, float rate, float spreadAngle)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.3f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed * 1.2f);
            main.startSize       = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.startColor      = new ParticleSystem.MinMaxGradient(color);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 600;

            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = spreadAngle;
            shape.radius    = 0.25f;
            shape.rotation  = new Vector3(90f, 0f, 0f);   // +Z → -Y (pointing down from nozzle)

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(coreColor, 0f),
                    new GradientColorKey(color,     0.35f),
                    new GradientColorKey(color,     1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.9f,  0f),
                    new GradientAlphaKey(0.7f,  0.4f),
                    new GradientAlphaKey(0f,    1f),
                }
            });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material        = Flight3DMaterials.Particle();
            renderer.renderMode      = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge    = -10f;

            return ps;
        }

        /// <summary>Unit sphere mesh (built from a temporary primitive; the mesh is shared).</summary>
        private static Mesh PrimitiveSphereMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(temp);
            return mesh;
        }
    }
}
