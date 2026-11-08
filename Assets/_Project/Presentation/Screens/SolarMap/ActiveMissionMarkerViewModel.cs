using System;
using Apogee.Core.Simulation.Cruise;
using Apogee.Core.Simulation.Report;
using Apogee.Strategy.Mission;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// ViewModel representing an in-flight mission spacecraft marker on the Solar System Map (GDD §6.2, §14.1 #4, M9-06).
    /// Interpolates orbital transfer positions between Earth and target destination based on CruiseProgress.
    /// </summary>
    public sealed class ActiveMissionMarkerViewModel
    {
        public string MissionId { get; }
        public string MissionName { get; }
        public string DestinationId { get; }
        public MissionStatus Status { get; }
        public double CruiseProgress { get; }
        public string ProgressPercent => $"%{CruiseProgress * 100:F0}";
        public int DaysRemaining { get; }
        public float NormalizedX { get; private set; }
        public float NormalizedY { get; private set; }
        public MissionInstance Instance { get; }

        public ActiveMissionMarkerViewModel(
            MissionInstance mission,
            CelestialBodyItemViewModel earthBody,
            CelestialBodyItemViewModel targetBody)
        {
            Instance = mission ?? throw new ArgumentNullException(nameof(mission));
            MissionId = mission.MissionId;
            MissionName = !string.IsNullOrWhiteSpace(mission.MissionName) ? mission.MissionName : mission.MissionId;
            DestinationId = mission.DestinationId;
            Status = mission.Status;
            CruiseProgress = Math.Clamp(mission.CruiseProgress, 0.0, 1.0);

            int totalDays = mission.CruiseDurationDays > 0 ? mission.CruiseDurationDays : 100;
            DaysRemaining = (int)Math.Max(0, Math.Round(totalDays * (1.0 - CruiseProgress)));

            UpdateCoordinates(earthBody, targetBody);
        }

        public void UpdateCoordinates(CelestialBodyItemViewModel earthBody, CelestialBodyItemViewModel targetBody)
        {
            float startX = earthBody?.NormalizedX ?? 50f;
            float startY = earthBody?.NormalizedY ?? 82f;

            float targetX = targetBody?.NormalizedX ?? 50f;
            float targetY = targetBody?.NormalizedY ?? 50f;

            float t = (float)CruiseProgress;
            NormalizedX = startX + (targetX - startX) * t;
            NormalizedY = startY + (targetY - startY) * t;
        }
    }
}
