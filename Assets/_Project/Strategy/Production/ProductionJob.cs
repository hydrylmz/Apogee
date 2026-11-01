using System;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Represents a discrete fabrication, integration assembly, or qualification testing job
    /// within the agency production queue (GDD §9.3, §19.13).
    /// </summary>
    public sealed class ProductionJob
    {
        public string Id { get; }
        public string Name { get; }
        public ProductionJobType JobType { get; }
        public string BlueprintId { get; }

        public int TotalWorkDays { get; }
        public double WorkedDays { get; private set; }
        public double RequiredCapacity { get; }
        public double CostMUSD { get; }

        public ProductionJobStatus Status { get; private set; }
        public GameDate StartDate { get; }
        public GameDate? CompletedDate { get; private set; }
        public int DelayDays { get; private set; }
        public double ReliabilityBonusApplied { get; }

        public bool IsFinished => Status == ProductionJobStatus.Completed;
        public bool IsActive => Status == ProductionJobStatus.InProgress;
        public bool IsQueued => Status == ProductionJobStatus.Queued;

        public double ProgressFraction => TotalWorkDays > 0 ? Math.Min(1.0, WorkedDays / TotalWorkDays) : 1.0;
        public int RemainingDays => Math.Max(0, (int)Math.Ceiling(TotalWorkDays - WorkedDays));

        public ProductionJob(
            string id,
            string name,
            ProductionJobType jobType,
            int totalWorkDays,
            double costMUSD,
            GameDate startDate,
            double requiredCapacity = 5.0,
            string blueprintId = null,
            double reliabilityBonusApplied = 0.0,
            ProductionJobStatus status = ProductionJobStatus.Queued)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Job ID cannot be empty.", nameof(id));

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : id;
            JobType = jobType;
            TotalWorkDays = Math.Max(1, totalWorkDays);
            CostMUSD = Math.Max(0.0, costMUSD);
            StartDate = startDate;
            RequiredCapacity = Math.Max(1.0, requiredCapacity);
            BlueprintId = blueprintId;
            ReliabilityBonusApplied = Math.Max(0.0, reliabilityBonusApplied);
            Status = status;
            WorkedDays = 0.0;
            DelayDays = 0;
        }

        public void SetStatus(ProductionJobStatus newStatus)
        {
            Status = newStatus;
        }

        public void AddWorkDays(double days)
        {
            if (days > 0.0)
            {
                WorkedDays = Math.Min((double)TotalWorkDays, WorkedDays + days);
            }
        }

        public void AddDelay(int days)
        {
            if (days > 0)
            {
                DelayDays += days;
            }
        }

        public void MarkCompleted(GameDate completionDate)
        {
            Status = ProductionJobStatus.Completed;
            WorkedDays = TotalWorkDays;
            CompletedDate = completionDate;
        }

        public void MarkCancelled()
        {
            Status = ProductionJobStatus.Cancelled;
            CompletedDate = null;
        }

        /// <summary>
        /// Symmetrically restores state during command Undo.
        /// </summary>
        public void RevertSnapshot(double workedDays, ProductionJobStatus status, GameDate? completedDate, int delayDays)
        {
            WorkedDays = workedDays;
            Status = status;
            CompletedDate = completedDate;
            DelayDays = delayDays;
        }

        public override string ToString() =>
            $"[Job {Id} ({JobType}) '{Name}' {WorkedDays:F1}/{TotalWorkDays}d ({Status})]";
    }
}
