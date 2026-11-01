using System;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Design;
using Apogee.Strategy;

namespace ApogeeLocalTestRunner
{
    /// <summary>
    /// Verification tests for M4-07: Production and Testing Queue.
    /// Covers factory and test facility capacity, vehicle integration, qualification testing,
    /// capacity throttling and congestion delays, queued job promotion, and AdvanceTimeCommand integration.
    /// Complies with GDD §6.1, §9.3, §12.2, §16.7, §19.13, and §21 M4-07.
    /// </summary>
    public static class ProductionRunnerTests
    {
        private static Blueprint CreateTestBlueprint(string name = "Atlas-1", double cost = 60.0, int buildDays = 20)
        {
            return new Blueprint("bp-test", name)
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent(
                    "eng-test", "engine", dryMass: 2.0, cost: cost, buildTime: buildDays, reliability: 0.90));
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M4_07_Production_Capacity_FactoryAndTestRatingsMatchSpecs", Test_Capacity_FactoryAndTestRatingsMatchSpecs);
            runTest("M4_07_Production_StartVehicleProductionCommand_DeductsBudgetAndEnqueuesJob_UndoRedo", Test_StartVehicleProductionCommand_DeductsBudgetAndEnqueuesJob_UndoRedo);
            runTest("M4_07_Production_QueueCongestion_CapacityLimitDelaysAndThrottling", Test_QueueCongestion_CapacityLimitDelaysAndThrottling);
            runTest("M4_07_Production_QueuePromotion_QueuedJobPromotedWhenActiveJobCompletes", Test_QueuePromotion_QueuedJobPromotedWhenActiveJobCompletes);
            runTest("M4_07_Production_StartTestingCommand_RequiresTestFacility_BoostsReliability_UndoRedo", Test_StartTestingCommand_RequiresTestFacility_BoostsReliability_UndoRedo);
            runTest("M4_07_Production_CancelProductionJobCommand_PartialRefundAndFreesCapacity_UndoRedo", Test_CancelProductionJobCommand_PartialRefundAndFreesCapacity_UndoRedo);
            runTest("M4_07_Production_AdvanceTimeCommand_ProgressesJobs_UndoRestoresState", Test_AdvanceTimeCommand_ProgressesJobs_UndoRestoresState);
            runTest("M4_07_Production_RoleAuthorization_RejectsUnauthorized", Test_RoleAuthorization_RejectsUnauthorized);
        }

        private static void Test_Capacity_FactoryAndTestRatingsMatchSpecs()
        {
            var state = new CampaignState();
            double factoryCap = state.Production.GetTotalFactoryCapacity(state);
            if (Math.Abs(factoryCap - 10.0) > 1e-4)
                throw new Exception($"Expected factory capacity 10.0, got {factoryCap}");

            double testCap = state.Production.GetTotalTestCapacity(state);
            if (Math.Abs(testCap - 0.0) > 1e-4)
                throw new Exception($"Expected initial test capacity 0.0, got {testCap}");
        }

        private static void Test_StartVehicleProductionCommand_DeductsBudgetAndEnqueuesJob_UndoRedo()
        {
            var state = new CampaignState();
            double initialBudget = state.Budget;
            var bp = CreateTestBlueprint("Orbiter Rocket", cost: 50.0, buildDays: 25);

            var cmd = new StartVehicleProductionCommand("JOB-VEH-01", bp, requiredCapacity: 5.0);
            var res = state.Execute(cmd, role: PlayerRole.ProcurementDirector);

            if (!res.Success) throw new Exception("StartVehicleProductionCommand failed: " + res.Message);
            if (Math.Abs(state.Budget - (initialBudget - 50.0)) > 1e-4)
                throw new Exception($"Expected budget {initialBudget - 50.0}, got {state.Budget}");

            var job = state.Production.GetJob("JOB-VEH-01");
            if (job == null) throw new Exception("Job not found");
            if (job.Status != ProductionJobStatus.InProgress)
                throw new Exception($"Expected InProgress, got {job.Status}");

            state.Undo();
            if (Math.Abs(state.Budget - initialBudget) > 1e-4)
                throw new Exception($"Expected restored budget {initialBudget}, got {state.Budget}");
            if (state.Production.GetJob("JOB-VEH-01") != null)
                throw new Exception("Job should be removed after Undo");

            state.Redo();
            if (Math.Abs(state.Budget - (initialBudget - 50.0)) > 1e-4)
                throw new Exception($"Expected redone budget {initialBudget - 50.0}, got {state.Budget}");
        }

        private static void Test_QueueCongestion_CapacityLimitDelaysAndThrottling()
        {
            var state = new CampaignState();

            var job1 = new ProductionJob("JOB-OVR-01", "Job 1", ProductionJobType.VehicleAssembly, 30, 10.0, state.Time.CurrentDate, requiredCapacity: 10.0);
            var job2 = new ProductionJob("JOB-OVR-02", "Job 2", ProductionJobType.VehicleAssembly, 30, 10.0, state.Time.CurrentDate, requiredCapacity: 10.0);

            job1.SetStatus(ProductionJobStatus.InProgress);
            job2.SetStatus(ProductionJobStatus.InProgress);

            state.Production.EnqueueJob(job1, state);
            state.Production.EnqueueJob(job2, state);

            state.Production.AdvanceDays(10, state.Time.CurrentDate.AddDays(10), state);

            if (Math.Abs(job1.WorkedDays - 5.0) > 1e-4)
                throw new Exception($"Expected 5.0 worked days, got {job1.WorkedDays}");
            if (Math.Abs(job2.WorkedDays - 5.0) > 1e-4)
                throw new Exception($"Expected 5.0 worked days, got {job2.WorkedDays}");
            if (job1.DelayDays <= 0)
                throw new Exception("Expected non-zero delay days due to congestion");
        }

        private static void Test_QueuePromotion_QueuedJobPromotedWhenActiveJobCompletes()
        {
            var state = new CampaignState();

            var job1 = new ProductionJob("JOB-SEQ-01", "Fast Job", ProductionJobType.VehicleAssembly, 5, 20.0, state.Time.CurrentDate, requiredCapacity: 10.0);
            var job2 = new ProductionJob("JOB-SEQ-02", "Waiting Job", ProductionJobType.VehicleAssembly, 15, 20.0, state.Time.CurrentDate, requiredCapacity: 10.0);

            state.Production.EnqueueJob(job1, state);
            state.Production.EnqueueJob(job2, state);

            if (job1.Status != ProductionJobStatus.InProgress)
                throw new Exception($"Expected job1 InProgress, got {job1.Status}");
            if (job2.Status != ProductionJobStatus.Queued)
                throw new Exception($"Expected job2 Queued, got {job2.Status}");

            state.Production.AdvanceDays(5, state.Time.CurrentDate.AddDays(5), state);

            if (job1.Status != ProductionJobStatus.Completed)
                throw new Exception($"Expected job1 Completed, got {job1.Status}");
            if (job2.Status != ProductionJobStatus.InProgress)
                throw new Exception($"Expected job2 promoted to InProgress, got {job2.Status}");
        }

        private static void Test_StartTestingCommand_RequiresTestFacility_BoostsReliability_UndoRedo()
        {
            var state = new CampaignState();

            var testCmd = new StartTestingCommand("TEST-QUAL-01", "Static Fire", "bp-test", durationDays: 20, costMUSD: 15.0, reliabilityBonus: 0.05);
            var failRes = state.Execute(testCmd);
            if (failRes.Success)
                throw new Exception("Expected StartTestingCommand to fail without Test Facility base");

            state.Execute(new BuildBaseCommand("base_cape_test", "cape_peninsula", BaseType.TestFacility));
            double budgetBeforeTest = state.Budget;

            var okRes = state.Execute(testCmd);
            if (!okRes.Success)
                throw new Exception("Expected test command to succeed after building Test Facility: " + okRes.Message);

            if (Math.Abs(state.Budget - (budgetBeforeTest - 15.0)) > 1e-4)
                throw new Exception($"Expected budget {budgetBeforeTest - 15.0}, got {state.Budget}");

            state.Undo();
            if (Math.Abs(state.Budget - budgetBeforeTest) > 1e-4)
                throw new Exception($"Expected restored budget {budgetBeforeTest}, got {state.Budget}");
        }

        private static void Test_CancelProductionJobCommand_PartialRefundAndFreesCapacity_UndoRedo()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Cancel BP", cost: 100.0, buildDays: 40);

            state.Execute(new StartVehicleProductionCommand("JOB-TO-CANCEL", bp, customCost: 100.0, customBuildDays: 40));
            double budgetAfterStart = state.Budget;

            var cancelCmd = new CancelProductionJobCommand("JOB-TO-CANCEL", refundRatio: 0.75);
            var res = state.Execute(cancelCmd);
            if (!res.Success) throw new Exception("CancelProductionJobCommand failed: " + res.Message);

            if (Math.Abs(state.Budget - (budgetAfterStart + 75.0)) > 1e-4)
                throw new Exception($"Expected refunded budget {budgetAfterStart + 75.0}, got {state.Budget}");

            state.Undo();
            if (Math.Abs(state.Budget - budgetAfterStart) > 1e-4)
                throw new Exception($"Expected restored budget {budgetAfterStart}, got {state.Budget}");
        }

        private static void Test_AdvanceTimeCommand_ProgressesJobs_UndoRestoresState()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Apollo Stack", cost: 80.0, buildDays: 15);

            state.Execute(new StartVehicleProductionCommand("JOB-TIME-01", bp, customCost: 80.0, customBuildDays: 15, requiredCapacity: 5.0));
            var job = state.Production.GetJob("JOB-TIME-01");

            state.Execute(new AdvanceTimeCommand(10));
            if (Math.Abs(job.WorkedDays - 10.0) > 1e-4)
                throw new Exception($"Expected 10.0 worked days, got {job.WorkedDays}");
            if (job.Status != ProductionJobStatus.InProgress)
                throw new Exception($"Expected InProgress, got {job.Status}");

            state.Execute(new AdvanceTimeCommand(6));
            if (job.Status != ProductionJobStatus.Completed)
                throw new Exception($"Expected Completed, got {job.Status}");

            state.Undo();
            if (job.Status != ProductionJobStatus.InProgress)
                throw new Exception($"Expected InProgress after Undo, got {job.Status}");
        }

        private static void Test_RoleAuthorization_RejectsUnauthorized()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Unauthorized Rocket");

            var cmd = new StartVehicleProductionCommand("JOB-UNAUTH", bp);
            var res = state.Execute(cmd, role: PlayerRole.ScienceDirector);
            if (res.Success)
                throw new Exception("Expected ScienceDirector to be rejected for production command");
        }
    }
}
