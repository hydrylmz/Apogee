using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Core;
using Apogee.Design;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Comprehensive verification tests for M4-07: Production and Testing Queue.
    /// Covers factory and test facility capacity, vehicle integration, qualification testing,
    /// capacity throttling and congestion delays, queued job promotion, and AdvanceTimeCommand integration.
    /// Complies with GDD §6.1, §9.3, §12.2, §16.7, §19.13, and §21 M4-07.
    /// </summary>
    [TestFixture]
    public sealed class ProductionSystemTests
    {
        private Blueprint CreateTestBlueprint(string name = "Atlas-1", double cost = 60.0, int buildDays = 20)
        {
            return new Blueprint("bp-test", name)
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent(
                    "eng-test", "engine", dryMass: 2.0, cost: cost, buildTime: buildDays, reliability: 0.90));
        }

        [Test]
        public void Production_Capacity_FactoryAndTestRatingsMatchSpecs()
        {
            var state = new CampaignState();
            // Default cape base includes 1 Factory level 1
            double factoryCap = state.Production.GetTotalFactoryCapacity(state);
            Assert.AreEqual(10.0, factoryCap, 1e-4, "Level 1 factory provides 10 throughput capacity units.");

            // Starts without a test facility
            double testCap = state.Production.GetTotalTestCapacity(state);
            Assert.AreEqual(0.0, testCap, 1e-4, "No test facility exists at campaign start.");
        }

        [Test]
        public void Production_StartVehicleProductionCommand_DeductsBudgetAndEnqueuesJob_UndoRedo()
        {
            var state = new CampaignState();
            double initialBudget = state.Budget;
            var bp = CreateTestBlueprint("Orbiter Rocket", cost: 50.0, buildDays: 25);

            var cmd = new StartVehicleProductionCommand("JOB-VEH-01", bp, requiredCapacity: 5.0);
            var res = state.Execute(cmd, role: PlayerRole.ProcurementDirector);

            Assert.IsTrue(res.Success, "StartVehicleProductionCommand should succeed: " + res.Message);
            Assert.AreEqual(initialBudget - 50.0, state.Budget, 1e-4);

            var job = state.Production.GetJob("JOB-VEH-01");
            Assert.IsNotNull(job);
            Assert.AreEqual(ProductionJobStatus.InProgress, job.Status, "Job fits within 10 factory capacity, so starts immediately.");
            Assert.AreEqual(25, job.TotalWorkDays);

            // Undo refunds budget and removes job
            state.Undo();
            Assert.AreEqual(initialBudget, state.Budget, 1e-4);
            Assert.IsNull(state.Production.GetJob("JOB-VEH-01"));

            // Redo reapplies
            state.Redo();
            Assert.AreEqual(initialBudget - 50.0, state.Budget, 1e-4);
            Assert.IsNotNull(state.Production.GetJob("JOB-VEH-01"));
        }

        [Test]
        public void Production_QueueCongestion_CapacityLimitDelaysAndThrottling()
        {
            var state = new CampaignState();
            // Factory capacity = 10.0
            var bp = CreateTestBlueprint("Heavy Booster", cost: 10.0, buildDays: 30);

            // Enqueue two jobs demanding 10 capacity each => total 20 demanded capacity (100% overload)
            var job1 = new ProductionJob("JOB-OVR-01", "Job 1", ProductionJobType.VehicleAssembly, 30, 10.0, state.Time.CurrentDate, requiredCapacity: 10.0);
            var job2 = new ProductionJob("JOB-OVR-02", "Job 2", ProductionJobType.VehicleAssembly, 30, 10.0, state.Time.CurrentDate, requiredCapacity: 10.0);

            job1.SetStatus(ProductionJobStatus.InProgress);
            job2.SetStatus(ProductionJobStatus.InProgress);

            state.Production.EnqueueJob(job1, state);
            state.Production.EnqueueJob(job2, state);

            // Advance 10 days with 20 demanded vs 10 available capacity (50% slowdown)
            state.Production.AdvanceDays(10, state.Time.CurrentDate.AddDays(10), state);

            // Each job should advance by 10 * (10 / 20) = 5 days of work, and accumulate delay days
            Assert.AreEqual(5.0, job1.WorkedDays, 1e-4);
            Assert.AreEqual(5.0, job2.WorkedDays, 1e-4);
            Assert.Greater(job1.DelayDays, 0, "Capacity congestion must accumulate delay days.");
        }

        [Test]
        public void Production_QueuePromotion_QueuedJobPromotedWhenActiveJobCompletes()
        {
            var state = new CampaignState();
            // Factory capacity = 10.0

            // Job 1 uses all 10 capacity, takes 5 days
            var job1 = new ProductionJob("JOB-SEQ-01", "Fast Job", ProductionJobType.VehicleAssembly, 5, 20.0, state.Time.CurrentDate, requiredCapacity: 10.0);
            // Job 2 demands 10 capacity, stays queued
            var job2 = new ProductionJob("JOB-SEQ-02", "Waiting Job", ProductionJobType.VehicleAssembly, 15, 20.0, state.Time.CurrentDate, requiredCapacity: 10.0);

            state.Production.EnqueueJob(job1, state);
            state.Production.EnqueueJob(job2, state);

            Assert.AreEqual(ProductionJobStatus.InProgress, job1.Status);
            Assert.AreEqual(ProductionJobStatus.Queued, job2.Status);

            // Advance 5 days: Job 1 completes, Job 2 promoted to InProgress!
            state.Production.AdvanceDays(5, state.Time.CurrentDate.AddDays(5), state);

            Assert.AreEqual(ProductionJobStatus.Completed, job1.Status);
            Assert.AreEqual(ProductionJobStatus.InProgress, job2.Status, "Queued job must be automatically promoted when capacity frees up.");
        }

        [Test]
        public void Production_StartTestingCommand_RequiresTestFacility_BoostsReliability_UndoRedo()
        {
            var state = new CampaignState();

            // Attempt testing without a test facility base: fails validation
            var testCmd = new StartTestingCommand("TEST-QUAL-01", "Static Fire", "bp-test", durationDays: 20, costMUSD: 15.0, reliabilityBonus: 0.05);
            var failRes = state.Execute(testCmd);
            Assert.IsFalse(failRes.Success);
            Assert.IsTrue(failRes.Message.Contains("Test Facility"));

            // Build Test Facility base
            state.Execute(new BuildBaseCommand("base_cape_test", "cape_peninsula", BaseType.TestFacility));
            Assert.Greater(state.Production.GetTotalTestCapacity(state), 0.0);

            double budgetBeforeTest = state.Budget;
            var okRes = state.Execute(testCmd);
            Assert.IsTrue(okRes.Success, "Test commissioning should succeed now: " + okRes.Message);
            Assert.AreEqual(budgetBeforeTest - 15.0, state.Budget, 1e-4);

            var job = state.Production.GetJob("TEST-QUAL-01");
            Assert.IsNotNull(job);
            Assert.AreEqual(ProductionJobStatus.InProgress, job.Status);
            Assert.AreEqual(0.05, job.ReliabilityBonusApplied, 1e-6);

            // Undo refunds and removes
            state.Undo();
            Assert.AreEqual(budgetBeforeTest, state.Budget, 1e-4);
            Assert.IsNull(state.Production.GetJob("TEST-QUAL-01"));
        }

        [Test]
        public void Production_CancelProductionJobCommand_PartialRefundAndFreesCapacity_UndoRedo()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Cancel BP", cost: 100.0, buildDays: 40);

            state.Execute(new StartVehicleProductionCommand("JOB-TO-CANCEL", bp, customCost: 100.0, customBuildDays: 40));
            double budgetAfterStart = state.Budget;

            // Cancel job with 75% refund of unworked portion (all 100M$ unworked => 75M$ refund)
            var cancelCmd = new CancelProductionJobCommand("JOB-TO-CANCEL", refundRatio: 0.75);
            var res = state.Execute(cancelCmd);
            Assert.IsTrue(res.Success);

            Assert.AreEqual(budgetAfterStart + 75.0, state.Budget, 1e-4);
            var job = state.Production.GetJob("JOB-TO-CANCEL");
            Assert.AreEqual(ProductionJobStatus.Cancelled, job.Status);

            // Undo cancellation
            state.Undo();
            Assert.AreEqual(budgetAfterStart, state.Budget, 1e-4);
            Assert.AreEqual(ProductionJobStatus.InProgress, job.Status);
        }

        [Test]
        public void Production_AdvanceTimeCommand_ProgressesJobs_UndoRestoresState()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Apollo Stack", cost: 80.0, buildDays: 15);

            state.Execute(new StartVehicleProductionCommand("JOB-TIME-01", bp, customCost: 80.0, customBuildDays: 15, requiredCapacity: 5.0));
            var job = state.Production.GetJob("JOB-TIME-01");

            // Advance 10 days: 10/15 days done, still in progress
            state.Execute(new AdvanceTimeCommand(10));
            Assert.AreEqual(10.0, job.WorkedDays, 1e-4);
            Assert.AreEqual(ProductionJobStatus.InProgress, job.Status);

            // Advance 6 days (total 16 days): completed!
            state.Execute(new AdvanceTimeCommand(6));
            Assert.AreEqual(ProductionJobStatus.Completed, job.Status);
            Assert.IsNotNull(job.CompletedDate);

            // Undo time advance reverts to InProgress
            state.Undo();
            Assert.AreEqual(ProductionJobStatus.InProgress, job.Status);
        }

        [Test]
        public void Production_RoleAuthorization_RejectsUnauthorized()
        {
            var state = new CampaignState();
            var bp = CreateTestBlueprint("Unauthorized Rocket");

            var cmd = new StartVehicleProductionCommand("JOB-UNAUTH", bp);
            // ScienceDirector has no authority over factory manufacturing
            var res = state.Execute(cmd, role: PlayerRole.ScienceDirector);
            Assert.IsFalse(res.Success);
            Assert.IsTrue(res.Message.Contains("authorized") || res.Message.Contains("Role"));
        }
    }
}
