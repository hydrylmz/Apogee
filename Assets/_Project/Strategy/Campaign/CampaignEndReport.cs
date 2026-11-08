using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;

namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Comprehensive end-of-campaign diagnostic and legacy report.
    /// Complies with GDD §4.1, §4.5, §19.13, and §21 M6-02.
    /// </summary>
    public sealed class CampaignEndReport
    {
        public string CampaignId { get; }
        public string AgencyName { get; }
        public CampaignEndStatus EndStatus { get; }
        public string ConclusionSummary { get; }
        public GameDate StartDate { get; }
        public GameDate EndDate { get; }
        public int TotalYearsActive { get; }
        public double FinalBudget { get; }
        public double FinalReputation { get; }
        public double FinalKnowledgePoints { get; }

        public int TotalMissionsLaunched { get; }
        public int TotalMissionsSucceeded { get; }
        public int TotalMissionsFailed { get; }
        public double MissionSuccessRate { get; }

        public int MilestonesClaimedCount { get; }
        public int TotalMilestonesCount { get; }

        public int CrewLossIncidents { get; }
        public double TotalScienceDataDownlinkedGb { get; }

        public IReadOnlyList<string> KeyAchievements { get; }

        public CampaignEndReport(
            string campaignId,
            string agencyName,
            CampaignEndStatus endStatus,
            string conclusionSummary,
            GameDate startDate,
            GameDate endDate,
            double finalBudget,
            double finalReputation,
            double finalKnowledgePoints,
            int totalMissionsLaunched,
            int totalMissionsSucceeded,
            int totalMissionsFailed,
            int milestonesClaimedCount,
            int totalMilestonesCount,
            int crewLossIncidents,
            double totalScienceDataDownlinkedGb,
            IEnumerable<string> keyAchievements)
        {
            CampaignId = campaignId ?? "CAM-DEFAULT";
            AgencyName = agencyName ?? "Birleşik Yörünge Ajansı";
            EndStatus = endStatus;
            ConclusionSummary = conclusionSummary ?? string.Empty;
            StartDate = startDate;
            EndDate = endDate;
            TotalYearsActive = Math.Max(0, (endDate.TotalDays - startDate.TotalDays) / 365);
            FinalBudget = finalBudget;
            FinalReputation = finalReputation;
            FinalKnowledgePoints = finalKnowledgePoints;

            TotalMissionsLaunched = totalMissionsLaunched;
            TotalMissionsSucceeded = totalMissionsSucceeded;
            TotalMissionsFailed = totalMissionsFailed;
            MissionSuccessRate = totalMissionsLaunched > 0
                ? (double)totalMissionsSucceeded / totalMissionsLaunched
                : 0.0;

            MilestonesClaimedCount = milestonesClaimedCount;
            TotalMilestonesCount = totalMilestonesCount;
            CrewLossIncidents = crewLossIncidents;
            TotalScienceDataDownlinkedGb = totalScienceDataDownlinkedGb;
            KeyAchievements = keyAchievements != null ? keyAchievements.ToList() : new List<string>();
        }

        public override string ToString() =>
            $"[CampaignEndReport:{CampaignId}] {AgencyName} - {EndStatus} ({TotalYearsActive} years, {TotalMissionsSucceeded}/{TotalMissionsLaunched} missions, {MilestonesClaimedCount}/{TotalMilestonesCount} milestones)";
    }
}
