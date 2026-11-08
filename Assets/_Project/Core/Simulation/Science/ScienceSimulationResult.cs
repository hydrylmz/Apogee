using System;
using System.Collections.Generic;

namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Outcome of a single operational science turn / step.
    /// Accurately exposes data collected, data delivered, buffer status, power balance, and bottlenecks.
    /// Complies with GDD §8.1, §10.1, §10.5, §11.1, §13.2.
    /// </summary>
    public sealed class ScienceSimulationResult
    {
        public int DayNumber { get; }

        // Power Status
        public double NetPowerBalanceKw { get; }
        public double FinalBatteryStorageKwh { get; }
        public bool HadPowerDeficit { get; }

        // Thermal Status
        public double NetThermalExcessKw { get; }
        public bool HadThermalOverload { get; }

        // Data & Downlink
        public double RawDataGeneratedGb { get; }
        public double EffectiveDataCollectedGb { get; } // after power/thermal throttling
        public double DownlinkDeliveredGb { get; }
        public double FinalBufferStorageGb { get; }
        public double DroppedDataGb { get; } // Buffer overflow loss

        // Bottlenecks & Health
        public bool HasDataBottleneck { get; }
        public bool HasPowerBottleneck { get; }
        public bool HasThermalBottleneck { get; }
        public string PlainLanguageSummary { get; }

        public IReadOnlyList<SciencePhaseEvent> Events { get; }

        public ScienceSimulationResult(
            int dayNumber,
            double netPowerBalanceKw,
            double finalBatteryStorageKwh,
            bool hadPowerDeficit,
            double netThermalExcessKw,
            bool hadThermalOverload,
            double rawDataGeneratedGb,
            double effectiveDataCollectedGb,
            double downlinkDeliveredGb,
            double finalBufferStorageGb,
            double droppedDataGb,
            bool hasDataBottleneck,
            bool hasPowerBottleneck,
            bool hasThermalBottleneck,
            string plainLanguageSummary,
            IReadOnlyList<SciencePhaseEvent> events)
        {
            DayNumber = dayNumber;
            NetPowerBalanceKw = netPowerBalanceKw;
            FinalBatteryStorageKwh = finalBatteryStorageKwh;
            HadPowerDeficit = hadPowerDeficit;
            NetThermalExcessKw = netThermalExcessKw;
            HadThermalOverload = hadThermalOverload;
            RawDataGeneratedGb = rawDataGeneratedGb;
            EffectiveDataCollectedGb = effectiveDataCollectedGb;
            DownlinkDeliveredGb = downlinkDeliveredGb;
            FinalBufferStorageGb = finalBufferStorageGb;
            DroppedDataGb = droppedDataGb;
            HasDataBottleneck = hasDataBottleneck;
            HasPowerBottleneck = hasPowerBottleneck;
            HasThermalBottleneck = hasThermalBottleneck;
            PlainLanguageSummary = plainLanguageSummary ?? string.Empty;
            Events = events ?? Array.Empty<SciencePhaseEvent>();
        }
    }
}
