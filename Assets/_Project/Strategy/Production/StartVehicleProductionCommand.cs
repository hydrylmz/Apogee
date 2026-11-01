using System;
using Apogee.Core;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command to commission vehicle fabrication and stage integration in an agency factory (GDD §9.3, §21 M4-07).
    /// Deducts manufacturing costs from the campaign budget and enqueues the job into the production pipeline.
    /// Supports symmetrical Undo/Redo and role authorization.
    /// </summary>
    public sealed class StartVehicleProductionCommand : CommandBase<CampaignState>
    {
        public string JobId { get; }
        public Blueprint TargetBlueprint { get; }
        public double ProductionCostMUSD { get; }
        public int TotalBuildDays { get; }
        public double RequiredCapacity { get; }

        public override string Name => nameof(StartVehicleProductionCommand);
        public override PlayerRole RequiredRoles => PlayerRole.ProcurementDirector | PlayerRole.AgencyDirector;
        public override bool CanUndo => true;

        public StartVehicleProductionCommand(
            string jobId,
            Blueprint blueprint,
            double? customCost = null,
            int? customBuildDays = null,
            double requiredCapacity = 10.0,
            string id = null) : base(id)
        {
            JobId = !string.IsNullOrWhiteSpace(jobId) ? jobId : throw new ArgumentException("JobId cannot be empty.", nameof(jobId));
            TargetBlueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            ProductionCostMUSD = customCost ?? Math.Max(1.0, stats.Cost);
            TotalBuildDays = customBuildDays ?? Math.Max(1, stats.BuildTimeDays);
            RequiredCapacity = Math.Max(1.0, requiredCapacity);
        }

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (state == null)
                return CommandValidationResult.Fail("CampaignState cannot be null.");

            if (state.Production == null)
                return CommandValidationResult.Fail("ProductionManager is not initialised on CampaignState.");

            if (state.Production.GetJob(JobId) != null)
                return CommandValidationResult.Fail($"Production job with ID '{JobId}' already exists.");

            if (state.Budget < ProductionCostMUSD)
                return CommandValidationResult.Fail($"Insufficient budget to start production: Requires {ProductionCostMUSD:F1}M$, available {state.Budget:F1}M$.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            state.Budget -= ProductionCostMUSD;

            var job = new ProductionJob(
                id: JobId,
                name: TargetBlueprint.Name,
                jobType: ProductionJobType.VehicleAssembly,
                totalWorkDays: TotalBuildDays,
                costMUSD: ProductionCostMUSD,
                startDate: state.Time.CurrentDate,
                requiredCapacity: RequiredCapacity,
                blueprintId: TargetBlueprint.Id);

            state.Production.EnqueueJob(job, state);

            return CommandExecutionResult.Succeeded(
                $"Enqueued production job {JobId} for vehicle '{TargetBlueprint.Name}'. Cost: {ProductionCostMUSD:F1}M$, Duration: {TotalBuildDays}d, Status: {job.Status}.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            state.Budget += ProductionCostMUSD;
            bool removed = state.Production.RemoveJob(JobId);

            if (!removed)
            {
                return CommandExecutionResult.Failed($"Undo failed: Job {JobId} not found in production queue.");
            }

            return CommandExecutionResult.Succeeded(
                $"Undid production job {JobId}. Refunded {ProductionCostMUSD:F1}M$.");
        }

        public override string ToString() =>
            $"[StartVehicleProductionCommand: {JobId} '{TargetBlueprint?.Name}' {ProductionCostMUSD:F1}M$]";
    }
}
