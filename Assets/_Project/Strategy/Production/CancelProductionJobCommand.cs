using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to cancel an active or queued production/test job (GDD §9.3).
    /// Refunds unspent production funds and frees up factory capacity for waiting jobs.
    /// Supports symmetrical Undo/Redo and role authorization.
    /// </summary>
    public sealed class CancelProductionJobCommand : CommandBase<CampaignState>
    {
        public string JobId { get; }
        public double RefundRatio { get; }

        private double _refundedAmount;
        private double _previousWorkedDays;
        private ProductionJobStatus _previousStatus;
        private GameDate? _previousCompletedDate;
        private int _previousDelayDays;

        public override string Name => nameof(CancelProductionJobCommand);
        public override PlayerRole RequiredRoles => PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public CancelProductionJobCommand(string jobId, double refundRatio = 0.75, string id = null)
            : base(id)
        {
            JobId = !string.IsNullOrWhiteSpace(jobId) ? jobId : throw new ArgumentException("JobId cannot be empty.", nameof(jobId));
            RefundRatio = Math.Max(0.0, Math.Min(1.0, refundRatio));
        }

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState cannot be null.");

            if (state.Production == null)
                return CommandValidationResult.Fail("ProductionManager is not initialised on CampaignState.");

            var job = state.Production.GetJob(JobId);
            if (job == null)
                return CommandValidationResult.Fail($"Job with ID '{JobId}' not found.");

            if (job.IsFinished)
                return CommandValidationResult.Fail($"Job '{JobId}' is already completed and cannot be cancelled.");

            if (job.Status == ProductionJobStatus.Cancelled)
                return CommandValidationResult.Fail($"Job '{JobId}' is already cancelled.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            var job = state.Production.GetJob(JobId);
            _previousWorkedDays = job.WorkedDays;
            _previousStatus = job.Status;
            _previousCompletedDate = job.CompletedDate;
            _previousDelayDays = job.DelayDays;

            // Refund is based on remaining unworked progress
            double remainingFraction = 1.0 - job.ProgressFraction;
            _refundedAmount = job.CostMUSD * remainingFraction * RefundRatio;
            state.Budget += _refundedAmount;

            state.Production.CancelJob(JobId, state);

            return CommandExecutionResult.Succeeded(
                $"Cancelled production job {JobId}. Refunded {_refundedAmount:F1}M$.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            if (state.Budget < _refundedAmount)
            {
                return CommandExecutionResult.Failed($"Undo failed: Insufficient budget to reverse refund of {_refundedAmount:F1}M$. Available: {state.Budget:F1}M$.");
            }

            state.Budget -= _refundedAmount;

            var job = state.Production.GetJob(JobId);
            job?.RevertSnapshot(_previousWorkedDays, _previousStatus, _previousCompletedDate, _previousDelayDays);

            return CommandExecutionResult.Succeeded(
                $"Undid cancellation of job {JobId}. Re-deducted {_refundedAmount:F1}M$, restored status to {_previousStatus}.");
        }

        public override string ToString() =>
            $"[CancelProductionJobCommand: {JobId}]";
    }
}
