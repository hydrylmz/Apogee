using System;
using System.Collections.Generic;
using Apogee.Core.Audio;
using Apogee.Core.Simulation.Cruise;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Science;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Pure C# factory that converts raw simulation results into an ordered array of
    /// <see cref="FlightBeat"/> objects ready for <see cref="PlaybackClock"/>.
    ///
    /// Rules:
    /// - No UnityEngine types — fully testable in EditMode.
    /// - Deterministic: same inputs → same beat array (replay safety).
    /// - Unknown phase/status → generic beat, never throws.
    /// - Skip-to-end produces identical final-state telemetry as natural playback.
    /// - Durations come from <see cref="FlightTimelineLimits"/>; never hardcoded here.
    ///
    /// GDD §3.4, §10.1, §15.1.
    /// </summary>
    public static class FlightTimeline
    {
        // ─────────────────────────────────────────────────────────────────────
        // LAUNCH
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a beat array from a completed <see cref="LaunchSimulationResult"/>.
        /// Total play time at 1× speed is capped to <see cref="FlightTimelineLimits.LaunchMaxSeconds"/>.
        /// </summary>
        public static FlightBeat[] FromLaunch(
            LaunchSimulationResult result,
            FlightTimelineLimits limits = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            limits ??= FlightTimelineLimits.Default;

            var beats = new List<FlightBeat>();
            var events = result.PhaseEvents;

            if (events == null || events.Count == 0)
            {
                beats.Add(MakeGenericBeat("Launch", result.Outcome.ToString(), 0, 0, 0, limits.LaunchMaxSeconds));
                beats[^1] = MakeTerminalLaunchBeat(result, limits.LaunchMaxSeconds);
                return beats.ToArray();
            }

            // Distribute total allowed duration across events
            float perBeatDur = Math.Max(1f, limits.LaunchMaxSeconds / events.Count);

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                bool isLast = (i == events.Count - 1);

                string camKey    = LaunchCameraKey(ev.Phase, ev.Status);
                string fxKey     = LaunchFxKey(ev.Phase, ev.Status);
                AudioEvent audio = LaunchAudio(ev.Phase, ev.Status);

                var beat = new FlightBeat(
                    phaseId:            ev.Phase.ToString(),
                    statusId:           ev.Status.ToString(),
                    titleKey:           ev.TitleKey,
                    narrativeKey:       ev.NarrativeKey,
                    technicalDetails:   ev.TechnicalDetails,
                    timestampSeconds:   ev.TimestampSeconds,
                    altitudeKm:         ev.AltitudeKm,
                    velocityMps:        ev.VelocityMps,
                    heatShieldIntegrity:0.0,
                    cruiseProgress:     0.0,
                    cameraMotionKey:    camKey,
                    visualEffectKey:    fxKey,
                    audioEventId:       audio,
                    durationSeconds:    perBeatDur,
                    requiresDecision:   false,
                    isTerminal:         isLast,
                    outcomeTag:         isLast ? result.Outcome.ToString() : null);

                beats.Add(beat);
            }

            return beats.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // CRUISE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build beats from a list of <see cref="CruiseSegmentEvent"/> objects.
        /// Each segment gets <see cref="FlightTimelineLimits.CruiseSegmentMaxSeconds"/> at 1×.
        /// </summary>
        public static FlightBeat[] FromCruise(
            IReadOnlyList<CruiseSegmentEvent> segments,
            FlightTimelineLimits limits = null)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            limits ??= FlightTimelineLimits.Default;

            if (segments.Count == 0)
                return new[] { MakeGenericBeat("Cruise", "Nominal", 0, 0, 0, limits.CruiseSegmentMaxSeconds) };

            var beats = new List<FlightBeat>(segments.Count);

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                bool isLast = (i == segments.Count - 1);

                string camKey    = CruiseCameraKey(seg.Status);
                string fxKey     = CruiseFxKey(seg.Status);
                AudioEvent audio = CruiseAudio(seg.Status);

                // Cruise timestamps are expressed as progress-based virtual "day"
                double virtualTimestamp = seg.Progress * 1000.0; // kept for log panel sorting

                var beat = new FlightBeat(
                    phaseId:            "Cruise." + seg.Status,
                    statusId:           seg.Status.ToString(),
                    titleKey:           seg.TitleKey,
                    narrativeKey:       seg.NarrativeKey,
                    technicalDetails:   seg.TechnicalDetails,
                    timestampSeconds:   virtualTimestamp,
                    altitudeKm:         0.0,
                    velocityMps:        0.0,
                    heatShieldIntegrity:0.0,
                    cruiseProgress:     seg.Progress,
                    cameraMotionKey:    camKey,
                    visualEffectKey:    fxKey,
                    audioEventId:       audio,
                    durationSeconds:    limits.CruiseSegmentMaxSeconds,
                    requiresDecision:   seg.RequiresDecision,
                    isTerminal:         isLast,
                    outcomeTag:         isLast ? seg.Status.ToString() : null);

                beats.Add(beat);
            }

            return beats.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // EDL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build beats from a completed <see cref="EdlSimulationResult"/>.
        /// Total play time capped to <see cref="FlightTimelineLimits.EdlMaxSeconds"/>.
        /// </summary>
        public static FlightBeat[] FromEdl(
            EdlSimulationResult result,
            bool hasAtmosphere,
            FlightTimelineLimits limits = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            limits ??= FlightTimelineLimits.Default;

            var events = result.PhaseEvents;
            if (events == null || events.Count == 0)
            {
                return new[] { MakeTerminalEdlBeat(result, limits.EdlMaxSeconds) };
            }

            float perBeatDur = Math.Max(1f, limits.EdlMaxSeconds / events.Count);
            var beats = new List<FlightBeat>(events.Count);

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                bool isLast = (i == events.Count - 1);

                // Skip atmosphere-only beats if target has no atmosphere
                string fxKey  = EdlFxKey(ev.Phase, ev.Status, hasAtmosphere);
                string camKey = EdlCameraKey(ev.Phase);
                AudioEvent audio = EdlAudio(ev.Phase, ev.Status);

                var beat = new FlightBeat(
                    phaseId:            "EDL." + ev.Phase,
                    statusId:           ev.Status.ToString(),
                    titleKey:           ev.TitleKey,
                    narrativeKey:       ev.NarrativeKey,
                    technicalDetails:   ev.TechnicalDetails,
                    timestampSeconds:   ev.ElapsedSeconds,
                    altitudeKm:         ev.AltitudeKm,
                    velocityMps:        ev.VelocityMps,
                    heatShieldIntegrity:ev.HeatShieldIntegrity,
                    cruiseProgress:     0.0,
                    cameraMotionKey:    camKey,
                    visualEffectKey:    fxKey,
                    audioEventId:       audio,
                    durationSeconds:    perBeatDur,
                    requiresDecision:   false,
                    isTerminal:         isLast,
                    outcomeTag:         isLast ? result.Outcome.ToString() : null);

                beats.Add(beat);
            }

            return beats.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCIENCE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build beats from a completed <see cref="ScienceSimulationResult"/>.
        /// Total play time capped to <see cref="FlightTimelineLimits.ScienceMaxSeconds"/>.
        /// Only fields actually produced by the simulator are reflected; nothing is invented.
        /// </summary>
        public static FlightBeat[] FromScience(
            ScienceSimulationResult result,
            FlightTimelineLimits limits = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            limits ??= FlightTimelineLimits.Default;

            var events = result.Events;
            if (events == null || events.Count == 0)
            {
                // Minimal generic beat — no data invented
                return new[]
                {
                    new FlightBeat(
                        phaseId:            "Science.Generic",
                        statusId:           result.HadPowerDeficit || result.HadThermalOverload ? "Degraded" : "Nominal",
                        titleKey:           "SCI_EVENT_GENERIC_TITLE",
                        narrativeKey:       "SCI_EVENT_GENERIC_DESC",
                        technicalDetails:   result.PlainLanguageSummary,
                        timestampSeconds:   result.DayNumber,
                        altitudeKm:         0.0,
                        velocityMps:        0.0,
                        heatShieldIntegrity:0.0,
                        cruiseProgress:     0.0,
                        cameraMotionKey:    "science_generic",
                        visualEffectKey:    "science_data_collect",
                        audioEventId:       AudioEvent.TelemetryPing,
                        durationSeconds:    limits.ScienceMaxSeconds,
                        isTerminal:         true,
                        outcomeTag:         result.HadPowerDeficit ? "PowerDeficit"
                                          : result.HadThermalOverload ? "ThermalOverload"
                                          : "Nominal")
                };
            }

            float perBeatDur = Math.Max(1f, limits.ScienceMaxSeconds / events.Count);
            var beats = new List<FlightBeat>(events.Count);

            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                bool isLast = (i == events.Count - 1);

                string fxKey     = ScienceFxKey(ev.EventType, ev.Status);
                AudioEvent audio = ScienceAudio(ev.EventType, ev.Status);

                var beat = new FlightBeat(
                    phaseId:            "Science." + ev.EventType,
                    statusId:           ev.Status.ToString(),
                    titleKey:           string.IsNullOrEmpty(ev.TitleKey) ? "SCI_EVENT_GENERIC_TITLE" : ev.TitleKey,
                    narrativeKey:       "SCI_EVENT_GENERIC_DESC", // NarrativeKey not on SciencePhaseEvent — TODO-VERİ
                    technicalDetails:   ev.Description,
                    timestampSeconds:   ev.DayNumber,
                    altitudeKm:         0.0,
                    velocityMps:        0.0,
                    heatShieldIntegrity:0.0,
                    cruiseProgress:     0.0,
                    cameraMotionKey:    "science_generic",
                    visualEffectKey:    fxKey,
                    audioEventId:       audio,
                    durationSeconds:    perBeatDur,
                    isTerminal:         isLast,
                    outcomeTag:         isLast ? (result.HadPowerDeficit ? "PowerDeficit"
                                              : result.HadThermalOverload ? "ThermalOverload"
                                              : "Nominal") : null);

                beats.Add(beat);
            }

            return beats.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Camera / FX / Audio key tables
        // (data-driven overrides come from FlightChoreography; these are safe fallbacks)
        // ─────────────────────────────────────────────────────────────────────

        private static string LaunchCameraKey(LaunchPhaseType phase, LaunchPhaseStatus status)
        {
            if (status == LaunchPhaseStatus.CriticalFailure || status == LaunchPhaseStatus.Aborted)
                return "launch_abort";

            return phase switch
            {
                LaunchPhaseType.Ignition          => "launch_pad_close",
                LaunchPhaseType.Liftoff           => "launch_liftoff",
                LaunchPhaseType.MaxQ              => "launch_maxq",
                LaunchPhaseType.Stage1Burn        => "launch_ascent",
                LaunchPhaseType.Stage1Separation  => "launch_staging",
                LaunchPhaseType.Stage2Ignition    => "launch_staging",
                LaunchPhaseType.FairingJettison   => "launch_fairing",
                LaunchPhaseType.Stage2Burn        => "launch_ascent_high",
                LaunchPhaseType.Stage2Separation  => "launch_staging",
                LaunchPhaseType.Stage3Ignition    => "launch_staging",
                LaunchPhaseType.Stage3Burn        => "launch_ascent_high",
                LaunchPhaseType.OrbitalInsertion  => "launch_orbit",
                _                                 => "generic"
            };
        }

        private static string LaunchFxKey(LaunchPhaseType phase, LaunchPhaseStatus status)
        {
            if (status == LaunchPhaseStatus.CriticalFailure) return "fx_explosion";
            if (status == LaunchPhaseStatus.Aborted) return "fx_abort";

            return phase switch
            {
                LaunchPhaseType.Ignition         => "fx_ignition",
                LaunchPhaseType.Liftoff          => "fx_liftoff_steam",
                LaunchPhaseType.MaxQ             => "fx_maxq_cone",
                LaunchPhaseType.Stage1Separation => "fx_stage_sep",
                LaunchPhaseType.Stage2Ignition   => "fx_stage_ignite",
                LaunchPhaseType.FairingJettison  => "fx_fairing",
                LaunchPhaseType.Stage2Separation => "fx_stage_sep",
                LaunchPhaseType.Stage3Ignition   => "fx_stage_ignite",
                LaunchPhaseType.OrbitalInsertion => "fx_orbit_ring",
                _                                => "none"
            };
        }

        private static AudioEvent LaunchAudio(LaunchPhaseType phase, LaunchPhaseStatus status)
        {
            if (status == LaunchPhaseStatus.CriticalFailure) return AudioEvent.AbortAlarm;
            if (status == LaunchPhaseStatus.Aborted)         return AudioEvent.AbortAlarm;

            return phase switch
            {
                LaunchPhaseType.Ignition         => AudioEvent.RocketIgnition,
                LaunchPhaseType.Liftoff          => AudioEvent.RocketIgnition,
                LaunchPhaseType.Stage1Separation => AudioEvent.StageSeparation,
                LaunchPhaseType.Stage2Ignition   => AudioEvent.RocketIgnition,
                LaunchPhaseType.Stage2Separation => AudioEvent.StageSeparation,
                LaunchPhaseType.Stage3Ignition   => AudioEvent.RocketIgnition,
                LaunchPhaseType.OrbitalInsertion => AudioEvent.OrbitInsertion,
                _                                => AudioEvent.TelemetryPing
            };
        }

        private static string CruiseCameraKey(CruiseSegmentStatus status) => status switch
        {
            CruiseSegmentStatus.DecisionRequired => "cruise_decision",
            CruiseSegmentStatus.CorrectionBurn   => "cruise_burn",
            CruiseSegmentStatus.CriticalFailure  => "cruise_signal_loss",
            _                                    => "cruise_transit"
        };

        private static string CruiseFxKey(CruiseSegmentStatus status) => status switch
        {
            CruiseSegmentStatus.Nominal          => "fx_cruise_nominal",
            CruiseSegmentStatus.MinorAnomaly     => "fx_warning_blink",
            CruiseSegmentStatus.DecisionRequired => "fx_decision_card",
            CruiseSegmentStatus.CorrectionBurn   => "fx_correction_burn",
            CruiseSegmentStatus.Degraded         => "fx_warning_blink",
            CruiseSegmentStatus.CriticalFailure  => "fx_signal_static",
            _                                    => "none"
        };

        private static AudioEvent CruiseAudio(CruiseSegmentStatus status) => status switch
        {
            CruiseSegmentStatus.DecisionRequired => AudioEvent.EventNotification,
            CruiseSegmentStatus.CorrectionBurn   => AudioEvent.TelemetryPing,
            CruiseSegmentStatus.CriticalFailure  => AudioEvent.AbortAlarm,
            _                                    => AudioEvent.TelemetryPing
        };

        private static string EdlCameraKey(EdlPhaseType phase) => phase switch
        {
            EdlPhaseType.DeorbitBurn      => "edl_deorbit",
            EdlPhaseType.AtmosphericEntry => "edl_entry",
            EdlPhaseType.PeakHeating      => "edl_heating",
            EdlPhaseType.ParachuteDeploy  => "edl_chute",
            EdlPhaseType.TerminalDescent  => "edl_descent",
            EdlPhaseType.Touchdown        => "edl_touchdown",
            _                             => "generic"
        };

        private static string EdlFxKey(EdlPhaseType phase, EdlPhaseStatus status, bool hasAtmosphere)
        {
            if (status == EdlPhaseStatus.Failed) return "fx_edl_fail";

            if (!hasAtmosphere)
            {
                // No atmosphere: skip aero fx, use powered-descent equivalents
                if (phase == EdlPhaseType.AtmosphericEntry) return "fx_edl_descent_vac";
                if (phase == EdlPhaseType.PeakHeating)      return "fx_edl_descent_vac";
                if (phase == EdlPhaseType.ParachuteDeploy)  return "fx_edl_retro_ignite";
            }

            return phase switch
            {
                EdlPhaseType.DeorbitBurn      => "fx_retro_burn",
                EdlPhaseType.AtmosphericEntry => "fx_entry_heating",
                EdlPhaseType.PeakHeating      => "fx_peak_heating",
                EdlPhaseType.ParachuteDeploy  => "fx_chute_deploy",
                EdlPhaseType.TerminalDescent  => "fx_retro_ignite",
                EdlPhaseType.Touchdown        => "fx_touchdown_dust",
                _                             => "none"
            };
        }

        private static AudioEvent EdlAudio(EdlPhaseType phase, EdlPhaseStatus status)
        {
            if (status == EdlPhaseStatus.Failed) return AudioEvent.AbortAlarm;

            return phase switch
            {
                EdlPhaseType.DeorbitBurn      => AudioEvent.TelemetryPing,
                EdlPhaseType.AtmosphericEntry => AudioEvent.AtmosphericEntryRumble,
                EdlPhaseType.PeakHeating      => AudioEvent.AtmosphericEntryRumble,
                EdlPhaseType.ParachuteDeploy  => AudioEvent.ParachuteDeployment,
                EdlPhaseType.TerminalDescent  => AudioEvent.RetroThrust,
                EdlPhaseType.Touchdown        => AudioEvent.LandingSuccess,
                _                             => AudioEvent.TelemetryPing
            };
        }

        private static string ScienceFxKey(ScienceEventType eventType, ScienceEventStatus status)
        {
            if (status == ScienceEventStatus.Failure) return "fx_science_fail";

            return eventType switch
            {
                ScienceEventType.DataCollection      => "fx_science_data_collect",
                ScienceEventType.DownlinkCompleted   => "fx_science_downlink",
                ScienceEventType.GroundStationContact=> "fx_science_contact",
                ScienceEventType.BufferOverflow      => "fx_warning_blink",
                ScienceEventType.SubsystemDegradation=> "fx_warning_blink",
                ScienceEventType.ObjectiveProgress   => "fx_science_progress",
                _                                    => "fx_science_data_collect"
            };
        }

        private static AudioEvent ScienceAudio(ScienceEventType eventType, ScienceEventStatus status)
        {
            if (status == ScienceEventStatus.Failure) return AudioEvent.AbortAlarm;

            return eventType switch
            {
                ScienceEventType.DownlinkCompleted    => AudioEvent.TelemetryPing,
                ScienceEventType.GroundStationContact => AudioEvent.TelemetryPing,
                ScienceEventType.ObjectiveProgress    => AudioEvent.EventNotification,
                _                                     => AudioEvent.TelemetryPing
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static FlightBeat MakeGenericBeat(
            string phase, string status,
            double ts, double alt, double vel, float dur) =>
            new FlightBeat(
                phaseId: phase, statusId: status,
                titleKey: "FLIGHT_GENERIC_TITLE", narrativeKey: "FLIGHT_GENERIC_DESC",
                technicalDetails: string.Empty,
                timestampSeconds: ts, altitudeKm: alt, velocityMps: vel,
                heatShieldIntegrity: 0, cruiseProgress: 0,
                cameraMotionKey: "generic", visualEffectKey: "none",
                audioEventId: AudioEvent.TelemetryPing,
                durationSeconds: dur);

        private static FlightBeat MakeTerminalLaunchBeat(LaunchSimulationResult result, float dur) =>
            new FlightBeat(
                phaseId: "Launch.Terminal", statusId: result.Outcome.ToString(),
                titleKey: "LAUNCH_OUTCOME_TITLE_" + result.Outcome.ToString().ToUpperInvariant(),
                narrativeKey: "LAUNCH_OUTCOME_DESC_" + result.Outcome.ToString().ToUpperInvariant(),
                technicalDetails: result.PlainLanguageSummary,
                timestampSeconds: 0, altitudeKm: 0, velocityMps: 0,
                heatShieldIntegrity: 0, cruiseProgress: 0,
                cameraMotionKey: "launch_orbit", visualEffectKey: "none",
                audioEventId: result.Outcome == LaunchOutcome.Success ? AudioEvent.OrbitInsertion : AudioEvent.AbortAlarm,
                durationSeconds: dur, isTerminal: true, outcomeTag: result.Outcome.ToString());

        private static FlightBeat MakeTerminalEdlBeat(EdlSimulationResult result, float dur) =>
            new FlightBeat(
                phaseId: "EDL.Terminal", statusId: result.Outcome.ToString(),
                titleKey: "EDL_OUTCOME_TITLE_" + result.Outcome.ToString().ToUpperInvariant(),
                narrativeKey: "EDL_OUTCOME_DESC_" + result.Outcome.ToString().ToUpperInvariant(),
                technicalDetails: result.PlainLanguageSummary,
                timestampSeconds: 0, altitudeKm: 0, velocityMps: result.TouchdownVelocityMps,
                heatShieldIntegrity: result.RemainingHeatShieldIntegrity, cruiseProgress: 0,
                cameraMotionKey: "edl_touchdown", visualEffectKey: "none",
                audioEventId: result.Outcome == EdlOutcome.Success ? AudioEvent.LandingSuccess : AudioEvent.LandingCrash,
                durationSeconds: dur, isTerminal: true, outcomeTag: result.Outcome.ToString());
    }
}
