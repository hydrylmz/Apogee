using Apogee.Design;

namespace Apogee.UI.Screens.Operations
{
    /// <summary>
    /// Arguments passed when navigating to the Operations screen.
    /// </summary>
    public sealed class OperationsNavigationArgs
    {
        public string MissionId { get; }
        public string MissionName { get; }
        public string DestinationId { get; }
        public Blueprint Blueprint { get; }
        public int CruiseDurationDays { get; }
        public double SpacecraftReliability { get; }
        public ulong MissionSeed { get; }
        public bool FromLaunchHandover { get; }

        public OperationsNavigationArgs(
            string missionId,
            string missionName,
            string destinationId,
            Blueprint blueprint,
            int cruiseDurationDays = 30,
            double spacecraftReliability = 0.95,
            ulong missionSeed = 0,
            bool fromLaunchHandover = false)
        {
            MissionId = missionId ?? "mission_active";
            MissionName = missionName ?? "Aktif Misyon";
            DestinationId = destinationId ?? "moon";
            Blueprint = blueprint;
            CruiseDurationDays = cruiseDurationDays;
            SpacecraftReliability = spacecraftReliability;
            MissionSeed = missionSeed;
            FromLaunchHandover = fromLaunchHandover;
        }
    }
}
