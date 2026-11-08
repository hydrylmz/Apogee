using System;

namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Parameters required to deterministically resolve one turn / step of science operations.
    /// Complies with GDD §8.1 (Power, Thermal, Data, Downlink), §16.1, §16.5, §19.4.
    /// </summary>
    public sealed class ScienceSimulationParameters
    {
        public string MissionId { get; }
        public string DestinationId { get; }
        public int DayNumber { get; }

        // Power
        public double SolarGenerationKw { get; }
        public double RtgGenerationKw { get; }
        public double PowerConsumptionKw { get; }
        public double BatteryCapacityKwh { get; }
        public double InitialBatteryStorageKwh { get; }
        public double EclipseFraction { get; } // 0.0 - 1.0 (fraction of day in shadow)

        // Thermal
        public double ThermalLoadKw { get; }
        public double ThermalDissipationKw { get; }

        // Data & Comms
        public double InstrumentDataRateGbPerDay { get; }
        public double InstrumentDutyCycle { get; } // 0.0 - 1.0
        public double MaxStorageCapacityGb { get; }
        public double InitialBufferStorageGb { get; }
        public double DownlinkRateGbPerDay { get; }
        public double GroundStationVisibilityFraction { get; } // 0.0 - 1.0 (contact coverage per day)

        // Reliability / Health
        public double SystemReliability { get; }
        public ulong Seed { get; }

        public ScienceSimulationParameters(
            string missionId,
            string destinationId,
            int dayNumber,
            double solarGenerationKw,
            double rtgGenerationKw,
            double powerConsumptionKw,
            double batteryCapacityKwh,
            double initialBatteryStorageKwh,
            double eclipseFraction,
            double thermalLoadKw,
            double thermalDissipationKw,
            double instrumentDataRateGbPerDay,
            double instrumentDutyCycle,
            double maxStorageCapacityGb,
            double initialBufferStorageGb,
            double downlinkRateGbPerDay,
            double groundStationVisibilityFraction,
            double systemReliability,
            ulong seed)
        {
            MissionId = !string.IsNullOrWhiteSpace(missionId) ? missionId : "mission_default";
            DestinationId = !string.IsNullOrWhiteSpace(destinationId) ? destinationId : "moon";
            DayNumber = Math.Max(1, dayNumber);

            SolarGenerationKw = Math.Max(0.0, solarGenerationKw);
            RtgGenerationKw = Math.Max(0.0, rtgGenerationKw);
            PowerConsumptionKw = Math.Max(0.0, powerConsumptionKw);
            BatteryCapacityKwh = Math.Max(0.0, batteryCapacityKwh);
            InitialBatteryStorageKwh = Math.Max(0.0, initialBatteryStorageKwh);
            EclipseFraction = Math.Max(0.0, Math.Min(1.0, eclipseFraction));

            ThermalLoadKw = Math.Max(0.0, thermalLoadKw);
            ThermalDissipationKw = Math.Max(0.0, thermalDissipationKw);

            InstrumentDataRateGbPerDay = Math.Max(0.0, instrumentDataRateGbPerDay);
            InstrumentDutyCycle = Math.Max(0.0, Math.Min(1.0, instrumentDutyCycle));
            MaxStorageCapacityGb = Math.Max(0.1, maxStorageCapacityGb);
            InitialBufferStorageGb = Math.Max(0.0, initialBufferStorageGb);
            DownlinkRateGbPerDay = Math.Max(0.0, downlinkRateGbPerDay);
            GroundStationVisibilityFraction = Math.Max(0.0, Math.Min(1.0, groundStationVisibilityFraction));

            SystemReliability = Math.Max(0.05, Math.Min(1.0, systemReliability));
            Seed = seed;
        }
    }
}
