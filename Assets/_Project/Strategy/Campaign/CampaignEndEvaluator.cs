using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;

namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Pure-static evaluator that determines whether a campaign has ended and builds
    /// the final legacy report.
    ///
    /// GDD §4.1 (time boundary), §4.4 (all milestones), §4.5 (bankruptcy, retirement).
    /// M6-02 — no Unity dependencies, no I/O.
    /// </summary>
    public static class CampaignEndEvaluator
    {
        // ---------------------------------------------------------------- condition evaluation

        /// <summary>
        /// Checks the four termination conditions in priority order and returns the first
        /// matching status (or <see cref="CampaignEndStatus.Active"/> if none apply).
        ///
        /// Priority:
        ///   1. Bankruptcy  — Budget ≤ 0 AND Reputation ≤ 0
        ///   2. TimeExpired — campaign clock has passed 2061-01-01
        ///   3. AllMilestonesCompleted — all catalogued milestones are Claimed
        ///   (VoluntaryRetirement is set explicitly by the player via EndCampaignCommand.)
        /// </summary>
        public static CampaignEndStatus EvaluateEndCondition(CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // 1. Bankruptcy (GDD §4.5)
            if (state.Budget <= 0.0 && state.Reputation <= 0.0)
                return CampaignEndStatus.Bankruptcy;

            // 2. Calendar boundary (GDD §4.1)
            if (state.Time.IsExpired)
                return CampaignEndStatus.TimeExpired;

            // 3. All milestones claimed — ultimate victory (GDD §4.4)
            if (state.Milestones != null && AllMilestonesClaimed(state.Milestones))
                return CampaignEndStatus.AllMilestonesCompleted;

            return CampaignEndStatus.Active;
        }

        // ---------------------------------------------------------------- report builder

        /// <summary>
        /// Assembles a <see cref="CampaignEndReport"/> from the final campaign state.
        /// Call after <see cref="EvaluateEndCondition"/> returns a non-Active status (or
        /// when the player triggers voluntary retirement).
        /// </summary>
        public static CampaignEndReport BuildEndReport(CampaignState state, CampaignEndStatus status)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            GameDate startDate = new GameDate(0);     // campaign epoch: 2031-01-01
            GameDate endDate   = state.Time.CurrentDate;

            // --- mission aggregates ------------------------------------------------
            int launched  = 0;
            int succeeded = 0;
            int failed    = 0;
            int crewLoss  = 0;
            double totalScienceGb = 0.0;

            foreach (var kvp in state.Missions)
            {
                MissionInstance m = kvp.Value;
                if (m.Status == MissionStatus.Completed || m.Status == MissionStatus.Failed)
                    launched++;

                if (m.Report != null)
                {
                    if (m.Report.IsOverallSuccess) succeeded++;
                    else                           failed++;

                    if (m.LaunchResult != null && !m.LaunchResult.IsCrewSafe)
                        crewLoss++;
                }

                totalScienceGb += m.AccumulatedDownlinkDataGb;
            }

            // --- milestone counts --------------------------------------------------
            int milestoneClaimed = 0;
            int milestoneTotal   = 0;
            if (state.Milestones != null)
            {
                milestoneTotal   = state.Milestones.AllMilestones.Count;
                milestoneClaimed = state.Milestones.GetClaimedMilestones().Count;
            }

            // --- conclusion text ---------------------------------------------------
            string conclusion = BuildConclusionText(status, state.AgencyName);

            // --- key achievements list ---------------------------------------------
            var achievements = BuildKeyAchievements(state, succeeded, milestoneClaimed, totalScienceGb);

            return new CampaignEndReport(
                campaignId: state.CampaignId,
                agencyName: state.AgencyName,
                endStatus: status,
                conclusionSummary: conclusion,
                startDate: startDate,
                endDate: endDate,
                finalBudget: state.Budget,
                finalReputation: state.Reputation,
                finalKnowledgePoints: state.KnowledgePoints,
                totalMissionsLaunched: launched,
                totalMissionsSucceeded: succeeded,
                totalMissionsFailed: failed,
                milestonesClaimedCount: milestoneClaimed,
                totalMilestonesCount: milestoneTotal,
                crewLossIncidents: crewLoss,
                totalScienceDataDownlinkedGb: totalScienceGb,
                keyAchievements: achievements);
        }

        // ---------------------------------------------------------------- helpers

        private static bool AllMilestonesClaimed(MilestoneManager manager)
        {
            var all = manager.AllMilestones;
            return all.Count > 0 && all.Values.All(p => p.Status == MilestoneStatus.Claimed);
        }

        private static string BuildConclusionText(CampaignEndStatus status, string agencyName)
        {
            switch (status)
            {
                case CampaignEndStatus.Bankruptcy:
                    return $"{agencyName} ran out of funding and public support. The agency was disbanded.";
                case CampaignEndStatus.TimeExpired:
                    return $"The 30-year campaign window closed in 2061. {agencyName} leaves a lasting legacy in space exploration.";
                case CampaignEndStatus.AllMilestonesCompleted:
                    return $"{agencyName} achieved every core milestone — a historic triumph for humanity.";
                case CampaignEndStatus.VoluntaryRetirement:
                    return $"{agencyName} chose to retire gracefully after a distinguished run.";
                default:
                    return $"{agencyName} campaign concluded.";
            }
        }

        private static IEnumerable<string> BuildKeyAchievements(
            CampaignState state,
            int successfulMissions,
            int milestoneClaimed,
            double totalScienceGb)
        {
            var list = new List<string>();

            if (successfulMissions >= 10)
                list.Add($"Completed {successfulMissions} successful missions");

            if (milestoneClaimed > 0)
                list.Add($"Claimed {milestoneClaimed} of {(state.Milestones?.AllMilestones.Count ?? 0)} milestones");

            if (totalScienceGb >= 1.0)
                list.Add($"Downlinked {totalScienceGb:F1} GB of science data");

            if (state.Reputation >= 80.0)
                list.Add($"Achieved elite agency reputation ({state.Reputation:F0}/100)");

            if (state.Research != null)
            {
                int unlockedCount = state.Research.CompletedNodeIds.Count();
                if (unlockedCount >= 15)
                    list.Add($"Unlocked {unlockedCount} technologies");
            }

            return list;
        }
    }
}
