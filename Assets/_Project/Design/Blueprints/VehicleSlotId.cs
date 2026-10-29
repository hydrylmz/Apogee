namespace Apogee.Design
{
    /// <summary>
    /// Identifiers for standard component slots on a vehicle blueprint (GDD §7.2, §14.3).
    /// Covers spacecraft / payload subsystems and multi-stage rocket stack components.
    /// </summary>
    public enum VehicleSlotId
    {
        // Spacecraft / Payload Slots
        PayloadBus = 0,
        PayloadPower = 1,
        PayloadThermal = 2,
        PayloadComms = 3,
        PayloadAdcs = 4,
        PayloadEdl = 5,
        PayloadLifeSupport = 6,
        PayloadInstrument1 = 7,
        PayloadInstrument2 = 8,

        // Rocket Propulsion Stages
        Stage1Engine = 10,
        Stage1Tank = 11,
        Stage2Engine = 20,
        Stage2Tank = 21,
        Stage3Engine = 30,
        Stage3Tank = 31
    }
}
