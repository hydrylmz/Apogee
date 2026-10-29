namespace Apogee.Design
{
    /// <summary>
    /// Identifies the functional structural role of a part in the 2D cross-section view (GDD §7.2, §14.3, §15.1).
    /// </summary>
    public enum CrossSectionPartType
    {
        FairingNose,
        PayloadInstrument,
        PayloadBus,
        PayloadSubsystem,
        PayloadEdl,
        InterstageDecoupler,
        StageTank,
        StageEngine,
        AeroFins
    }
}
