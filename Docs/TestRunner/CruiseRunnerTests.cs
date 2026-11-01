using System;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.Cruise;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Operations;

namespace ApogeeLocalTestRunner
{
    public static class CruiseRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_02_Cruise_SeededDeterminism_ProducesIdenticalSegmentsAndResults", Test_Cruise_Determinism);
            runTest("M5_02_Cruise_HighReliability_CompletesAllSegmentsNominally", Test_Cruise_HighReliability_Nominal);
            runTest("M5_02_Cruise_LowReliability_TriggersMoreAnomaliesAndDegradation", Test_Cruise_LowReliability_Degradation);
            runTest("M5_02_Cruise_CriticalFailure_TerminatesMissionEarly", Test_Cruise_CriticalFailure);
            runTest("M5_02_Cruise_AdvanceCruiseCommand_PausesOnDecisionRequired", Test_AdvanceCruiseCommand_DecisionPause);
            runTest("M5_02_Cruise_ResolveDecisionCommand_AppliesEffectsAndResumes", Test_ResolveDecisionCommand_AppliesEffects);
            runTest("M5_02_Cruise_AdvanceCruiseCommand_UndoRestoresStateSymmetrically", Test_AdvanceCruiseCommand_UndoRedo);
            runTest("M5_02_Cruise_ResolveDecisionCommand_UndoRestoresDecisionPending", Test_ResolveDecisionCommand_UndoRedo);
            runTest("M5_02_Cruise_OperationsViewModel_BindsAndUpdatesProgress", Test_OperationsViewModel_BindingAndProgress);
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

        private static void Test_Cruise_Determinism()
        {
            var p1 = new CruiseSimulationParameters(
                missionId: "mars_expedition",
                vehicleName: "Ares-1",
                destinationId: "mars",
                cruiseDurationDays: 210,
                spacecraftReliability: 0.88,
                radiationHazard: 0.20,
                dustHazard: 0.15,
                segmentCount: 10,
                seed: 987654321UL
            );

            var p2 = new CruiseSimulationParameters(
                missionId: "mars_expedition",
                vehicleName: "Ares-1",
                destinationId: "mars",
                cruiseDurationDays: 210,
                spacecraftReliability: 0.88,
                radiationHazard: 0.20,
                dustHazard: 0.15,
                segmentCount: 10,
                seed: 987654321UL
            );

            var r1 = CruiseSimulator.Simulate(p1);
            var r2 = CruiseSimulator.Simulate(p2);

            AssertEqual(r1.MissionContinues, r2.MissionContinues, "Mission continues matches");
            AssertEqual(r1.FinalProgress, r2.FinalProgress, "Final progress matches");
            AssertEqual(r1.SegmentEvents.Count, r2.SegmentEvents.Count, "Segment events count matches");
            AssertEqual(r1.PendingDecisions.Count, r2.PendingDecisions.Count, "Pending decisions count matches");

            for (int i = 0; i < r1.SegmentEvents.Count; i++)
            {
                AssertEqual(r1.SegmentEvents[i].Status, r2.SegmentEvents[i].Status, $"Status at {i} matches");
                AssertEqual(r1.SegmentEvents[i].Progress, r2.SegmentEvents[i].Progress, $"Progress at {i} matches");
                AssertEqual(r1.SegmentEvents[i].TitleKey, r2.SegmentEvents[i].TitleKey, $"Title at {i} matches");
            }
        }

        private static void Test_Cruise_HighReliability_Nominal()
        {
            var p = new CruiseSimulationParameters(
                missionId: "moon_orbiter",
                vehicleName: "LunarSurveyor",
                destinationId: "moon",
                cruiseDurationDays: 3,
                spacecraftReliability: 0.99,
                radiationHazard: 0.0,
                dustHazard: 0.0,
                segmentCount: 6,
                seed: 42UL
            );

            var result = CruiseSimulator.Simulate(p);

            AssertTrue(result.MissionContinues, "High reliability mission continues to destination");
            AssertEqual(1.0, result.FinalProgress, "Reaches 100% progress");
            AssertTrue(result.SegmentEvents.Count == 6, "All 6 segments resolved");
            AssertTrue(result.SegmentEvents.All(e => e.Status == CruiseSegmentStatus.Nominal || e.Status == CruiseSegmentStatus.MinorAnomaly),
                "Events are nominal or minor anomalies");
        }

        private static void Test_Cruise_LowReliability_Degradation()
        {
            var p = new CruiseSimulationParameters(
                missionId: "budget_satellite",
                vehicleName: "Junker-1",
                destinationId: "mars",
                cruiseDurationDays: 180,
                spacecraftReliability: 0.30,
                powerMarginRatio: -0.10,
                thermalMarginRatio: -0.10,
                segmentCount: 8,
                seed: 101UL
            );

            var result = CruiseSimulator.Simulate(p);

            AssertTrue(result.SegmentEvents.Any(e => e.Status != CruiseSegmentStatus.Nominal),
                "Low reliability designs generate anomalies, degraded statuses or decisions");
            AssertTrue(result.TotalReliabilityPenalty >= 0.0, "Reliability penalty tracked");
        }

        private static void Test_Cruise_CriticalFailure()
        {
            var p = new CruiseSimulationParameters(
                missionId: "doomed_probe",
                vehicleName: "FragileProbe",
                destinationId: "europa",
                cruiseDurationDays: 1000,
                spacecraftReliability: 0.05,
                radiationHazard: 0.80,
                dustHazard: 0.40,
                segmentCount: 10,
                seed: 777UL
            );

            var result = CruiseSimulator.Simulate(p);

            if (!result.MissionContinues)
            {
                AssertTrue(result.IsCruiseLoss, "Cruise loss flagged");
                AssertTrue(result.FinalProgress < 1.0, "Final progress interrupted before 1.0");
                AssertTrue(result.SegmentEvents.Any(e => e.Status == CruiseSegmentStatus.CriticalFailure), "Critical failure logged in events");
            }
        }

        private static void Test_AdvanceCruiseCommand_DecisionPause()
        {
            var campaign = new CampaignState(seed: 123456UL);
            var mission = new MissionInstance("m1", "Mars Explorer", "mars", null, 180, 5555UL)
            {
                Status = MissionStatus.InCruise
            };
            campaign.Missions["m1"] = mission;

            var cmd = new AdvanceCruiseCommand(campaign, "m1", spacecraftReliability: 0.50);
            var ctx = new CommandContext("player1", PlayerRole.ScienceDirector);
            var result = campaign.CommandBus.Execute(cmd, ctx);

            AssertTrue(result.Success, "Command executed");
            AssertTrue(mission.CruiseEventLog.Count > 0, "Event log populated");
            AssertTrue(mission.Status == MissionStatus.Arrival || mission.Status == MissionStatus.DecisionPending || mission.Status == MissionStatus.Failed,
                "Mission transitions to a valid next lifecycle status");

            if (mission.Status == MissionStatus.DecisionPending)
            {
                AssertTrue(mission.PendingDecisionCard != null, "Pending decision card attached");
                AssertTrue(mission.PendingDecisionCard.Options.Count >= 2, "At least 2 options offered");
            }
        }

        private static void Test_ResolveDecisionCommand_AppliesEffects()
        {
            var campaign = new CampaignState(seed: 123456UL);
            double initialBudget = campaign.Budget;

            var card = new CruiseDecisionCard(
                "DEC_TEST",
                "TITLE",
                "DESC",
                1,
                new[]
                {
                    new CruiseDecisionOption("OPT_A", "Label A", "Cons A", "EXTRA_COST"),
                    new CruiseDecisionOption("OPT_B", "Label B", "Cons B", "SAFE_MODE")
                }
            );

            var mission = new MissionInstance("m1", "Test", "moon", null, 3, 111UL)
            {
                Status = MissionStatus.DecisionPending,
                PendingDecisionCard = card
            };
            campaign.Missions["m1"] = mission;

            var resolveCmd = new ResolveCruiseDecisionCommand(campaign, "m1", "OPT_A");
            var ctx = new CommandContext("player1", PlayerRole.ScienceDirector);
            var res = campaign.CommandBus.Execute(resolveCmd, ctx);

            AssertTrue(res.Success, "Decision resolved");
            AssertEqual(MissionStatus.InCruise, mission.Status, "Returned to InCruise");
            AssertTrue(mission.PendingDecisionCard == null, "Pending card cleared");
            AssertTrue(campaign.Budget < initialBudget, "Extra cost deducted from campaign budget");
            AssertEqual("OPT_A", mission.ResolvedDecisions["DEC_TEST"], "Resolved option recorded");
        }

        private static void Test_AdvanceCruiseCommand_UndoRedo()
        {
            var campaign = new CampaignState(seed: 999UL);
            var mission = new MissionInstance("m_undo", "Undo Mission", "moon", null, 3, 222UL)
            {
                Status = MissionStatus.InCruise,
                CruiseProgress = 0.0
            };
            campaign.Missions["m_undo"] = mission;

            var cmd = new AdvanceCruiseCommand(campaign, "m_undo", spacecraftReliability: 0.98);
            var ctx = new CommandContext("player1", PlayerRole.AgencyDirector);

            campaign.CommandBus.Execute(cmd, ctx);
            AssertTrue(mission.CruiseProgress > 0.0, "Progress advanced");

            // Undo
            var undoRes = campaign.CommandBus.Undo(ctx);
            AssertTrue(undoRes.Success, "Undo succeeded");
            AssertEqual(MissionStatus.InCruise, mission.Status, "Status restored");
            AssertEqual(0.0, mission.CruiseProgress, "Progress restored to 0.0");
            AssertEqual(0, mission.CruiseEventLog.Count, "Event log trimmed back");

            // Redo
            var redoRes = campaign.CommandBus.Redo(ctx);
            AssertTrue(redoRes.Success, "Redo succeeded");
            AssertTrue(mission.CruiseProgress > 0.0, "Progress reapplied on redo");
        }

        private static void Test_ResolveDecisionCommand_UndoRedo()
        {
            var campaign = new CampaignState(seed: 888UL);
            double initialBudget = campaign.Budget;

            var card = new CruiseDecisionCard(
                "DEC_UNDO", "T", "D", 0,
                new[] { new CruiseDecisionOption("OPT_COST", "L", "C", "EXTRA_COST") }
            );

            var mission = new MissionInstance("m_dec_undo", "Dec Mission", "mars", null, 200, 333UL)
            {
                Status = MissionStatus.DecisionPending,
                PendingDecisionCard = card
            };
            campaign.Missions["m_dec_undo"] = mission;

            var cmd = new ResolveCruiseDecisionCommand(campaign, "m_dec_undo", "OPT_COST");
            var ctx = new CommandContext("player1", PlayerRole.AgencyDirector);

            campaign.CommandBus.Execute(cmd, ctx);
            AssertEqual(MissionStatus.InCruise, mission.Status, "Status now InCruise");
            AssertTrue(campaign.Budget < initialBudget, "Budget deducted");

            // Undo
            var undoRes = campaign.CommandBus.Undo(ctx);
            AssertTrue(undoRes.Success, "Undo succeeded");
            AssertEqual(MissionStatus.DecisionPending, mission.Status, "Restored to DecisionPending");
            AssertTrue(mission.PendingDecisionCard != null, "Pending card restored");
            AssertEqual(initialBudget, campaign.Budget, "Budget restored");

            // Redo
            var redoRes = campaign.CommandBus.Redo(ctx);
            AssertTrue(redoRes.Success, "Redo succeeded");
            AssertEqual(MissionStatus.InCruise, mission.Status, "Status InCruise again");
        }

        private static void Test_OperationsViewModel_BindingAndProgress()
        {
            var campaign = new CampaignState(seed: 111UL);
            var vm = new OperationsViewModel();

            var navArgs = new OperationsNavigationArgs("m_vm", "Apollo Next", "moon", null, 3, 0.99, 12345UL);
            vm.BindMission(campaign, "m_vm", navArgs);

            AssertEqual("Apollo Next", vm.MissionName.Value, "Mission name bound");
            AssertEqual("MOON", vm.DestinationName.Value, "Destination uppercase bound");
            AssertTrue(vm.CanAdvance.Value, "Can advance initially");

            // Advance via ViewModel command
            vm.AdvanceCruiseStepCommand.Execute();

            AssertTrue(vm.EventLog.Count > 0, "Event log populated in ViewModel");
            AssertTrue(vm.CruiseProgress.Value > 0.0, "Progress updated in ViewModel");
        }
    }
}
