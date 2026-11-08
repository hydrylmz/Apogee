using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Report;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;
using Apogee.UI.Milestones;

namespace ApogeeLocalTestRunner
{
    public static class MilestoneLocalRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M6_01_Milestones_InitialState_CorrectEligibility", Test_InitialState);
            runTest("M6_01_Milestones_Evaluator_MatchesSuccessfulMission", Test_Evaluator_MatchesSuccessfulMission);
            runTest("M6_01_Milestones_Evaluator_RejectsFailedOrMismatchedMission", Test_Evaluator_RejectsFailedOrMismatched);
            runTest("M6_01_Milestones_Manager_ProgressEvaluation_AdvancesStatus", Test_Manager_ProgressEvaluation);
            runTest("M6_01_Milestones_ClaimCommand_GrantsRewards_And_UnlocksNextMilestone", Test_ClaimCommand_GrantsRewards);
            runTest("M6_01_Milestones_ClaimCommand_Undo_RestoresResourcesAndStatus", Test_ClaimCommand_Undo);
            runTest("M6_01_Milestones_ViewModel_BindsCollectionAndTriggersClaim", Test_ViewModel_BindsAndClaims);
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

        private static MissionReport CreateSimpleReport(string missionId, bool isSuccess)
        {
            return new MissionReport(
                missionId: missionId,
                missionName: "Test Mission",
                destinationId: "test_dest",
                isOverallSuccess: isSuccess,
                finalStatusSummary: isSuccess ? "Başarılı" : "Başarısız",
                criteriaResults: null,
                scienceSummary: null,
                causeAndEffectChain: null,
                campaignImpact: null,
                lessonsLearned: null,
                scoreBreakdown: null);
        }

        private static MissionProfile CreateSimpleProfile(string destId, string typeId, bool isCrewed)
        {
            return new MissionProfile(
                id: "profile_test",
                name: "Test Profile",
                targetDestinationId: destId,
                targetMissionTypeId: typeId,
                requiredDeltaV: 5000,
                minReliabilityThreshold: 0.8,
                requiredLaunchPadLevel: 1,
                minDownlinkRate: 1.0,
                minDataStorage: 5.0,
                requiresEdl: false,
                requiresLifeSupport: isCrewed,
                requiredModuleCategories: null,
                requiredResearchIds: null,
                selectedObjectiveIds: null,
                requiredInstrumentCategories: null,
                successCriteria: null);
        }

        private static List<MilestoneRecord> CreateDefaultMilestoneRecords()
        {
            return new List<MilestoneRecord>
            {
                new MilestoneRecord
                {
                    Id = "milestone_leo_satellite",
                    Title = "İlk Yörünge Uydusu",
                    TargetDestinationId = "leo",
                    TargetMissionTypeId = "orbiter",
                    IsCrewed = false,
                    RewardBudget = 50.0,
                    RewardReputation = 10.0,
                    RewardKnowledgePoints = 25.0,
                    OrderIndex = 1
                },
                new MilestoneRecord
                {
                    Id = "milestone_moon_probe",
                    Title = "Ay'a İlk Sonda",
                    TargetDestinationId = "moon",
                    TargetMissionTypeId = "orbiter",
                    IsCrewed = false,
                    RequiredMilestoneIds = new List<string> { "milestone_leo_satellite" },
                    RewardBudget = 80.0,
                    RewardReputation = 15.0,
                    RewardKnowledgePoints = 40.0,
                    OrderIndex = 2
                },
                new MilestoneRecord
                {
                    Id = "milestone_crewed_moon",
                    Title = "İnsanlı Ay Görevi",
                    TargetDestinationId = "moon",
                    TargetMissionTypeId = "crewed",
                    IsCrewed = true,
                    RequiredMilestoneIds = new List<string> { "milestone_moon_probe" },
                    RewardBudget = 300.0,
                    RewardReputation = 40.0,
                    RewardKnowledgePoints = 150.0,
                    OrderIndex = 3
                }
            };
        }

        private static void Test_InitialState()
        {
            var records = CreateDefaultMilestoneRecords();
            var manager = new MilestoneManager(records);

            AssertTrue(manager.TryGetMilestone("milestone_leo_satellite", out var leo), "leo found");
            AssertEqual(MilestoneStatus.Available, leo.Status, "leo available");

            AssertTrue(manager.TryGetMilestone("milestone_moon_probe", out var moon), "moon found");
            AssertEqual(MilestoneStatus.Locked, moon.Status, "moon locked");

            AssertTrue(manager.TryGetMilestone("milestone_crewed_moon", out var crewed), "crewed found");
            AssertEqual(MilestoneStatus.Locked, crewed.Status, "crewed locked");
        }

        private static void Test_Evaluator_MatchesSuccessfulMission()
        {
            var records = CreateDefaultMilestoneRecords();
            var milestone = records[0];

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            mission.Status = MissionStatus.Completed;
            mission.Report = CreateSimpleReport("M-01", true);

            bool satisfies = MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, mission);
            AssertTrue(satisfies, "mission satisfies milestone");
        }

        private static void Test_Evaluator_RejectsFailedOrMismatched()
        {
            var records = CreateDefaultMilestoneRecords();
            var milestone = records[0];

            var failedMission = new MissionInstance("M-02", "Failed Probe", "leo", null, 10, 12345);
            failedMission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            failedMission.Status = MissionStatus.Failed;
            failedMission.Report = CreateSimpleReport("M-02", false);

            AssertTrue(!MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, failedMission), "failed mission rejected");

            var wrongDestMission = new MissionInstance("M-03", "Mars Probe", "mars", null, 10, 12345);
            wrongDestMission.Profile = CreateSimpleProfile("mars", "orbiter", false);
            wrongDestMission.Status = MissionStatus.Completed;
            wrongDestMission.Report = CreateSimpleReport("M-03", true);

            AssertTrue(!MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, wrongDestMission), "wrong dest rejected");
        }

        private static void Test_Manager_ProgressEvaluation()
        {
            var records = CreateDefaultMilestoneRecords();
            var manager = new MilestoneManager(records);

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            mission.Status = MissionStatus.Completed;
            mission.Report = CreateSimpleReport("M-01", true);

            var missions = new List<MissionInstance> { mission };
            var completedIds = manager.EvaluateProgress(missions, "2031-03-01");

            AssertTrue(completedIds.Contains("milestone_leo_satellite"), "completedIds contains leo");
            AssertTrue(manager.TryGetMilestone("milestone_leo_satellite", out var leo), "leo found");
            AssertEqual(MilestoneStatus.CompletedPendingClaim, leo.Status, "leo status CompletedPendingClaim");
            AssertEqual("M-01", leo.CompletedByMissionId, "completed by mission id");
        }

        private static void Test_ClaimCommand_GrantsRewards()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            mission.Status = MissionStatus.Completed;
            mission.Report = CreateSimpleReport("M-01", true);
            campaign.Missions[mission.MissionId] = mission;

            campaign.Milestones.EvaluateProgress(campaign.Missions.Values, "2031-03-01");

            double initialBudget = campaign.Budget;
            double initialRep = campaign.Reputation;
            double initialKp = campaign.KnowledgePoints;

            var claimCmd = new ClaimMilestoneRewardCommand(campaign, "milestone_leo_satellite");
            var result = campaign.Execute(claimCmd, PlayerRole.AgencyDirector);

            AssertTrue(result.Success, "claim executed");
            AssertEqual(initialBudget + 50.0, campaign.Budget, "budget rewarded");
            AssertEqual(initialRep + 10.0, campaign.Reputation, "rep rewarded");
            AssertEqual(initialKp + 25.0, campaign.KnowledgePoints, "kp rewarded");

            AssertTrue(campaign.Milestones.TryGetMilestone("milestone_leo_satellite", out var leo), "leo found");
            AssertEqual(MilestoneStatus.Claimed, leo.Status, "leo claimed");

            AssertTrue(campaign.Milestones.TryGetMilestone("milestone_moon_probe", out var moon), "moon found");
            AssertEqual(MilestoneStatus.Available, moon.Status, "moon unlocked to available");
        }

        private static void Test_ClaimCommand_Undo()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            mission.Status = MissionStatus.Completed;
            mission.Report = CreateSimpleReport("M-01", true);
            campaign.Missions[mission.MissionId] = mission;

            campaign.Milestones.EvaluateProgress(campaign.Missions.Values, "2031-03-01");

            double initialBudget = campaign.Budget;
            double initialRep = campaign.Reputation;

            var claimCmd = new ClaimMilestoneRewardCommand(campaign, "milestone_leo_satellite");
            campaign.Execute(claimCmd, PlayerRole.AgencyDirector);

            var undoResult = campaign.Undo(PlayerRole.AgencyDirector);
            AssertTrue(undoResult.Success, "undo successful");

            AssertEqual(initialBudget, campaign.Budget, "budget restored");
            AssertEqual(initialRep, campaign.Reputation, "rep restored");

            AssertTrue(campaign.Milestones.TryGetMilestone("milestone_leo_satellite", out var leo), "leo found");
            AssertEqual(MilestoneStatus.CompletedPendingClaim, leo.Status, "leo reverted to completed pending claim");

            AssertTrue(campaign.Milestones.TryGetMilestone("milestone_moon_probe", out var moon), "moon found");
            AssertEqual(MilestoneStatus.Locked, moon.Status, "moon re-locked");
        }

        private static void Test_ViewModel_BindsAndClaims()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = CreateSimpleProfile("leo", "orbiter", false);
            mission.Status = MissionStatus.Completed;
            mission.Report = CreateSimpleReport("M-01", true);
            campaign.Missions[mission.MissionId] = mission;

            using (var boardVm = new MilestoneBoardViewModel(campaign))
            {
                AssertEqual(3, boardVm.MilestoneItems.Count, "milestone item count");
                boardVm.Refresh();

                var leoVm = boardVm.MilestoneItems[0];
                AssertEqual(MilestoneStatus.CompletedPendingClaim, leoVm.Status.Value, "vm status completed pending claim");
                AssertTrue(leoVm.CanClaim.Value, "can claim is true");

                // Claim via VM
                leoVm.ClaimCommand.Execute();

                AssertEqual(MilestoneStatus.Claimed, leoVm.Status.Value, "vm status claimed");
                AssertTrue(!leoVm.CanClaim.Value, "can claim is false");
                AssertTrue(leoVm.IsCompleted.Value, "is completed is true");
                AssertTrue(boardVm.HasNotification.Value, "notification shown");
            }
        }
    }
}
