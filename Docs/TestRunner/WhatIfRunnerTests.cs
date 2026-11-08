using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Science;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Report;

namespace ApogeeLocalTestRunner
{
    public static class WhatIfRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_07_WhatIf_GetEligibleEvents_FiltersOnlyMultiChoiceResolvedEvents", Test_GetEligibleEvents_FiltersProperly);
            runTest("M5_07_WhatIf_GetAlternativeOptions_ExcludesChosenOption", Test_GetAlternativeOptions_ExcludesChosen);
            runTest("M5_07_WhatIf_Simulate_OutcomeFlipsFromFailureToSuccess", Test_Simulate_OutcomeFlipsToSuccess);
            runTest("M5_07_WhatIf_Simulate_CalculatesNetDeltasAccurately", Test_Simulate_CalculatesNetDeltas);
            runTest("M5_07_WhatIf_Simulate_LeavesOriginalMissionAndReportIntact", Test_Simulate_LeavesOriginalIntact);
            runTest("M5_07_WhatIf_Simulate_DeterministicReproducibility", Test_Simulate_DeterministicReproducibility);
            runTest("M5_07_WhatIf_ViewModel_BindsEligibleEventsAndSelections", Test_ViewModel_BindsAndSelects);
            runTest("M5_07_WhatIf_ViewModel_RunWhatIf_PopulatesComparisonAndBadges", Test_ViewModel_RunWhatIf_PopulatesData);
            runTest("M5_07_WhatIf_ViewModel_ClearWhatIf_ResetsActiveState", Test_ViewModel_ClearWhatIf_Resets);
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

        private static (MissionInstance mission, MissionReport report, ActiveOperationEvent testEvent) CreateMockMissionWithEvent(
            double initialDownlinkedGb,
            double thresholdGb)
        {
            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("crit_science", "data-delivered-gb", thresholdGb, "Gereken Bilim Verisi")
            };

            var profile = new MissionProfile(
                id: "prof_whatif",
                name: "WhatIf Profil",
                targetDestinationId: "moon",
                targetMissionTypeId: "orbiter",
                requiredDeltaV: 13500,
                minReliabilityThreshold: 0.9,
                requiredLaunchPadLevel: 2,
                minDownlinkRate: 5.0,
                minDataStorage: 50.0,
                rewardMultiplier: 1.0,
                totalScienceValue: 50.0,
                successCriteria: criteria
            );

            var mission = new MissionInstance("m_test_whatif", "WhatIf Misyonu", "moon", null, 10, 12345UL)
            {
                Profile = profile,
                AccumulatedDownlinkDataGb = initialDownlinkedGb,
                AccumulatedRawDataGeneratedGb = initialDownlinkedGb + 5.0,
                AccumulatedDroppedDataGb = 2.0,
                ScienceDaysCompleted = 5,
                AccumulatedReliabilityPenalty = 0.05,
                Status = MissionStatus.Operating
            };

            var eventRecord = new OperationEventRecord(
                eventId: "op_test_decision",
                titleKey: "Test Karar Kartı",
                narrativeKey: "Kritik karar anı",
                phase: OperationEventPhase.Science,
                baseWeight: 10.0,
                options: new[]
                {
                    new OperationEventOption("opt_safe", "Güvenli Mod", "Bilim kaybı yaşanır", "SAFE", budgetDelta: 0, reliabilityPenalty: 0.0, scienceDataDeltaGb: -5.0),
                    new OperationEventOption("opt_aggressive", "Agresif İletişim", "Ekstra veri indirilir", "AGGRESSIVE", budgetDelta: -2.0, reliabilityPenalty: 0.02, scienceDataDeltaGb: +6.0)
                }
            );

            var activeEvent = new ActiveOperationEvent("evt_inst_1", eventRecord, OperationEventPhase.Science, 3)
            {
                ChosenOptionId = "opt_safe"
            };

            mission.OperationEventLog.Add(activeEvent);

            var report = MissionReportEvaluator.Evaluate(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                profile: mission.Profile,
                blueprint: mission.Blueprint,
                launchResult: null,
                edlResult: null,
                downlinkedGb: mission.AccumulatedDownlinkDataGb,
                rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                droppedGb: mission.AccumulatedDroppedDataGb,
                scienceDaysCompleted: mission.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: mission.AccumulatedReliabilityPenalty,
                accumulatedCorrectionFuel: 0.0,
                operationEvents: mission.OperationEventLog
            );

            mission.Report = report;
            return (mission, report, activeEvent);
        }

        private static void Test_GetEligibleEvents_FiltersProperly()
        {
            var (mission, report, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);

            // Add a pending event (should be excluded)
            var pendingRecord = new OperationEventRecord("op_pending", "Pending Title", "Desc", OperationEventPhase.Cruise, 5.0,
                new[] { new OperationEventOption("o1", "L1", "C1", "T1"), new OperationEventOption("o2", "L2", "C2", "T2") });
            var pendingEvent = new ActiveOperationEvent("evt_pending", pendingRecord, OperationEventPhase.Cruise, 1);
            mission.OperationEventLog.Add(pendingEvent);

            // Add a single-option event (should be excluded)
            var singleRecord = new OperationEventRecord("op_single", "Single", "Desc", OperationEventPhase.Cruise, 5.0,
                new[] { new OperationEventOption("s1", "S1", "C1", "T1") });
            var singleEvent = new ActiveOperationEvent("evt_single", singleRecord, OperationEventPhase.Cruise, 2)
            {
                ChosenOptionId = "s1"
            };
            mission.OperationEventLog.Add(singleEvent);

            var eligible = WhatIfSimulator.GetEligibleEvents(mission.OperationEventLog);

            AssertEqual(1, eligible.Count, "Eligible events count should be exactly 1");
            AssertEqual(activeEvent.InstanceId, eligible[0].InstanceId, "Eligible event instance ID must match activeEvent");
        }

        private static void Test_GetAlternativeOptions_ExcludesChosen()
        {
            var (mission, report, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);
            var alternatives = WhatIfSimulator.GetAlternativeOptions(activeEvent);

            AssertEqual(1, alternatives.Count, "Should have exactly 1 alternative option");
            AssertEqual("opt_aggressive", alternatives[0].OptionId, "Alternative option must be opt_aggressive");
        }

        private static void Test_Simulate_OutcomeFlipsToSuccess()
        {
            // Initial downlinked data = 10.0 GB, threshold = 15.0 GB -> Fails initially
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);
            AssertTrue(!originalReport.IsOverallSuccess, "Original report must be a failure");

            // In opt_aggressive, scienceDataDeltaGb is +6.0, compared to -5.0 in opt_safe (net +11.0 GB)
            // Downlinked data becomes 10.0 + 11.0 = 21.0 GB >= 15.0 GB threshold -> Becomes Success!
            var comparison = WhatIfSimulator.Simulate(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                profile: mission.Profile,
                blueprint: mission.Blueprint,
                launchResult: mission.LaunchResult,
                edlResult: mission.EdlResult,
                downlinkedGb: mission.AccumulatedDownlinkDataGb,
                rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                droppedGb: mission.AccumulatedDroppedDataGb,
                scienceDaysCompleted: mission.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: mission.AccumulatedReliabilityPenalty,
                accumulatedCorrectionFuel: 0.0,
                operationEventLog: mission.OperationEventLog,
                targetEvent: activeEvent,
                alternativeOptionId: "opt_aggressive",
                originalReport: originalReport
            );

            AssertTrue(comparison.OutcomeChanged, "Outcome must have changed from failure to success");
            AssertTrue(comparison.WhatIfReport.IsOverallSuccess, "What-If report must be overall success");
            AssertTrue(comparison.ScienceDataDelta > 5.0, "Science data delta must be strictly positive");
            AssertTrue(comparison.DeltaSummaryText.Contains("BAŞARILI"), "Summary text must mention BAŞARILI");
        }

        private static void Test_Simulate_CalculatesNetDeltas()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);

            var comparison = WhatIfSimulator.Simulate(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                profile: mission.Profile,
                blueprint: mission.Blueprint,
                launchResult: null,
                edlResult: null,
                downlinkedGb: mission.AccumulatedDownlinkDataGb,
                rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                droppedGb: mission.AccumulatedDroppedDataGb,
                scienceDaysCompleted: mission.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: mission.AccumulatedReliabilityPenalty,
                accumulatedCorrectionFuel: 0.0,
                operationEventLog: mission.OperationEventLog,
                targetEvent: activeEvent,
                alternativeOptionId: "opt_aggressive",
                originalReport: originalReport
            );

            // Original failed (impact: partial completion or low), WhatIf succeeded (full bonus)
            AssertTrue(comparison.BudgetDelta > 0.0, "Budget delta should be positive due to success bonus");
            AssertTrue(comparison.ReputationDelta > 0.0, "Reputation delta should be positive due to success bonus");
            AssertEqual("opt_safe", comparison.Change.OriginalOptionId, "Original option id must match");
            AssertEqual("opt_aggressive", comparison.Change.AlternativeOptionId, "Alternative option id must match");
        }

        private static void Test_Simulate_LeavesOriginalIntact()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);

            double origDownlink = mission.AccumulatedDownlinkDataGb;
            string origChosen = activeEvent.ChosenOptionId;
            bool origStatus = originalReport.IsOverallSuccess;

            WhatIfSimulator.Simulate(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                profile: mission.Profile,
                blueprint: mission.Blueprint,
                launchResult: null,
                edlResult: null,
                downlinkedGb: mission.AccumulatedDownlinkDataGb,
                rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                droppedGb: mission.AccumulatedDroppedDataGb,
                scienceDaysCompleted: mission.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: mission.AccumulatedReliabilityPenalty,
                accumulatedCorrectionFuel: 0.0,
                operationEventLog: mission.OperationEventLog,
                targetEvent: activeEvent,
                alternativeOptionId: "opt_aggressive",
                originalReport: originalReport
            );

            AssertEqual(origDownlink, mission.AccumulatedDownlinkDataGb, "Mission downlink data must not be mutated");
            AssertEqual(origChosen, activeEvent.ChosenOptionId, "Active event chosen option must not be mutated");
            AssertEqual(origStatus, originalReport.IsOverallSuccess, "Original report success flag must not be mutated");
        }

        private static void Test_Simulate_DeterministicReproducibility()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);

            var comp1 = WhatIfSimulator.Simulate(
                mission.MissionId, mission.MissionName, mission.DestinationId, mission.Profile, mission.Blueprint,
                null, null, mission.AccumulatedDownlinkDataGb, mission.AccumulatedRawDataGeneratedGb,
                mission.AccumulatedDroppedDataGb, mission.ScienceDaysCompleted, mission.AccumulatedReliabilityPenalty,
                0.0, mission.OperationEventLog, activeEvent, "opt_aggressive", originalReport
            );

            var comp2 = WhatIfSimulator.Simulate(
                mission.MissionId, mission.MissionName, mission.DestinationId, mission.Profile, mission.Blueprint,
                null, null, mission.AccumulatedDownlinkDataGb, mission.AccumulatedRawDataGeneratedGb,
                mission.AccumulatedDroppedDataGb, mission.ScienceDaysCompleted, mission.AccumulatedReliabilityPenalty,
                0.0, mission.OperationEventLog, activeEvent, "opt_aggressive", originalReport
            );

            AssertEqual(comp1.BudgetDelta, comp2.BudgetDelta, "Budget deltas must be identical across runs");
            AssertEqual(comp1.ScienceDataDelta, comp2.ScienceDataDelta, "Science data deltas must be identical across runs");
            AssertEqual(comp1.DeltaSummaryText, comp2.DeltaSummaryText, "Delta summary texts must match");
        }

        private static void Test_ViewModel_BindsAndSelects()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);
            var vm = new WhatIfViewModel();

            bool updatedFired = false;
            vm.OnWhatIfUpdated += () => updatedFired = true;

            vm.BindMission(mission, originalReport);

            AssertTrue(updatedFired, "OnWhatIfUpdated must fire on BindMission");
            AssertTrue(vm.HasEligibleEvents.Value, "HasEligibleEvents should be true");
            AssertEqual(1, vm.EligibleEvents.Count, "EligibleEvents count should be 1");

            vm.SelectEvent(vm.EligibleEvents[0]);
            AssertEqual(activeEvent.InstanceId, vm.SelectedEvent.InstanceId, "Selected event instance ID must match");
            AssertEqual(1, vm.AlternativeOptions.Count, "AlternativeOptions count should be 1");
            AssertEqual("opt_aggressive", vm.AlternativeOptions[0].OptionId, "Alternative option ID must match");
        }

        private static void Test_ViewModel_RunWhatIf_PopulatesData()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);
            var vm = new WhatIfViewModel();
            vm.BindMission(mission, originalReport);
            vm.SelectEvent(vm.EligibleEvents[0]);

            vm.RunWhatIf("opt_aggressive");

            AssertTrue(vm.IsWhatIfActive.Value, "IsWhatIfActive must be true after run");
            AssertTrue(vm.WhatIfOutcomeChanged.Value, "WhatIfOutcomeChanged must be true (failed -> success)");
            AssertTrue(!string.IsNullOrEmpty(vm.WhatIfOutcomeBadge.Value), "WhatIfOutcomeBadge must be populated");
            AssertTrue(!string.IsNullOrEmpty(vm.WhatIfDeltaSummary.Value), "WhatIfDeltaSummary must be populated");
            AssertTrue(vm.LastComparison != null, "LastComparison must not be null");
        }

        private static void Test_ViewModel_ClearWhatIf_Resets()
        {
            var (mission, originalReport, activeEvent) = CreateMockMissionWithEvent(10.0, 15.0);
            var vm = new WhatIfViewModel();
            vm.BindMission(mission, originalReport);
            vm.SelectEvent(vm.EligibleEvents[0]);
            vm.RunWhatIf("opt_aggressive");

            vm.ClearWhatIfCommand.Execute();

            AssertTrue(!vm.IsWhatIfActive.Value, "IsWhatIfActive must be false after clear");
            AssertEqual(string.Empty, vm.WhatIfDeltaSummary.Value, "WhatIfDeltaSummary must be empty");
            AssertEqual(string.Empty, vm.WhatIfOutcomeBadge.Value, "WhatIfOutcomeBadge must be empty");
            AssertTrue(!vm.WhatIfOutcomeChanged.Value, "WhatIfOutcomeChanged must be false");
            AssertTrue(vm.LastComparison == null, "LastComparison must be null");
        }
    }
}
