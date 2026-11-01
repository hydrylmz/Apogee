namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Overall outcome of an EDL sequence execution.
    /// Complies with GDD §8.1, §10.1, §10.4, and §21 M5-03.
    /// </summary>
    public enum EdlOutcome
    {
        /// <summary>Spacecraft performed a textbook arrival/landing and touched down or entered orbit safely.</summary>
        Success,

        /// <summary>Spacecraft touched down with non-critical subsystem damage or harsh impact.</summary>
        PartialSuccess,

        /// <summary>Spacecraft burned up during atmospheric entry due to lack of / failure of thermal protection.</summary>
        ThermalDestruction,

        /// <summary>Spacecraft crashed into the surface at terminal velocity due to parachute/propulsion failure.</summary>
        HighSpeedImpact,

        /// <summary>Spacecraft exhausted landing propellant or suffered engine shutdown during powered descent.</summary>
        PropulsionLoss,

        /// <summary>Spacecraft drifted off trajectory or missed the landing ellipse in an unrecoverable manner.</summary>
        TrajectoryLoss
    }
}
