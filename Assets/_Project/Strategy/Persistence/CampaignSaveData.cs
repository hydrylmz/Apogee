using System;
using System.Collections.Generic;
using Apogee.Design;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;

namespace Apogee.Strategy.Persistence
{
    /// <summary>
    /// Root persistent data container for campaign state (GDD §17.5, §19.13, §21 M6-04).
    /// Versioned schema enabling migration and backward compatibility.
    /// WebGL compliant: pure data contract without file system or engine references.
    /// </summary>
    public sealed class CampaignSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string CampaignId { get; set; }
        public ulong Seed { get; set; }
        public string AgencyName { get; set; }
        public CampaignEndStatus EndStatus { get; set; }
        public CampaignEndReportSaveData EndReport { get; set; }

        // Time
        public int ElapsedDays { get; set; }
        public long CurrentTick { get; set; }

        // Resources
        public double Budget { get; set; }
        public double Reputation { get; set; }
        public double ResearchPoints { get; set; }
        public double KnowledgePoints { get; set; }

        // Strategy map
        public string SelectedRegionId { get; set; }
        public int ActiveMapLens { get; set; }

        // Bases
        public List<BaseFacilitySaveData> Bases { get; set; } = new();

        // Research
        public List<ResearchNodeSaveData> ResearchNodes { get; set; } = new();
        public List<string> ResearchQueue { get; set; } = new();

        // Milestones
        public List<MilestoneProgressSaveData> Milestones { get; set; } = new();

        // Active Blueprint Mission Code
        public string ActiveBlueprintMissionCode { get; set; }

        // Completed / Active Missions
        public List<MissionInstanceSaveData> Missions { get; set; } = new();
    }

    public sealed class BaseFacilitySaveData
    {
        public string Id { get; set; }
        public string RegionId { get; set; }
        public BaseType Type { get; set; }
        public int Level { get; set; }
    }

    public sealed class ResearchNodeSaveData
    {
        public string NodeId { get; set; }
        public ResearchStatus Status { get; set; }
        public int ProgressDays { get; set; }
        public long StartTick { get; set; }
        public long CompletedTick { get; set; }
        public double PointsSpent { get; set; }
    }

    public sealed class MilestoneProgressSaveData
    {
        public string MilestoneId { get; set; }
        public MilestoneStatus Status { get; set; }
        public int CompletedMissionCount { get; set; }
        public string CompletionDate { get; set; }
    }

    public sealed class MissionInstanceSaveData
    {
        public string MissionId { get; set; }
        public string MissionName { get; set; }
        public string DestinationId { get; set; }
        public MissionStatus Status { get; set; }
        public string BlueprintMissionCode { get; set; }
        public int CruiseDurationDays { get; set; }
        public ulong MissionSeed { get; set; }
        public double CruiseProgress { get; set; }
        public int LaunchTick { get; set; }
        public int CruiseStartTick { get; set; }
        public double AccumulatedDownlinkDataGb { get; set; }
        public double AccumulatedRawDataGeneratedGb { get; set; }
        public double AccumulatedDroppedDataGb { get; set; }
        public int ScienceDaysCompleted { get; set; }
        public bool HasReport { get; set; }
        public bool IsOverallSuccess { get; set; }
        public string FinalStatusSummary { get; set; }
        public double TotalScienceDataDownlinkedGb { get; set; }
    }

    public sealed class CampaignEndReportSaveData
    {
        public string CampaignId { get; set; }
        public string AgencyName { get; set; }
        public CampaignEndStatus EndStatus { get; set; }
        public string ConclusionSummary { get; set; }
        public int StartTotalDays { get; set; }
        public int EndTotalDays { get; set; }
        public double FinalBudget { get; set; }
        public double FinalReputation { get; set; }
        public double FinalKnowledgePoints { get; set; }
        public int TotalMissionsLaunched { get; set; }
        public int TotalMissionsSucceeded { get; set; }
        public int TotalMissionsFailed { get; set; }
        public int MilestonesClaimedCount { get; set; }
        public int TotalMilestonesCount { get; set; }
        public int CrewLossIncidents { get; set; }
        public double TotalScienceDataDownlinkedGb { get; set; }
        public List<string> KeyAchievements { get; set; } = new();
    }
}
