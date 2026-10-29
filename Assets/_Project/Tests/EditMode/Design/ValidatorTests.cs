using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Apogee.Design;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for Design Review Validators.
    /// Complies with GDD §7.4, §8.1, §8.3, §17.3 item 6, §20.1, and §21 M1-07.
    /// Tests positive and negative cases for each of the 8 validation rules.
    /// </summary>
    [TestFixture]
    public class ValidatorTests
    {
        private const double Tol = 1e-4;

        #region Helper Factories

        private static DesignComponent CreateBus(double dryMass = 1.0, double cost = 2.0, double powerCon = 0.5, double thermalLoad = 0.3, double rel = 0.99, string researchId = null) =>
            new DesignComponent("bus-std", "bus", "Standard Bus", dryMass: dryMass, cost: cost, powerConsumption: powerCon, thermalLoad: thermalLoad, reliability: rel, requiredResearchId: researchId);

        private static DesignComponent CreateSolar(double dryMass = 0.2, double cost = 1.0, double powerGen = 2.0, double rel = 0.99, string researchId = null) =>
            new DesignComponent("solar-std", "power", "Solar Array", dryMass: dryMass, cost: cost, powerGeneration: powerGen, reliability: rel, requiredResearchId: researchId);

        private static DesignComponent CreateRadiator(double dryMass = 0.1, double cost = 0.5, double diss = 1.0, double rel = 0.99, string researchId = null) =>
            new DesignComponent("rad-std", "thermal", "Radiator Panel", dryMass: dryMass, cost: cost, thermalDissipation: diss, reliability: rel, requiredResearchId: researchId);

        private static DesignComponent CreateEngine(string id = "eng-1", double dryMass = 2.0, double cost = 5.0, double isp = 320.0, double rel = 0.98, string researchId = null) =>
            new DesignComponent(id, "engine", "Rocket Engine", dryMass: dryMass, cost: cost, reliability: rel, isp: isp, requiredResearchId: researchId);

        private static DesignComponent CreateTank(string id = "tank-1", double dryMass = 3.0, double cost = 2.0, double rel = 0.99, string researchId = null) =>
            new DesignComponent(id, "tank", "Propellant Tank", dryMass: dryMass, cost: cost, reliability: rel, requiredResearchId: researchId);

        private static Blueprint CreateStandardValidBlueprint(
            string targetDest = "leo",
            string targetType = "orbiter",
            double stage1Prop = 100.0,
            double isp = 310.0,
            string busResearch = null)
        {
            var bus = CreateBus(researchId: busResearch);
            var power = CreateSolar();
            var thermal = CreateRadiator();
            var payload = new PayloadDesign("sat-payload", bus: bus, power: power, thermal: thermal);

            var s1Engine = CreateEngine("s1-eng", dryMass: 4.0, isp: isp);
            var s1Tank = CreateTank("s1-tank", dryMass: 6.0);
            var stage1 = new StageDesign("stage-1", engine: s1Engine, tank: s1Tank, propellantMass: stage1Prop);

            return new Blueprint(
                id: "bp-valid",
                name: "Valid Scout",
                targetDestinationId: targetDest,
                targetMissionTypeId: targetType,
                payload: payload,
                stages: new[] { stage1 });
        }

        #endregion

        #region 1. Delta-V Validator Tests

        [Test]
        public void DeltaVValidator_SufficientDeltaV_ReturnsInfo()
        {
            // Vehicle produces > 5000 m/s
            var bp = CreateStandardValidBlueprint(stage1Prop: 100.0, isp: 310.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("leo-test", requiredDeltaV: 4000.0);
            var context = new ValidationContext(mission);

            var validator = new DeltaVValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError), "Should have no errors");
            Assert.IsFalse(issues.Any(i => i.IsWarning), "Should have no warnings with high margin");
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.DeltaV), "Should have Info confirming delta-v");
        }

        [Test]
        public void DeltaVValidator_InsufficientDeltaV_ReturnsErrorWithReasonAndHint()
        {
            // Very low propellant -> low delta-v
            var bp = CreateStandardValidBlueprint(stage1Prop: 2.0, isp: 250.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("moon-test", requiredDeltaV: 9000.0);
            var context = new ValidationContext(mission);

            var validator = new DeltaVValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.DeltaV);
            Assert.IsNotNull(error, "Must emit Delta-V deficit error");
            Assert.IsTrue(error.Reason.Contains("Menzil Açığı") || error.Title.Contains("Menzil Açığı"));
            Assert.IsNotEmpty(error.Hint, "Hint must provide guidance on what to inspect");
            Assert.Less(error.CurrentValue.Value, error.TargetValue.Value);
        }

        [Test]
        public void DeltaVValidator_NarrowMargin_ReturnsWarning()
        {
            // Set required DeltaV to be within 2% of actual net DeltaV
            var bp = CreateStandardValidBlueprint(stage1Prop: 50.0, isp: 300.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Make target 98% of vehicle net delta-v (margin = ~2% < 5% threshold)
            double required = stats.NetDeltaV * 0.98;
            var mission = new MissionProfile("tight-mission", requiredDeltaV: required);
            var context = new ValidationContext(mission, deltaVMarginWarningThreshold: 0.05);

            var validator = new DeltaVValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.DeltaV);
            Assert.IsNotNull(warning, "Must emit low Delta-V margin warning");
            Assert.IsNotEmpty(warning.Reason);
            Assert.IsNotEmpty(warning.Hint);
        }

        #endregion

        #region 2. Mass / Capacity Validator Tests

        [Test]
        public void MassCapacityValidator_WithinPadLimits_ReturnsInfo()
        {
            var bp = CreateStandardValidBlueprint(stage1Prop: 50.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            var pad = new LaunchPadInfo(level: 2, maxWetMass: 300.0, maxStages: 3);
            var context = new ValidationContext(launchPad: pad);

            var validator = new MassCapacityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.MassCapacity));
        }

        [Test]
        public void MassCapacityValidator_ExceedsPadMaxWetMass_ReturnsErrorWithReasonAndHint()
        {
            var bp = CreateStandardValidBlueprint(stage1Prop: 500.0); // Wet mass ~513t
            var stats = BlueprintEvaluator.Evaluate(bp);

            var pad = new LaunchPadInfo(level: 1, maxWetMass: 60.0, maxStages: 2);
            var context = new ValidationContext(launchPad: pad);

            var validator = new MassCapacityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MassCapacity);
            Assert.IsNotNull(error, "Must emit mass capacity error");
            Assert.Greater(error.CurrentValue.Value, error.TargetValue.Value);
            Assert.IsNotEmpty(error.Reason);
            Assert.IsNotEmpty(error.Hint);
        }

        [Test]
        public void MassCapacityValidator_NearPadLimit_ReturnsWarning()
        {
            var bp = CreateStandardValidBlueprint(stage1Prop: 50.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Pad capacity just above vehicle wet mass (96% of pad capacity)
            double padCapacity = stats.WetMass / 0.96;
            var pad = new LaunchPadInfo(level: 2, maxWetMass: padCapacity, maxStages: 3);
            var context = new ValidationContext(launchPad: pad, massMarginWarningThreshold: 0.95);

            var validator = new MassCapacityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.MassCapacity);
            Assert.IsNotNull(warning, "Must emit near-capacity warning");
        }

        [Test]
        public void MassCapacityValidator_EmptyVehicle_ReturnsError()
        {
            var emptyBp = new Blueprint("empty-bp", "Empty");
            var stats = BlueprintEvaluator.Evaluate(emptyBp);
            var context = new ValidationContext();

            var validator = new MassCapacityValidator();
            var issues = validator.Validate(emptyBp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MassCapacity);
            Assert.IsNotNull(error, "Empty blueprint must trigger validation error");
        }

        #endregion

        #region 3. Launch Pad Validator Tests

        [Test]
        public void LaunchPadValidator_ValidStageCountAndLevel_ReturnsInfo()
        {
            var bp = CreateStandardValidBlueprint();
            var stats = BlueprintEvaluator.Evaluate(bp);

            var pad = new LaunchPadInfo(level: 2, maxWetMass: 200.0, maxStages: 3);
            var mission = new MissionProfile("m", requiredLaunchPadLevel: 2);
            var context = new ValidationContext(mission, pad);

            var validator = new LaunchPadValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.LaunchPad));
        }

        [Test]
        public void LaunchPadValidator_StageCountExceedsPad_ReturnsError()
        {
            var stage1 = new StageDesign("s1", engine: CreateEngine(), tank: CreateTank(), propellantMass: 20.0);
            var stage2 = new StageDesign("s2", engine: CreateEngine(), tank: CreateTank(), propellantMass: 10.0);
            var stage3 = new StageDesign("s3", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0);

            var bp = new Blueprint("3-stage-bp", "Tri-Stage", payload: new PayloadDesign("p", bus: CreateBus()), stages: new[] { stage1, stage2, stage3 });
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Pad only supports 2 stages
            var pad = new LaunchPadInfo(level: 1, maxWetMass: 100.0, maxStages: 2);
            var context = new ValidationContext(launchPad: pad);

            var validator = new LaunchPadValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.LaunchPad);
            Assert.IsNotNull(error, "Stage count excess must produce an error");
            Assert.AreEqual(3.0, error.CurrentValue.Value);
            Assert.AreEqual(2.0, error.TargetValue.Value);
        }

        [Test]
        public void LaunchPadValidator_PadLevelBelowMissionRequirement_ReturnsError()
        {
            var bp = CreateStandardValidBlueprint();
            var stats = BlueprintEvaluator.Evaluate(bp);

            var pad = new LaunchPadInfo(level: 1, maxWetMass: 200.0, maxStages: 3);
            var mission = new MissionProfile("heavy-mission", requiredLaunchPadLevel: 3);
            var context = new ValidationContext(mission, pad);

            var validator = new LaunchPadValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.LaunchPad);
            Assert.IsNotNull(error, "Pad tier mismatch must produce an error");
            Assert.AreEqual(1.0, error.CurrentValue.Value);
            Assert.AreEqual(3.0, error.TargetValue.Value);
        }

        #endregion

        #region 4. Research Lock Validator Tests

        [Test]
        public void ResearchLockValidator_AllUnlocked_ReturnsInfo()
        {
            var bp = CreateStandardValidBlueprint();
            var stats = BlueprintEvaluator.Evaluate(bp);
            var context = new ValidationContext();

            var validator = new ResearchLockValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.ResearchLock));
        }

        [Test]
        public void ResearchLockValidator_LockedModule_ReturnsError()
        {
            var lockedBus = CreateBus(researchId: "tech_advanced_avionics");
            var payload = new PayloadDesign("p", bus: lockedBus);
            var stage = new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 10.0);
            var bp = new Blueprint("locked-bp", "Locked", payload: payload, stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Context has no research unlocked
            var context = new ValidationContext(unlockedResearchIds: new[] { "tech_basic" });

            var validator = new ResearchLockValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.ResearchLock);
            Assert.IsNotNull(error, "Locked module research must trigger error");
            Assert.IsTrue(error.Reason.Contains("tech_advanced_avionics"));
            Assert.IsNotEmpty(error.Hint);
        }

        [Test]
        public void ResearchLockValidator_LockedMaterial_ReturnsError()
        {
            var lockedMaterial = MaterialSpec.CarbonComposite; // Requires "tech_composites"
            var engine = CreateEngine();
            var tank = CreateTank();
            var stage = new StageDesign("s", engine: engine, tank: tank, propellantMass: 10.0, structureMaterial: lockedMaterial);
            var bp = new Blueprint("mat-bp", "Mat", payload: new PayloadDesign("p", bus: CreateBus()), stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext(unlockedResearchIds: new[] { "tech_basic" });

            var validator = new ResearchLockValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.ResearchLock);
            Assert.IsNotNull(error, "Locked material must trigger error");
            Assert.IsTrue(error.Reason.Contains("tech_composites"));
        }

        [Test]
        public void ResearchLockValidator_UnlockedTechnology_PassesCleanly()
        {
            var lockedBus = CreateBus(researchId: "tech_advanced_avionics");
            var bp = new Blueprint("unlocked-bp", "Unlocked", payload: new PayloadDesign("p", bus: lockedBus), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Provide the unlocked research in context
            var context = new ValidationContext(unlockedResearchIds: new[] { "tech_advanced_avionics" });

            var validator = new ResearchLockValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError), "Unlocked technology must not trigger error");
        }

        #endregion

        #region 5. Power Balance Validator Tests

        [Test]
        public void PowerValidator_SurplusGeneration_ReturnsInfo()
        {
            var bus = new DesignComponent("b", "bus", powerConsumption: 0.5);
            var solar = new DesignComponent("s", "power", powerGeneration: 2.0); // 2.0 kW vs 0.5 kW
            var bp = new Blueprint("pow-bp", "Power", payload: new PayloadDesign("p", bus: bus, power: solar), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext();
            var validator = new PowerValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.Power));
        }

        [Test]
        public void PowerValidator_PowerDeficit_ReturnsError()
        {
            var bus = new DesignComponent("b", "bus", powerConsumption: 3.0);
            var solar = new DesignComponent("s", "power", powerGeneration: 1.0); // Deficit: 2.0 kW
            var bp = new Blueprint("pow-bp", "Power", payload: new PayloadDesign("p", bus: bus, power: solar), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext();
            var validator = new PowerValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.Power);
            Assert.IsNotNull(error, "Power deficit must trigger error");
            Assert.AreEqual(1.0, error.CurrentValue.Value, Tol);
            Assert.AreEqual(3.0, error.TargetValue.Value, Tol);
            Assert.IsNotEmpty(error.Hint);
        }

        [Test]
        public void PowerValidator_LowPowerMargin_ReturnsWarning()
        {
            var bus = new DesignComponent("b", "bus", powerConsumption: 0.95);
            var solar = new DesignComponent("s", "power", powerGeneration: 1.0); // Margin: 0.05 / 1.0 = 5% < 10%
            var bp = new Blueprint("pow-bp", "Power", payload: new PayloadDesign("p", bus: bus, power: solar), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext(powerMarginWarningThreshold: 0.10);
            var validator = new PowerValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.Power);
            Assert.IsNotNull(warning, "Low margin should trigger warning");
        }

        #endregion

        #region 6. Thermal Balance Validator Tests

        [Test]
        public void ThermalValidator_SufficientDissipation_ReturnsInfo()
        {
            var bus = new DesignComponent("b", "bus", thermalLoad: 0.5);
            var rad = new DesignComponent("r", "thermal", thermalDissipation: 1.5);
            var bp = new Blueprint("th-bp", "Thermal", payload: new PayloadDesign("p", bus: bus, thermal: rad), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext();
            var validator = new ThermalValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.Thermal));
        }

        [Test]
        public void ThermalValidator_ThermalOverload_ReturnsError()
        {
            var bus = new DesignComponent("b", "bus", thermalLoad: 2.0);
            var rad = new DesignComponent("r", "thermal", thermalDissipation: 0.5); // Load 2.0 vs Diss 0.5
            var bp = new Blueprint("th-bp", "Thermal", payload: new PayloadDesign("p", bus: bus, thermal: rad), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext();
            var validator = new ThermalValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.Thermal);
            Assert.IsNotNull(error, "Thermal deficit must trigger error");
            Assert.AreEqual(0.5, error.CurrentValue.Value, Tol);
            Assert.AreEqual(2.0, error.TargetValue.Value, Tol);
        }

        [Test]
        public void ThermalValidator_LowThermalMargin_ReturnsWarning()
        {
            var bus = new DesignComponent("b", "bus", thermalLoad: 0.95);
            var rad = new DesignComponent("r", "thermal", thermalDissipation: 1.0); // Margin = 5% < 10%
            var bp = new Blueprint("th-bp", "Thermal", payload: new PayloadDesign("p", bus: bus, thermal: rad), stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var context = new ValidationContext(thermalMarginWarningThreshold: 0.10);
            var validator = new ThermalValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.Thermal);
            Assert.IsNotNull(warning, "Low thermal margin must trigger warning");
        }

        #endregion

        #region 7. Mandatory Modules Validator Tests

        [Test]
        public void MandatoryModuleValidator_MissingBus_ReturnsError()
        {
            // Payload without Bus
            var payload = new PayloadDesign("p", bus: null);
            var bp = new Blueprint("no-bus", "No Bus", payload: payload, stages: new[] { new StageDesign("s", engine: CreateEngine(), tank: CreateTank(), propellantMass: 5.0) });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var validator = new MandatoryModuleValidator();
            var issues = validator.Validate(bp, stats, new ValidationContext()).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MandatoryModules);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Title.Contains("Bus"));
        }

        [Test]
        public void MandatoryModuleValidator_MissingStageEngine_ReturnsError()
        {
            var stageWithoutEngine = new StageDesign("s1", engine: null, tank: CreateTank(), propellantMass: 10.0);
            var bp = new Blueprint("no-eng", "No Eng", payload: new PayloadDesign("p", bus: CreateBus()), stages: new[] { stageWithoutEngine });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var validator = new MandatoryModuleValidator();
            var issues = validator.Validate(bp, stats, new ValidationContext()).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MandatoryModules);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Title.Contains("Motor"));
        }

        [Test]
        public void MandatoryModuleValidator_LanderWithoutEdl_ReturnsError()
        {
            var bp = CreateStandardValidBlueprint(targetType: "lander");
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("lander-mission", targetMissionTypeId: "lander", requiresEdl: true);
            var context = new ValidationContext(mission);

            var validator = new MandatoryModuleValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MandatoryModules);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Title.Contains("EDL"));
        }

        [Test]
        public void MandatoryModuleValidator_CrewedWithoutLifeSupport_ReturnsError()
        {
            var bp = CreateStandardValidBlueprint(targetType: "crewed");
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("crewed-mission", targetMissionTypeId: "crewed", requiresLifeSupport: true);
            var context = new ValidationContext(mission);

            var validator = new MandatoryModuleValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MandatoryModules);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Title.Contains("Yaşam Destek"));
        }

        [Test]
        public void MandatoryModuleValidator_MissingRequiredCategory_ReturnsError()
        {
            var bp = CreateStandardValidBlueprint(); // Has bus, power, thermal, engine, tank
            var stats = BlueprintEvaluator.Evaluate(bp);

            // Mission explicitly asks for "radar" category
            var mission = new MissionProfile("radar-mission", requiredModuleCategories: new[] { "radar" });
            var context = new ValidationContext(mission);

            var validator = new MandatoryModuleValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.MandatoryModules);
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Title.Contains("radar"));
        }

        #endregion

        #region 8. Reliability Validator Tests

        [Test]
        public void ReliabilityValidator_AboveThreshold_ReturnsInfo()
        {
            var bp = CreateStandardValidBlueprint(); // Reliability ~0.95
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("safe-m", minReliabilityThreshold: 0.80);
            var context = new ValidationContext(mission);

            var validator = new ReliabilityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError));
            Assert.IsTrue(issues.Any(i => i.IsInfo && i.RuleId == ValidationRuleId.Reliability));
        }

        [Test]
        public void ReliabilityValidator_BelowThreshold_ReturnsError()
        {
            // Low reliability engine
            var badEngine = CreateEngine(rel: 0.50);
            var stage = new StageDesign("s", engine: badEngine, tank: CreateTank(), propellantMass: 10.0);
            var bp = new Blueprint("unreliable-bp", "Unreliable", payload: new PayloadDesign("p", bus: CreateBus()), stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("strict-m", minReliabilityThreshold: 0.90);
            var context = new ValidationContext(mission);

            var validator = new ReliabilityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.Reliability);
            Assert.IsNotNull(error);
            Assert.Less(error.CurrentValue.Value, error.TargetValue.Value);
            Assert.IsNotEmpty(error.Hint);
        }

        [Test]
        public void ReliabilityValidator_NearThreshold_ReturnsWarning()
        {
            var engine = CreateEngine(rel: 0.86);
            var stage = new StageDesign("s", engine: engine, tank: CreateTank(rel: 1.0), propellantMass: 10.0);
            var bp = new Blueprint("marginal-bp", "Marginal", payload: new PayloadDesign("p", bus: CreateBus(rel: 1.0)), stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp); // Reliability = 0.86

            // Threshold is 0.84, current is 0.86 (margin is 0.02 < 0.05 warning threshold)
            var mission = new MissionProfile("marginal-m", minReliabilityThreshold: 0.84);
            var context = new ValidationContext(mission, reliabilityMarginWarningThreshold: 0.05);

            var validator = new ReliabilityValidator();
            var issues = validator.Validate(bp, stats, context).ToList();

            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.Reliability);
            Assert.IsNotNull(warning, "Near threshold reliability must produce warning");
        }

        #endregion

        #region 9. Integrated BlueprintValidator & ValidationReport Tests

        [Test]
        public void BlueprintValidator_FullyCompliantVehicle_ProducesPassingReport()
        {
            var bp = CreateStandardValidBlueprint(stage1Prop: 60.0, isp: 320.0);
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile(
                id: "leo-sat",
                targetDestinationId: "leo",
                targetMissionTypeId: "orbiter",
                requiredDeltaV: 4000.0,
                minReliabilityThreshold: 0.85);

            var pad = LaunchPadInfo.Level2Medium;
            var context = new ValidationContext(mission, pad);

            var report = BlueprintValidator.Validate(bp, stats, context);

            Assert.IsNotNull(report);
            Assert.IsTrue(report.IsValid, "Clean blueprint must pass validation");
            Assert.AreEqual(0, report.ErrorCount, "Error count must be zero");
            Assert.Greater(report.InfoCount, 0, "Should contain positive info confirmations");
            StringAssert.Contains("PASS", report.ToString());
        }

        [Test]
        public void BlueprintValidator_MultiDefectVehicle_CapturesAllFailuresWithHints()
        {
            // Creates a blueprint with multiple defects:
            // 1. Missing bus (Mandatory module)
            // 2. Power deficit (Power)
            // 3. Thermal overload (Thermal)
            // 4. Locked research (Research lock)
            // 5. Huge mass exceeding small pad (Mass capacity)
            // 6. Low delta-v vs high requirement (Delta-V)

            var badSolar = new DesignComponent("bad-solar", "power", powerGeneration: 0.1, powerConsumption: 2.0); // deficit
            var badThermal = new DesignComponent("bad-thermal", "thermal", thermalLoad: 5.0, thermalDissipation: 0.2); // overload
            var lockedEngine = CreateEngine(researchId: "tech_fusion");

            var payload = new PayloadDesign("bad-p", bus: null, power: badSolar, thermal: badThermal);
            var stage = new StageDesign("s", engine: lockedEngine, tank: CreateTank(), propellantMass: 100.0); // 100t > 60t pad limit

            var bp = new Blueprint("defect-bp", "Defective Vehicle", payload: payload, stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var mission = new MissionProfile("hard-mission", requiredDeltaV: 15000.0, minReliabilityThreshold: 0.99);
            var pad = LaunchPadInfo.Level1Small; // Max 60t
            var context = new ValidationContext(mission, pad, unlockedResearchIds: new[] { "tech_basic" });

            var report = BlueprintValidator.Validate(bp, stats, context);

            Assert.IsFalse(report.IsValid, "Defective blueprint must fail validation");
            Assert.GreaterOrEqual(report.ErrorCount, 4, "Should catch multiple distinct errors");

            // Verify each issue has non-empty Title, Reason, and Hint
            foreach (var issue in report.Issues)
            {
                Assert.IsNotEmpty(issue.Title, "Issue title must not be empty");
                Assert.IsNotEmpty(issue.Reason, "Issue reason must not be empty");
                Assert.IsNotEmpty(issue.Hint, "Issue hint must not be empty");
            }

            Assert.IsTrue(report.HasError(ValidationRuleId.MandatoryModules));
            Assert.IsTrue(report.HasError(ValidationRuleId.Power));
            Assert.IsTrue(report.HasError(ValidationRuleId.Thermal));
            Assert.IsTrue(report.HasError(ValidationRuleId.ResearchLock));
            Assert.IsTrue(report.HasError(ValidationRuleId.MassCapacity));
            Assert.IsTrue(report.HasError(ValidationRuleId.DeltaV));
        }

        #endregion
    }
}
