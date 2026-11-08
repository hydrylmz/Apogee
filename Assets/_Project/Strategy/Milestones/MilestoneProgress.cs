using System;
using Apogee.Data;

namespace Apogee.Strategy.Milestones
{
    /// <summary>
    /// Runtime progress and state for a single campaign milestone.
    /// </summary>
    public sealed class MilestoneProgress
    {
        public MilestoneRecord Record { get; }
        public MilestoneStatus Status { get; set; }
        public string CompletedByMissionId { get; set; }
        public string CompletedDate { get; set; }

        public MilestoneProgress(MilestoneRecord record, MilestoneStatus initialStatus = MilestoneStatus.Locked)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Status = initialStatus;
        }

        public bool IsEligible(System.Collections.Generic.IReadOnlyDictionary<string, MilestoneProgress> allMilestones)
        {
            if (Record.RequiredMilestoneIds == null || Record.RequiredMilestoneIds.Count == 0)
                return true;

            foreach (var reqId in Record.RequiredMilestoneIds)
            {
                if (!allMilestones.TryGetValue(reqId, out var reqProgress))
                    return false;

                // A milestone only unlocks subsequent milestones once it is claimed
                if (reqProgress.Status != MilestoneStatus.Claimed)
                    return false;
            }

            return true;
        }
    }
}
