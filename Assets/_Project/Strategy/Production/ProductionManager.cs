using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Strategy
{
    /// <summary>
    /// Coordinates agency manufacturing capacity, vehicle integration pipelines,
    /// test qualification programs, and queue congestion delays (GDD §9.3, §19.13).
    /// </summary>
    public sealed class ProductionManager
    {
        private readonly List<ProductionJob> _jobs;

        public event Action<ProductionJob> OnJobEnqueued;
        public event Action<ProductionJob> OnJobStarted;
        public event Action<ProductionJob> OnJobCompleted;
        public event Action<ProductionJob> OnJobCancelled;

        public IReadOnlyList<ProductionJob> AllJobs => _jobs;

        public IEnumerable<ProductionJob> ActiveJobs =>
            _jobs.Where(j => j.Status == ProductionJobStatus.InProgress);

        public IEnumerable<ProductionJob> QueuedJobs =>
            _jobs.Where(j => j.Status == ProductionJobStatus.Queued);

        public IEnumerable<ProductionJob> CompletedJobs =>
            _jobs.Where(j => j.Status == ProductionJobStatus.Completed);

        public ProductionManager()
        {
            _jobs = new List<ProductionJob>();
        }

        public ProductionJob GetJob(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;
            return _jobs.FirstOrDefault(j => string.Equals(j.Id, jobId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Total manufacturing throughput capacity units provided by all active agency factories.
        /// </summary>
        public double GetTotalFactoryCapacity(CampaignState state)
        {
            if (state == null || state.Bases == null) return 5.0; // Minimal baseline

            double total = 0.0;
            foreach (var b in state.Bases.Values)
            {
                if (b.Type == BaseType.Factory)
                {
                    total += BaseLevelSpec.GetCapacityRating(BaseType.Factory, b.Level);
                }
            }
            return Math.Max(5.0, total);
        }

        /// <summary>
        /// Total testing throughput capacity units provided by all active agency test facilities.
        /// </summary>
        public double GetTotalTestCapacity(CampaignState state)
        {
            if (state == null || state.Bases == null) return 0.0;

            double total = 0.0;
            foreach (var b in state.Bases.Values)
            {
                if (b.Type == BaseType.TestFacility)
                {
                    total += BaseLevelSpec.GetCapacityRating(BaseType.TestFacility, b.Level);
                }
            }
            return total;
        }

        /// <summary>
        /// Current capacity in active use by ongoing assembly and fabrication jobs.
        /// </summary>
        public double GetActiveFactoryCapacityUsage()
        {
            return _jobs
                .Where(j => j.Status == ProductionJobStatus.InProgress && j.JobType != ProductionJobType.SystemTesting)
                .Sum(j => j.RequiredCapacity);
        }

        /// <summary>
        /// Adds a job to the production queue, immediately activating it if agency capacity allows.
        /// </summary>
        public void EnqueueJob(ProductionJob job, CampaignState state)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            _jobs.Add(job);

            // Check if capacity permits immediate activation
            if (job.JobType == ProductionJobType.SystemTesting)
            {
                double testCap = GetTotalTestCapacity(state);
                if (testCap > 0.0)
                {
                    job.SetStatus(ProductionJobStatus.InProgress);
                    OnJobStarted?.Invoke(job);
                }
            }
            else
            {
                double totalCap = GetTotalFactoryCapacity(state);
                double currentUsage = GetActiveFactoryCapacityUsage();

                if (currentUsage + job.RequiredCapacity <= totalCap)
                {
                    job.SetStatus(ProductionJobStatus.InProgress);
                    OnJobStarted?.Invoke(job);
                }
            }

            OnJobEnqueued?.Invoke(job);
        }

        /// <summary>
        /// Cancels a job and promotes subsequent queued jobs that can fit within released capacity.
        /// </summary>
        public bool CancelJob(string jobId, CampaignState state)
        {
            var job = GetJob(jobId);
            if (job == null || job.IsFinished || job.Status == ProductionJobStatus.Cancelled)
                return false;

            job.MarkCancelled();
            OnJobCancelled?.Invoke(job);

            PromoteQueuedJobs(state);
            return true;
        }

        /// <summary>
        /// Removes a job completely from the system (used for command Undo).
        /// </summary>
        public bool RemoveJob(string jobId)
        {
            var job = GetJob(jobId);
            if (job == null) return false;
            return _jobs.Remove(job);
        }

        /// <summary>
        /// Advances daily work on all active jobs. Overcrowded queues experience throughput throttling
        /// and accumulate schedule delay days.
        /// </summary>
        public IReadOnlyList<ProductionJob> AdvanceDays(int days, GameDate currentDate, CampaignState state)
        {
            var completedThisTick = new List<ProductionJob>();
            if (days <= 0) return completedThisTick;

            double factoryCap = GetTotalFactoryCapacity(state);
            var activeFabrication = _jobs
                .Where(j => j.Status == ProductionJobStatus.InProgress && j.JobType != ProductionJobType.SystemTesting)
                .ToList();

            double demandedFactoryCap = activeFabrication.Sum(j => j.RequiredCapacity);
            double factoryWorkProgress = (double)days;

            // Capacity limit slowdown: if demanded capacity exceeds factory capacity, work is proportionally slowed
            if (demandedFactoryCap > factoryCap && demandedFactoryCap > 0.0)
            {
                double ratio = factoryCap / demandedFactoryCap;
                factoryWorkProgress = days * ratio;
                int delays = (int)Math.Ceiling(days * (1.0 - ratio));
                foreach (var j in activeFabrication)
                {
                    j.AddDelay(delays);
                }
            }

            foreach (var job in activeFabrication)
            {
                job.AddWorkDays(factoryWorkProgress);
                if (job.WorkedDays >= job.TotalWorkDays)
                {
                    job.MarkCompleted(currentDate);
                    completedThisTick.Add(job);
                    OnJobCompleted?.Invoke(job);
                }
            }

            // System testing jobs
            double testCap = GetTotalTestCapacity(state);
            var activeTesting = _jobs
                .Where(j => j.Status == ProductionJobStatus.InProgress && j.JobType == ProductionJobType.SystemTesting)
                .ToList();

            foreach (var job in activeTesting)
            {
                if (testCap <= 0.0)
                {
                    // No test facility available: testing is stalled and delayed
                    job.AddDelay(days);
                    continue;
                }

                job.AddWorkDays(days);
                if (job.WorkedDays >= job.TotalWorkDays)
                {
                    job.MarkCompleted(currentDate);
                    completedThisTick.Add(job);
                    OnJobCompleted?.Invoke(job);
                }
            }

            // After completions, promote any queued jobs whose capacity demands can now be met
            PromoteQueuedJobs(state);

            return completedThisTick;
        }

        /// <summary>
        /// Symmetrically restores state snapshots across all jobs (used for AdvanceTimeCommand Undo).
        /// </summary>
        public void UndoAdvance(GameDate previousDate)
        {
            foreach (var job in _jobs)
            {
                if (job.Status == ProductionJobStatus.Completed &&
                    job.CompletedDate.HasValue &&
                    job.CompletedDate.Value > previousDate)
                {
                    // Revert from Completed to InProgress
                    job.SetStatus(ProductionJobStatus.InProgress);
                }
            }
        }

        private void PromoteQueuedJobs(CampaignState state)
        {
            double totalCap = GetTotalFactoryCapacity(state);
            double currentUsage = GetActiveFactoryCapacityUsage();
            double testCap = GetTotalTestCapacity(state);

            foreach (var job in _jobs.Where(j => j.Status == ProductionJobStatus.Queued))
            {
                if (job.JobType == ProductionJobType.SystemTesting)
                {
                    if (testCap > 0.0)
                    {
                        job.SetStatus(ProductionJobStatus.InProgress);
                        OnJobStarted?.Invoke(job);
                    }
                }
                else
                {
                    if (currentUsage + job.RequiredCapacity <= totalCap)
                    {
                        job.SetStatus(ProductionJobStatus.InProgress);
                        currentUsage += job.RequiredCapacity;
                        OnJobStarted?.Invoke(job);
                    }
                }
            }
        }
    }
}
