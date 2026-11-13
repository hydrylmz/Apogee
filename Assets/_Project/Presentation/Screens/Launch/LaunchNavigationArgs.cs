using Apogee.Design;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Navigation arguments passed when transitioning to the Launch Sequence screen (ScreenId.LaunchSequence).
    /// Carries the validated vehicle blueprint, optional mission context, and launch parameters.
    /// Complies with GDD §10.1, §14.1 #13, and §21 M5-01.
    ///
    /// ADR-030 additive change: DestinationId added as optional parameter.
    /// All existing callers remain valid — DestinationId defaults to null and
    /// the screen falls back to MissionName.ToLowerInvariant() for backward compatibility.
    /// </summary>
    public sealed class LaunchNavigationArgs
    {
        /// <summary>Fallback seed used when no explicit LaunchSeed is provided. Named constant per STANDARDS §4.</summary>
        public const ulong DefaultFallbackSeed = 1_234_567UL;

        public Blueprint Blueprint { get; }
        public BlueprintStats Stats { get; }
        public string MissionName { get; }
        public string LaunchPadBaseId { get; }
        public double WeatherRisk { get; }
        public double EquatorialBonusDv { get; }
        public bool IsCrewed { get; }
        public ulong LaunchSeed { get; }

        /// <summary>
        /// Explicit destination catalog ID (e.g. "moon", "mars").
        /// When null, the screen falls back to MissionName.ToLowerInvariant() for
        /// backward compatibility with callers that have not yet been updated.
        /// </summary>
        public string DestinationId { get; }

        public LaunchNavigationArgs(
            Blueprint blueprint,
            BlueprintStats stats = null,
            string missionName = null,
            string launchPadBaseId = "base_cape_pad",
            double weatherRisk = 0.20,
            double equatorialBonusDv = 0.0,
            bool isCrewed = false,
            ulong launchSeed = 0,
            string destinationId = null)
        {
            Blueprint        = blueprint;
            Stats            = stats;
            MissionName      = !string.IsNullOrWhiteSpace(missionName) ? missionName : (blueprint != null ? blueprint.Name : "Misyon");
            LaunchPadBaseId  = launchPadBaseId;
            WeatherRisk      = weatherRisk;
            EquatorialBonusDv= equatorialBonusDv;
            IsCrewed         = isCrewed;
            LaunchSeed       = launchSeed;
            DestinationId    = destinationId;
        }
    }
}
