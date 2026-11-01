using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Core;
using Apogee.Data;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Comprehensive verification tests for M4-06: Suppliers and Contracts.
    /// Covers catalog loading, dynamic terms & relationship scoring, single-source risk evaluation,
    /// SetSlotSupplierCommand & BatchAssignSupplierCommand reflecting in design stats,
    /// SignContractCommand & CancelContractCommand with budget & calendar tracking,
    /// and AdvanceTimeCommand deterministic delivery fulfillment with Undo/Redo.
    /// Complies with GDD §9.1, §12.2, §19.6, §19.12, §19.13, and §21 M4-06.
    /// </summary>
    [TestFixture]
    public sealed class SupplierSystemTests
    {
        private CatalogCollection<SupplierRecord> _catalog;

        [OneTimeSetUp]
        public void Setup()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            }

            string json = File.ReadAllText(path);
            _catalog = CatalogLoader.Load<SupplierRecord>(json);
            Assert.IsTrue(_catalog.Validation.IsValid, "Suppliers catalog must be valid.");
        }

        [Test]
        public void Suppliers_Catalog_ContainsAllSixCompaniesWithSpecialCapabilities()
        {
            Assert.AreEqual(6, _catalog.All.Count(), "GDD §9.1 defines exactly 6 supplier companies.");

            var helion = _catalog.GetById("helion-dynamics");
            Assert.IsNotNull(helion);
            Assert.Contains("reusable-first-stage", helion.SpecialCapabilities);
            Assert.AreEqual(0.80, helion.CostMultiplierByCategory["engine"], 1e-6);

            var kestrel = _catalog.GetById("kestrel-aerospace");
            Assert.IsNotNull(kestrel);
            Assert.Contains("crewed-certification", kestrel.SpecialCapabilities);
            Assert.AreEqual(1.15, kestrel.ReliabilityMultiplierByCategory["engine"], 1e-6);

            var nordvik = _catalog.GetById("nordvik-propulsion");
            Assert.IsNotNull(nordvik);
            Assert.Contains("upper-stage-specialist", nordvik.SpecialCapabilities);

            var tarsus = _catalog.GetById("tarsus-engineering");
            Assert.IsNotNull(tarsus);
            Assert.Contains("composite-structures", tarsus.SpecialCapabilities);

            var meridian = _catalog.GetById("meridian-systems");
            Assert.IsNotNull(meridian);
            Assert.Contains("avionics-specialist", meridian.SpecialCapabilities);

            var aurelia = _catalog.GetById("aurelia-labs");
            Assert.IsNotNull(aurelia);
            Assert.Contains("science-instrument-specialist", aurelia.SpecialCapabilities);
        }

        [Test]
        public void Suppliers_CompanyState_EffectiveMultipliersReflectRelationshipAndReputation()
        {
            var helionRecord = _catalog.GetById("helion-dynamics");
            var state = new SupplierCompanyState(helionRecord, initialRelationship: 0.5);

            // Neutral terms (rel = 0.5, rep = 50, vol = 0)
            double baseCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            Assert.AreEqual(0.80, baseCost, 1e-4);

            // High relationship gives 10% discount on cost
            state.AdjustRelationship(0.5); // score becomes 1.0
            double highRelCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            Assert.AreEqual(0.80 * 0.90, highRelCost, 1e-4);

            // High agency reputation gives additional 5% discount
            double highRepCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 100.0);
            Assert.AreEqual(0.80 * 0.90 * 0.95, highRepCost, 1e-4);

            // Poor relationship penalizes cost (+10%)
            state.SetRelationship(0.0);
            double lowRelCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            Assert.AreEqual(0.80 * 1.10, lowRelCost, 1e-4);
        }

        [Test]
        public void Suppliers_SingleSourceRisk_DiversifiedComponents_ReturnsNoRisk()
        {
            var comp1 = new DesignComponent("c1", "engine", supplier: new SupplierSpec("helion-dynamics"));
            var comp2 = new DesignComponent("c2", "comms", supplier: new SupplierSpec("meridian-systems"));
            var comp3 = new DesignComponent("c3", "instrument", supplier: new SupplierSpec("aurelia-labs"));
            var comp4 = new DesignComponent("c4", "structure", supplier: new SupplierSpec("tarsus-engineering"));

            var report = SingleSourceRiskEvaluator.Evaluate(new[] { comp1, comp2, comp3, comp4 });

            Assert.AreEqual(4, report.TotalComponents);
            Assert.AreEqual(SingleSourceRiskLevel.None, report.RiskLevel);
            Assert.IsFalse(report.HasSingleSourceConcentration);
            Assert.AreEqual(0.0, report.VolumeDiscountPercent, 1e-6);
        }

        [Test]
        public void Suppliers_SingleSourceRisk_HeavyConcentration_AppliesVolumeDiscountAndTriggersWarning()
        {
            // 4 out of 5 components from Helion Dynamics (80% concentration)
            var comp1 = new DesignComponent("c1", "engine", supplier: new SupplierSpec("helion-dynamics"));
            var comp2 = new DesignComponent("c2", "tank", supplier: new SupplierSpec("helion-dynamics"));
            var comp3 = new DesignComponent("c3", "structure", supplier: new SupplierSpec("helion-dynamics"));
            var comp4 = new DesignComponent("c4", "bus", supplier: new SupplierSpec("helion-dynamics"));
            var comp5 = new DesignComponent("c5", "instrument", supplier: new SupplierSpec("aurelia-labs"));

            var report = SingleSourceRiskEvaluator.Evaluate(new[] { comp1, comp2, comp3, comp4, comp5 });

            Assert.AreEqual(5, report.TotalComponents);
            Assert.AreEqual("helion-dynamics", report.DominantSupplierId);
            Assert.AreEqual(0.80, report.DominantSupplierShare, 1e-6);
            Assert.IsTrue(report.HasSingleSourceConcentration);
            Assert.AreEqual(0.10, report.VolumeDiscountPercent, 1e-6, "80% concentration unlocks 10% volume discount.");
            Assert.AreEqual(SingleSourceRiskLevel.Critical, report.RiskLevel);
            Assert.IsNotEmpty(report.SummaryText);
            Assert.IsNotEmpty(report.RiskExplanation);
        }

        [Test]
        public void Suppliers_SetSlotSupplierCommand_ReflectsInDesignStatistics_UndoRedo()
        {
            var bp = new Blueprint("test-bp", "Test Blueprint")
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent(
                    "eng-base", "engine", "Test Engine", dryMass: 2.0, cost: 50.0, buildTime: 40, reliability: 0.90));

            var campaign = new CampaignState();
            campaign.ActiveBlueprint = bp;

            var baseStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(50.0, baseStats.Cost, 1e-4);
            Assert.AreEqual(0.90, baseStats.Reliability, 1e-4);

            // Assign Helion supplier (Cost x0.80, Delivery x0.85, Reliability x0.88)
            var helionSpec = new SupplierSpec("helion-dynamics", "Helion Dynamics", costMultiplier: 0.80, deliveryMultiplier: 0.85, reliabilityMultiplier: 0.88);
            var cmd = new SetSlotSupplierCommand(VehicleSlotId.Stage1Engine, helionSpec);

            var res = campaign.Execute(cmd);
            Assert.IsTrue(res.Success, "SetSlotSupplierCommand should succeed: " + res.Message);

            var helionStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(40.0, helionStats.Cost, 1e-4, "50 * 0.80 = 40 M$");
            Assert.AreEqual(0.90 * 0.88, helionStats.Reliability, 1e-4, "0.90 * 0.88 = 0.792");

            // Undo restores original component stats
            campaign.Undo();
            var undoneStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(50.0, undoneStats.Cost, 1e-4);
            Assert.AreEqual(0.90, undoneStats.Reliability, 1e-4);

            // Redo reapplies Helion
            campaign.Redo();
            var redoneStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(40.0, redoneStats.Cost, 1e-4);
        }

        [Test]
        public void Suppliers_BatchAssignSupplierCommand_AppliesAcrossMultipleSlots()
        {
            var bp = new Blueprint("batch-bp", "Batch BP")
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent("e1", "engine", dryMass: 1.0, cost: 30.0))
                .WithSlotComponent(VehicleSlotId.Stage1Tank, new DesignComponent("t1", "tank", dryMass: 1.0, cost: 20.0));

            var campaign = new CampaignState();
            campaign.ActiveBlueprint = bp;

            var kestrelSpec = new SupplierSpec("kestrel-aerospace", "Kestrel Aerospace", costMultiplier: 1.25, deliveryMultiplier: 1.30, reliabilityMultiplier: 1.15);
            var batchCmd = new BatchAssignSupplierCommand(
                new[] { VehicleSlotId.Stage1Engine, VehicleSlotId.Stage1Tank },
                kestrelSpec);

            var res = campaign.Execute(batchCmd);
            Assert.IsTrue(res.Success);

            var stats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(30.0 * 1.25 + 20.0 * 1.25, stats.Cost, 1e-4);

            campaign.Undo();
            var revertedStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            Assert.AreEqual(50.0, revertedStats.Cost, 1e-4);
        }

        [Test]
        public void Suppliers_SignContractCommand_DeductsBudgetAndSchedulesDelivery()
        {
            var state = new CampaignState();
            state.InitialiseSuppliers(_catalog.All);
            double initialBudget = state.Budget;

            var signCmd = new SignContractCommand(
                contractId: "CTR-ENG-01",
                supplierId: "helion-dynamics",
                itemName: "Kerosen Mk-1 Motoru",
                category: "engine",
                costMUSD: 40.0,
                deliveryDays: 30);

            var res = state.Execute(signCmd, role: PlayerRole.ProcurementDirector);
            Assert.IsTrue(res.Success, "Signing contract should succeed: " + res.Message);

            Assert.AreEqual(initialBudget - 40.0, state.Budget, 1e-4);
            var contract = state.Suppliers.GetContract("CTR-ENG-01");
            Assert.IsNotNull(contract);
            Assert.AreEqual(ContractStatus.PendingDelivery, contract.Status);
            Assert.AreEqual(state.Time.CurrentDate.AddDays(30), contract.DeliveryDate);

            // Undo refunds budget and removes contract
            state.Undo();
            Assert.AreEqual(initialBudget, state.Budget, 1e-4);
            Assert.IsNull(state.Suppliers.GetContract("CTR-ENG-01"));
        }

        [Test]
        public void Suppliers_SignContractCommand_RoleAuthorization_RejectsUnauthorized()
        {
            var state = new CampaignState();
            state.InitialiseSuppliers(_catalog.All);

            var signCmd = new SignContractCommand(
                contractId: "CTR-UNAUTH",
                supplierId: "helion-dynamics",
                itemName: "Motor",
                category: "engine",
                costMUSD: 10.0,
                deliveryDays: 10);

            // ScienceDirector has no authority to sign procurement contracts
            var res = state.Execute(signCmd, role: PlayerRole.ScienceDirector);
            Assert.IsFalse(res.Success);
            Assert.IsTrue(res.Message.Contains("authorized") || res.Message.Contains("Role"));
        }

        [Test]
        public void Suppliers_CancelContractCommand_PartialRefundAndPenalty_UndoRedo()
        {
            var state = new CampaignState();
            state.InitialiseSuppliers(_catalog.All);

            state.Execute(new SignContractCommand("CTR-TO-CANCEL", "helion-dynamics", "Tank", "tank", costMUSD: 100.0, deliveryDays: 60));
            double budgetAfterSign = state.Budget;
            double initialRel = state.Suppliers.GetCompany("helion-dynamics").RelationshipScore;

            var cancelCmd = new CancelContractCommand("CTR-TO-CANCEL", refundRatio: 0.5, relationshipPenalty: 0.05);
            var res = state.Execute(cancelCmd);
            Assert.IsTrue(res.Success);

            // 50% refunded (50M$)
            Assert.AreEqual(budgetAfterSign + 50.0, state.Budget, 1e-4);
            var contract = state.Suppliers.GetContract("CTR-TO-CANCEL");
            Assert.AreEqual(ContractStatus.Cancelled, contract.Status);
            Assert.AreEqual(initialRel - 0.05, state.Suppliers.GetCompany("helion-dynamics").RelationshipScore, 1e-4);

            // Undo cancellation restores Pending status and re-deducts refund
            state.Undo();
            Assert.AreEqual(budgetAfterSign, state.Budget, 1e-4);
            Assert.AreEqual(ContractStatus.PendingDelivery, contract.Status);
            Assert.AreEqual(initialRel, state.Suppliers.GetCompany("helion-dynamics").RelationshipScore, 1e-4);
        }

        [Test]
        public void Suppliers_AdvanceTimeCommand_ProgressesDeliveryAndFulfillsContract_UndoRedo()
        {
            var state = new CampaignState();
            state.InitialiseSuppliers(_catalog.All);

            state.Execute(new SignContractCommand("CTR-TIME-01", "helion-dynamics", "Gövde", "structure", costMUSD: 20.0, deliveryDays: 10));
            var company = state.Suppliers.GetCompany("helion-dynamics");
            double relBefore = company.RelationshipScore;

            // Advance 5 days: not yet delivered
            state.Execute(new AdvanceTimeCommand(5));
            var contract = state.Suppliers.GetContract("CTR-TIME-01");
            Assert.AreEqual(ContractStatus.PendingDelivery, contract.Status);

            // Advance 6 days (total 11 days): reached delivery date!
            state.Execute(new AdvanceTimeCommand(6));
            Assert.AreEqual(ContractStatus.Fulfilled, contract.Status);
            Assert.AreEqual(contract.DeliveryDate, contract.FulfilledDate);
            Assert.AreEqual(relBefore + 0.02, company.RelationshipScore, 1e-4, "Successful delivery improves relationship.");

            // Undo time advance reverts contract back to pending
            state.Undo();
            Assert.AreEqual(ContractStatus.PendingDelivery, contract.Status);
            Assert.IsNull(contract.FulfilledDate);
            Assert.AreEqual(relBefore, company.RelationshipScore, 1e-4);
        }
    }
}
