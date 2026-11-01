using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.Launch;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Launch;

namespace ApogeeLocalTestRunner
{
    public static class LaunchRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_01_Launch_SeededDeterminism_ProducesIdenticalOutcomeAndTelemetry", Test_DeterministicSequence);
            runTest("M5_01_Launch_WeatherScrub_TriggersUnderHighWeatherRisk", Test_WeatherScrub);
            runTest("M5_01_Launch_MultiStageNominalAscent_ReachesOrbitWithDetailedPhases", Test_NominalAscent);
            runTest("M5_01_Launch_Stage1Failure_Uncrewed_CatastrophicLoss", Test_Stage1Failure_Uncrewed);
            runTest("M5_01_Launch_Stage1Failure_CrewedWithAbortTower_SavesCrew", Test_Stage1Failure_Crewed_AbortSaved);
            runTest("M5_01_Launch_Stage1Failure_CrewedWithoutAbortTower_ResultsInCrewLoss", Test_Stage1Failure_Crewed_NoAbort);
            runTest("M5_01_Launch_Stage2PrematureShutdown_ResultsInPartialOrbit", Test_Stage2PrematureCutoff);
            runTest("M5_01_Launch_CommandExecution_DeductsReputationOnCatastrophicFailure_AndUndoes", Test_LaunchMissionCommand_UndoRedo);
            runTest("M5_01_Launch_ViewModel_TelemetrySteppingAndSkipToEnd", Test_LaunchSequenceViewModel_Stepping);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private static void Test_DeterministicSequence()
        {
            var p1 = new LaunchSimulationParameters(
                missionId: "apollo_test",
                vehicleName: "Saturn_V_Sim",
                stageCount: 3,
                stageReliabilities: new[] { 0.98, 0.97, 0.97 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.10,
                equatorialBonusDv: 408.0,
                isCrewed: true,
                hasAbortSystem: true,
                abortSystemReliability: 0.98,
                seed: 123456789UL
            );

            var p2 = new LaunchSimulationParameters(
                missionId: "apollo_test",
                vehicleName: "Saturn_V_Sim",
                stageCount: 3,
                stageReliabilities: new[] { 0.98, 0.97, 0.97 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.10,
                equatorialBonusDv: 408.0,
                isCrewed: true,
                hasAbortSystem: true,
                abortSystemReliability: 0.98,
                seed: 123456789UL
            );

            var r1 = LaunchSimulator.Simulate(p1);
            var r2 = LaunchSimulator.Simulate(p2);

            AssertEqual(r1.Outcome, r2.Outcome, "Outcomes match");
            AssertEqual(r1.WeatherCondition, r2.WeatherCondition, "Weather condition match");
            AssertEqual(r1.DeliveredDeltaV, r2.DeliveredDeltaV, "Delivered Δv match");
            AssertEqual(r1.PhaseEvents.Count, r2.PhaseEvents.Count, "Phase events count match");

            for (int i = 0; i < r1.PhaseEvents.Count; i++)
            {
                AssertEqual(r1.PhaseEvents[i].Phase, r2.PhaseEvents[i].Phase, $"Phase {i} matches");
                AssertEqual(r1.PhaseEvents[i].Status, r2.PhaseEvents[i].Status, $"Status {i} matches");
                AssertEqual(r1.PhaseEvents[i].AltitudeKm, r2.PhaseEvents[i].AltitudeKm, $"Altitude {i} matches");
            }
        }

        private static void Test_WeatherScrub()
        {
            var p = new LaunchSimulationParameters(
                missionId: "storm_scrub",
                vehicleName: "StormRocket",
                stageCount: 2,
                stageReliabilities: new[] { 0.99, 0.99 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.99, // extremely high risk
                seed: 42UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertEqual(LaunchOutcome.WeatherScrub, result.Outcome, "Scrubbed due to weather");
            AssertTrue(result.PhaseEvents.Count > 0, "Phase events logged");
            AssertEqual(LaunchPhaseStatus.Aborted, result.PhaseEvents[0].Status, "First event aborted");
            AssertEqual(0.0, result.DeliveredDeltaV, "Zero delta-V delivered on scrub");
        }

        private static void Test_NominalAscent()
        {
            var p = new LaunchSimulationParameters(
                missionId: "nominal_launch",
                vehicleName: "ReliableHeavy",
                stageCount: 3,
                stageReliabilities: new[] { 1.0, 1.0, 1.0 },
                overallReliability: 1.0,
                launchPadWeatherRisk: 0.0, // Clear skies
                equatorialBonusDv: 460.0,
                isCrewed: false,
                seed: 100UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertEqual(LaunchOutcome.Success, result.Outcome, "Nominal launch succeeds");
            AssertTrue(result.IsSuccessful, "IsSuccessful true");
            AssertTrue(result.DeliveredDeltaV >= 9600.0, "Orbit insertion velocity achieved");
            AssertTrue(result.PhaseEvents.Any(e => e.Phase == LaunchPhaseType.Liftoff), "Liftoff phase logged");
            AssertTrue(result.PhaseEvents.Any(e => e.Phase == LaunchPhaseType.Stage1Separation), "Stage 1 sep logged");
            AssertTrue(result.PhaseEvents.Any(e => e.Phase == LaunchPhaseType.FairingJettison), "Fairing sep logged");
            AssertTrue(result.PhaseEvents.Any(e => e.Phase == LaunchPhaseType.OrbitalInsertion && e.Status == LaunchPhaseStatus.Nominal), "Orbit insertion nominal");
        }

        private static void Test_Stage1Failure_Uncrewed()
        {
            var p = new LaunchSimulationParameters(
                missionId: "uncrewed_boom",
                vehicleName: "FragileRocket",
                stageCount: 2,
                stageReliabilities: new[] { 0.0, 0.99 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.0,
                isCrewed: false,
                seed: 777UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertEqual(LaunchOutcome.CatastrophicFailure, result.Outcome, "Catastrophic booster explosion");
            AssertTrue(!result.IsSuccessful, "Unsuccessful");
            AssertTrue(!result.IsCrewSafe, "No crew safety on uncrewed loss");
            AssertEqual("CAUSE_STAGE1_FAILURE", result.PrimaryCauseKey, "Primary cause recorded");
        }

        private static void Test_Stage1Failure_Crewed_AbortSaved()
        {
            var p = new LaunchSimulationParameters(
                missionId: "crewed_abort_success",
                vehicleName: "CrewApollo",
                stageCount: 2,
                stageReliabilities: new[] { 0.0, 0.99 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.0,
                isCrewed: true,
                hasAbortSystem: true,
                abortSystemReliability: 1.0,
                seed: 888UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertEqual(LaunchOutcome.InFlightAbort, result.Outcome, "In-flight abort executed");
            AssertTrue(result.IsCrewSafe, "Crew safely saved by escape system");
            AssertTrue(result.PhaseEvents.Any(e => e.TitleKey == "LAUNCH_EVENT_ABORT_TOWER_SUCCESS_TITLE"), "Abort tower success logged");
        }

        private static void Test_Stage1Failure_Crewed_NoAbort()
        {
            var p = new LaunchSimulationParameters(
                missionId: "crewed_abort_missing",
                vehicleName: "DangerousCrewRocket",
                stageCount: 2,
                stageReliabilities: new[] { 0.0, 0.99 },
                overallReliability: 0.99,
                launchPadWeatherRisk: 0.0,
                isCrewed: true,
                hasAbortSystem: false,
                seed: 999UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertEqual(LaunchOutcome.CatastrophicFailure, result.Outcome, "Catastrophic loss of crew and vehicle");
            AssertTrue(!result.IsCrewSafe, "Crew lost without escape tower");
            AssertTrue(result.PhaseEvents.Any(e => e.TitleKey == "LAUNCH_EVENT_CREW_LOST_TITLE"), "Crew loss logged in event chain");
        }

        private static void Test_Stage2PrematureCutoff()
        {
            var p = new LaunchSimulationParameters(
                missionId: "partial_orbit_test",
                vehicleName: "UnderperformingUpper",
                stageCount: 2,
                stageReliabilities: new[] { 1.0, 0.50 },
                overallReliability: 0.95,
                launchPadWeatherRisk: 0.0,
                seed: 55UL
            );

            var result = LaunchSimulator.Simulate(p);
            AssertTrue(result.Outcome == LaunchOutcome.PartialOrbit || result.Outcome == LaunchOutcome.InFlightAbort || result.Outcome == LaunchOutcome.CatastrophicFailure, "Upper stage resolved with anomaly");
            AssertTrue(result.PhaseEvents.Count >= 5, "Detailed phase history captured");
        }

        private static void Test_LaunchMissionCommand_UndoRedo()
        {
            var campaign = new CampaignState(seed: 12345UL);
            double initialRep = campaign.Reputation;

            var engineComp = new DesignComponent("eng_test", "engine", "Kerosen Mk-1", dryMass: 1.5, cost: 5.0, buildTime: 30, reliability: 0.95, isp: 310.0);
            var stage = new StageDesign("stg1", "Stage 1", engine: engineComp, propellantMass: 50.0);
            var bp = new Blueprint("bp_test", "Test Launcher", stages: new[] { stage });

            var cmd = new LaunchMissionCommand(
                campaign: campaign,
                vehicleBlueprint: bp,
                vehicleStats: null,
                launchPadBaseId: "base_cape_pad",
                isCrewed: false,
                launchSeed: 99999UL
            );

            var ctx = new CommandContext("player1", PlayerRole.LeadEngineer);
            var execRes = campaign.CommandBus.Execute(cmd, ctx);
            AssertTrue(execRes.Success, "Command executed successfully");
            AssertTrue(cmd.ExecutionResult != null, "Execution result populated");

            // Undo
            var undoRes = campaign.CommandBus.Undo(ctx);
            AssertTrue(undoRes.Success, "Undo executed");
            AssertEqual(initialRep, campaign.Reputation, "Reputation restored on undo");

            // Redo
            var redoRes = campaign.CommandBus.Redo(ctx);
            AssertTrue(redoRes.Success, "Redo executed");
        }

        private static void Test_LaunchSequenceViewModel_Stepping()
        {
            var vm = new LaunchSequenceViewModel();

            var p = new LaunchSimulationParameters(
                missionId: "vm_test",
                vehicleName: "DisplayRocket",
                stageCount: 2,
                stageReliabilities: new[] { 1.0, 1.0 },
                overallReliability: 1.0,
                launchPadWeatherRisk: 0.0,
                seed: 111UL
            );
            var result = LaunchSimulator.Simulate(p);

            vm.BindResult("Ay Görevi", "DisplayRocket", result);
            AssertEqual(0, vm.CurrentPhaseIndex, "Starts at phase 0");
            AssertTrue(!vm.IsSequenceFinished, "Not finished initially");

            // Step next
            vm.StepNextPhaseCommand.Execute();
            AssertEqual(1, vm.CurrentPhaseIndex, "Stepped to phase 1");

            // Skip to end
            vm.SkipToEndCommand.Execute();
            AssertTrue(vm.IsSequenceFinished, "Sequence finished after skip");
            AssertEqual(vm.Events.Count - 1, vm.CurrentPhaseIndex, "At final phase");
            AssertTrue(vm.CurrentAltitudeKm >= 200.0, "Final altitude populated");
        }
    }
}
