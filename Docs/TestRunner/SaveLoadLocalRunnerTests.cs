using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Report;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;
using Apogee.Strategy.Persistence;

namespace ApogeeLocalTestRunner
{
    public static class SaveLoadLocalRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M6_04_SaveLoad_RoundTrip_ResourcesAndAgency", Test_RoundTrip_ResourcesAndAgency);
            runTest("M6_04_SaveLoad_RoundTrip_TimeSystem", Test_RoundTrip_TimeSystem);
            runTest("M6_04_SaveLoad_RoundTrip_Bases", Test_RoundTrip_Bases);
            runTest("M6_04_SaveLoad_RoundTrip_ResearchTreeAndQueue", Test_RoundTrip_ResearchTreeAndQueue);
            runTest("M6_04_SaveLoad_RoundTrip_Milestones", Test_RoundTrip_Milestones);
            runTest("M6_04_SaveLoad_RoundTrip_MissionsAndReports", Test_RoundTrip_MissionsAndReports);
            runTest("M6_04_SaveLoad_RoundTrip_ActiveBlueprintMissionCode", Test_RoundTrip_ActiveBlueprintMissionCode);
            runTest("M6_04_SaveLoad_RoundTrip_CompletedCampaignEndReport", Test_RoundTrip_CompletedCampaignEndReport);
            runTest("M6_04_SaveLoad_Idempotent_DoubleSerializationEquals", Test_Idempotent_DoubleSerializationEquals);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private static void AssertApproxEqual(double expected, double actual, double tolerance = 1e-4, string message = "")
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"Assertion Failed: {message}. Expected: {expected:0.####}, Actual: {actual:0.####}");
        }

        private static List<ResearchNodeRecord> CreateMockResearchCatalog()
        {
            string json = @"[
              {
                ""id"": ""tech_propulsion_1"",
                ""nameKey"": ""Basic Propulsion"",
                ""descKey"": ""Basic propulsion tech"",
                ""icon"": ""icon_prop"",
                ""branch"": ""Propulsion"",
                ""prerequisites"": [],
                ""researchPointCost"": 50,
                ""baseDurationDays"": 30,
                ""startCostMUSD"": 10.0,
                ""tier"": 1
              },
              {
                ""id"": ""tech_propulsion_2"",
                ""nameKey"": ""Advanced Propulsion"",
                ""descKey"": ""Advanced propulsion tech"",
                ""icon"": ""icon_prop2"",
                ""branch"": ""Propulsion"",
                ""prerequisites"": [""tech_propulsion_1""],
                ""researchPointCost"": 100,
                ""baseDurationDays"": 60,
                ""startCostMUSD"": 25.0,
                ""tier"": 2
              }
            ]";

            var col = CatalogLoader.Load<ResearchNodeRecord>(json);
            return new List<ResearchNodeRecord>(col.All);
        }

        private static List<MilestoneRecord> CreateMockMilestoneCatalog()
        {
            return new List<MilestoneRecord>
            {
                new MilestoneRecord
                {
                    Id = "ms_orbit",
                    Title = "First Orbit",
                    Description = "Enter stable orbit",
                    TargetDestinationId = "earth_leo",
                    TargetMissionTypeId = "orbiter",
                    RewardBudget = 100,
                    RewardReputation = 15,
                    OrderIndex = 1
                },
                new MilestoneRecord
                {
                    Id = "ms_moon",
                    Title = "Lunar Landing",
                    Description = "Land on Moon",
                    TargetDestinationId = "moon",
                    TargetMissionTypeId = "lander",
                    RequiredMilestoneIds = new List<string> { "ms_orbit" },
                    RewardBudget = 250,
                    RewardReputation = 30,
                    OrderIndex = 2
                }
            };
        }

        private static void Test_RoundTrip_ResourcesAndAgency()
        {
            var initial = new CampaignState(seed: 12345UL, campaignId: "CAM-001");
            initial.AgencyName = "Galactic Odyssey Agency";
            initial.Budget = 750.5;
            initial.Reputation = 65.0;
            initial.ResearchPoints = 120.0;
            initial.KnowledgePoints = 45.0;
            initial.SelectedRegionId = "steppe_cosmodrome";
            initial.ActiveMapLens = MapLens.LaunchEfficiency;

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState(seed: 999UL, campaignId: "CAM-TEMP");
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual("Galactic Odyssey Agency", restored.AgencyName, "Agency name match");
            AssertApproxEqual(750.5, restored.Budget, 1e-4, "Budget match");
            AssertApproxEqual(65.0, restored.Reputation, 1e-4, "Reputation match");
            AssertApproxEqual(120.0, restored.ResearchPoints, 1e-4, "Research points match");
            AssertApproxEqual(45.0, restored.KnowledgePoints, 1e-4, "Knowledge points match");
            AssertEqual("steppe_cosmodrome", restored.SelectedRegionId, "Selected region match");
            AssertEqual(MapLens.LaunchEfficiency, restored.ActiveMapLens, "Map lens match");
        }

        private static void Test_RoundTrip_TimeSystem()
        {
            var initial = new CampaignState(seed: 42UL);
            initial.Execute(new AdvanceTimeCommand(120));

            int elapsedDays = initial.Time.ElapsedDays;
            long tick = initial.Time.Tick;
            GameDate date = initial.Time.CurrentDate;

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState(seed: 42UL);
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual(elapsedDays, restored.Time.ElapsedDays, "Elapsed days match");
            AssertEqual(tick, restored.Time.Tick, "Tick match");
            AssertEqual(date, restored.Time.CurrentDate, "Current date match");
        }

        private static void Test_RoundTrip_Bases()
        {
            var initial = new CampaignState();
            initial.Bases.Clear();
            initial.Bases["base_custom_pad"] = new BaseFacility("base_custom_pad", "pacific_atoll", BaseType.LaunchPad, level: 3);
            initial.Bases["base_custom_factory"] = new BaseFacility("base_custom_factory", "pacific_atoll", BaseType.Factory, level: 2);

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual(2, restored.Bases.Count, "Base count match");
            AssertTrue(restored.Bases.ContainsKey("base_custom_pad"), "Contains pad");
            AssertEqual(3, restored.Bases["base_custom_pad"].Level, "Pad level match");
            AssertEqual("pacific_atoll", restored.Bases["base_custom_pad"].RegionId, "Pad region match");
            AssertEqual(BaseType.LaunchPad, restored.Bases["base_custom_pad"].Type, "Pad type match");
            AssertEqual(2, restored.Bases["base_custom_factory"].Level, "Factory level match");
        }

        private static void Test_RoundTrip_ResearchTreeAndQueue()
        {
            var catalog = CreateMockResearchCatalog();

            var initial = new CampaignState();
            initial.InitialiseResearch(catalog);
            initial.Research.RestoreNodeState("tech_propulsion_1", ResearchStatus.Completed, progressDays: 30, startTick: 10, completedTick: 40, pointsSpent: 50);
            initial.Research.RestoreNodeState("tech_propulsion_2", ResearchStatus.InProgress, progressDays: 15, startTick: 45, completedTick: -1, pointsSpent: 25);
            initial.Research.RestoreQueue(new[] { "tech_propulsion_2" });
            initial.Research.RefreshAllUnlockStates();

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            restored.InitialiseResearch(catalog);
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual(ResearchStatus.Completed, restored.Research.GetStatus("tech_propulsion_1"), "Tech 1 completed");
            AssertEqual(ResearchStatus.InProgress, restored.Research.GetStatus("tech_propulsion_2"), "Tech 2 in progress");
            AssertEqual(15, restored.Research.GetNodeState("tech_propulsion_2").ProgressDays, "Tech 2 progress days");
            AssertEqual("tech_propulsion_2", restored.Research.ActiveNodeId, "Tech 2 in active queue");
        }

        private static void Test_RoundTrip_Milestones()
        {
            var catalog = CreateMockMilestoneCatalog();

            var initial = new CampaignState();
            initial.InitialiseMilestones(catalog);
            if (initial.Milestones.TryGetMilestone("ms_orbit", out var p1))
            {
                p1.Status = MilestoneStatus.Claimed;
                p1.CompletedDate = "2032-05-15";
            }

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            restored.InitialiseMilestones(catalog);
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertTrue(restored.Milestones.TryGetMilestone("ms_orbit", out var restP1), "Finds ms_orbit");
            AssertEqual(MilestoneStatus.Claimed, restP1.Status, "Milestone claimed status");
            AssertEqual("2032-05-15", restP1.CompletedDate, "Milestone completed date");
        }

        private static void Test_RoundTrip_MissionsAndReports()
        {
            var initial = new CampaignState();

            var bp = new Blueprint("bp_voyager", "Voyager I", targetDestinationId: "moon", targetMissionTypeId: "lander");
            var mission = new MissionInstance("msn_001", "Lunar Odyssey", "moon", bp, cruiseDurationDays: 4, missionSeed: 777UL)
            {
                Status = MissionStatus.Completed,
                CruiseProgress = 1.0,
                LaunchTick = 10,
                CruiseStartTick = 12,
                AccumulatedDownlinkDataGb = 15.5,
                AccumulatedRawDataGeneratedGb = 18.0,
                AccumulatedDroppedDataGb = 2.5,
                ScienceDaysCompleted = 5
            };

            var scienceSummary = new ScienceDeliverySummary(18.0, 15.5, 2.5);
            mission.Report = new MissionReport(
                missionId: "msn_001",
                missionName: "Lunar Odyssey",
                destinationId: "moon",
                isOverallSuccess: true,
                finalStatusSummary: "Tarihi Başarı",
                criteriaResults: null,
                scienceSummary: scienceSummary,
                causeAndEffectChain: null,
                campaignImpact: null,
                lessonsLearned: null);

            initial.Missions[mission.MissionId] = mission;

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual(1, restored.Missions.Count, "One mission restored");
            AssertTrue(restored.Missions.TryGetValue("msn_001", out var restMsn), "Mission found");
            AssertEqual("Lunar Odyssey", restMsn.MissionName, "Mission name match");
            AssertEqual(MissionStatus.Completed, restMsn.Status, "Mission status match");
            AssertApproxEqual(15.5, restMsn.AccumulatedDownlinkDataGb, 1e-4, "Downlinked Gb match");
            AssertTrue(restMsn.Report != null, "Report restored");
            AssertTrue(restMsn.Report.IsOverallSuccess, "Report overall success match");
            AssertEqual("Tarihi Başarı", restMsn.Report.FinalStatusSummary, "Status summary match");
        }

        private static void Test_RoundTrip_ActiveBlueprintMissionCode()
        {
            var initial = new CampaignState();
            initial.ActiveBlueprint = new Blueprint("bp_test", "Apollo Alpha", targetDestinationId: "moon", targetMissionTypeId: "crewed_lander");

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertTrue(restored.ActiveBlueprint != null, "Active blueprint restored");
            AssertEqual("Apollo Alpha", restored.ActiveBlueprint.Name, "Blueprint name match");
            AssertEqual("moon", restored.ActiveBlueprint.TargetDestinationId, "Destination match");
            AssertEqual("crewed_lander", restored.ActiveBlueprint.TargetMissionTypeId, "Mission type match");
        }

        private static void Test_RoundTrip_CompletedCampaignEndReport()
        {
            var initial = new CampaignState();
            initial.AgencyName = "Global Space Endeavor";
            initial.Execute(new EndCampaignCommand(CampaignEndStatus.AllMilestonesCompleted));

            string json = CampaignSaveSerializer.ToJson(initial);
            var loadedData = CampaignSaveSerializer.FromJson(json);

            var restored = new CampaignState();
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedData);

            AssertEqual(CampaignEndStatus.AllMilestonesCompleted, restored.EndStatus, "End status restored");
            AssertTrue(restored.EndReport != null, "End report restored");
            AssertEqual("Global Space Endeavor", restored.EndReport.AgencyName, "End report agency name");
            AssertEqual(CampaignEndStatus.AllMilestonesCompleted, restored.EndReport.EndStatus, "End report status");
        }

        private static void Test_Idempotent_DoubleSerializationEquals()
        {
            var initial = new CampaignState(seed: 888UL, campaignId: "CAM-IDEMPOTENT");
            initial.AgencyName = "Strict Idempotency Directorate";
            initial.Budget = 1000.0;
            initial.Reputation = 90.0;
            initial.Bases["base_main"] = new BaseFacility("base_main", "cape_peninsula", BaseType.LaunchPad, level: 4);

            string firstJson = CampaignSaveSerializer.ToJson(initial);
            var loadedFirst = CampaignSaveSerializer.FromJson(firstJson);

            var restored = new CampaignState(seed: 888UL, campaignId: "CAM-IDEMPOTENT");
            CampaignSaveSerializer.RestoreFromSaveData(restored, loadedFirst);

            string secondJson = CampaignSaveSerializer.ToJson(restored);

            AssertEqual(firstJson, secondJson, "Double serialization produces identical JSON output");
        }
    }
}
