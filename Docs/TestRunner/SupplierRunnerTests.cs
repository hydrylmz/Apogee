using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.Strategy;

namespace ApogeeLocalTestRunner
{
    /// <summary>
    /// Verification tests for M4-06: Suppliers and Contracts.
    /// Covers catalog loading, dynamic terms & relationship scoring, single-source risk evaluation,
    /// SetSlotSupplierCommand & BatchAssignSupplierCommand reflecting in design stats,
    /// SignContractCommand & CancelContractCommand with budget & calendar tracking,
    /// and AdvanceTimeCommand deterministic delivery fulfillment with Undo/Redo.
    /// Complies with GDD §9.1, §12.2, §19.6, §19.12, §19.13, and §21 M4-06.
    /// </summary>
    public static class SupplierRunnerTests
    {
        private static string GetJsonDir()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            }
            return path;
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M4_06_Catalog_Suppliers_LoadsAllSixWithCapabilities", Test_Catalog_LoadsAllSixWithCapabilities);
            runTest("M4_06_Suppliers_EffectiveMultipliers_ReflectRelationshipAndReputation", Test_EffectiveMultipliers_ReflectRelationshipAndReputation);
            runTest("M4_06_Suppliers_SingleSourceRisk_Diversified_NoRisk", Test_SingleSourceRisk_Diversified_NoRisk);
            runTest("M4_06_Suppliers_SingleSourceRisk_Dominant_VolumeDiscountAndCriticalRisk", Test_SingleSourceRisk_Dominant_VolumeDiscountAndCriticalRisk);
            runTest("M4_06_Suppliers_SetSlotSupplierCommand_ReflectsInStats_UndoRedo", Test_SetSlotSupplierCommand_ReflectsInStats_UndoRedo);
            runTest("M4_06_Suppliers_BatchAssignSupplierCommand_AppliesAcrossSlots_UndoRedo", Test_BatchAssignSupplierCommand_AppliesAcrossSlots_UndoRedo);
            runTest("M4_06_Suppliers_SignContractCommand_DeductsBudgetAndSchedulesDelivery", Test_SignContractCommand_DeductsBudgetAndSchedulesDelivery);
            runTest("M4_06_Suppliers_SignContractCommand_RoleAuthorization_RejectsUnauthorized", Test_SignContractCommand_RoleAuthorization_RejectsUnauthorized);
            runTest("M4_06_Suppliers_CancelContractCommand_PartialRefundAndPenalty_UndoRedo", Test_CancelContractCommand_PartialRefundAndPenalty_UndoRedo);
            runTest("M4_06_Suppliers_AdvanceTimeCommand_ProgressesDeliveryAndFulfillsContract_UndoRedo", Test_AdvanceTimeCommand_ProgressesDeliveryAndFulfillsContract_UndoRedo);
        }

        private static List<SupplierRecord> LoadCatalog()
        {
            string file = Path.Combine(GetJsonDir(), "suppliers.json");
            string json = File.ReadAllText(file);
            var col = CatalogLoader.Load<SupplierRecord>(json);
            if (!col.Validation.IsValid)
            {
                throw new Exception("Suppliers catalog validation failed: " + string.Join("; ", col.Validation.Errors));
            }
            return col.All.ToList();
        }

        private static void Test_Catalog_LoadsAllSixWithCapabilities()
        {
            var suppliers = LoadCatalog();
            if (suppliers.Count != 6)
                throw new Exception($"Expected 6 suppliers per GDD §9.1, got {suppliers.Count}");

            var helion = suppliers.FirstOrDefault(s => s.Id == "helion-dynamics");
            if (helion == null) throw new Exception("helion-dynamics not found");
            if (!helion.SpecialCapabilities.Contains("reusable-first-stage"))
                throw new Exception("helion missing reusable-first-stage capability");

            var kestrel = suppliers.FirstOrDefault(s => s.Id == "kestrel-aerospace");
            if (kestrel == null) throw new Exception("kestrel-aerospace not found");
            if (!kestrel.SpecialCapabilities.Contains("crewed-certification"))
                throw new Exception("kestrel missing crewed-certification capability");

            var nordvik = suppliers.FirstOrDefault(s => s.Id == "nordvik-propulsion");
            if (nordvik == null) throw new Exception("nordvik-propulsion not found");
            if (!nordvik.SpecialCapabilities.Contains("upper-stage-specialist"))
                throw new Exception("nordvik missing upper-stage-specialist capability");

            var tarsus = suppliers.FirstOrDefault(s => s.Id == "tarsus-engineering");
            if (tarsus == null) throw new Exception("tarsus-engineering not found");
            if (!tarsus.SpecialCapabilities.Contains("composite-structures"))
                throw new Exception("tarsus missing composite-structures capability");

            var meridian = suppliers.FirstOrDefault(s => s.Id == "meridian-systems");
            if (meridian == null) throw new Exception("meridian-systems not found");
            if (!meridian.SpecialCapabilities.Contains("avionics-specialist"))
                throw new Exception("meridian missing avionics-specialist capability");

            var aurelia = suppliers.FirstOrDefault(s => s.Id == "aurelia-labs");
            if (aurelia == null) throw new Exception("aurelia-labs not found");
            if (!aurelia.SpecialCapabilities.Contains("science-instrument-specialist"))
                throw new Exception("aurelia missing science-instrument-specialist capability");
        }

        private static void Test_EffectiveMultipliers_ReflectRelationshipAndReputation()
        {
            var suppliers = LoadCatalog();
            var helion = suppliers.First(s => s.Id == "helion-dynamics");
            var state = new SupplierCompanyState(helion, initialRelationship: 0.5);

            double baseCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            if (Math.Abs(baseCost - 0.80) > 1e-4)
                throw new Exception($"Expected base cost 0.80, got {baseCost}");

            // High relationship gives 10% discount on cost
            state.AdjustRelationship(0.5);
            double highRelCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            if (Math.Abs(highRelCost - (0.80 * 0.90)) > 1e-4)
                throw new Exception($"Expected high-rel cost {0.80 * 0.90}, got {highRelCost}");

            // High agency reputation gives additional 5% discount
            double highRepCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 100.0);
            if (Math.Abs(highRepCost - (0.80 * 0.90 * 0.95)) > 1e-4)
                throw new Exception($"Expected high-rep cost {0.80 * 0.90 * 0.95}, got {highRepCost}");

            // Low relationship increases cost
            state.SetRelationship(0.0);
            double lowRelCost = state.CalculateEffectiveCostMultiplier("engine", agencyReputation: 50.0);
            if (Math.Abs(lowRelCost - (0.80 * 1.10)) > 1e-4)
                throw new Exception($"Expected low-rel cost {0.80 * 1.10}, got {lowRelCost}");
        }

        private static void Test_SingleSourceRisk_Diversified_NoRisk()
        {
            var comp1 = new DesignComponent("c1", "engine", supplier: new SupplierSpec("helion-dynamics"));
            var comp2 = new DesignComponent("c2", "comms", supplier: new SupplierSpec("meridian-systems"));
            var comp3 = new DesignComponent("c3", "instrument", supplier: new SupplierSpec("aurelia-labs"));
            var comp4 = new DesignComponent("c4", "structure", supplier: new SupplierSpec("tarsus-engineering"));

            var report = SingleSourceRiskEvaluator.Evaluate(new[] { comp1, comp2, comp3, comp4 });
            if (report.TotalComponents != 4)
                throw new Exception($"Expected 4 total components, got {report.TotalComponents}");
            if (report.RiskLevel != SingleSourceRiskLevel.None)
                throw new Exception($"Expected None risk level, got {report.RiskLevel}");
            if (report.HasSingleSourceConcentration)
                throw new Exception("Expected no concentration");
            if (report.VolumeDiscountPercent != 0.0)
                throw new Exception("Expected 0% volume discount");
        }

        private static void Test_SingleSourceRisk_Dominant_VolumeDiscountAndCriticalRisk()
        {
            var comp1 = new DesignComponent("c1", "engine", supplier: new SupplierSpec("helion-dynamics"));
            var comp2 = new DesignComponent("c2", "tank", supplier: new SupplierSpec("helion-dynamics"));
            var comp3 = new DesignComponent("c3", "structure", supplier: new SupplierSpec("helion-dynamics"));
            var comp4 = new DesignComponent("c4", "bus", supplier: new SupplierSpec("helion-dynamics"));
            var comp5 = new DesignComponent("c5", "instrument", supplier: new SupplierSpec("aurelia-labs"));

            var report = SingleSourceRiskEvaluator.Evaluate(new[] { comp1, comp2, comp3, comp4, comp5 });
            if (report.DominantSupplierId != "helion-dynamics")
                throw new Exception($"Expected dominant helion-dynamics, got {report.DominantSupplierId}");
            if (Math.Abs(report.DominantSupplierShare - 0.80) > 1e-4)
                throw new Exception($"Expected 0.80 share, got {report.DominantSupplierShare}");
            if (report.RiskLevel != SingleSourceRiskLevel.Critical)
                throw new Exception($"Expected Critical risk level, got {report.RiskLevel}");
            if (Math.Abs(report.VolumeDiscountPercent - 0.10) > 1e-4)
                throw new Exception($"Expected 0.10 volume discount, got {report.VolumeDiscountPercent}");
            if (string.IsNullOrWhiteSpace(report.RiskExplanation))
                throw new Exception("Expected non-empty risk explanation");
        }

        private static void Test_SetSlotSupplierCommand_ReflectsInStats_UndoRedo()
        {
            var bp = new Blueprint("test-bp", "Test BP")
                .WithSlotComponent(VehicleSlotId.Stage1Engine, new DesignComponent(
                    "eng-base", "engine", "Test Engine", dryMass: 2.0, cost: 50.0, buildTime: 40, reliability: 0.90));

            var campaign = new CampaignState();
            campaign.ActiveBlueprint = bp;

            var helionSpec = new SupplierSpec("helion-dynamics", "Helion Dynamics", costMultiplier: 0.80, deliveryMultiplier: 0.85, reliabilityMultiplier: 0.88);
            var cmd = new SetSlotSupplierCommand(VehicleSlotId.Stage1Engine, helionSpec);

            var res = campaign.Execute(cmd);
            if (!res.Success) throw new Exception("SetSlotSupplierCommand failed: " + res.Message);

            var helionStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            if (Math.Abs(helionStats.Cost - 40.0) > 1e-4)
                throw new Exception($"Expected cost 40.0, got {helionStats.Cost}");
            if (Math.Abs(helionStats.Reliability - (0.90 * 0.88)) > 1e-4)
                throw new Exception($"Expected reliability {0.90 * 0.88}, got {helionStats.Reliability}");

            campaign.Undo();
            var undoneStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            if (Math.Abs(undoneStats.Cost - 50.0) > 1e-4)
                throw new Exception($"Expected restored cost 50.0, got {undoneStats.Cost}");

            campaign.Redo();
            var redoneStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            if (Math.Abs(redoneStats.Cost - 40.0) > 1e-4)
                throw new Exception($"Expected redone cost 40.0, got {redoneStats.Cost}");
        }

        private static void Test_BatchAssignSupplierCommand_AppliesAcrossSlots_UndoRedo()
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
            if (!res.Success) throw new Exception("BatchAssignSupplierCommand failed: " + res.Message);

            var stats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            double expectedCost = (30.0 * 1.25) + (20.0 * 1.25);
            if (Math.Abs(stats.Cost - expectedCost) > 1e-4)
                throw new Exception($"Expected cost {expectedCost}, got {stats.Cost}");

            campaign.Undo();
            var undoneStats = BlueprintEvaluator.Evaluate(campaign.ActiveBlueprint);
            if (Math.Abs(undoneStats.Cost - 50.0) > 1e-4)
                throw new Exception($"Expected undone cost 50.0, got {undoneStats.Cost}");
        }

        private static void Test_SignContractCommand_DeductsBudgetAndSchedulesDelivery()
        {
            var suppliers = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseSuppliers(suppliers);
            double initialBudget = state.Budget;

            var signCmd = new SignContractCommand(
                contractId: "CTR-ENG-01",
                supplierId: "helion-dynamics",
                itemName: "Kerosen Mk-1 Motoru",
                category: "engine",
                costMUSD: 40.0,
                deliveryDays: 30);

            var res = state.Execute(signCmd, role: PlayerRole.ProcurementDirector);
            if (!res.Success) throw new Exception("SignContractCommand failed: " + res.Message);

            if (Math.Abs(state.Budget - (initialBudget - 40.0)) > 1e-4)
                throw new Exception($"Expected budget {initialBudget - 40.0}, got {state.Budget}");

            var contract = state.Suppliers.GetContract("CTR-ENG-01");
            if (contract == null) throw new Exception("Contract CTR-ENG-01 not found");
            if (contract.Status != ContractStatus.PendingDelivery)
                throw new Exception($"Expected PendingDelivery, got {contract.Status}");
            if (contract.DeliveryDate != state.Time.CurrentDate.AddDays(30))
                throw new Exception($"Expected delivery date {state.Time.CurrentDate.AddDays(30)}, got {contract.DeliveryDate}");

            state.Undo();
            if (Math.Abs(state.Budget - initialBudget) > 1e-4)
                throw new Exception($"Expected restored budget {initialBudget}, got {state.Budget}");
            if (state.Suppliers.GetContract("CTR-ENG-01") != null)
                throw new Exception("Contract should be removed after Undo");
        }

        private static void Test_SignContractCommand_RoleAuthorization_RejectsUnauthorized()
        {
            var suppliers = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseSuppliers(suppliers);

            var signCmd = new SignContractCommand(
                contractId: "CTR-UNAUTH",
                supplierId: "helion-dynamics",
                itemName: "Motor",
                category: "engine",
                costMUSD: 10.0,
                deliveryDays: 10);

            var res = state.Execute(signCmd, role: PlayerRole.ScienceDirector);
            if (res.Success)
                throw new Exception("Expected ScienceDirector to be rejected for signing procurement contracts");
        }

        private static void Test_CancelContractCommand_PartialRefundAndPenalty_UndoRedo()
        {
            var suppliers = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseSuppliers(suppliers);

            state.Execute(new SignContractCommand("CTR-TO-CANCEL", "helion-dynamics", "Tank", "tank", costMUSD: 100.0, deliveryDays: 60));
            double budgetAfterSign = state.Budget;
            double initialRel = state.Suppliers.GetCompany("helion-dynamics").RelationshipScore;

            var cancelCmd = new CancelContractCommand("CTR-TO-CANCEL", refundRatio: 0.5, relationshipPenalty: 0.05);
            var res = state.Execute(cancelCmd);
            if (!res.Success) throw new Exception("CancelContractCommand failed: " + res.Message);

            if (Math.Abs(state.Budget - (budgetAfterSign + 50.0)) > 1e-4)
                throw new Exception($"Expected refunded budget {budgetAfterSign + 50.0}, got {state.Budget}");

            var contract = state.Suppliers.GetContract("CTR-TO-CANCEL");
            if (contract.Status != ContractStatus.Cancelled)
                throw new Exception($"Expected Cancelled status, got {contract.Status}");

            double relAfter = state.Suppliers.GetCompany("helion-dynamics").RelationshipScore;
            if (Math.Abs(relAfter - (initialRel - 0.05)) > 1e-4)
                throw new Exception($"Expected penalized relationship {initialRel - 0.05}, got {relAfter}");

            state.Undo();
            if (Math.Abs(state.Budget - budgetAfterSign) > 1e-4)
                throw new Exception($"Expected budget reverted to {budgetAfterSign}, got {state.Budget}");
            if (contract.Status != ContractStatus.PendingDelivery)
                throw new Exception($"Expected status reverted to PendingDelivery, got {contract.Status}");
            if (Math.Abs(state.Suppliers.GetCompany("helion-dynamics").RelationshipScore - initialRel) > 1e-4)
                throw new Exception($"Expected relationship restored to {initialRel}, got {state.Suppliers.GetCompany("helion-dynamics").RelationshipScore}");
        }

        private static void Test_AdvanceTimeCommand_ProgressesDeliveryAndFulfillsContract_UndoRedo()
        {
            var suppliers = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseSuppliers(suppliers);

            state.Execute(new SignContractCommand("CTR-TIME-01", "helion-dynamics", "Gövde", "structure", costMUSD: 20.0, deliveryDays: 10));
            var company = state.Suppliers.GetCompany("helion-dynamics");
            double relBefore = company.RelationshipScore;

            // Advance 5 days: not yet delivered
            state.Execute(new AdvanceTimeCommand(5));
            var contract = state.Suppliers.GetContract("CTR-TIME-01");
            if (contract.Status != ContractStatus.PendingDelivery)
                throw new Exception($"Expected PendingDelivery at day 5, got {contract.Status}");

            // Advance 6 days (total 11 days): reached delivery date!
            state.Execute(new AdvanceTimeCommand(6));
            if (contract.Status != ContractStatus.Fulfilled)
                throw new Exception($"Expected Fulfilled at day 11, got {contract.Status}");
            if (contract.FulfilledDate != contract.DeliveryDate)
                throw new Exception($"Expected fulfilled date {contract.DeliveryDate}, got {contract.FulfilledDate}");
            if (Math.Abs(company.RelationshipScore - (relBefore + 0.02)) > 1e-4)
                throw new Exception($"Expected relationship boosted to {relBefore + 0.02}, got {company.RelationshipScore}");

            // Undo time advance reverts contract back to pending
            state.Undo();
            if (contract.Status != ContractStatus.PendingDelivery)
                throw new Exception($"Expected PendingDelivery after Undo, got {contract.Status}");
            if (contract.FulfilledDate != null)
                throw new Exception("Expected null FulfilledDate after Undo");
            if (Math.Abs(company.RelationshipScore - relBefore) > 1e-4)
                throw new Exception($"Expected relationship reverted to {relBefore}, got {company.RelationshipScore}");
        }
    }
}
