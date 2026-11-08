using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Scoring;
using Apogee.Design;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Report;

namespace ApogeeLocalTestRunner
{
    public static class ScoringRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_08_Scoring_Config_DefaultCreationAndMedalCount", Test_ScoringConfig_Defaults);
            runTest("M5_08_Scoring_Pillars_AllPillarsNominal_GeneratesHighScoreAndGradeS", Test_AllPillarsNominal_HighScore);
            runTest("M5_08_Scoring_FailedMission_GeneratesGradeDOrF", Test_FailedMission_LowScore);
            runTest("M5_08_Scoring_ExcessiveResourceMargin_AppliesWastePenalty", Test_ExcessiveResourceMargin_Penalty);
            runTest("M5_08_Scoring_DifficultyMultiplier_ScalesFinalScoreAccurately", Test_DifficultyMultiplier_Scales);
            runTest("M5_08_Scoring_Medals_ZeroDataLoss_AwardedWhenNoDroppedData", Test_Medal_ZeroDataLoss);
            runTest("M5_08_Scoring_Medals_FirstCrewed_AwardedOnCrewedSuccess", Test_Medal_FirstCrewed);
            runTest("M5_08_Scoring_Medals_WeatheredStorm_AwardedWhenSafeModeChosen", Test_Medal_WeatheredStorm);
            runTest("M5_08_Scoring_ViewModel_BindsScoreAndMedals", Test_ViewModel_BindsScoreAndMedals);
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

        private static void Test_ScoringConfig_Defaults()
        {
            var config = ScoringConfig.CreateDefault();

            AssertEqual(0.35, config.ScienceDeliveryWeight, "Science weight default");
            AssertEqual(0.20, config.ObjectiveSuccessWeight, "Objective weight default");
            AssertEqual(0.15, config.ResourceEfficiencyWeight, "Resource efficiency default");
            AssertEqual(0.15, config.DecisionQualityWeight, "Decision quality default");
            AssertEqual(0.15, config.PlanningEfficiencyWeight, "Planning efficiency default");
            AssertTrue(config.Medals.Count >= 7, "At least 7 default medals configured");
        }

        private static void Test_AllPillarsNominal_HighScore()
        {
            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("crit_1", "data-delivered-gb", 10.0, "Desc")
            };

            var profile = new MissionProfile(
                id: "prof_high",
                name: "High Profile",
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

            var report = new MissionReport(
                "m1", "Nominal Mission", "moon", true, "Success",
                new[] { new CriterionEvaluationResult(criteria[0], true, 10.0, "Met") },
                new ScienceDeliverySummary(10.0, 10.0, 0.0),
                null, null, null
            );

            var score = MissionScoreCalculator.Calculate(
                profile: profile,
                blueprint: null,
                report: report,
                launchResult: new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.0, 9500, "nominal", "ok"),
                edlResult: null,
                downlinkedGb: 10.0,
                rawGeneratedGb: 10.0,
                droppedGb: 0.0,
                scienceDaysCompleted: 10,
                accumulatedReliabilityPenalty: 0.0,
                operationEvents: null,
                caughtLaunchWindow: true
            );

            AssertTrue(score.FinalScore >= 800.0, $"Nominal mission score must be >= 800, got {score.FinalScore}");
            AssertTrue(score.Grade == "S" || score.Grade == "A", $"Nominal grade should be S or A, got {score.Grade}");
            AssertEqual(1.0, score.ScienceDeliveryScore, "Science delivery score ratio");
            AssertEqual(1.0, score.ObjectiveSuccessScore, "Objective success score ratio");
        }

        private static void Test_FailedMission_LowScore()
        {
            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("crit_1", "data-delivered-gb", 10.0, "Desc")
            };

            var profile = new MissionProfile(
                id: "prof_fail",
                name: "Fail Profile",
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

            var report = new MissionReport(
                "m2", "Failed Mission", "moon", false, "Failed at Launch",
                new[] { new CriterionEvaluationResult(criteria[0], false, 0.0, "Not met") },
                new ScienceDeliverySummary(0.0, 0.0, 0.0),
                null, null, null
            );

            var score = MissionScoreCalculator.Calculate(
                profile: profile,
                blueprint: null,
                report: report,
                launchResult: new LaunchSimulationResult(LaunchOutcome.CatastrophicFailure, false, LaunchWeatherCondition.Stormy, 0.0, 500, "fail", "exploded"),
                edlResult: null,
                downlinkedGb: 0.0,
                rawGeneratedGb: 0.0,
                droppedGb: 0.0,
                scienceDaysCompleted: 0,
                accumulatedReliabilityPenalty: 0.5,
                operationEvents: null,
                caughtLaunchWindow: false
            );

            AssertTrue(score.FinalScore < 400.0, $"Failed mission score must be low (< 400), got {score.FinalScore}");
            AssertEqual("F", score.Grade, "Failed mission should receive F grade");
        }

        private static void Test_ExcessiveResourceMargin_Penalty()
        {
            var criteria = new List<MissionSuccessCriterion>();
            var profile = new MissionProfile(
                id: "prof_margin",
                name: "Margin Profile",
                targetDestinationId: "moon",
                targetMissionTypeId: "orbiter",
                requiredDeltaV: 10000,
                minReliabilityThreshold: 0.9,
                requiredLaunchPadLevel: 2,
                minDownlinkRate: 5.0,
                minDataStorage: 50.0,
                rewardMultiplier: 1.0,
                totalScienceValue: 50.0,
                successCriteria: criteria
            );

            var report = new MissionReport("m3", "Margin Test", "moon", true, "ok", null, null, null, null, null);

            var normalScore = MissionScoreCalculator.Calculate(
                profile, null, report, null, null, 10, 10, 0, 5, 0, null, true
            );

            AssertTrue(normalScore.ResourceEfficiencyScore >= 0.7, "Baseline fallback resource efficiency should be >= 0.7");
        }

        private static void Test_DifficultyMultiplier_Scales()
        {
            var criteria = new List<MissionSuccessCriterion>();
            var profileStandard = new MissionProfile(
                "p_std", "Std", "moon", "orbiter", 10000, 0.9, 1, 5, 50, rewardMultiplier: 1.0, totalScienceValue: 50, successCriteria: criteria
            );
            var profileChallenging = new MissionProfile(
                "p_chg", "Chg", "mars", "crewed", 20000, 0.98, 4, 10, 100, rewardMultiplier: 2.5, totalScienceValue: 150, successCriteria: criteria
            );

            var report = new MissionReport("m4", "Scale Test", "target", true, "ok", null, null, null, null, null);

            var score1 = MissionScoreCalculator.Calculate(profileStandard, null, report, null, null, 10, 10, 0, 5, 0, null, true);
            var score2 = MissionScoreCalculator.Calculate(profileChallenging, null, report, null, null, 10, 10, 0, 5, 0, null, true);

            AssertTrue(score2.FinalScore > score1.FinalScore, "Higher difficulty multiplier must produce higher total score");
            AssertEqual(2.5, score2.DifficultyMultiplier, "Difficulty multiplier must match profile reward multiplier");
        }

        private static void Test_Medal_ZeroDataLoss()
        {
            var report = new MissionReport("m5", "Data Test", "moon", true, "ok", null, null, null, null, null);

            var score = MissionScoreCalculator.Calculate(
                null, null, report, null, null, downlinkedGb: 15.0, rawGeneratedGb: 15.0, droppedGb: 0.0, scienceDaysCompleted: 5, accumulatedReliabilityPenalty: 0, operationEvents: null, caughtLaunchWindow: true
            );

            AssertTrue(score.Medals.Any(m => m.Definition.Id == "medal_zero_data_loss"), "Zero data loss medal should be awarded");
        }

        private static void Test_Medal_FirstCrewed()
        {
            var profile = new MissionProfile(
                "p_crew", "Crewed", "moon", "crewed", 15000, 0.98, 3, 5, 50, requiresLifeSupport: true, totalScienceValue: 100
            );
            var report = new MissionReport("m6", "Crewed Test", "moon", true, "ok", null, null, null, null, null);

            var score = MissionScoreCalculator.Calculate(
                profile, null, report, null, null, 10, 10, 0, 5, 0, null, true
            );

            AssertTrue(score.Medals.Any(m => m.Definition.Id == "medal_first_crewed"), "First crewed medal should be awarded");
        }

        private static void Test_Medal_WeatheredStorm()
        {
            var stormRecord = new OperationEventRecord(
                "op_solar_storm", "Solar Storm", "Storm", OperationEventPhase.Cruise, 10,
                new[] { new OperationEventOption("safe_mode", "Safe", "Conseq", "SAFE") }
            );
            var activeStorm = new ActiveOperationEvent("storm_1", stormRecord, OperationEventPhase.Cruise, 2)
            {
                ChosenOptionId = "safe_mode"
            };

            var report = new MissionReport("m7", "Storm Test", "moon", true, "ok", null, null, null, null, null);

            var score = MissionScoreCalculator.Calculate(
                null, null, report, null, null, 10, 10, 0, 5, 0, new[] { activeStorm }, true
            );

            AssertTrue(score.Medals.Any(m => m.Definition.Id == "medal_weathered_storm"), "Weathered storm medal should be awarded");
        }

        private static void Test_ViewModel_BindsScoreAndMedals()
        {
            var medalDef = new MedalDefinition("test_med", "Test Medal", "Desc", "icon", "Cat", 100);
            var awarded = new List<AwardedMedal> { new AwardedMedal(medalDef, "Test Reason") };
            var breakdown = new MissionScoreBreakdown(
                1.0, 1.0, 0.8, 0.9, 0.85, 900, 1.5, 100, 1450, "S", awarded
            );

            var report = new MissionReport(
                "m_vm", "VM Score Test", "moon", true, "ok", null, null, null, null, null, breakdown
            );

            var vm = new MissionReportViewModel();
            vm.BindReport("m_vm", report);

            AssertEqual("1450", vm.FinalScoreText.Value, "Final score text bound");
            AssertEqual("S", vm.GradeText.Value, "Grade text bound");
            AssertEqual(1, vm.AwardedMedalsList.Count, "Awarded medals list populated");
            AssertEqual("test_med", vm.AwardedMedalsList[0].Definition.Id, "Medal ID matches");
        }
    }
}
