using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Deterministic simulator for space science operations.
    /// Simulates daily power generation/eclipse/consumption, thermal balance,
    /// scientific instrument data generation, storage buffers, and ground downlink.
    /// Accurately triggers bottlenecks and overflows when systems are imbalanced.
    ///
    /// Complies with GDD §8.1 (Power, Thermal, Data, Downlink), §8.4 (Determinism),
    /// §10.1 (Phase 4), §10.5 (Downlink vs Generated data), §13.2 ("Neden?").
    /// </summary>
    public static class ScienceSimulator
    {
        public static ScienceSimulationResult SimulateDay(ScienceSimulationParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            var events = new List<SciencePhaseEvent>();
            var rng = new SeededRng(parameters.Seed ^ ((ulong)parameters.DayNumber * 1000000007UL));

            // ---------------------------------------------------------------- Power Balance
            // Daily daylight vs eclipse hours (24h standard day)
            double daylightFraction = 1.0 - parameters.EclipseFraction;
            // Solar produces during daylight; RTG produces continuously (24h)
            double avgDailyPowerGenKw = (parameters.SolarGenerationKw * daylightFraction) + parameters.RtgGenerationKw;
            double avgDailyPowerDemandKw = parameters.PowerConsumptionKw;
            double netPowerBalanceKw = avgDailyPowerGenKw - avgDailyPowerDemandKw;

            double currentBattery = parameters.InitialBatteryStorageKwh;
            bool hadPowerDeficit = false;
            double powerThrottlingFactor = 1.0;

            // Night time deficit check: in eclipse, solar is 0, only RTG + battery available
            double nightDemandKw = parameters.PowerConsumptionKw;
            double nightGenKw = parameters.RtgGenerationKw;
            double nightNetDeficitRate = nightDemandKw - nightGenKw; // kW needed from battery

            if (nightNetDeficitRate > 0)
            {
                double nightHours = parameters.EclipseFraction * 24.0;
                double batteryDrainNeededKwh = nightNetDeficitRate * nightHours;

                if (currentBattery >= batteryDrainNeededKwh)
                {
                    currentBattery -= batteryDrainNeededKwh;
                    events.Add(new SciencePhaseEvent(
                        parameters.DayNumber,
                        ScienceEventType.EclipseTransition,
                        ScienceEventStatus.Nominal,
                        "eclipse_battery_discharge",
                        $"Gölge geçişinde bataryadan {batteryDrainNeededKwh:F1} kWh enerji kullanıldı. Kalan batarya: {currentBattery:F1} kWh.",
                        currentBattery));
                }
                else
                {
                    // Battery ran dry during night!
                    double deficitEnergy = batteryDrainNeededKwh - currentBattery;
                    currentBattery = 0.0;
                    hadPowerDeficit = true;
                    // Throttle instruments proportional to missing energy
                    double fractionCovered = (batteryDrainNeededKwh - deficitEnergy) / Math.Max(0.1, batteryDrainNeededKwh);
                    powerThrottlingFactor = Math.Max(0.2, fractionCovered);

                    events.Add(new SciencePhaseEvent(
                        parameters.DayNumber,
                        ScienceEventType.PowerBalance,
                        ScienceEventStatus.Critical,
                        "power_deficit_brownout",
                        $"Batarya yetersizliği nedeniyle gölge geçişinde güç kesintisi yaşandı. Bilimsel aletler %{(powerThrottlingFactor * 100.0):F0} kapasiteye kısıtlandı.",
                        deficitEnergy));
                }
            }
            else
            {
                // RTG alone covered night demand or no eclipse
                events.Add(new SciencePhaseEvent(
                    parameters.DayNumber,
                    ScienceEventType.PowerBalance,
                    ScienceEventStatus.Nominal,
                    "power_nominal",
                    $"Kesintisiz güç sağlandı. Günlük net güç marjı: +{netPowerBalanceKw:F2} kW.",
                    netPowerBalanceKw));
            }

            // Daytime recharge if surplus solar
            if (netPowerBalanceKw > 0 && parameters.BatteryCapacityKwh > 0)
            {
                double rechargeRateKw = (parameters.SolarGenerationKw + parameters.RtgGenerationKw) - parameters.PowerConsumptionKw;
                double daytimeHours = daylightFraction * 24.0;
                double energyRechargeKwh = rechargeRateKw * daytimeHours;
                currentBattery = Math.Min(parameters.BatteryCapacityKwh, currentBattery + energyRechargeKwh);
            }

            // ---------------------------------------------------------------- Thermal Balance
            double netThermalExcessKw = Math.Max(0.0, parameters.ThermalLoadKw - parameters.ThermalDissipationKw);
            bool hadThermalOverload = false;
            double thermalThrottlingFactor = 1.0;

            if (netThermalExcessKw > 0.01)
            {
                hadThermalOverload = true;
                // Heat overload forces safe-mode duty cycle reduction
                thermalThrottlingFactor = Math.Max(0.25, parameters.ThermalDissipationKw / Math.Max(0.1, parameters.ThermalLoadKw));
                events.Add(new SciencePhaseEvent(
                    parameters.DayNumber,
                    ScienceEventType.ThermalStatus,
                    ScienceEventStatus.Warning,
                    "thermal_throttling_warning",
                    $"Radyatör kapasitesi yetersizliği (+{netThermalExcessKw:F1} kW ısı fazlası) nedeniyle aletler soğuma moduna alındı.",
                    netThermalExcessKw));
            }
            else
            {
                events.Add(new SciencePhaseEvent(
                    parameters.DayNumber,
                    ScienceEventType.ThermalStatus,
                    ScienceEventStatus.Nominal,
                    "thermal_nominal",
                    "Isıl kontrol nominal. Isı birikimi tespit edilmedi.",
                    0.0));
            }

            // ---------------------------------------------------------------- Data Generation & Throttling
            // Raw theoretical data generation
            double rawDataGeneratedGb = parameters.InstrumentDataRateGbPerDay * parameters.InstrumentDutyCycle;
            // Effective data generation after power / thermal constraints and reliability degradation
            double operationalFactor = powerThrottlingFactor * thermalThrottlingFactor;
            double effectiveDataCollectedGb = rawDataGeneratedGb * operationalFactor;

            events.Add(new SciencePhaseEvent(
                parameters.DayNumber,
                ScienceEventType.DataCollection,
                operationalFactor < 0.95 ? ScienceEventStatus.Warning : ScienceEventStatus.Nominal,
                "data_harvested",
                $"Enstrümanlar {effectiveDataCollectedGb:F2} GB bilim verisi üretti (Ham potansiyel: {rawDataGeneratedGb:F2} GB).",
                effectiveDataCollectedGb));

            // ---------------------------------------------------------------- Storage Buffer & Overflow
            double currentBuffer = parameters.InitialBufferStorageGb + effectiveDataCollectedGb;
            double droppedDataGb = 0.0;
            bool hasDataBottleneck = false;

            if (currentBuffer > parameters.MaxStorageCapacityGb)
            {
                droppedDataGb = currentBuffer - parameters.MaxStorageCapacityGb;
                currentBuffer = parameters.MaxStorageCapacityGb;
                hasDataBottleneck = true;

                events.Add(new SciencePhaseEvent(
                    parameters.DayNumber,
                    ScienceEventType.BufferOverflow,
                    ScienceEventStatus.Bottleneck,
                    "buffer_overflow_loss",
                    $"On-board veri depolama kapasitesi ({parameters.MaxStorageCapacityGb:F1} GB) aşıldı! {droppedDataGb:F2} GB bilim verisi yere iletilemeden kayboldu.",
                    droppedDataGb));
            }

            // ---------------------------------------------------------------- Ground Downlink
            // Max data that could be downlinked today given antenna bandwidth and ground station coverage
            double dailyDownlinkCapacityGb = parameters.DownlinkRateGbPerDay * parameters.GroundStationVisibilityFraction;
            double actualDownlinkGb = Math.Min(currentBuffer, dailyDownlinkCapacityGb);
            currentBuffer -= actualDownlinkGb;

            if (dailyDownlinkCapacityGb < effectiveDataCollectedGb && currentBuffer > (parameters.MaxStorageCapacityGb * 0.75))
            {
                hasDataBottleneck = true;
            }

            events.Add(new SciencePhaseEvent(
                parameters.DayNumber,
                ScienceEventType.DownlinkCompleted,
                actualDownlinkGb > 0 ? ScienceEventStatus.Nominal : ScienceEventStatus.Warning,
                "downlink_delivered",
                $"Yer istasyonu temasında {actualDownlinkGb:F2} GB veri Dünya'ya başarıyla indirildi. Kalan tampon: {currentBuffer:F2} GB.",
                actualDownlinkGb));

            // ---------------------------------------------------------------- Summary Construction
            string summary;
            if (hasDataBottleneck && droppedDataGb > 0)
            {
                summary = $"Veri Darboğazı: Depolama ve downlink kısıtları nedeniyle {droppedDataGb:F2} GB veri kayboldu, {actualDownlinkGb:F2} GB teslim edildi.";
            }
            else if (hadPowerDeficit)
            {
                summary = $"Güç Darboğazı: Batarya yetersizliği veri üretimini %{(operationalFactor * 100):F0} seviyesine düşürdü. {actualDownlinkGb:F2} GB indirildi.";
            }
            else if (hadThermalOverload)
            {
                summary = $"Isı Kısıtı: Aşırı ısınma nedeniyle aletler kısıtlandı. {actualDownlinkGb:F2} GB veri Dünya'ya indirildi.";
            }
            else
            {
                summary = $"Nominal Operasyon: Tüm aletler dengede çalıştı. {actualDownlinkGb:F2} GB bilim verisi başarıyla Dünya'ya indirildi.";
            }

            return new ScienceSimulationResult(
                dayNumber: parameters.DayNumber,
                netPowerBalanceKw: netPowerBalanceKw,
                finalBatteryStorageKwh: currentBattery,
                hadPowerDeficit: hadPowerDeficit,
                netThermalExcessKw: netThermalExcessKw,
                hadThermalOverload: hadThermalOverload,
                rawDataGeneratedGb: rawDataGeneratedGb,
                effectiveDataCollectedGb: effectiveDataCollectedGb,
                downlinkDeliveredGb: actualDownlinkGb,
                finalBufferStorageGb: currentBuffer,
                droppedDataGb: droppedDataGb,
                hasDataBottleneck: hasDataBottleneck,
                hasPowerBottleneck: hadPowerDeficit,
                hasThermalBottleneck: hadThermalOverload,
                plainLanguageSummary: summary,
                events: events);
        }
    }
}
