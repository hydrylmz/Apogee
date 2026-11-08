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
            runTest("M6_05_Crewed_Mars_FullDesign_PassesAllValidators",             Test_Crewed_Mars_FullDesign_PassesAllValidators);
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

        private static void Test_Crewed_Mars_FullDesign_PassesAllValidators()
        {
            // GDD §21 M6-05 Acceptance Criterion:
            // "Kabul: İçerik doğrulayıcıdan geçer; Mars insanlı tasarlanabilir."

            var bus = new DesignComponent("bus-sat", "bus", dryMass: 0.85, reliability: 0.999, powerConsumption: 0.20, thermalLoad: 0.15);
            var cabin = new DesignComponent("cabin-crewed-mk1", "cabin", dryMass: 4.5, cost: 42.0, reliability: 0.999, powerConsumption: 1.2, thermalLoad: 0.8, requiredResearchId: "life-open-loop");
            var eclss = new DesignComponent("eclss-life-support-mk1", "life-support", dryMass: 1.8, cost: 18.0, reliability: 0.999, powerConsumption: 0.8, thermalLoad: 0.5, requiredResearchId: "life-open-loop");
            var abort = new DesignComponent("abort-system-tower-mk1", "abort-system", dryMass: 2.1, cost: 14.0, reliability: 0.9995, powerConsumption: 0.05, requiredResearchId: "life-open-loop");
            var edl = new DesignComponent("edl-heat-shield-mk2", "edl", dryMass: 0.70, cost: 9.5, reliability: 0.999, requiredResearchId: "edl-heat-shield-mk2");
            var power = new DesignComponent("power-rtg-deepspace", "power", dryMass: 0.28, cost: 24.0, reliability: 0.999, powerGeneration: 4.5, thermalLoad: 0.5, requiredResearchId: "power-rtg");
            var thermal = new DesignComponent("thermal-active-heatpipe", "thermal", dryMass: 0.15, cost: 2.6, reliability: 0.999, powerConsumption: 0.10, thermalDissipation: 3.5);
            var comms = new DesignComponent("comms-dish-highgain", "comms", dryMass: 0.12, cost: 4.0, reliability: 0.999, powerConsumption: 0.35, downlinkRate: 28.0);
            var adcs = new DesignComponent("adcs-autonomous-ai-core", "adcs", dryMass: 0.12, cost: 12.0, reliability: 0.999, powerConsumption: 0.25, requiredResearchId: "adcs-autonomous-ai");

            var payload = new PayloadDesign(
                id: "mars-crewed-payload",
                name: "Mars Mürettebat Keşif Aracı",
                bus: bus,
                power: power,
                thermal: thermal,
                comms: comms,
                adcs: adcs,
                edl: edl,
                lifeSupport: eclss,
                instruments: null,
                extraModules: null,
                cabin: cabin,
                abortSystem: abort
            );

            // Staged propulsion stack engineered for Mars crewed round-trip DeltaV (> 19,200 m/s)
            var s3Engine = new DesignComponent("engine-hydrolox-s3", "engine", dryMass: 0.7, reliability: 0.999, isp: 450.0, thrust: 150.0, requiredResearchId: "prop-hydrogen-upper");
            var s3Tank = new DesignComponent("tank-hydrolox-s3", "tank", dryMass: 2.0, reliability: 0.999);
            var stage3 = new StageDesign("stage-3-return", "Aşama 3 (Mars Dönüş)", s3Engine, s3Tank, propellantMass: 65.0);

            var s2Engine = new DesignComponent("engine-hydrolox-s2", "engine", dryMass: 2.0, reliability: 0.999, isp: 450.0, thrust: 600.0, requiredResearchId: "prop-hydrogen-upper");
            var s2Tank = new DesignComponent("tank-hydrolox-s2", "tank", dryMass: 8.0, reliability: 0.999);
            var stage2 = new StageDesign("stage-2-transfer", "Aşama 2 (Mars Transfer)", s2Engine, s2Tank, propellantMass: 380.0);

            var s1Engine = new DesignComponent("engine-heavy-s1", "engine", dryMass: 10.0, reliability: 0.999, isp: 330.0, thrust: 8500.0, requiredResearchId: "prop-advanced-kerosene");
            var s1Tank = new DesignComponent("tank-heavy-s1", "tank", dryMass: 40.0, reliability: 0.999);
            var stage1 = new StageDesign("stage-1-booster", "Aşama 1 (Kalkış Güçlendirici)", s1Engine, s1Tank, propellantMass: 2200.0);

            var blueprint = new Blueprint("mars-crewed-bp", "Mars Hermes-1", payload: payload, stages: new[] { stage1, stage2, stage3 })
                .WithTargetDestination("mars")
                .WithTargetMissionType("crewed");

            var stats = BlueprintEvaluator.Evaluate(blueprint);

            var profile = new MissionProfile(
                id: "profile-mars-crewed",
                name: "Mars İnsanlı Keşif",
                targetDestinationId: "mars",
                targetMissionTypeId: "crewed",
                requiredDeltaV: 19200.0,
                minReliabilityThreshold: 0.98,
                requiredLaunchPadLevel: 5,
                minDownlinkRate: 5.0,
                minDataStorage: 10.0,
                requiresEdl: true,
                requiresLifeSupport: true,
                requiredModuleCategories: new[] { "bus", "power", "edl", "cabin", "life-support", "abort-system" },
                requiredResearchIds: new[] { "life-open-loop", "edl-heat-shield-mk2", "prop-hydrogen-upper", "power-rtg", "adcs-autonomous-ai" }
            );

            var pad = LaunchPadInfo.Level5Colossal;
            var unlockedTechs = new HashSet<string>
            {
                "life-open-loop", "edl-heat-shield-mk2", "prop-hydrogen-upper",
                "prop-advanced-kerosene", "power-rtg", "adcs-autonomous-ai"
            };

            var context = new ValidationContext(profile, pad, unlockedTechs);
            var report = BlueprintValidator.Validate(blueprint, stats, context);

            AssertTrue(report.IsValid, $"Mars crewed design must pass all validators. Errors: {string.Join("; ", report.Errors.Select(e => e.Title + ": " + e.Reason))}");
            AssertEqual(0, report.ErrorCount, "Error count must be zero");
            AssertTrue(stats.NetDeltaV >= 19200.0, "Mars crewed must deliver sufficient Delta-V");
            AssertTrue(stats.Reliability >= 0.98, "Mars crewed reliability must meet or exceed 98%");
            AssertTrue(stats.WetMass <= pad.MaxWetMass, "Vehicle liftoff mass must remain within Pad Level 5 capacity");
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
