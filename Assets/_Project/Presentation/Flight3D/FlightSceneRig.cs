using System;
using UnityEngine;
using Apogee.UI.Core.Flight;

// Nested-type aliases — the shared enums/structs live inside FlightStageView (2D stage).
using ScenePhase = Apogee.UI.Core.Flight.FlightStageView.ScenePhase;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// Runtime-built environment rig for the 3D Flight Theatre (3D stage plan, Faz 3).
    /// Four environment groups mirror the 2D scene phases:
    ///   - Pad     : ground plane, concrete pad, launch tower
    ///   - Space   : departure + destination planets, atmosphere shells, rings,
    ///               trajectory arc, arrival capture ring
    ///   - Stars   : static star field particle burst (shown in high ascent,
    ///               always in space, and in airless landing skies)
    ///   - Surface : destination terrain, landing pad, rock props (EDL/Science)
    /// Plus a rig-local directional sun. All colors are driven by the
    /// <see cref="FlightStageView.DestinationProfile"/> (flight_visuals.json).
    /// </summary>
    public sealed class FlightSceneRig
    {
        public const float SurfaceZ = 90f; // shared with FlightVehicleRig landing placement

        public GameObject Root { get; }

        // Environment groups
        private readonly GameObject _padGroup;
        private readonly GameObject _spaceGroup;
        private readonly GameObject _starsGroup;
        private readonly GameObject _surfaceGroup;

        // Space objects
        private readonly GameObject _departurePlanet;
        private readonly GameObject _destinationPlanet;
        private readonly GameObject _destinationAtmosphere;
        private readonly GameObject _destinationRings;
        private readonly LineRenderer _trajectory;
        private readonly LineRenderer _captureRing;
        private readonly Transform    _starsTransform;
        private readonly Transform    _destinationSpin;

        // Surface objects
        private readonly Renderer    _terrainRenderer;
        private readonly Renderer    _landingPadRenderer;
        private readonly Material    _destinationBodyMaterial;
        private readonly Material    _destinationAtmoMaterial;
        private readonly Material    _trajectoryMaterial;
        private readonly Material    _captureMaterial;

        private float _starSpin;
        private bool  _reduceMotion;

        private static readonly System.Random Rng = new System.Random(7);

        // ── Construction ──────────────────────────────────────────────────────────
        public FlightSceneRig(Transform parent)
        {
            var rootGo = new GameObject("Environment");
            Root = rootGo;
            Root.transform.SetParent(parent, false);

            // ── Sun (rig-local directional light; enabled only while the stage lives) ──
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(Root.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type      = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color     = new Color(1.0f, 0.96f, 0.90f);
            sun.shadows   = LightShadows.None; // placeholder perf budget

            // ── Pad group ──
            _padGroup = new GameObject("Pad");
            _padGroup.transform.SetParent(Root.transform, false);
            BuildPad(_padGroup.transform);

            // ── Space group ──
            _spaceGroup = new GameObject("Space");
            _spaceGroup.transform.SetParent(Root.transform, false);

            Material departureMat = Flight3DMaterials.Lit(new Color(0.10f, 0.35f, 0.60f));
            _departurePlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _departurePlanet.name = "DeparturePlanet";
            UnityEngine.Object.Destroy(_departurePlanet.GetComponent<Collider>());
            _departurePlanet.transform.SetParent(_spaceGroup.transform, false);
            _departurePlanet.transform.position = new Vector3(-70f, 8f, -30f);
            _departurePlanet.transform.localScale = Vector3.one * 16f;   // radius 8
            _departurePlanet.GetComponent<Renderer>().sharedMaterial = departureMat;

            _destinationBodyMaterial = Flight3DMaterials.Lit(new Color(0.10f, 0.35f, 0.60f));
            _destinationAtmoMaterial = Flight3DMaterials.Transparent(new Color(0.35f, 0.55f, 0.8f, 0.30f));
            _destinationPlanet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _destinationPlanet.name = "DestinationPlanet";
            UnityEngine.Object.Destroy(_destinationPlanet.GetComponent<Collider>());
            _destinationPlanet.transform.SetParent(_spaceGroup.transform, false);
            _destinationPlanet.transform.position = new Vector3(70f, 10f, -35f);
            _destinationPlanet.transform.localScale = Vector3.one * 18f; // radius 9 (profile-driven)
            _destinationPlanet.GetComponent<Renderer>().sharedMaterial = _destinationBodyMaterial;
            _destinationSpin = _destinationPlanet.transform;

            _destinationAtmosphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _destinationAtmosphere.name = "DestinationAtmosphere";
            UnityEngine.Object.Destroy(_destinationAtmosphere.GetComponent<Collider>());
            _destinationAtmosphere.transform.SetParent(_spaceGroup.transform, false);
            _destinationAtmosphere.transform.position = _destinationPlanet.transform.position;
            _destinationAtmosphere.transform.localScale = Vector3.one * 20f;
            _destinationAtmosphere.GetComponent<Renderer>().sharedMaterial = _destinationAtmoMaterial;

            _destinationRings = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _destinationRings.name = "DestinationRings";
            UnityEngine.Object.Destroy(_destinationRings.GetComponent<Collider>());
            _destinationRings.transform.SetParent(_spaceGroup.transform, false);
            _destinationRings.transform.position = _destinationPlanet.transform.position;
            _destinationRings.transform.localScale = new Vector3(44f, 0.05f, 44f); // flat disk
            _destinationRings.transform.rotation = Quaternion.Euler(12f, 0f, 8f);
            _destinationRings.GetComponent<Renderer>().sharedMaterial =
                Flight3DMaterials.Transparent(new Color(0.8f, 0.75f, 0.5f, 0.35f));
            _destinationRings.SetActive(false);

            // Trajectory arc (bezier, world space)
            _trajectoryMaterial = Flight3DMaterials.Line();
            _trajectory = new GameObject("Trajectory").AddComponent<LineRenderer>();
            _trajectory.transform.SetParent(_spaceGroup.transform, false);
            _trajectory.useWorldSpace = true;
            _trajectory.positionCount = 40;
            _trajectory.startWidth = 0.12f;
            _trajectory.endWidth   = 0.12f;
            _trajectory.material   = _trajectoryMaterial;
            _trajectory.startColor = new Color(0.024f, 0.714f, 0.831f, 0.65f);
            _trajectory.endColor   = new Color(0.024f, 0.714f, 0.831f, 0.65f);
            Vector3 p0 = new Vector3(-45f, 10f, -12f);
            Vector3 p1 = new Vector3(  0f, 32f,   6f);
            Vector3 p2 = new Vector3( 45f, 12f, -18f);
            for (int i = 0; i < 40; i++)
            {
                float t = i / 39f;
                float u = 1f - t;
                _trajectory.SetPosition(i, u * u * p0 + 2f * u * t * p1 + t * t * p2);
            }

            // Arrival capture ring (dashed-look via alpha pulse in Tick)
            _captureMaterial = Flight3DMaterials.Line();
            _captureRing = new GameObject("CaptureRing").AddComponent<LineRenderer>();
            _captureRing.transform.SetParent(_spaceGroup.transform, false);
            _captureRing.useWorldSpace = true;
            _captureRing.loop = true;
            _captureRing.positionCount = 48;
            _captureRing.startWidth = 0.25f;
            _captureRing.endWidth   = 0.25f;
            _captureRing.material   = _captureMaterial;
            _captureRing.startColor = new Color(0.024f, 0.714f, 0.831f, 0.8f);
            _captureRing.endColor   = new Color(0.024f, 0.714f, 0.831f, 0.8f);
            const float ringRadius = 15f;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                _captureRing.SetPosition(i, _destinationPlanet.transform.position +
                    new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius));
            }
            _captureRing.gameObject.SetActive(false);

            // ── Stars group (independent of Space group — reused in Ascent/EDL skies) ──
            _starsGroup = new GameObject("Stars");
            _starsGroup.transform.SetParent(Root.transform, false);
            _starsTransform = _starsGroup.transform;
            BuildStars(_starsGroup.transform);
            _starsGroup.SetActive(false);

            // ── Surface group (EDL / Science) ──
            _surfaceGroup = new GameObject("Surface");
            _surfaceGroup.transform.SetParent(Root.transform, false);
            _surfaceGroup.transform.position = new Vector3(0f, 0f, SurfaceZ);
            Material terrainMat = Flight3DMaterials.Lit(new Color(0.04f, 0.10f, 0.18f));
            _landingPadRenderer = null;
            var terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "Terrain";
            UnityEngine.Object.Destroy(terrain.GetComponent<Collider>());
            terrain.transform.SetParent(_surfaceGroup.transform, false);
            terrain.transform.localScale = new Vector3(8f, 1f, 8f); // Plane is 10x10 → 80x80
            _terrainRenderer = terrain.GetComponent<Renderer>();
            _terrainRenderer.sharedMaterial = terrainMat;

            // Landing pad disk
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "LandingPad";
            UnityEngine.Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(_surfaceGroup.transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pad.transform.localScale = new Vector3(6f, 0.05f, 6f);
            _landingPadRenderer = pad.GetComponent<Renderer>();
            _landingPadRenderer.sharedMaterial = Flight3DMaterials.Lit(new Color(0.16f, 0.30f, 0.42f));

            // Deterministic rock props
            Material rockMat = Flight3DMaterials.Lit(new Color(0.13f, 0.20f, 0.28f));
            for (int i = 0; i < 14; i++)
            {
                float x = (float)Rng.NextDouble() * 60f - 30f;
                float z = (float)Rng.NextDouble() * 60f - 30f;
                if (Mathf.Abs(x) < 5f && Mathf.Abs(z) < 5f) continue; // keep the pad clear
                float s = 0.3f + (float)Rng.NextDouble() * 1.4f;
                var rock = GameObject.CreatePrimitive(Rng.NextDouble() > 0.5 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                rock.name = $"Rock{i}";
                UnityEngine.Object.Destroy(rock.GetComponent<Collider>());
                rock.transform.SetParent(_surfaceGroup.transform, false);
                rock.transform.localPosition = new Vector3(x, s * 0.35f, z);
                rock.transform.localRotation = Quaternion.Euler(
                    (float)Rng.NextDouble() * 40f, (float)Rng.NextDouble() * 360f, (float)Rng.NextDouble() * 40f);
                rock.transform.localScale = new Vector3(s, s * 0.7f, s * (0.7f + (float)Rng.NextDouble() * 0.6f));
                rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            }

            _surfaceGroup.SetActive(false);
            _spaceGroup.SetActive(false);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Toggle environment groups for the new phase.</summary>
        public void SetPhase(ScenePhase phase, bool starsVisible)
        {
            _padGroup.SetActive(phase == ScenePhase.Pad || phase == ScenePhase.Ascent);
            _spaceGroup.SetActive(phase == ScenePhase.Space);
            _surfaceGroup.SetActive(phase == ScenePhase.Landing);
            _starsGroup.SetActive(starsVisible);
        }

        /// <summary>Apply the destination visual profile (flight_visuals.json → 3D materials).</summary>
        public void ApplyDestinationProfile(FlightStageView.DestinationProfile profile)
        {
            _destinationBodyMaterial.color = profile.BodyColor;
            _destinationAtmoMaterial.color = new Color(
                profile.AtmosphereColor.r, profile.AtmosphereColor.g,
                profile.AtmosphereColor.b, Mathf.Max(0.12f, profile.AtmosphereColor.a * 0.5f));
            _destinationAtmosphere.SetActive(profile.HasAtmosphere || profile.AtmosphereColor.a > 0.01f);
            _destinationPlanet.transform.localScale = Vector3.one * (18f * Mathf.Clamp(profile.BodyScale, 0.2f, 1.4f));
            _destinationRings.SetActive(profile.HasRings);

            Color ring = profile.OrbitRingColor;
            _trajectoryMaterial.color = new Color(ring.r, ring.g, ring.b, 0.6f);
            _captureMaterial.color    = new Color(ring.r, ring.g, ring.b, 0.85f);

            // Surface tint: body colour darkened + landing pad accent
            _terrainRenderer.sharedMaterial.color = Color.Lerp(profile.BodyColor, Color.black, 0.6f);
            _landingPadRenderer.sharedMaterial.color = Color.Lerp(profile.BodyColor, Color.white, 0.25f);
        }

        /// <summary>Camera clear colour for the current phase / altitude context.</summary>
        public Color ResolveBackground(ScenePhase phase, float altProgress, FlightStageView.DestinationProfile profile)
        {
            switch (phase)
            {
                case ScenePhase.Pad:
                    return new Color(0.09f, 0.14f, 0.25f);
                case ScenePhase.Ascent:
                    return Color.Lerp(new Color(0.09f, 0.14f, 0.25f), new Color(0.008f, 0.010f, 0.020f), altProgress);
                case ScenePhase.Space:
                    return new Color(0.010f, 0.010f, 0.030f);
                case ScenePhase.Landing:
                    return profile.HasAtmosphere
                        ? Color.Lerp(profile.AtmosphereColor * 0.7f, new Color(0.02f, 0.03f, 0.06f), 0.35f)
                        : new Color(0.02f, 0.03f, 0.06f);
                default:
                    return new Color(0.05f, 0.07f, 0.12f);
            }
        }

        /// <summary>Slow ambient motion (star spin, planet rotation, capture pulse).</summary>
        public void Tick(float dt)
        {
            if (_reduceMotion) return;

            _starSpin += dt * 0.6f;
            _starsTransform.rotation = Quaternion.Euler(0f, _starSpin, 0f);

            if (_destinationSpin != null)
                _destinationSpin.Rotate(Vector3.up, dt * 1.2f, Space.World);

            if (_captureRing.gameObject.activeSelf)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
                _captureMaterial.color = new Color(
                    _captureMaterial.color.r, _captureMaterial.color.g, _captureMaterial.color.b, pulse);
            }
        }

        public void SetReduceMotion(bool reduce) => _reduceMotion = reduce;

        /// <summary>Destroy every GameObject owned by this rig.</summary>
        public void Teardown()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root);
        }

        // ── Builders ──────────────────────────────────────────────────────────────

        private static void BuildPad(Transform parent)
        {
            Material groundMat = Flight3DMaterials.Lit(new Color(0.10f, 0.12f, 0.16f));
            Material padMat    = Flight3DMaterials.Lit(new Color(0.18f, 0.20f, 0.24f));
            Material towerMat  = Flight3DMaterials.Lit(new Color(0.12f, 0.17f, 0.24f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            UnityEngine.Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "ConcretePad";
            UnityEngine.Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(parent, false);
            pad.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            pad.transform.localScale = new Vector3(5f, 0.2f, 5f);
            pad.GetComponent<Renderer>().sharedMaterial = padMat;

            // Launch tower: mast + cross arms (mirrors the 2D silhouette)
            var tower = new GameObject("Tower");
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = new Vector3(2.6f, 0f, -1.4f);
            CreateBox(tower.transform, "Mast", new Vector3(0f, 4.5f, 0f), new Vector3(0.5f, 9f, 0.5f), towerMat);
            for (int i = 1; i <= 4; i++)
            {
                CreateBox(tower.transform, $"Arm{i}",
                    new Vector3(-1.1f, 1.4f + i * 1.7f, 0f),
                    new Vector3(1.8f, 0.14f, 0.3f), towerMat);
            }
            // Lightning mast tip
            CreateBox(tower.transform, "Tip", new Vector3(0f, 9.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), towerMat);
        }

        private static void BuildStars(Transform parent)
        {
            var go = new GameObject("StarField");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop             = false;
            main.playOnAwake      = false;
            main.startLifetime    = 1.0e6f;   // effectively static
            main.startSpeed       = 0f;
            main.startSize        = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startColor       = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.95f, 1.0f));
            main.gravityModifier  = 0f;
            main.simulationSpace  = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime  = new ParticleSystem.MinMaxCurve(0f);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 900) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 180f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = Flight3DMaterials.Particle();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        private static void CreateBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
