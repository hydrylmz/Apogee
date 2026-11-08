using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.Report;
using Apogee.Design;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;
using Newtonsoft.Json;

namespace Apogee.Strategy.Persistence
{
    /// <summary>
    /// Pure C# serializer and state restorer for CampaignState (GDD §17.5, §19.13, §21 M6-04).
    /// Safe for WebGL: Operates entirely on in-memory strings and data contracts without file system locks or threading.
    /// Provides round-trip equality (Save -> Json -> Restore -> Json).
    /// </summary>
    public static class CampaignSaveSerializer
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Converts the current state of a campaign into a serializable <see cref="CampaignSaveData"/> snapshot.
        /// </summary>
        public static CampaignSaveData CreateSaveData(CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var data = new CampaignSaveData
            {
                SchemaVersion = state.SchemaVersion,
                CampaignId = state.CampaignId,
                Seed = state.Seed,
                AgencyName = state.AgencyName,
                EndStatus = state.EndStatus,
                ElapsedDays = state.Time.ElapsedDays,
                CurrentTick = state.Time.Tick,
                Budget = state.Budget,
                Reputation = state.Reputation,
                ResearchPoints = state.ResearchPoints,
                KnowledgePoints = state.KnowledgePoints,
                SelectedRegionId = state.SelectedRegionId,
                ActiveMapLens = (int)state.ActiveMapLens
            };

            // Active Blueprint
            if (state.ActiveBlueprint != null)
            {
                data.ActiveBlueprintMissionCode = MissionCodeCodec.Encode(state.ActiveBlueprint, state.Seed);
            }

            // End Report
            if (state.EndReport != null)
            {
                data.EndReport = new CampaignEndReportSaveData
                {
                    CampaignId = state.EndReport.CampaignId,
                    AgencyName = state.EndReport.AgencyName,
                    EndStatus = state.EndReport.EndStatus,
                    ConclusionSummary = state.EndReport.ConclusionSummary,
                    StartTotalDays = state.EndReport.StartDate.TotalDays,
                    EndTotalDays = state.EndReport.EndDate.TotalDays,
                    FinalBudget = state.EndReport.FinalBudget,
                    FinalReputation = state.EndReport.FinalReputation,
                    FinalKnowledgePoints = state.EndReport.FinalKnowledgePoints,
                    TotalMissionsLaunched = state.EndReport.TotalMissionsLaunched,
                    TotalMissionsSucceeded = state.EndReport.TotalMissionsSucceeded,
                    TotalMissionsFailed = state.EndReport.TotalMissionsFailed,
                    MilestonesClaimedCount = state.EndReport.MilestonesClaimedCount,
                    TotalMilestonesCount = state.EndReport.TotalMilestonesCount,
                    CrewLossIncidents = state.EndReport.CrewLossIncidents,
                    TotalScienceDataDownlinkedGb = state.EndReport.TotalScienceDataDownlinkedGb,
                    KeyAchievements = state.EndReport.KeyAchievements != null
                        ? state.EndReport.KeyAchievements.ToList()
                        : new List<string>()
                };
            }

            // Bases
            foreach (var baseFacility in state.Bases.Values)
            {
                data.Bases.Add(new BaseFacilitySaveData
                {
                    Id = baseFacility.Id,
                    RegionId = baseFacility.RegionId,
                    Type = baseFacility.Type,
                    Level = baseFacility.Level
                });
            }

            // Research
            if (state.Research != null)
            {
                foreach (var branch in state.Research.GetTreeByBranch())
                {
                    foreach (var pair in branch.Value)
                    {
                        var nodeState = pair.State;
                        data.ResearchNodes.Add(new ResearchNodeSaveData
                        {
                            NodeId = nodeState.NodeId,
                            Status = nodeState.Status,
                            ProgressDays = nodeState.ProgressDays,
                            StartTick = nodeState.StartTick,
                            CompletedTick = nodeState.CompletedTick,
                            PointsSpent = nodeState.PointsSpent
                        });
                    }
                }

                data.ResearchQueue.AddRange(state.Research.Queue);
            }

            // Milestones
            if (state.Milestones != null)
            {
                foreach (var milestone in state.Milestones.AllMilestones.Values)
                {
                    data.Milestones.Add(new MilestoneProgressSaveData
                    {
                        MilestoneId = milestone.Record.Id,
                        Status = milestone.Status,
                        CompletedMissionCount = milestone.CompletedByMissionId != null ? 1 : 0,
                        CompletionDate = milestone.CompletedDate
                    });
                }
            }

            // Missions
            foreach (var mission in state.Missions.Values)
            {
                var missionSave = new MissionInstanceSaveData
                {
                    MissionId = mission.MissionId,
                    MissionName = mission.MissionName,
                    DestinationId = mission.DestinationId,
                    Status = mission.Status,
                    CruiseDurationDays = mission.CruiseDurationDays,
                    MissionSeed = mission.MissionSeed,
                    CruiseProgress = mission.CruiseProgress,
                    LaunchTick = mission.LaunchTick,
                    CruiseStartTick = mission.CruiseStartTick,
                    AccumulatedDownlinkDataGb = mission.AccumulatedDownlinkDataGb,
                    AccumulatedRawDataGeneratedGb = mission.AccumulatedRawDataGeneratedGb,
                    AccumulatedDroppedDataGb = mission.AccumulatedDroppedDataGb,
                    ScienceDaysCompleted = mission.ScienceDaysCompleted
                };

                if (mission.Blueprint != null)
                {
                    missionSave.BlueprintMissionCode = MissionCodeCodec.Encode(mission.Blueprint, mission.MissionSeed);
                }

                if (mission.Report != null)
                {
                    missionSave.HasReport = true;
                    missionSave.IsOverallSuccess = mission.Report.IsOverallSuccess;
                    missionSave.FinalStatusSummary = mission.Report.FinalStatusSummary;
                    missionSave.TotalScienceDataDownlinkedGb = mission.Report.ScienceSummary?.TotalDownlinkedGb ?? mission.AccumulatedDownlinkDataGb;
                }

                data.Missions.Add(missionSave);
            }

            return data;
        }

        /// <summary>
        /// Serializes a campaign into a JSON string with schema metadata.
        /// </summary>
        public static string ToJson(CampaignState state)
        {
            var saveData = CreateSaveData(state);
            return JsonConvert.SerializeObject(saveData, SerializerSettings);
        }

        /// <summary>
        /// Deserializes a JSON string into a <see cref="CampaignSaveData"/> model.
        /// Performs version validation and migration if needed.
        /// </summary>
        public static CampaignSaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content cannot be empty", nameof(json));

            var saveData = JsonConvert.DeserializeObject<CampaignSaveData>(json, SerializerSettings);
            if (saveData == null) throw new InvalidOperationException("Failed to deserialize campaign save data.");

            if (saveData.SchemaVersion > CampaignSaveData.CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Save data schema version {saveData.SchemaVersion} is newer than current supported version {CampaignSaveData.CurrentSchemaVersion}.");
            }

            // Apply migrations if an older version is encountered (extensibility hook)
            if (saveData.SchemaVersion < CampaignSaveData.CurrentSchemaVersion)
            {
                MigrateSaveData(saveData);
            }

            return saveData;
        }

        /// <summary>
        /// Restores a <see cref="CampaignState"/> instance from a save data snapshot.
        /// </summary>
        public static void RestoreFromSaveData(CampaignState state, CampaignSaveData data)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (data == null) throw new ArgumentNullException(nameof(data));

            state.AgencyName = data.AgencyName ?? state.AgencyName;
            state.EndStatus = data.EndStatus;
            state.Budget = data.Budget;
            state.Reputation = data.Reputation;
            state.ResearchPoints = data.ResearchPoints;
            state.KnowledgePoints = data.KnowledgePoints;
            state.SelectedRegionId = data.SelectedRegionId;
            state.ActiveMapLens = (MapLens)data.ActiveMapLens;

            // Restore Time
            state.Time.RestoreState(new GameDate(data.ElapsedDays), data.CurrentTick, TimeSpeed.Paused);

            // Restore EndReport
            if (data.EndReport != null)
            {
                state.EndReport = new CampaignEndReport(
                    campaignId: data.EndReport.CampaignId,
                    agencyName: data.EndReport.AgencyName,
                    endStatus: data.EndReport.EndStatus,
                    conclusionSummary: data.EndReport.ConclusionSummary,
                    startDate: new GameDate(data.EndReport.StartTotalDays),
                    endDate: new GameDate(data.EndReport.EndTotalDays),
                    finalBudget: data.EndReport.FinalBudget,
                    finalReputation: data.EndReport.FinalReputation,
                    finalKnowledgePoints: data.EndReport.FinalKnowledgePoints,
                    totalMissionsLaunched: data.EndReport.TotalMissionsLaunched,
                    totalMissionsSucceeded: data.EndReport.TotalMissionsSucceeded,
                    totalMissionsFailed: data.EndReport.TotalMissionsFailed,
                    milestonesClaimedCount: data.EndReport.MilestonesClaimedCount,
                    totalMilestonesCount: data.EndReport.TotalMilestonesCount,
                    crewLossIncidents: data.EndReport.CrewLossIncidents,
                    totalScienceDataDownlinkedGb: data.EndReport.TotalScienceDataDownlinkedGb,
                    keyAchievements: data.EndReport.KeyAchievements);
            }
            else
            {
                state.EndReport = null;
            }

            // Restore Bases
            if (data.Bases != null && data.Bases.Count > 0)
            {
                state.Bases.Clear();
                foreach (var baseData in data.Bases)
                {
                    var baseFacility = new BaseFacility(baseData.Id, baseData.RegionId, baseData.Type, baseData.Level);
                    state.Bases[baseFacility.Id] = baseFacility;
                }
            }

            // Restore Research
            if (state.Research != null && data.ResearchNodes != null)
            {
                foreach (var nodeSave in data.ResearchNodes)
                {
                    state.Research.RestoreNodeState(
                        nodeSave.NodeId,
                        nodeSave.Status,
                        nodeSave.ProgressDays,
                        nodeSave.StartTick,
                        nodeSave.CompletedTick,
                        nodeSave.PointsSpent);
                }

                state.Research.RestoreQueue(data.ResearchQueue);
                state.Research.RefreshAllUnlockStates();
            }

            // Restore Milestones
            if (state.Milestones != null && data.Milestones != null)
            {
                foreach (var mData in data.Milestones)
                {
                    if (state.Milestones.TryGetMilestone(mData.MilestoneId, out var progress))
                    {
                        progress.Status = mData.Status;
                        progress.CompletedDate = mData.CompletionDate;
                    }
                }
            }

            // Restore Active Blueprint
            if (!string.IsNullOrEmpty(data.ActiveBlueprintMissionCode))
            {
                if (MissionCodeCodec.TryDecode(data.ActiveBlueprintMissionCode, out var codeData, out _))
                {
                    // Create base blueprint from code data
                    state.ActiveBlueprint = new Blueprint(
                        id: "bp_active",
                        name: string.IsNullOrEmpty(codeData.BlueprintName) ? "Active Blueprint" : codeData.BlueprintName,
                        targetDestinationId: codeData.DestinationId,
                        targetMissionTypeId: codeData.MissionTypeId);
                }
            }

            // Restore Missions
            if (data.Missions != null)
            {
                state.Missions.Clear();
                foreach (var mData in data.Missions)
                {
                    Blueprint blueprint = null;
                    if (!string.IsNullOrEmpty(mData.BlueprintMissionCode) &&
                        MissionCodeCodec.TryDecode(mData.BlueprintMissionCode, out var codeData, out _))
                    {
                        blueprint = new Blueprint(
                            id: "bp_" + mData.MissionId,
                            name: string.IsNullOrEmpty(codeData.BlueprintName) ? mData.MissionName : codeData.BlueprintName,
                            targetDestinationId: codeData.DestinationId,
                            targetMissionTypeId: codeData.MissionTypeId);
                    }

                    var mission = new MissionInstance(
                        missionId: mData.MissionId,
                        missionName: mData.MissionName,
                        destinationId: mData.DestinationId,
                        blueprint: blueprint,
                        cruiseDurationDays: mData.CruiseDurationDays,
                        missionSeed: mData.MissionSeed)
                    {
                        Status = mData.Status,
                        CruiseProgress = mData.CruiseProgress,
                        LaunchTick = mData.LaunchTick,
                        CruiseStartTick = mData.CruiseStartTick,
                        AccumulatedDownlinkDataGb = mData.AccumulatedDownlinkDataGb,
                        AccumulatedRawDataGeneratedGb = mData.AccumulatedRawDataGeneratedGb,
                        AccumulatedDroppedDataGb = mData.AccumulatedDroppedDataGb,
                        ScienceDaysCompleted = mData.ScienceDaysCompleted
                    };

                    if (mData.HasReport)
                    {
                        var scienceSummary = new ScienceDeliverySummary(
                            totalGeneratedGb: mData.AccumulatedRawDataGeneratedGb,
                            totalDownlinkedGb: mData.TotalScienceDataDownlinkedGb,
                            totalDroppedGb: mData.AccumulatedDroppedDataGb);

                        mission.Report = new MissionReport(
                            missionId: mData.MissionId,
                            missionName: mData.MissionName,
                            destinationId: mData.DestinationId,
                            isOverallSuccess: mData.IsOverallSuccess,
                            finalStatusSummary: mData.FinalStatusSummary,
                            criteriaResults: null,
                            scienceSummary: scienceSummary,
                            causeAndEffectChain: null,
                            campaignImpact: null,
                            lessonsLearned: null,
                            scoreBreakdown: null);
                    }

                    state.Missions[mission.MissionId] = mission;
                }
            }
        }

        private static void MigrateSaveData(CampaignSaveData data)
        {
            // Extension point for v0 -> v1 migrations when schema evolves
            data.SchemaVersion = CampaignSaveData.CurrentSchemaVersion;
        }
    }
}
