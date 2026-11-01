using System;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.EDL;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Operations;

namespace ApogeeLocalTestRunner
{
    public static class EdlRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_03_EDL_SeededDeterminism_ProducesIdenticalOutcomeAndTelemetry", Test_Edl_Determinism);
            runTest("M5_03_EDL_LunarVacuumLanding_NominalDescentAndTouchdown", Test_LunarLanding_Nominal);
            runTest("M5_03_EDL_VacuumParachuteFailure_HighSpeedImpact", Test_VacuumParachute_Crashes);
            runTest("M5_03_EDL_MarsAtmosphere_WithoutHeatShield_ThermalDestruction", Test_MarsEntry_MissingHeatShield_BurnsUp);
            runTest("M5_03_EDL_MarsAtmosphere_WithHeatShieldAndParachute_Success", Test_MarsLanding_NominalSuccess);
            runTest("M5_03_EDL_VenusExtremeAtmosphere_Mk1Shield_Burnthrough", Test_VenusEntry_Mk1Shield_Fails);
            runTest("M5_03_EDL_InsufficientLandingDeltaV_PropulsionDepleted", Test_InsufficientLandingDeltaV_PropulsionDepleted);
            runTest("M5_03_EDL_ExecuteEdlCommand_Success_TransitionsToOperatingAndUndoes", Test_ExecuteEdlCommand_Success_UndoRedo);
            runTest("M5_03_EDL_OperationsViewModel_EdlExecutionAndTelemetryBinding", Test_OperationsViewModel_EdlBinding);
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

        private static void Test_Edl_Determinism()
        {
            var p1 = new EdlSimulationParameters(
                missionId: "edl_seed_test",
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: false,
                hasSkycraneOrRetro: true,
                availableLandingDv: 2000.0,
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.94,
                isCrewed: false,
                seed: 44556677UL
            );

            var p2 = new EdlSimulationParameters(
                missionId: "edl_seed_test",
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: false,
                hasSkycraneOrRetro: true,
                availableLandingDv: 2000.0,
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.94,
                isCrewed: false,
                seed: 44556677UL
            );

            var r1 = EdlSimulator.Simulate(p1);
            var r2 = EdlSimulator.Simulate(p2);

            AssertEqual(r1.Outcome, r2.Outcome, "EDL outcome determinism");
            AssertEqual(r1.IsSuccessful, r2.IsSuccessful, "Success status determinism");
            AssertEqual(r1.PhaseEvents.Count, r2.PhaseEvents.Count, "Event count determinism");
            AssertEqual(r1.PrimaryCauseKey, r2.PrimaryCauseKey, "Primary cause determinism");
            AssertEqual(r1.TouchdownVelocityMps, r2.TouchdownVelocityMps, "Touchdown velocity determinism");
        }

        private static void Test_LunarLanding_Nominal()
        {
            var p = new EdlSimulationParameters(
                missionId: "apollo_lunar",
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: false,
                hasSkycraneOrRetro: true,
                availableLandingDv: 2200.0,
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.98,
                edlSystemReliability: 0.96,
                isCrewed: true,
                seed: 12345UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(res.IsSuccessful, "Lunar landing should succeed with retro propulsion");
            AssertEqual(EdlOutcome.Success, res.Outcome, "Expected nominal success");
            AssertTrue(res.IsCrewSafe, "Crew must be safe");
            AssertTrue(res.TouchdownVelocityMps < 5.0, "Touchdown velocity soft (< 5 m/s)");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.TerminalDescent), "Has terminal descent");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.Touchdown), "Has touchdown phase");
        }

        private static void Test_VacuumParachute_Crashes()
        {
            var p = new EdlSimulationParameters(
                missionId: "vacuum_chute_fail",
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: true, // Fatal design mistake: parachute in vacuum
                hasSkycraneOrRetro: false,
                availableLandingDv: 0.0,
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.95,
                isCrewed: false,
                seed: 88888UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(!res.IsSuccessful, "Parachute in vacuum must fail landing");
            AssertEqual(EdlOutcome.HighSpeedImpact, res.Outcome, "Expected HighSpeedImpact crash");
            AssertEqual("EDL_CAUSE_VACUUM_PARACHUTE_INEFFECTIVE", res.PrimaryCauseKey, "Accurate cause key");
            AssertTrue(res.PlainLanguageSummary.Contains("vakum") || res.PlainLanguageSummary.Contains("Havasız"), "Educational explanation present");
        }

        private static void Test_MarsEntry_MissingHeatShield_BurnsUp()
        {
            var p = new EdlSimulationParameters(
                missionId: "mars_no_shield",
                destinationId: "mars",
                hasAtmosphere: true,
                atmospherePressureKpa: 0.636,
                hasHeatShield: false, // Fatal: no heatshield entering atmosphere
                heatShieldTier: "none",
                hasParachute: true,
                hasSkycraneOrRetro: true,
                availableLandingDv: 800.0,
                requiredLandingDv: 600.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.95,
                isCrewed: false,
                seed: 11111UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(!res.IsSuccessful, "Entry without heat shield must fail");
            AssertEqual(EdlOutcome.ThermalDestruction, res.Outcome, "Expected ThermalDestruction");
            AssertEqual("EDL_CAUSE_NO_HEATSHIELD", res.PrimaryCauseKey, "Primary cause key");
            AssertTrue(res.RemainingHeatShieldIntegrity == 0.0, "Zero shield integrity");
        }

        private static void Test_MarsLanding_NominalSuccess()
        {
            var p = new EdlSimulationParameters(
                missionId: "mars_rover_edl",
                destinationId: "mars",
                hasAtmosphere: true,
                atmospherePressureKpa: 0.636,
                hasHeatShield: true,
                heatShieldTier: "mk1",
                hasParachute: true,
                hasSkycraneOrRetro: true,
                availableLandingDv: 900.0,
                requiredLandingDv: 600.0,
                spacecraftReliability: 0.98,
                edlSystemReliability: 0.96,
                isCrewed: false,
                seed: 77777UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(res.IsSuccessful, "Mars rover with heat shield + chute + skycrane must succeed");
            AssertTrue(res.RemainingHeatShieldIntegrity > 0.10, "Remaining shield integrity > 10%");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.AtmosphericEntry), "Entry event logged");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.PeakHeating), "Peak heating event logged");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.ParachuteDeploy), "Parachute event logged");
            AssertTrue(res.PhaseEvents.Any(e => e.Phase == EdlPhaseType.Touchdown), "Touchdown event logged");
        }

        private static void Test_VenusEntry_Mk1Shield_Fails()
        {
            var p = new EdlSimulationParameters(
                missionId: "venus_probe_edl",
                destinationId: "venus",
                hasAtmosphere: true,
                atmospherePressureKpa: 9200.0,
                hasHeatShield: true,
                heatShieldTier: "mk1", // Mk1 is inadequate for Venus extreme pressure/heat
                hasParachute: true,
                hasSkycraneOrRetro: false,
                availableLandingDv: 0.0,
                requiredLandingDv: 0.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.95,
                isCrewed: false,
                seed: 99999UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(!res.IsSuccessful, "Venus requires Mk2 extreme shielding");
            AssertEqual(EdlOutcome.ThermalDestruction, res.Outcome, "Expected burnthrough");
            AssertEqual("EDL_CAUSE_HEATSHIELD_BURNTHROUGH", res.PrimaryCauseKey, "Cause key burnthrough");
        }

        private static void Test_InsufficientLandingDeltaV_PropulsionDepleted()
        {
            var p = new EdlSimulationParameters(
                missionId: "lunar_out_of_fuel",
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: false,
                hasSkycraneOrRetro: true,
                availableLandingDv: 400.0, // Insufficient for Moon 1700 m/s
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.95,
                edlSystemReliability: 0.95,
                isCrewed: false,
                seed: 55555UL
            );

            var res = EdlSimulator.Simulate(p);

            AssertTrue(!res.IsSuccessful, "Insufficient landing propellant must fail");
            AssertEqual(EdlOutcome.PropulsionLoss, res.Outcome, "Expected PropulsionLoss");
            AssertEqual("EDL_CAUSE_LANDING_PROPELLANT_DEPLETED", res.PrimaryCauseKey, "Cause key");
        }

        private static void Test_ExecuteEdlCommand_Success_UndoRedo()
        {
            var campaign = new CampaignState(seed: 1234UL);
            campaign.Reputation = 50.0;
            var ctx = new CommandContext("player1", PlayerRole.AgencyDirector);

            var bp = new Blueprint("lunar_craft", "Lunar Lander");
            var edlComp = new DesignComponent("edl-lunar-skycrane", "Skycrane", "edl", 0.65, 8.5, 45, 0.95);
            bp = bp.WithSlotComponent(VehicleSlotId.PayloadEdl, edlComp);

            var mission = new MissionInstance("mis_lunar_01", "Luna Explorer", "moon", bp, 3, 1001UL)
            {
                Status = MissionStatus.Arrival
            };
            campaign.Missions["mis_lunar_01"] = mission;

            var cmd = new ExecuteEdlCommand(campaign, "mis_lunar_01", spacecraftReliability: 0.95);
            var res = campaign.CommandBus.Execute(cmd, ctx);

            AssertTrue(res.Success, "ExecuteEdlCommand should succeed: " + res.Message);
            AssertEqual(MissionStatus.Operating, mission.Status, "Mission status should transition to Operating");
            AssertTrue(campaign.Reputation > 50.0, "Reputation should increase on landing success");
            AssertTrue(mission.EdlResult != null, "EdlResult attached to mission");

            // Undo
            var undoRes = campaign.CommandBus.Undo(ctx);
            AssertTrue(undoRes.Success, "Undo should succeed");
            AssertEqual(MissionStatus.Arrival, mission.Status, "Status reverted to Arrival");
            AssertEqual(50.0, campaign.Reputation, "Reputation restored");

            // Redo
            var redoRes = campaign.CommandBus.Redo(ctx);
            AssertTrue(redoRes.Success, "Redo should succeed");
            AssertEqual(MissionStatus.Operating, mission.Status, "Status re-applied to Operating");
        }

        private static void Test_OperationsViewModel_EdlBinding()
        {
            var campaign = new CampaignState(seed: 555UL);
            var bp = new Blueprint("test_rover", "Lunar Rover");
            var edlComp = new DesignComponent("edl-lunar-skycrane", "Skycrane", "edl", 0.65, 8.5, 45, 0.95);
            bp = bp.WithSlotComponent(VehicleSlotId.PayloadEdl, edlComp);

            var mission = new MissionInstance("mis_test_01", "Silver Rover", "moon", bp, 3, 5005UL)
            {
                Status = MissionStatus.Arrival
            };
            campaign.Missions["mis_test_01"] = mission;

            var vm = new OperationsViewModel();
            var navArgs = new OperationsNavigationArgs("mis_test_01", "Silver Rover", "moon", bp, 3, 0.95, 5005UL);
            vm.BindMission(campaign, "mis_test_01", navArgs);

            AssertEqual("ARRIVAL", vm.MissionStatusText.Value, "ViewModel status matches arrival");
            AssertTrue(vm.CanExecuteEdl.Value, "CanExecuteEdl must be true when in Arrival phase");
            AssertTrue(!vm.CanAdvance.Value, "CanAdvance cruise should be false when in Arrival phase");

            // Execute EDL via ViewModel command
            vm.ExecuteEdlStepCommand.Execute();

            AssertEqual("OPERATING", vm.MissionStatusText.Value, "ViewModel status reflects OPERATING");
            AssertTrue(vm.EdlEventLog.Count > 0, "EDL event log populated in ViewModel");
            AssertTrue(vm.IsComplete.Value, "Mission is marked complete / operational");
        }
    }
}
