namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Outcome classification for an individual launch sequence phase.
    /// </summary>
    public enum LaunchPhaseStatus
    {
        Nominal = 0,
        Degraded = 1,
        CriticalFailure = 2,
        Aborted = 3
    }
}
