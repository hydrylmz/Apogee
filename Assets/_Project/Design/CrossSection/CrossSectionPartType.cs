namespace Apogee.Design
{
    /// <summary>
    /// Identifies the functional structural role of a part in the 2D cross-section and hangar views (GDD §7.2, §14.3, §15.1).
    /// </summary>
    public enum CrossSectionPartType
    {
        FairingNose = 0,
        PayloadInstrument = 1,
        PayloadBus = 2,
        PayloadSubsystem = 3,
        PayloadEdl = 4,
        InterstageDecoupler = 5,
        StageTank = 6,
        StageEngine = 7,
        AeroFins = 8,
        PayloadCabin = 9,
        PayloadLifeSupport = 10,
        PayloadAbortSystem = 11,
        PayloadPower = 12,
        PayloadThermal = 13,
        PayloadComms = 14,
        PayloadAdcs = 15
    }
}
