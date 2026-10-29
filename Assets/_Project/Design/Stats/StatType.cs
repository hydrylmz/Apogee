namespace Apogee.Design
{
    /// <summary>
    /// Enumerates all core engineering, physical, and mission statistics in APOGEE.
    /// Complies with GDD §8.1, §8.2 and §16.
    /// </summary>
    public enum StatType
    {
        // Mass & Propulsion
        DryMass = 10,           // Metric tons (t) - dry structure without propellants
        WetMass = 11,           // Metric tons (t) - total launch/stage mass
        PropellantMass = 12,    // Metric tons (t) - usable propellant
        DeltaV = 20,            // Meters per second (m/s)
        Thrust = 21,            // Kilonewtons (kN)
        SpecificImpulse = 22,   // Seconds (s) - engine efficiency Isp
        PayloadCapacity = 23,   // Metric tons (t) - max payload to mission trajectory (M1-08)

        // Power System
        PowerGeneration = 30,   // Kilowatts (kW) - solar / RTG output
        PowerConsumption = 31,  // Kilowatts (kW) - active system draw
        PowerMargin = 32,       // Kilowatts (kW) or ratio - generation vs consumption
        BatteryCapacity = 33,   // Kilowatt-hours (kWh) - eclipse reserve

        // Thermal System
        ThermalLoad = 40,       // Kilowatts (kW) - internal heat + solar absorption
        ThermalDissipation = 41,// Kilowatts (kW) - radiator & insulation capacity
        ThermalMargin = 42,     // Kilowatts (kW) - dissipation vs load

        // Communications & Data
        DataRate = 50,          // Megabits per second (Mbps) or GB/day generation
        DataStorage = 51,       // Gigabytes (GB) onboard capacity
        DownlinkRate = 52,      // Gigabytes per day (GB/day) ground station downlink

        // Programmatic & Operations
        Cost = 60,              // Millions USD (M$)
        BuildTime = 61,         // Game days for manufacturing and assembly
        Reliability = 70,       // Normalized probability [0.0 to 1.0]

        // Custom / Dynamic
        Custom = 999
    }
}
