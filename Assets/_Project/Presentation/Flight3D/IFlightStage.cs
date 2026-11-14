using UnityEngine;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Vehicle;

namespace Apogee.UI.Flight3D
{
    /// <summary>
    /// Shared presentation contract for the Flight Theatre stage renderers.
    /// Implemented by <see cref="FlightStageView"/> (Painter2D, 2D fallback) and
    /// <see cref="FlightStageView3D"/> (URP RenderTexture stage) so the
    /// LaunchSequenceScreenController can swap renderers without touching the
    /// beat-driven orchestration (FlightTimeline → FlightBeat → PlaybackClock).
    /// (GDD §3.4, §15.1 — 3D stage plan, Faz 0)
    /// </summary>
    public interface IFlightStage
    {
        /// <summary>Apply a new beat — updates scene phase, environment, vehicle, camera and FX.</summary>
        void ApplyBeat(FlightBeat beat);

        /// <summary>Set the payload silhouette type (Rover, Lander, Orbiter).</summary>
        void SetPayloadType(VehicleSilhouetteView.PayloadType type);

        /// <summary>Set the destination visual profile (from flight_visuals.json).</summary>
        void SetDestinationProfile(FlightStageView.DestinationProfile profile);

        /// <summary>Toggle ReduceMotion at runtime (Ayarlar binding).</summary>
        void SetReduceMotion(bool reduce);

        /// <summary>Attach the shared FlightPerformanceBudget (ADR-061 WebGL audit).</summary>
        void SetPerformanceBudget(FlightPerformanceBudget budget);

        /// <summary>Zoom in/out on top of the current choreography preset (player control).</summary>
        void AdjustCameraZoom(float delta);

        /// <summary>Reset the player zoom layer back to the choreographed default.</summary>
        void ResetCameraZoom();

        /// <summary>Current player zoom multiplier (1.0 = choreographed default).</summary>
        float CameraZoom { get; }

        /// <summary>Player drag-pan: offsets the camera by delta pixels (PointerMove deltas).</summary>
        void PanCameraBy(Vector2 delta);

        /// <summary>Snap the camera instantly to its current targets (sequence handoff).</summary>
        void SnapCamera();

        /// <summary>Start the per-frame animation schedule (call after AddToHierarchy).</summary>
        void StartAnimation();

        /// <summary>Stop the animation schedule and release renderer resources (screen hidden).</summary>
        void StopAnimation();
    }
}
