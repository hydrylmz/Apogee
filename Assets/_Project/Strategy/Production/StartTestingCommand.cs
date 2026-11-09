using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to commission a qualification testing program at an agency Test Facility (GDD §9.3, §21 M4-07).
    /// Deducts test budget, schedules test facility usage, and grants a verified reliability bonus upon completion.
    /// Supports symmetrical Undo/Redo and role authorization.
    /// </summary>
    public sealed class StartTestingCommand : CommandBase<CampaignState>
    {
        public string JobId { get; }
        public string TestName { get; }
        public string BlueprintId { get; }
        public int DurationDays { get; }
        public double CostMUSD { get; }
        public double ReliabilityBonus { get; }

        public override string Name => nameof(StartTestingCommand);
        public override PlayerRole RequiredRoles =>
            PlayerRole.ProcurementDirector | PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public StartTestingCommand(
            string jobId,
            string testName,
            string blueprintId,
            int durationDays = 30,
            double costMUSD = 15.0,
            double reliabilityBonus = 0.05,
            string id = null) : base(id)
        {
            JobId = !string.IsNullOrWhiteSpace(jobId) ? jobId : throw new ArgumentException("JobId cannot be empty.", nameof(jobId));
            TestName = !string.IsNullOrWhiteSpace(testName) ? testName : "Qualification Test";
            BlueprintId = blueprintId;
            DurationDays = Math.Max(1, durationDays);
            CostMUSD = Math.Max(0.0, costMUSD);
            ReliabilityBonus = Math.Max(0.0, Math.Min(0.20, reliabilityBonus));
        }

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState cannot be null.");

            if (state.Production == null)
                return CommandValidationResult.Fail("ProductionManager is not initialised on CampaignState.");

            if (state.Production.GetJob(JobId) != null)
                return CommandValidationResult.Fail($"Production/test job with ID '{JobId}' already exists.");

            if (state.Budget < CostMUSD)
                return CommandValidationResult.Fail($"Insufficient budget to start test program: Requires {CostMUSD:F1}M$, available {state.Budget:F1}M$.");

            double testCap = state.Production.GetTotalTestCapacity(state);
            if (testCap <= 0.0)
                return CommandValidationResult.Fail("No active Test Facility available. Build a Test Facility base before starting qualification testing.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            state.Budget -= CostMUSD;

            var job = new ProductionJob(
                id: JobId,
                name: TestName,
                jobType: ProductionJobType.SystemTesting,
                totalWorkDays: DurationDays,
                costMUSD: CostMUSD,
                startDate: state.Time.CurrentDate,
                requiredCapacity: 1.0,
                blueprintId: BlueprintId,
                reliabilityBonusApplied: ReliabilityBonus);

            state.Production.EnqueueJob(job, state);

            return CommandExecutionResult.Succeeded(
                $"Commissioned test program {JobId} ('{TestName}'). Cost: {CostMUSD:F1}M$, Duration: {DurationDays}d, Expected bonus: +{ReliabilityBonus:P1}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            state.Budget += CostMUSD;
            bool removed = state.Production.RemoveJob(JobId);

            if (!removed)
            {
                return CommandExecutionResult.Failed($"Undo failed: Test job {JobId} not found in production queue.");
            }

            return CommandExecutionResult.Succeeded(
                $"Undid test program {JobId}. Refunded {CostMUSD:F1}M$.");
        }

        public override string ToString() =>
            $"[StartTestingCommand: {JobId} '{TestName}' {CostMUSD:F1}M$]";
    }
}
