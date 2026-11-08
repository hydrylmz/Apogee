namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Type of telemetry/operational event occurring during science phase.
    /// Complies with GDD §8.1, §10.1, §10.4, §13.2.
    /// </summary>
    public enum ScienceEventType
    {
        PowerBalance,
        EclipseTransition,
        ThermalStatus,
        DataCollection,
        BufferOverflow,
        GroundStationContact,
        DownlinkCompleted,
        ObjectiveProgress,
        SubsystemDegradation
    }
}
