namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Sequential milestones evaluated during the ascent and orbital insertion of a rocket.
    /// Complies with GDD §10.1 and §14.1 (#13 Fırlatma Sekansı).
    /// </summary>
    public enum LaunchPhaseType
    {
        Ignition = 0,
        Liftoff = 1,
        MaxQ = 2,
        Stage1Burn = 3,
        Stage1Separation = 4,
        Stage2Ignition = 5,
        Stage2Burn = 6,
        FairingJettison = 7,
        Stage2Separation = 8,
        Stage3Ignition = 9,
        Stage3Burn = 10,
        OrbitalInsertion = 11
    }
}
