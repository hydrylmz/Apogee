using System;
using System.Collections.Generic;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;
using Apogee.UI.Milestones;
using NUnit.Framework;

namespace Apogee.Tests.Verification
{
    [TestFixture]
    public class MilestoneRunnerTests
    {
        private List<MilestoneRecord> CreateDefaultMilestoneRecords()
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

        [Test]
        public void M6_01_Milestones_InitialState_CorrectEligibility()
        {
            var records = CreateDefaultMilestoneRecords();
            var manager = new MilestoneManager(records);

            Assert.IsTrue(manager.TryGetMilestone("milestone_leo_satellite", out var leo));
            Assert.AreEqual(MilestoneStatus.Available, leo.Status);

            Assert.IsTrue(manager.TryGetMilestone("milestone_moon_probe", out var moon));
            Assert.AreEqual(MilestoneStatus.Locked, moon.Status);

            Assert.IsTrue(manager.TryGetMilestone("milestone_crewed_moon", out var crewed));
            Assert.AreEqual(MilestoneStatus.Locked, crewed.Status);
        }

        [Test]
        public void M6_01_Milestones_Evaluator_MatchesSuccessfulMission()
        {
            var records = CreateDefaultMilestoneRecords();
            var milestone = records[0];

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            mission.Status = MissionStatus.Completed;
            mission.Report = new Apogee.Core.Simulation.Report.MissionReport
            {
                IsOverallSuccess = true
            };

            bool satisfies = MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, mission);
            Assert.IsTrue(satisfies);
        }

        [Test]
        public void M6_01_Milestones_Evaluator_RejectsFailedOrMismatchedMission()
        {
            var records = CreateDefaultMilestoneRecords();
            var milestone = records[0]; // leo, orbiter

            var failedMission = new MissionInstance("M-02", "Failed Probe", "leo", null, 10, 12345);
            failedMission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            failedMission.Status = MissionStatus.Failed;
            failedMission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = false };

            Assert.IsFalse(MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, failedMission));

            var wrongDestMission = new MissionInstance("M-03", "Mars Probe", "mars", null, 10, 12345);
            wrongDestMission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            wrongDestMission.Status = MissionStatus.Completed;
            wrongDestMission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = true };

            Assert.IsFalse(MilestoneEvaluator.DoesMissionSatisfyMilestone(milestone, wrongDestMission));
        }

        [Test]
        public void M6_01_Milestones_Manager_ProgressEvaluation_AdvancesStatus()
        {
            var records = CreateDefaultMilestoneRecords();
            var manager = new MilestoneManager(records);

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            mission.Status = MissionStatus.Completed;
            mission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = true };

            var missions = new List<MissionInstance> { mission };
            var completedIds = manager.EvaluateProgress(missions, "2031-03-01");

            Assert.Contains("milestone_leo_satellite", completedIds);
            Assert.IsTrue(manager.TryGetMilestone("milestone_leo_satellite", out var leo));
            Assert.AreEqual(MilestoneStatus.CompletedPendingClaim, leo.Status);
            Assert.AreEqual("M-01", leo.CompletedByMissionId);
        }

        [Test]
        public void M6_01_Milestones_ClaimCommand_GrantsRewards_And_UnlocksNextMilestone()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            mission.Status = MissionStatus.Completed;
            mission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = true };
            campaign.Missions[mission.MissionId] = mission;

            campaign.Milestones.EvaluateProgress(campaign.Missions.Values, "2031-03-01");

            double initialBudget = campaign.Budget;
            double initialRep = campaign.Reputation;
            double initialKp = campaign.KnowledgePoints;

            var claimCmd = new ClaimMilestoneRewardCommand(campaign, "milestone_leo_satellite");
            var result = campaign.Execute(claimCmd, Apogee.Core.PlayerRole.AgencyDirector);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(initialBudget + 50.0, campaign.Budget, 0.001);
            Assert.AreEqual(initialRep + 10.0, campaign.Reputation, 0.001);
            Assert.AreEqual(initialKp + 25.0, campaign.KnowledgePoints, 0.001);

            Assert.IsTrue(campaign.Milestones.TryGetMilestone("milestone_leo_satellite", out var leo));
            Assert.AreEqual(MilestoneStatus.Claimed, leo.Status);

            // Prerequisite fulfilled: Next milestone (moon probe) should now unlock to Available
            Assert.IsTrue(campaign.Milestones.TryGetMilestone("milestone_moon_probe", out var moon));
            Assert.AreEqual(MilestoneStatus.Available, moon.Status);
        }

        [Test]
        public void M6_01_Milestones_ClaimCommand_Undo_RestoresResourcesAndStatus()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            mission.Status = MissionStatus.Completed;
            mission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = true };
            campaign.Missions[mission.MissionId] = mission;

            campaign.Milestones.EvaluateProgress(campaign.Missions.Values, "2031-03-01");

            double initialBudget = campaign.Budget;
            double initialRep = campaign.Reputation;

            var claimCmd = new ClaimMilestoneRewardCommand(campaign, "milestone_leo_satellite");
            campaign.Execute(claimCmd, Apogee.Core.PlayerRole.AgencyDirector);

            var undoResult = campaign.Undo(Apogee.Core.PlayerRole.AgencyDirector);
            Assert.IsTrue(undoResult.Success);

            Assert.AreEqual(initialBudget, campaign.Budget, 0.001);
            Assert.AreEqual(initialRep, campaign.Reputation, 0.001);

            Assert.IsTrue(campaign.Milestones.TryGetMilestone("milestone_leo_satellite", out var leo));
            Assert.AreEqual(MilestoneStatus.CompletedPendingClaim, leo.Status);

            Assert.IsTrue(campaign.Milestones.TryGetMilestone("milestone_moon_probe", out var moon));
            Assert.AreEqual(MilestoneStatus.Locked, moon.Status);
        }

        [Test]
        public void M6_01_Milestones_ViewModel_BindsCollectionAndTriggersClaim()
        {
            var campaign = new CampaignState(seed: 42);
            campaign.InitialiseMilestones(CreateDefaultMilestoneRecords());

            var mission = new MissionInstance("M-01", "Explorer 1", "leo", null, 10, 12345);
            mission.Profile = new MissionProfile { MissionType = new MissionTypeRecord { Id = "orbiter" }, IsCrewed = false };
            mission.Status = MissionStatus.Completed;
            mission.Report = new Apogee.Core.Simulation.Report.MissionReport { IsOverallSuccess = true };
            campaign.Missions[mission.MissionId] = mission;

            using (var boardVm = new MilestoneBoardViewModel(campaign))
            {
                Assert.AreEqual(3, boardVm.MilestoneItems.Count);
                boardVm.Refresh();

                var leoVm = boardVm.MilestoneItems[0];
                Assert.AreEqual(MilestoneStatus.CompletedPendingClaim, leoVm.Status.Value);
                Assert.IsTrue(leoVm.CanClaim.Value);

                // Execute claim through VM
                leoVm.ClaimCommand.Execute(null);

                Assert.AreEqual(MilestoneStatus.Claimed, leoVm.Status.Value);
                Assert.IsFalse(leoVm.CanClaim.Value);
                Assert.IsTrue(leoVm.IsCompleted.Value);
                Assert.IsTrue(boardVm.HasNotification.Value);
            }
        }
    }
}
