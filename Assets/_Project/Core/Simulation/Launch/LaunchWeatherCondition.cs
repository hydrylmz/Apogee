namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Represents the weather conditions at the launch pad during the scheduled launch window.
    /// Complies with GDD §6.1, §9.4, §10.1, and §19.9.
    /// </summary>
    public enum LaunchWeatherCondition
    {
        Clear = 0,
        Windy = 1,
        Stormy = 2
    }
}
