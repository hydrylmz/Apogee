using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Pure functional, deterministic Entry, Descent, and Landing (EDL) simulator.
    ///
    /// Simulates:
    /// 1. Deorbit burn / Trajectory alignment
    /// 2. Atmospheric entry & Aerodynamic decelerations (if target has atmosphere)
    /// 3. Peak heating & Thermal protection verification
    /// 4. Parachute deployment & Aerodynamic chute stability (fails in vacuum)
    /// 5. Terminal powered descent (skycrane, retro-rockets, landing thrusters)
    /// 6. Touchdown damping & surface contact
    ///
    /// Adheres strictly to GDD §7.7, §8.1, §8.4, §10.1, §10.4, §10.5, and §13.2:
    /// - Deterministic execution via isolated <see cref="IRandom"/>.
    /// - Cause-and-effect chain generation explaining successes or catastrophic failures in plain language.
    /// </summary>
    public static class EdlSimulator
    {
        public static EdlSimulationResult Simulate(EdlSimulationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var rng = new SeededRng(parameters.Seed);
            return Simulate(parameters, rng);
        }

        public static EdlSimulationResult Simulate(EdlSimulationParameters parameters, IRandom rng)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var events = new List<EdlPhaseEvent>();
            double heatShield = parameters.HasHeatShield ? 1.0 : 0.0;
            double currentReliability = (parameters.SpacecraftReliability * 0.4) + (parameters.EdlSystemReliability * 0.6);

            // Phase 1: Deorbit Burn / Trajectory Alignment
            double deorbitRoll = rng.NextDouble();
            double deorbitThreshold = Math.Max(0.70, currentReliability);

            if (deorbitRoll > deorbitThreshold + 0.15)
            {
                // Engine misfire during deorbit burn -> Trajectory Loss
                events.Add(new EdlPhaseEvent(
                    phase: EdlPhaseType.DeorbitBurn,
                    elapsedSeconds: 0.0,
                    status: EdlPhaseStatus.Failed,
                    titleKey: "EDL_EVENT_DEORBIT_FAILED_TITLE",
                    narrativeKey: "EDL_EVENT_DEORBIT_FAILED_DESC",
                    altitudeKm: 120.0,
                    velocityMps: 3400.0,
                    heatShieldIntegrity: heatShield,
                    technicalDetails: $"Deorbit burn engine ignition failed (Roll={deorbitRoll:F3}, Threshold={deorbitThreshold:F3})."
                ));

                return new EdlSimulationResult(
                    outcome: EdlOutcome.TrajectoryLoss,
                    isCrewSafe: false,
                    remainingHeatShieldIntegrity: heatShield,
                    touchdownVelocityMps: 0.0,
                    primaryCauseKey: "EDL_CAUSE_DEORBIT_FAILED",
                    plainLanguageSummary: "İniş motoru ateşleme hatası nedeniyle yörüngeden ayrılma manevrası yapılamadı; araç iniş koridorunu kaçırdı.",
                    phaseEvents: events
                );
            }

            events.Add(new EdlPhaseEvent(
                phase: EdlPhaseType.DeorbitBurn,
                elapsedSeconds: 45.0,
                status: EdlPhaseStatus.Nominal,
                titleKey: "EDL_EVENT_DEORBIT_NOMINAL_TITLE",
                narrativeKey: "EDL_EVENT_DEORBIT_NOMINAL_DESC",
                altitudeKm: 100.0,
                velocityMps: 3100.0,
                heatShieldIntegrity: heatShield,
                technicalDetails: "Deorbit burn nominal; entry trajectory locked."
            ));

            // Atmospheric vs Vacuum Divergence
            if (parameters.HasAtmosphere)
            {
                // Phase 2: Atmospheric Entry
                events.Add(new EdlPhaseEvent(
                    phase: EdlPhaseType.AtmosphericEntry,
                    elapsedSeconds: 150.0,
                    status: EdlPhaseStatus.Nominal,
                    titleKey: "EDL_EVENT_ENTRY_NOMINAL_TITLE",
                    narrativeKey: "EDL_EVENT_ENTRY_NOMINAL_DESC",
                    altitudeKm: 60.0,
                    velocityMps: 5800.0,
                    heatShieldIntegrity: heatShield,
                    technicalDetails: $"Atmospheric interface reached at {parameters.AtmospherePressureKpa:F1} kPa entry regime."
                ));

                // Phase 3: Peak Heating
                if (!parameters.HasHeatShield)
                {
                    // No heat shield in atmosphere -> Thermal destruction!
                    events.Add(new EdlPhaseEvent(
                        phase: EdlPhaseType.PeakHeating,
                        elapsedSeconds: 220.0,
                        status: EdlPhaseStatus.Failed,
                        titleKey: "EDL_EVENT_HEATSHIELD_MISSING_TITLE",
                        narrativeKey: "EDL_EVENT_HEATSHIELD_MISSING_DESC",
                        altitudeKm: 35.0,
                        velocityMps: 4200.0,
                        heatShieldIntegrity: 0.0,
                        technicalDetails: "No thermal protection system installed. Hull temperature exceeded catastrophic structural limits."
                    ));

                    return new EdlSimulationResult(
                        outcome: EdlOutcome.ThermalDestruction,
                        isCrewSafe: false,
                        remainingHeatShieldIntegrity: 0.0,
                        touchdownVelocityMps: 4200.0,
                        primaryCauseKey: "EDL_CAUSE_NO_HEATSHIELD",
                        plainLanguageSummary: "Atmosfere girişte aşırı sürtünme ısısı oluştu; araçta ısı kalkanı bulunmadığı için gövde yanarak parçalandı.",
                        phaseEvents: events
                    );
                }

                // Check Venus extreme heating if tier is inadequate
                if (parameters.DestinationId.Equals("venus", StringComparison.OrdinalIgnoreCase) &&
                    !parameters.HeatShieldTier.Equals("mk2", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(new EdlPhaseEvent(
                        phase: EdlPhaseType.PeakHeating,
                        elapsedSeconds: 210.0,
                        status: EdlPhaseStatus.Failed,
                        titleKey: "EDL_EVENT_HEATSHIELD_BURNTHROUGH_TITLE",
                        narrativeKey: "EDL_EVENT_HEATSHIELD_BURNTHROUGH_DESC",
                        altitudeKm: 42.0,
                        velocityMps: 4800.0,
                        heatShieldIntegrity: 0.0,
                        technicalDetails: $"Atmosphere pressure {parameters.AtmospherePressureKpa} kPa exceeded Mk1 thermal shield capacity."
                    ));

                    return new EdlSimulationResult(
                        outcome: EdlOutcome.ThermalDestruction,
                        isCrewSafe: false,
                        remainingHeatShieldIntegrity: 0.0,
                        touchdownVelocityMps: 3500.0,
                        primaryCauseKey: "EDL_CAUSE_HEATSHIELD_BURNTHROUGH",
                        plainLanguageSummary: "Venüs'ün aşırı yoğun atmosferi ve termal yükü standart ısı kalkanını eritti.",
                        phaseEvents: events
                    );
                }

                // Normal heat shield ablation check
                double ablationRoll = rng.NextDouble();
                double ablationDamage = (1.0 - currentReliability) * 0.40;
                heatShield = Math.Max(0.10, 1.0 - ablationDamage - (ablationRoll * 0.15));

                events.Add(new EdlPhaseEvent(
                    phase: EdlPhaseType.PeakHeating,
                    elapsedSeconds: 240.0,
                    status: heatShield < 0.35 ? EdlPhaseStatus.Warning : EdlPhaseStatus.Nominal,
                    titleKey: "EDL_EVENT_PEAK_HEATING_SURVIVED_TITLE",
                    narrativeKey: "EDL_EVENT_PEAK_HEATING_SURVIVED_DESC",
                    altitudeKm: 25.0,
                    velocityMps: 1800.0,
                    heatShieldIntegrity: heatShield,
                    technicalDetails: $"Peak heating survived. Ablative shield integrity: {heatShield:P0}."
                ));

                // Phase 4: Parachute Deployment
                if (parameters.HasParachute)
                {
                    double chuteRoll = rng.NextDouble();
                    double chuteThreshold = Math.Max(0.65, parameters.EdlSystemReliability);

                    if (chuteRoll > chuteThreshold + 0.18)
                    {
                        // Chute shredded or failed to deploy
                        events.Add(new EdlPhaseEvent(
                            phase: EdlPhaseType.ParachuteDeploy,
                            elapsedSeconds: 310.0,
                            status: EdlPhaseStatus.Failed,
                            titleKey: "EDL_EVENT_CHUTE_FAILURE_TITLE",
                            narrativeKey: "EDL_EVENT_CHUTE_FAILURE_DESC",
                            altitudeKm: 8.0,
                            velocityMps: 450.0,
                            heatShieldIntegrity: heatShield,
                            technicalDetails: $"Parachute lines tangled or canopy sheared under aerodynamic pressure (Roll={chuteRoll:F3}, Threshold={chuteThreshold:F3})."
                        ));

                        return new EdlSimulationResult(
                            outcome: EdlOutcome.HighSpeedImpact,
                            isCrewSafe: false,
                            remainingHeatShieldIntegrity: heatShield,
                            touchdownVelocityMps: 320.0,
                            primaryCauseKey: "EDL_CAUSE_CHUTE_FAILURE",
                            plainLanguageSummary: "Paraşüt açılma aşamasında ipler dolandı ve gölgelik parçalandı; araç terminal hızla yüzeye çarptı.",
                            phaseEvents: events
                        );
                    }

                    events.Add(new EdlPhaseEvent(
                        phase: EdlPhaseType.ParachuteDeploy,
                        elapsedSeconds: 310.0,
                        status: EdlPhaseStatus.Nominal,
                        titleKey: "EDL_EVENT_CHUTE_DEPLOY_TITLE",
                        narrativeKey: "EDL_EVENT_CHUTE_DEPLOY_DESC",
                        altitudeKm: 9.0,
                        velocityMps: 85.0,
                        heatShieldIntegrity: heatShield,
                        technicalDetails: "Supersonic parachute canopy fully deployed; velocity dropped below 90 m/s."
                    ));
                }
            }
            else
            {
                // Vacuum Target (e.g. Moon, Europa)
                // If designer foolishly equipped ONLY a parachute without propulsion
                if (parameters.HasParachute && !parameters.HasSkycraneOrRetro && parameters.AvailableLandingDv < 100.0)
                {
                    events.Add(new EdlPhaseEvent(
                        phase: EdlPhaseType.ParachuteDeploy,
                        elapsedSeconds: 180.0,
                        status: EdlPhaseStatus.Failed,
                        titleKey: "EDL_EVENT_VACUUM_CHUTE_TITLE",
                        narrativeKey: "EDL_EVENT_VACUUM_CHUTE_DESC",
                        altitudeKm: 15.0,
                        velocityMps: 1600.0,
                        heatShieldIntegrity: heatShield,
                        technicalDetails: "No atmospheric drag exists. Parachute is completely inoperable in vacuum."
                    ));

                    return new EdlSimulationResult(
                        outcome: EdlOutcome.HighSpeedImpact,
                        isCrewSafe: false,
                        remainingHeatShieldIntegrity: heatShield,
                        touchdownVelocityMps: 1600.0,
                        primaryCauseKey: "EDL_CAUSE_VACUUM_PARACHUTE_INEFFECTIVE",
                        plainLanguageSummary: "Havasız ortamda (vakum) paraşüt açılamaz; itkili iniş motoru bulunmadığı için araç yüzeye çarptı.",
                        phaseEvents: events
                    );
                }
            }

            // Phase 5: Terminal Descent (Skycrane / Retro-rockets / Airbag)
            // Check Propellant Requirement if needed
            if (parameters.RequiredLandingDv > 0.0)
            {
                if (parameters.AvailableLandingDv < parameters.RequiredLandingDv * 0.90)
                {
                    // Insufficient delta-V to brake
                    events.Add(new EdlPhaseEvent(
                        phase: EdlPhaseType.TerminalDescent,
                        elapsedSeconds: 420.0,
                        status: EdlPhaseStatus.Failed,
                        titleKey: "EDL_EVENT_PROP_EXHAUSTED_TITLE",
                        narrativeKey: "EDL_EVENT_PROP_EXHAUSTED_DESC",
                        altitudeKm: 1.5,
                        velocityMps: 120.0,
                        heatShieldIntegrity: heatShield,
                        technicalDetails: $"Available landing Δv ({parameters.AvailableLandingDv:F0} m/s) insufficient for required {parameters.RequiredLandingDv:F0} m/s."
                    ));

                    return new EdlSimulationResult(
                        outcome: EdlOutcome.PropulsionLoss,
                        isCrewSafe: false,
                        remainingHeatShieldIntegrity: heatShield,
                        touchdownVelocityMps: 95.0,
                        primaryCauseKey: "EDL_CAUSE_LANDING_PROPELLANT_DEPLETED",
                        plainLanguageSummary: "İniş itki yakıtı yüzeye 1.5 km kala tükendi; araç sert iniş yaparak kaybedildi.",
                        phaseEvents: events
                    );
                }
            }

            // Terminal Descent System Reliability Check
            double terminalRoll = rng.NextDouble();
            double terminalThreshold = Math.Max(0.70, currentReliability);

            if (terminalRoll > terminalThreshold + 0.15)
            {
                // Retro thruster or skycrane valve failure
                events.Add(new EdlPhaseEvent(
                    phase: EdlPhaseType.TerminalDescent,
                    elapsedSeconds: 460.0,
                    status: EdlPhaseStatus.Failed,
                    titleKey: "EDL_EVENT_RETRO_THRUST_FAILURE_TITLE",
                    narrativeKey: "EDL_EVENT_RETRO_THRUST_FAILURE_DESC",
                    altitudeKm: 0.3,
                    velocityMps: 45.0,
                    heatShieldIntegrity: heatShield,
                    technicalDetails: $"Terminal descent thruster shutdown (Roll={terminalRoll:F3}, Threshold={terminalThreshold:F3})."
                ));

                return new EdlSimulationResult(
                    outcome: EdlOutcome.HighSpeedImpact,
                    isCrewSafe: false,
                    remainingHeatShieldIntegrity: heatShield,
                    touchdownVelocityMps: 45.0,
                    primaryCauseKey: "EDL_CAUSE_THRUSTER_SHUTDOWN",
                    plainLanguageSummary: "Son yaklaşmada iniş itki motoru aniden durdu; araç yüzeye sert çarptı.",
                    phaseEvents: events
                );
            }

            events.Add(new EdlPhaseEvent(
                phase: EdlPhaseType.TerminalDescent,
                elapsedSeconds: 470.0,
                status: EdlPhaseStatus.Nominal,
                titleKey: "EDL_EVENT_TERMINAL_DESCENT_NOMINAL_TITLE",
                narrativeKey: "EDL_EVENT_TERMINAL_DESCENT_NOMINAL_DESC",
                altitudeKm: 0.05,
                velocityMps: 3.2,
                heatShieldIntegrity: heatShield,
                technicalDetails: "Skycrane / retro thrusters firing nominally. Vertical velocity stabilized."
            ));

            // Phase 6: Touchdown
            double touchdownRoll = rng.NextDouble();
            double finalVelocity = 1.2 + (touchdownRoll * 1.8); // 1.2 - 3.0 m/s

            if (currentReliability < 0.75 && touchdownRoll > 0.85)
            {
                // Hard landing - partial success
                events.Add(new EdlPhaseEvent(
                    phase: EdlPhaseType.Touchdown,
                    elapsedSeconds: 490.0,
                    status: EdlPhaseStatus.Damaged,
                    titleKey: "EDL_EVENT_HARD_TOUCHDOWN_TITLE",
                    narrativeKey: "EDL_EVENT_HARD_TOUCHDOWN_DESC",
                    altitudeKm: 0.0,
                    velocityMps: 4.8,
                    heatShieldIntegrity: heatShield,
                    technicalDetails: "Hard touchdown. One landing strut collapsed; secondary antenna damaged but vehicle intact."
                ));

                return new EdlSimulationResult(
                    outcome: EdlOutcome.PartialSuccess,
                    isCrewSafe: true,
                    remainingHeatShieldIntegrity: heatShield,
                    touchdownVelocityMps: 4.8,
                    primaryCauseKey: "EDL_CAUSE_HARD_TOUCHDOWN",
                    plainLanguageSummary: "Yüzeye iniş sert oldu; iniş ayaklarından biri büküldü ancak araç operasyonel kaldı.",
                    phaseEvents: events
                );
            }

            events.Add(new EdlPhaseEvent(
                phase: EdlPhaseType.Touchdown,
                elapsedSeconds: 495.0,
                status: EdlPhaseStatus.Nominal,
                titleKey: "EDL_EVENT_TOUCHDOWN_SUCCESS_TITLE",
                narrativeKey: "EDL_EVENT_TOUCHDOWN_SUCCESS_DESC",
                altitudeKm: 0.0,
                velocityMps: finalVelocity,
                heatShieldIntegrity: heatShield,
                technicalDetails: $"Touchdown confirmed at {finalVelocity:F1} m/s. All surface contact sensors triggered."
            ));

            return new EdlSimulationResult(
                outcome: EdlOutcome.Success,
                isCrewSafe: true,
                remainingHeatShieldIntegrity: heatShield,
                touchdownVelocityMps: finalVelocity,
                primaryCauseKey: "EDL_CAUSE_NOMINAL_LANDING",
                plainLanguageSummary: "EDL sekansı kusursuz tamamlandı; araç hedef yüzeye yumuşak ve güvenli bir şekilde indi.",
                phaseEvents: events
            );
        }
    }
}
