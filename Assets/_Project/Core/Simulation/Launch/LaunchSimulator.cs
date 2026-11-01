using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Pure functional, deterministic launch resolution engine.
    /// Simulates vehicle ignition, atmospheric ascent, staging, fairing separation,
    /// weather risk, abort sequences, and orbital insertion.
    ///
    /// Adheres strictly to GDD §8.1, §8.4, §10.1, §10.4, and §10.5:
    /// - Deterministic seeded execution via isolated <see cref="IRandom"/>.
    /// - Weather checks based on launch pad region risks.
    /// - Sequential staging and failure cause chain tracking.
    /// - Crew abort detection and survivability outcome derivation.
    /// - Plain-language educational explanation generation.
    /// </summary>
    public static class LaunchSimulator
    {
        public static LaunchSimulationResult Simulate(LaunchSimulationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var rng = new SeededRng(parameters.Seed);
            return Simulate(parameters, rng);
        }

        public static LaunchSimulationResult Simulate(LaunchSimulationParameters parameters, IRandom rng)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var events = new List<LaunchPhaseEvent>();

            // 1. Weather check at the pad
            double weatherRoll = rng.NextDouble();
            LaunchWeatherCondition weather = EvaluateWeather(parameters.LaunchPadWeatherRisk, weatherRoll);

            if (weather == LaunchWeatherCondition.Stormy)
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.Ignition,
                    timestampSeconds: -10.0,
                    LaunchPhaseStatus.Aborted,
                    titleKey: "LAUNCH_EVENT_SCRUB_WEATHER_TITLE",
                    narrativeKey: "LAUNCH_EVENT_SCRUB_WEATHER_DESC",
                    altitudeKm: 0.0,
                    velocityMps: 0.0,
                    technicalDetails: $"Ramp weather risk {parameters.LaunchPadWeatherRisk:P0} exceeded safe limits (Roll: {weatherRoll:F3})."
                ));

                return new LaunchSimulationResult(
                    outcome: LaunchOutcome.WeatherScrub,
                    isCrewSafe: true,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 0.0,
                    primaryCauseKey: "CAUSE_WEATHER_SCRUB",
                    plainLanguageSummary: "Hava muhalefeti (şiddetli fırtına/yüksek rüzgar) nedeniyle fırlatma otomatik geri sayımda iptal edildi. Roket ve yük güvende.",
                    phaseEvents: events
                );
            }

            // 2. Pad Ignition & Liftoff
            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Ignition,
                timestampSeconds: -3.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_IGNITION_TITLE",
                narrativeKey: "LAUNCH_EVENT_IGNITION_DESC",
                altitudeKm: 0.0,
                velocityMps: 0.0,
                technicalDetails: "Stage 1 main engines ignited and verified at full chamber pressure."
            ));

            double stage1Reliability = parameters.StageReliabilities.Count > 0 ? parameters.StageReliabilities[0] : 0.95;
            // Pad abort check (rare startup sensor anomaly before release, e.g. 1% of failures)
            double ignitionRoll = rng.NextDouble();
            if (stage1Reliability < 0.99 && ignitionRoll > 0.99)
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.Liftoff,
                    timestampSeconds: 0.0,
                    LaunchPhaseStatus.Aborted,
                    titleKey: "LAUNCH_EVENT_PAD_ABORT_TITLE",
                    narrativeKey: "LAUNCH_EVENT_PAD_ABORT_DESC",
                    altitudeKm: 0.0,
                    velocityMps: 0.0,
                    technicalDetails: $"Engine startup sensor detected thrust anomaly (Roll: {ignitionRoll:F3})."
                ));

                return new LaunchSimulationResult(
                    outcome: LaunchOutcome.PadAbort,
                    isCrewSafe: true,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 0.0,
                    primaryCauseKey: "CAUSE_PAD_ABORT_ENGINE_ANOMALY",
                    plainLanguageSummary: "Fırlatma rampasında motor ateşleme anında telemetri basınç anomalisi tespit edildi ve klempler açılmadan motorlar durduruldu.",
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Liftoff,
                timestampSeconds: 0.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_LIFTOFF_TITLE",
                narrativeKey: "LAUNCH_EVENT_LIFTOFF_DESC",
                altitudeKm: 0.1,
                velocityMps: 15.0,
                technicalDetails: "Hold-down clamps released. Vehicle cleared tower."
            ));

            // 3. Max-Q Atmospheric Ascent
            double weatherMaxQFactor = weather == LaunchWeatherCondition.Windy ? 0.05 : 0.0;
            double stage1FlightRoll = rng.NextDouble();
            double stage1Threshold = stage1Reliability - weatherMaxQFactor;

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.MaxQ,
                timestampSeconds: 65.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_MAXQ_TITLE",
                narrativeKey: "LAUNCH_EVENT_MAXQ_DESC",
                altitudeKm: 14.0,
                velocityMps: 450.0,
                technicalDetails: "Maximum dynamic aerodynamic pressure passed successfully."
            ));

            if (stage1FlightRoll > stage1Threshold)
            {
                // Stage 1 structural / propulsion failure in flight
                bool crewSaved = HandleCrewAbort(parameters, rng, events, LaunchPhaseType.Stage1Burn, 28.0, 950.0, "Stage 1 turbopump catastrophic anomaly");

                LaunchOutcome outcome = crewSaved ? LaunchOutcome.InFlightAbort : LaunchOutcome.CatastrophicFailure;
                string summary = crewSaved
                    ? "Aşama-1 tırmanış sırasında motor arızası oluştu; acil fırlatma kaçış sistemi (abort) başarıyla devreye girerek yükü/mürettebatı kurtardı."
                    : "Aşama-1 tırmanışta yapısal/motor felaketi yaşandı ve fırlatıcı kaybedildi.";

                return new LaunchSimulationResult(
                    outcome: outcome,
                    isCrewSafe: crewSaved,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 1100.0,
                    primaryCauseKey: "CAUSE_STAGE1_FAILURE",
                    plainLanguageSummary: summary,
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage1Burn,
                timestampSeconds: 140.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_MECO_TITLE",
                narrativeKey: "LAUNCH_EVENT_MECO_DESC",
                altitudeKm: 68.0,
                velocityMps: 1950.0,
                technicalDetails: "Main Engine Cut-Off (MECO) nominal."
            ));

            // 4. Stage 1 Separation
            double sep1Roll = rng.NextDouble();
            if (sep1Roll > 0.985)
            {
                bool crewSaved = HandleCrewAbort(parameters, rng, events, LaunchPhaseType.Stage1Separation, 72.0, 2000.0, "Pneumatic/pyro separation staging mechanism jam");
                LaunchOutcome outcome = crewSaved ? LaunchOutcome.InFlightAbort : LaunchOutcome.CatastrophicFailure;
                string summary = crewSaved
                    ? "Aşama-1 ayrılma mekanizması kilitlendi; kaçış sistemi aracı ayırarak mürettebat kapsülünü emniyetle okyanusa indirdi."
                    : "Aşama-1 ayrılması başarısız oldu; roket kontrolünü kaybederek infilak etti.";

                return new LaunchSimulationResult(
                    outcome: outcome,
                    isCrewSafe: crewSaved,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 2000.0,
                    primaryCauseKey: "CAUSE_STAGE1_SEPARATION_FAILURE",
                    plainLanguageSummary: summary,
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage1Separation,
                timestampSeconds: 144.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_STAGE1_SEP_TITLE",
                narrativeKey: "LAUNCH_EVENT_STAGE1_SEP_DESC",
                altitudeKm: 74.0,
                velocityMps: 2020.0,
                technicalDetails: "Interstage pyrotechnic ring separated cleanly."
            ));

            // If 1-stage rocket (single stage to orbit)
            if (parameters.StageCount == 1)
            {
                return EvaluateOrbitalInsertion(parameters, rng, events, weather, deliveredDv: 9200.0 + parameters.EquatorialBonusDv);
            }

            // 5. Stage 2 Ignition & Burn
            double stage2Reliability = parameters.StageReliabilities.Count > 1 ? parameters.StageReliabilities[1] : 0.95;
            double stage2IgnitionRoll = rng.NextDouble();
            if (stage2IgnitionRoll > stage2Reliability)
            {
                bool crewSaved = HandleCrewAbort(parameters, rng, events, LaunchPhaseType.Stage2Ignition, 82.0, 2100.0, "Stage 2 vacuum engine ignition restart failed");
                LaunchOutcome outcome = crewSaved ? LaunchOutcome.InFlightAbort : LaunchOutcome.CatastrophicFailure;
                string summary = crewSaved
                    ? "Aşama-2 vakum motoru ateşlenemedi; kaçış sistemi devreye girerek aracı suborbital balistik yörüngeden kurtardı."
                    : "Aşama-2 motoru ateşlenemedi; araç yetersiz hızla atmosfere geri dönerek parçalandı.";

                return new LaunchSimulationResult(
                    outcome: outcome,
                    isCrewSafe: crewSaved,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 2200.0,
                    primaryCauseKey: "CAUSE_STAGE2_IGNITION_FAILURE",
                    plainLanguageSummary: summary,
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage2Ignition,
                timestampSeconds: 148.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_STAGE2_IGNITION_TITLE",
                narrativeKey: "LAUNCH_EVENT_STAGE2_IGNITION_DESC",
                altitudeKm: 78.0,
                velocityMps: 2080.0,
                technicalDetails: "Stage 2 vacuum engine startup nominal."
            ));

            // Fairing Jettison (above atmosphere)
            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.FairingJettison,
                timestampSeconds: 190.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_FAIRING_JETTISON_TITLE",
                narrativeKey: "LAUNCH_EVENT_FAIRING_JETTISON_DESC",
                altitudeKm: 115.0,
                velocityMps: 2950.0,
                technicalDetails: "Aerodynamic fairing halves jettisoned outside sensible atmosphere."
            ));

            double stage2BurnRoll = rng.NextDouble();
            if (stage2BurnRoll > stage2Reliability + 0.02)
            {
                // Early shutdown -> Partial Orbit or abort
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.Stage2Burn,
                    timestampSeconds: 380.0,
                    LaunchPhaseStatus.Degraded,
                    titleKey: "LAUNCH_EVENT_STAGE2_UNDERPERFORM_TITLE",
                    narrativeKey: "LAUNCH_EVENT_STAGE2_UNDERPERFORM_DESC",
                    altitudeKm: 165.0,
                    velocityMps: 6100.0,
                    technicalDetails: "Stage 2 suffered premature shutdown due to oxidizer depletion."
                ));

                return new LaunchSimulationResult(
                    outcome: LaunchOutcome.PartialOrbit,
                    isCrewSafe: true,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 6800.0,
                    primaryCauseKey: "CAUSE_STAGE2_PREMATURE_CUTOFF",
                    plainLanguageSummary: "Aşama-2 planlanandan erken kapandı; araç hedeflenen yörüngeye tam erişemedi ve eliptik düşük bozulma yörüngesinde kaldı.",
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage2Burn,
                timestampSeconds: 480.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_SECO1_TITLE",
                narrativeKey: "LAUNCH_EVENT_SECO1_DESC",
                altitudeKm: 185.0,
                velocityMps: 7450.0,
                technicalDetails: "Second Engine Cut-Off 1 (SECO-1) on profile."
            ));

            // If 2-stage rocket, orbital insertion is done by stage 2
            if (parameters.StageCount == 2)
            {
                return EvaluateOrbitalInsertion(parameters, rng, events, weather, deliveredDv: 9400.0 + parameters.EquatorialBonusDv);
            }

            // 6. Stage 3 (if 3-stage vehicle)
            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage2Separation,
                timestampSeconds: 485.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_STAGE2_SEP_TITLE",
                narrativeKey: "LAUNCH_EVENT_STAGE2_SEP_DESC",
                altitudeKm: 188.0,
                velocityMps: 7460.0,
                technicalDetails: "Stage 2 separated."
            ));

            double stage3Reliability = parameters.StageReliabilities.Count > 2 ? parameters.StageReliabilities[2] : 0.95;
            double stage3Roll = rng.NextDouble();
            if (stage3Roll > stage3Reliability)
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.Stage3Burn,
                    timestampSeconds: 610.0,
                    LaunchPhaseStatus.CriticalFailure,
                    titleKey: "LAUNCH_EVENT_STAGE3_FAILURE_TITLE",
                    narrativeKey: "LAUNCH_EVENT_STAGE3_FAILURE_DESC",
                    altitudeKm: 210.0,
                    velocityMps: 7550.0,
                    technicalDetails: "Stage 3 upper stage attitude drift and combustion instability."
                ));

                return new LaunchSimulationResult(
                    outcome: LaunchOutcome.PartialOrbit,
                    isCrewSafe: true,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: 7800.0,
                    primaryCauseKey: "CAUSE_STAGE3_COMBUSTION_FAILURE",
                    plainLanguageSummary: "Üst aşama (Aşama-3) ateşleme anormalliği nedeniyle yörünge hızı eksik kaldı; araç kararsız alt-yörüngede kilitlendi.",
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage3Ignition,
                timestampSeconds: 490.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_STAGE3_IGNITION_TITLE",
                narrativeKey: "LAUNCH_EVENT_STAGE3_IGNITION_DESC",
                altitudeKm: 190.0,
                velocityMps: 7500.0,
                technicalDetails: "Stage 3 burn nominal."
            ));

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.Stage3Burn,
                timestampSeconds: 640.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_STAGE3_CUTOFF_TITLE",
                narrativeKey: "LAUNCH_EVENT_STAGE3_CUTOFF_DESC",
                altitudeKm: 230.0,
                velocityMps: 7850.0,
                technicalDetails: "Stage 3 orbit injection cutoff nominal."
            ));

            return EvaluateOrbitalInsertion(parameters, rng, events, weather, deliveredDv: 9600.0 + parameters.EquatorialBonusDv);
        }

        private static LaunchSimulationResult EvaluateOrbitalInsertion(
            LaunchSimulationParameters parameters,
            IRandom rng,
            List<LaunchPhaseEvent> events,
            LaunchWeatherCondition weather,
            double deliveredDv)
        {
            double insertionRoll = rng.NextDouble();
            // Overall vehicle avionics and ADCS reliability factor
            if (insertionRoll > parameters.OverallReliability + 0.05)
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.OrbitalInsertion,
                    timestampSeconds: 700.0,
                    LaunchPhaseStatus.Degraded,
                    titleKey: "LAUNCH_EVENT_INSERTION_ANOMALY_TITLE",
                    narrativeKey: "LAUNCH_EVENT_INSERTION_ANOMALY_DESC",
                    altitudeKm: 240.0,
                    velocityMps: 7750.0,
                    technicalDetails: "Avionics insertion burn vector offset resulted in non-nominal orbit insertion."
                ));

                return new LaunchSimulationResult(
                    outcome: LaunchOutcome.PartialOrbit,
                    isCrewSafe: true,
                    weatherCondition: weather,
                    weatherRisk: parameters.LaunchPadWeatherRisk,
                    deliveredDeltaV: deliveredDv - 300.0,
                    primaryCauseKey: "CAUSE_ORBITAL_INSERTION_DEGRADED",
                    plainLanguageSummary: "Aviyonik/yönelim sapması nedeniyle yörüngeye giriş kısmen sağlandı ancak hedeflenen yükseklik ve eksantrisite sapmalı gerçekleşti.",
                    phaseEvents: events
                );
            }

            events.Add(new LaunchPhaseEvent(
                LaunchPhaseType.OrbitalInsertion,
                timestampSeconds: 720.0,
                LaunchPhaseStatus.Nominal,
                titleKey: "LAUNCH_EVENT_ORBIT_ACHIEVED_TITLE",
                narrativeKey: "LAUNCH_EVENT_ORBIT_ACHIEVED_DESC",
                altitudeKm: 250.0,
                velocityMps: 7800.0,
                technicalDetails: $"Nominal low Earth orbit insertion confirmed (Δv delivered: {deliveredDv:0} m/s)."
            ));

            return new LaunchSimulationResult(
                outcome: LaunchOutcome.Success,
                isCrewSafe: true,
                weatherCondition: weather,
                weatherRisk: parameters.LaunchPadWeatherRisk,
                deliveredDeltaV: deliveredDv,
                primaryCauseKey: "CAUSE_NOMINAL_LAUNCH",
                plainLanguageSummary: "Fırlatma ve tüm aşama ayrılmaları kusursuz tamamlandı; araç hedeflenen transfer/başlangıç yörüngesine başarıyla oturdu.",
                phaseEvents: events
            );
        }

        private static bool HandleCrewAbort(
            LaunchSimulationParameters parameters,
            IRandom rng,
            List<LaunchPhaseEvent> events,
            LaunchPhaseType failedPhase,
            double altitudeKm,
            double velocityMps,
            string failureDetails)
        {
            events.Add(new LaunchPhaseEvent(
                failedPhase,
                timestampSeconds: 110.0,
                LaunchPhaseStatus.CriticalFailure,
                titleKey: "LAUNCH_EVENT_INFLIGHT_FAILURE_TITLE",
                narrativeKey: "LAUNCH_EVENT_INFLIGHT_FAILURE_DESC",
                altitudeKm: altitudeKm,
                velocityMps: velocityMps,
                technicalDetails: failureDetails
            ));

            if (!parameters.IsCrewed)
            {
                // Uncrewed mission has no abort recovery module unless specifically fitted
                return false;
            }

            if (!parameters.HasAbortSystem)
            {
                // Crewed without mandatory abort system -> Catastrophic crew loss
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.OrbitalInsertion,
                    timestampSeconds: 125.0,
                    LaunchPhaseStatus.CriticalFailure,
                    titleKey: "LAUNCH_EVENT_CREW_LOST_TITLE",
                    narrativeKey: "LAUNCH_EVENT_CREW_LOST_DESC",
                    altitudeKm: altitudeKm,
                    velocityMps: velocityMps,
                    technicalDetails: "No launch abort system was installed; crew capsule was destroyed in vehicle breakup."
                ));
                return false;
            }

            double abortRoll = rng.NextDouble();
            bool abortSuccess = abortRoll <= parameters.AbortSystemReliability;

            if (abortSuccess)
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.OrbitalInsertion,
                    timestampSeconds: 130.0,
                    LaunchPhaseStatus.Aborted,
                    titleKey: "LAUNCH_EVENT_ABORT_TOWER_SUCCESS_TITLE",
                    narrativeKey: "LAUNCH_EVENT_ABORT_TOWER_SUCCESS_DESC",
                    altitudeKm: altitudeKm + 2.0,
                    velocityMps: 600.0,
                    technicalDetails: "Launch abort tower ignited and pulled crew capsule safely away from booster. Parachutes deployed nominally."
                ));
                return true;
            }
            else
            {
                events.Add(new LaunchPhaseEvent(
                    LaunchPhaseType.OrbitalInsertion,
                    timestampSeconds: 135.0,
                    LaunchPhaseStatus.CriticalFailure,
                    titleKey: "LAUNCH_EVENT_ABORT_SYSTEM_FAILURE_TITLE",
                    narrativeKey: "LAUNCH_EVENT_ABORT_SYSTEM_FAILURE_DESC",
                    altitudeKm: altitudeKm,
                    velocityMps: velocityMps,
                    technicalDetails: $"Launch abort system motor ignition failure (Roll: {abortRoll:F3} vs Rel: {parameters.AbortSystemReliability:F3})."
                ));
                return false;
            }
        }

        private static LaunchWeatherCondition EvaluateWeather(double weatherRisk, double roll)
        {
            if (weatherRisk >= 0.90)
                return LaunchWeatherCondition.Stormy;

            // If weatherRisk is high, greater chance of Windy or Stormy scrub
            double stormThreshold = 1.0 - (weatherRisk * 0.40); // e.g. 0.35 risk -> storm above 0.86
            double windThreshold = 1.0 - (weatherRisk * 0.85);  // e.g. 0.35 risk -> wind above 0.7025

            if (roll >= stormThreshold)
                return LaunchWeatherCondition.Stormy;
            if (roll >= windThreshold)
                return LaunchWeatherCondition.Windy;
            return LaunchWeatherCondition.Clear;
        }
    }
}
