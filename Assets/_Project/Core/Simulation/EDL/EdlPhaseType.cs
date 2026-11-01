namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Sequential milestones in an Entry, Descent, and Landing (EDL) phase.
    /// GDD §8.1, §10.1, §10.4.
    /// </summary>
    public enum EdlPhaseType
    {
        /// <summary>Deorbit burn or transfer departure trajectory burn.</summary>
        DeorbitBurn,

        /// <summary>Atmospheric entry interface and peak aerodynamic deceleration.</summary>
        AtmosphericEntry,

        /// <summary>Peak heating zone where thermal protection is tested.</summary>
        PeakHeating,

        /// <summary>Parachute deployment / aerodynamic deceleration phase.</summary>
        ParachuteDeploy,

        /// <summary>Terminal powered descent / skycrane or retro-rocket braking.</summary>
        TerminalDescent,

        /// <summary>Touchdown / landing gear contact or airbag impact damping.</summary>
        Touchdown
    }
}
