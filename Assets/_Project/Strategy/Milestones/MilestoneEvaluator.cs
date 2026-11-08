using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy.Mission;

namespace Apogee.Strategy.Milestones
{
    /// <summary>
    /// Evaluates mission results against milestone definitions to advance the agency quest board.
    /// Pure functional calculation. GDD §4.4, §19.13, §21 M6-01.
    /// </summary>
    public static class MilestoneEvaluator
    {
        public static bool DoesMissionSatisfyMilestone(MilestoneRecord milestone, MissionInstance mission)
        {
            if (milestone == null || mission == null)
                return false;

            // Only successfully completed missions can fulfill milestones
            if (mission.Status != MissionStatus.Completed)
                return false;

            if (mission.Report == null || !mission.Report.IsOverallSuccess)
                return false;

            // Destination match check
            if (!string.IsNullOrWhiteSpace(milestone.TargetDestinationId))
            {
                if (!string.Equals(milestone.TargetDestinationId, mission.DestinationId, StringComparison.OrdinalIgnoreCase))
                {
                    // Allow mars interplanetary probe to match either mars or venus
                    if (milestone.Id == "milestone_interplanetary_probe")
                    {
                        if (!string.Equals("mars", mission.DestinationId, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals("venus", mission.DestinationId, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Mission type match check
            if (!string.IsNullOrWhiteSpace(milestone.TargetMissionTypeId))
            {
                var profileTypeId = mission.Profile?.TargetMissionTypeId;
                if (!string.Equals(milestone.TargetMissionTypeId, profileTypeId, StringComparison.OrdinalIgnoreCase))
                {
                    // If mission is crewed and milestone requires crewed, it satisfies
                    if (milestone.IsCrewed == true && mission.Profile != null && mission.Profile.IsCrewed)
                    {
                        // OK
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Crewed check
            if (milestone.IsCrewed.HasValue)
            {
                bool isMissionCrewed = mission.Profile != null && mission.Profile.IsCrewed;
                if (milestone.IsCrewed.Value != isMissionCrewed)
                    return false;
            }

            // Sample return check
            if (milestone.RequiresSampleReturn == true)
            {
                bool isSampleReturn = string.Equals(mission.Profile?.TargetMissionTypeId, "sample_return", StringComparison.OrdinalIgnoreCase);
                if (!isSampleReturn)
                    return false;
            }

            return true;
        }

        public static List<string> CheckAllPendingMilestones(
            IReadOnlyDictionary<string, MilestoneProgress> milestones,
            IEnumerable<MissionInstance> missions,
            string currentDate = null)
        {
            var newlyCompletedIds = new List<string>();
            var missionList = missions?.ToList() ?? new List<MissionInstance>();

            foreach (var kvp in milestones.OrderBy(m => m.Value.Record.OrderIndex))
            {
                var progress = kvp.Value;
                if (progress.Status == MilestoneStatus.Claimed || progress.Status == MilestoneStatus.CompletedPendingClaim)
                    continue;

                // Check eligibility
                if (!progress.IsEligible(milestones))
                {
                    progress.Status = MilestoneStatus.Locked;
                    continue;
                }

                // If eligible and was locked, mark available
                if (progress.Status == MilestoneStatus.Locked)
                {
                    progress.Status = MilestoneStatus.Available;
                }

                // Check if any completed mission satisfies this milestone
                foreach (var mission in missionList)
                {
                    if (DoesMissionSatisfyMilestone(progress.Record, mission))
                    {
                        progress.Status = MilestoneStatus.CompletedPendingClaim;
                        progress.CompletedByMissionId = mission.MissionId;
                        progress.CompletedDate = currentDate ?? string.Empty;
                        newlyCompletedIds.Add(progress.Record.Id);
                        break;
                    }
                }
            }

            return newlyCompletedIds;
        }
    }
}
