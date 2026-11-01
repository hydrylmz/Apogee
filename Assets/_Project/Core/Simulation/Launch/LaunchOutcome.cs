namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Overall result outcome of a rocket launch attempt.
    /// Complies with GDD §10.1, §10.4, and §10.5.
    /// </summary>
    public enum LaunchOutcome
    {
        Success = 0,
        PartialOrbit = 1,
        CatastrophicFailure = 2,
        PadAbort = 3,
        InFlightAbort = 4,
        WeatherScrub = 5
    }
}
