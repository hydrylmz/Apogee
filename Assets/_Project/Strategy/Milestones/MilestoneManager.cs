using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy.Mission;

namespace Apogee.Strategy.Milestones
{
    /// <summary>
    /// Runtime manager for the campaign quest board and milestones (GDD §4.4, §19.13, §21 M6-01).
    /// </summary>
    public sealed class MilestoneManager
    {
        private readonly Dictionary<string, MilestoneProgress> _milestones =
            new Dictionary<string, MilestoneProgress>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, MilestoneProgress> AllMilestones => _milestones;

        public event Action<MilestoneProgress> OnMilestoneCompleted;
        public event Action<MilestoneProgress> OnMilestoneClaimed;

        public MilestoneManager(IEnumerable<MilestoneRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            foreach (var rec in records)
            {
                var initialStatus = (rec.RequiredMilestoneIds == null || rec.RequiredMilestoneIds.Count == 0)
                    ? MilestoneStatus.Available
                    : MilestoneStatus.Locked;

                _milestones[rec.Id] = new MilestoneProgress(rec, initialStatus);
            }
        }

        public bool TryGetMilestone(string milestoneId, out MilestoneProgress progress)
        {
            return _milestones.TryGetValue(milestoneId, out progress);
        }

        public List<MilestoneProgress> GetAvailableMilestones() =>
            _milestones.Values.Where(m => m.Status == MilestoneStatus.Available).OrderBy(m => m.Record.OrderIndex).ToList();

        public List<MilestoneProgress> GetPendingClaimMilestones() =>
            _milestones.Values.Where(m => m.Status == MilestoneStatus.CompletedPendingClaim).OrderBy(m => m.Record.OrderIndex).ToList();

        public List<MilestoneProgress> GetClaimedMilestones() =>
            _milestones.Values.Where(m => m.Status == MilestoneStatus.Claimed).OrderBy(m => m.Record.OrderIndex).ToList();

        public List<string> EvaluateProgress(IEnumerable<MissionInstance> missions, string currentDate)
        {
            var completedIds = MilestoneEvaluator.CheckAllPendingMilestones(_milestones, missions, currentDate);
            foreach (var id in completedIds)
            {
                if (_milestones.TryGetValue(id, out var progress))
                {
                    OnMilestoneCompleted?.Invoke(progress);
                }
            }
            return completedIds;
        }

        public void MarkClaimed(string milestoneId)
        {
            if (_milestones.TryGetValue(milestoneId, out var progress))
            {
                progress.Status = MilestoneStatus.Claimed;

                // Re-evaluate eligibility for remaining milestones to unlock newly available ones
                foreach (var other in _milestones.Values)
                {
                    if (other.Status == MilestoneStatus.Locked && other.IsEligible(_milestones))
                    {
                        other.Status = MilestoneStatus.Available;
                    }
                }

                OnMilestoneClaimed?.Invoke(progress);
            }
        }

        public void RevertClaim(string milestoneId, MilestoneStatus previousStatus)
        {
            if (_milestones.TryGetValue(milestoneId, out var progress))
            {
                progress.Status = previousStatus;

                // Lock milestones whose prerequisites are no longer claimed/completed
                foreach (var other in _milestones.Values)
                {
                    if (other.Status == MilestoneStatus.Available && !other.IsEligible(_milestones))
                    {
                        other.Status = MilestoneStatus.Locked;
                    }
                }
            }
        }
    }
}
