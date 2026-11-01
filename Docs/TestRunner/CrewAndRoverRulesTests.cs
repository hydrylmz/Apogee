using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;

namespace ApogeeLocalTestRunner
{
    /// <summary>
    /// Verification tests for Crewed and Rover/Lander specific rules (GDD §7.6, §7.7, §21 M3-06).
    /// Acceptance:
    ///   - Crewed selected: Cabin, Life-Support, Abort-System mandatory (GDD §7.6).
    ///   - Rover selected: EDL mandatory (GDD §7.7).
    ///   - High reliability threshold (>= 98%) enforced for crewed missions.
    ///   - Round-trip return Delta-V and high pad levels for crewed missions.
    /// </summary>
    public static class CrewAndRoverRulesTests
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

        public static CatalogCollection<DestinationRecord> LoadDestinations()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "destinations.json"));
            return CatalogLoader.Load<DestinationRecord>(json);
        }

        public static CatalogCollection<MissionTypeRecord> LoadMissionTypes()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "mission_types.json"));
            return CatalogLoader.Load<MissionTypeRecord>(json);
        }

        public static CatalogCollection<ModuleRecord> LoadModules()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "modules.json"));
            return CatalogLoader.Load<ModuleRecord>(json);
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M3_06_Crewed_MissingCabin_FailsValidation",                    Test_Crewed_MissingCabin_FailsValidation);
            runTest("M3_06_Crewed_MissingLifeSupport_FailsValidation",              Test_Crewed_MissingLifeSupport_FailsValidation);
            runTest("M3_06_Crewed_MissingAbortSystem_FailsValidation",              Test_Crewed_MissingAbortSystem_FailsValidation);
            runTest("M3_06_Crewed_AllRequiredEquipped_PassesValidation",            Test_Crewed_AllRequiredEquipped_PassesValidation);
            runTest("M3_06_Crewed_ReliabilityThreshold_EnforcedAt98Percent",        Test_Crewed_ReliabilityThreshold_EnforcedAt98Percent);
            runTest("M3_06_Crewed_Composer_DerivesReturnDeltaVAndPadLevel",         Test_Crewed_Composer_DerivesReturnDeltaVAndPadLevel);
            runTest("M3_06_Rover_MissingEdl_FailsValidation",                       Test_Rover_MissingEdl_FailsValidation);
            runTest("M3_06_Rover_EquippingEdl_PassesValidation",                    Test_Rover_EquippingEdl_PassesValidation);
            runTest("M3_06_Catalog_CrewAndRoverModules_PresentAndValid",            Test_Catalog_CrewAndRoverModules_PresentAndValid);
        }

        private static void Test_Crewed_MissingCabin_FailsValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var crewed = typesCatalog.GetById("crewed");
            var profile = MissionComposer.Compose(moon, crewed, null);

            var validator = new MandatoryModuleValidator();

            // Vehicle without cabin but with life support and abort system
            var bus = CreateComponent(modules.GetById("bus-satellite-standard"));
            var ls = CreateComponent(modules.GetById("eclss-life-support-mk1"));
            var abort = CreateComponent(modules.GetById("abort-system-tower-mk1"));

            var payload = new PayloadDesign(
                id: "crewed-payload",
                bus: bus,
                lifeSupport: ls,
                cabin: null, // MISSING
                abortSystem: abort
            );

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("crewed-bp", "Crewed Craft")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("crewed");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level4SuperHeavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Title.Contains("Mürettebat Kabini")),
                "Should report missing crew cabin error");
        }

        private static void Test_Crewed_MissingLifeSupport_FailsValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var crewed = typesCatalog.GetById("crewed");
            var profile = MissionComposer.Compose(moon, crewed, null);

            var validator = new MandatoryModuleValidator();

            var bus = CreateComponent(modules.GetById("bus-satellite-standard"));
            var cabin = CreateComponent(modules.GetById("cabin-crewed-mk1"));
            var abort = CreateComponent(modules.GetById("abort-system-tower-mk1"));

            var payload = new PayloadDesign(
                id: "crewed-payload",
                bus: bus,
                lifeSupport: null, // MISSING
                cabin: cabin,
                abortSystem: abort
            );

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("crewed-bp", "Crewed Craft")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("crewed");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level4SuperHeavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Title.Contains("Yaşam Destek")),
                "Should report missing life support error");
        }

        private static void Test_Crewed_MissingAbortSystem_FailsValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var crewed = typesCatalog.GetById("crewed");
            var profile = MissionComposer.Compose(moon, crewed, null);

            var validator = new MandatoryModuleValidator();

            var bus = CreateComponent(modules.GetById("bus-satellite-standard"));
            var cabin = CreateComponent(modules.GetById("cabin-crewed-mk1"));
            var ls = CreateComponent(modules.GetById("eclss-life-support-mk1"));

            var payload = new PayloadDesign(
                id: "crewed-payload",
                bus: bus,
                lifeSupport: ls,
                cabin: cabin,
                abortSystem: null // MISSING
            );

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("crewed-bp", "Crewed Craft")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("crewed");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level4SuperHeavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Title.Contains("Abort")),
                "Should report missing abort system error");
        }

        private static void Test_Crewed_AllRequiredEquipped_PassesValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var crewed = typesCatalog.GetById("crewed");
            var profile = MissionComposer.Compose(moon, crewed, null);

            var validator = new MandatoryModuleValidator();

            var bus = CreateComponent(modules.GetById("bus-satellite-standard"));
            var cabin = CreateComponent(modules.GetById("cabin-crewed-mk1"));
            var ls = CreateComponent(modules.GetById("eclss-life-support-mk1"));
            var abort = CreateComponent(modules.GetById("abort-system-tower-mk1"));
            var edl = CreateComponent(modules.GetById("edl-lunar-skycrane"));
            var power = CreateComponent(modules.GetById("power-solar-array-basic"));

            var payload = new PayloadDesign(
                id: "crewed-payload",
                bus: bus,
                power: power,
                edl: edl,
                lifeSupport: ls,
                cabin: cabin,
                abortSystem: abort
            );

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("crewed-bp", "Crewed Craft")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("crewed");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level4SuperHeavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(!issues.Any(i => i.Severity == ValidationSeverity.Error),
                "Should pass mandatory module validation when cabin, life-support, abort, edl and power are all equipped");
        }

        private static void Test_Crewed_ReliabilityThreshold_EnforcedAt98Percent()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var moon = destCatalog.GetById("moon");
            var crewed = typesCatalog.GetById("crewed");
            var profile = MissionComposer.Compose(moon, crewed, null);

            AssertEqual(0.98, profile.MinReliabilityThreshold, "Crewed mission requires >= 98% reliability");

            var validator = new ReliabilityValidator();
            var blueprint = new Blueprint("test", "test");

            // Context with crewed profile requiring 0.98
            var context = new ValidationContext(profile, LaunchPadInfo.Level4SuperHeavy);

            // Stats with 0.95 reliability (below 0.98)
            var statsCollection = new StatCollection();
            statsCollection.GetOrCreate(StatType.Reliability).BaseValue = 0.95;
            var statsLow = new BlueprintStats(statsCollection);

            var issues = validator.Validate(blueprint, statsLow, context).ToList();
            AssertTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Title.Contains("Güvenilirlik")),
                "Reliability below 98% must trigger error on crewed missions");
        }

        private static void Test_Crewed_Composer_DerivesReturnDeltaVAndPadLevel()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var mars = destCatalog.GetById("mars");
            var crewed = typesCatalog.GetById("crewed");
            var rover = typesCatalog.GetById("rover");

            var crewedProfile = MissionComposer.Compose(mars, crewed, null);
            var roverProfile = MissionComposer.Compose(mars, rover, null);

            AssertTrue(crewedProfile.RequiredDeltaV > roverProfile.RequiredDeltaV,
                "Crewed profile requires round-trip Delta-V greater than one-way rover");
            AssertTrue(crewedProfile.RequiredLaunchPadLevel >= 4,
                "Crewed Mars mission requires Pad Level >= 4");
            AssertTrue(crewedProfile.IsCrewed, "Profile flagged as crewed");
            AssertTrue(crewedProfile.RequiresLifeSupport, "Profile requires life support");
        }

        private static void Test_Rover_MissingEdl_FailsValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var profile = MissionComposer.Compose(moon, rover, null);

            var validator = new MandatoryModuleValidator();

            var bus = CreateComponent(modules.GetById("bus-rover-light"));
            var power = CreateComponent(modules.GetById("power-solar-array-basic"));
            var payload = new PayloadDesign(id: "rover-payload", bus: bus, power: power, edl: null); // MISSING EDL

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("rover-bp", "Moon Rover")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("rover");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level3Heavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(issues.Any(i => i.Severity == ValidationSeverity.Error && i.Title.Contains("EDL")),
                "Should report missing EDL error for rover mission");
        }

        private static void Test_Rover_EquippingEdl_PassesValidation()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var modules = LoadModules();

            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var profile = MissionComposer.Compose(moon, rover, null);

            var validator = new MandatoryModuleValidator();

            var bus = CreateComponent(modules.GetById("bus-rover-light"));
            var power = CreateComponent(modules.GetById("power-solar-array-basic"));
            var edl = CreateComponent(modules.GetById("edl-lunar-skycrane"));
            var payload = new PayloadDesign(id: "rover-payload", bus: bus, power: power, edl: edl);

            var stageEngine = CreateComponent(modules.GetById("engine-kerosene-lox-heavy"));
            var stageTank = CreateComponent(modules.GetById("tank-kerosene-medium"));
            var stage = new StageDesign("stage-1", "Stage 1", stageEngine, stageTank);

            var blueprint = new Blueprint("rover-bp", "Moon Rover")
                .WithPayload(payload)
                .WithStage(0, stage)
                .WithTargetMissionType("rover");

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            var context = new ValidationContext(profile, LaunchPadInfo.Level3Heavy);

            var issues = validator.Validate(blueprint, stats, context).ToList();
            AssertTrue(!issues.Any(i => i.Severity == ValidationSeverity.Error),
                "Should pass mandatory validation when EDL is equipped for rover");
        }

        private static void Test_Catalog_CrewAndRoverModules_PresentAndValid()
        {
            var modules = LoadModules();

            var cabin = modules.GetById("cabin-crewed-mk1");
            AssertTrue(cabin != null, "Crew cabin exists in catalog");
            AssertEqual("cabin", cabin.Category, "Crew cabin category");
            AssertTrue(cabin.BaseReliability >= 0.99, "Crew cabin high base reliability");

            var eclss = modules.GetById("eclss-life-support-mk1");
            AssertTrue(eclss != null, "ECLSS exists in catalog");
            AssertEqual("life-support", eclss.Category, "ECLSS category");

            var abort = modules.GetById("abort-system-tower-mk1");
            AssertTrue(abort != null, "Abort tower exists in catalog");
            AssertEqual("abort-system", abort.Category, "Abort tower category");

            var rtg = modules.GetById("power-rtg-deepspace");
            AssertTrue(rtg != null, "RTG power exists in catalog");
            AssertEqual("power", rtg.Category, "RTG category");
            AssertTrue(rtg.PowerGenerationKw > 0, "RTG generates power");

            var ai = modules.GetById("adcs-autonomous-ai-core");
            AssertTrue(ai != null, "Autonomous AI core exists in catalog");
            AssertEqual("adcs", ai.Category, "AI core category");
        }

        private static DesignComponent CreateComponent(ModuleRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            return new DesignComponent(
                id: record.Id,
                category: record.Category,
                name: record.Id,
                dryMass: record.DryMassTons,
                cost: record.CostMUSD,
                buildTime: record.BuildTimeDays,
                reliability: record.BaseReliability,
                powerGeneration: record.PowerGenerationKw,
                powerConsumption: record.PowerConsumptionKw,
                thermalLoad: record.ThermalLoadKw,
                thermalDissipation: record.ThermalDissipationKw,
                dataRate: record.DataRateGbPerDay,
                dataStorage: record.DataStorageGb,
                downlinkRate: record.DownlinkRateGbPerDay,
                isp: record.Isp,
                thrust: record.ThrustVacKn,
                requiredResearchId: record.ResearchRequiredId
            );
        }

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
            }
        }

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }
    }
}
