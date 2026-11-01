using Apogee.Design;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Navigation arguments passed when transitioning to the Launch Sequence screen (ScreenId.LaunchSequence).
    /// Carries the validated vehicle blueprint, optional mission context, and launch parameters.
    /// Complies with GDD §10.1, §14.1 #13, and §21 M5-01.
    /// </summary>
    public sealed class LaunchNavigationArgs
    {
        public Blueprint Blueprint { get; }
        public BlueprintStats Stats { get; }
        public string MissionName { get; }
        public string LaunchPadBaseId { get; }
        public double WeatherRisk { get; }
        public double EquatorialBonusDv { get; }
        public bool IsCrewed { get; }
        public ulong LaunchSeed { get; }

        public LaunchNavigationArgs(
            Blueprint blueprint,
            BlueprintStats stats = null,
            string missionName = null,
            string launchPadBaseId = "base_cape_pad",
            double weatherRisk = 0.20,
            double equatorialBonusDv = 0.0,
            bool isCrewed = false,
            ulong launchSeed = 0)
        {
            Blueprint = blueprint;
            Stats = stats;
            MissionName = !string.IsNullOrWhiteSpace(missionName) ? missionName : (blueprint != null ? blueprint.Name : "Misyon");
            LaunchPadBaseId = launchPadBaseId;
            WeatherRisk = weatherRisk;
            EquatorialBonusDv = equatorialBonusDv;
            IsCrewed = isCrewed;
            LaunchSeed = launchSeed;
        }
    }
}
