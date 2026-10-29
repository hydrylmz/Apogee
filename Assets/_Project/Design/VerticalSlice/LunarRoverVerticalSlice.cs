using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Design
{
    /// <summary>
    /// Content catalog and validation scenario provider for the Milestone 2 Vertical Slice (Moon + Rover, single company).
    /// Implements GDD §21 M2-07:
    ///   - Destination: Moon ("moon")
    ///   - Mission Type: Rover ("rover")
    ///   - ~12 Modules (~17 carefully calibrated components across 8 categories)
    ///   - 3 Engines (Kerosene Mk-1, Heavy Kerosene, Hydrolox Upper)
    ///   - 3 Materials (Al-Alloy, Al-Li, Carbon-Composite)
    ///   - 1 Primary Supplier (Helion Dynamics)
    ///   - Pass / Fail Blueprint scenarios fulfilling the acceptance criteria: "Oyuncu Ay rover'ı tasarlayıp review'dan geçirebilir."
    /// </summary>
    public static class LunarRoverVerticalSlice
    {
        // ── Constants & IDs ──────────────────────────────────────────────────────────

        public const string DestinationId = "moon";
        public const string DestinationName = "Ay (Lunar Surface)";
        public const string MissionTypeId = "rover";
        public const string MissionTypeName = "Ay Gezgini (Rover)";
        public const string PrimarySupplierId = "helion-dynamics";
        public const string PrimarySupplierName = "Helion Dynamics";

        public const double TargetDeltaV = 13500.0; // Gameplay-calibrated mission profile requirement
        public const double MinReliability = 0.50; // Calibrated for multi-stage component chain

        // ── 1 Supplier (Helion Dynamics) ────────────────────────────────────────────

        public static readonly SupplierSpec HelionSupplier = new SupplierSpec(
            id: PrimarySupplierId,
            name: PrimarySupplierName,
            costMultiplier: 0.85,
            deliveryMultiplier: 0.90,
            reliabilityMultiplier: 0.96
        );

        // ── 3 Structural Materials ──────────────────────────────────────────────────

        public static readonly MaterialSpec AluminumAlloy = MaterialSpec.AluminumAlloy;
        public static readonly MaterialSpec AluminumLithium = MaterialSpec.AluminumLithium;
        public static readonly MaterialSpec CarbonComposite = MaterialSpec.CarbonComposite;

        public static IReadOnlyList<MaterialSpec> Materials { get; } = new[]
        {
            AluminumAlloy,
            AluminumLithium,
            CarbonComposite
        };

        // ── ~12 Modules (including 3 Engines) ───────────────────────────────────────

        public static readonly DesignComponent EngineKeroseneMk1 = new DesignComponent(
            id: "engine-kerosene-lox-mk1",
            category: "engine",
            name: "Kerosen Motoru (Mk-1)",
            dryMass: 1.5,
            cost: 7.5,
            buildTime: 45,
            reliability: 0.95,
            isp: 310.0,
            thrust: 850.0
        );

        public static readonly DesignComponent EngineKeroseneHeavy = new DesignComponent(
            id: "engine-kerosene-lox-heavy",
            category: "engine",
            name: "Ağır Kerosen Motoru",
            dryMass: 2.8,
            cost: 14.0,
            buildTime: 60,
            reliability: 0.94,
            isp: 320.0,
            thrust: 1900.0
        );

        public static readonly DesignComponent EngineHydroloxUpper = new DesignComponent(
            id: "engine-hydrolox-upper-mk1",
            category: "engine",
            name: "Hidrojen Üst Aşama Motoru",
            dryMass: 0.7,
            cost: 11.0,
            buildTime: 55,
            reliability: 0.93,
            isp: 445.0,
            thrust: 140.0
        );

        public static readonly DesignComponent TankKeroseneHeavy = new DesignComponent(
            id: "tank-kerosene-heavy",
            category: "tank",
            name: "Ağır Kerosen Tankı (140t)",
            dryMass: 4.0,
            cost: 3.5,
            buildTime: 30,
            reliability: 0.99
        );

        public static readonly DesignComponent TankKeroseneMedium = new DesignComponent(
            id: "tank-kerosene-medium",
            category: "tank",
            name: "Orta Kerosen Tankı (50t)",
            dryMass: 2.0,
            cost: 2.0,
            buildTime: 20,
            reliability: 0.99
        );

        public static readonly DesignComponent TankHydroloxUpper = new DesignComponent(
            id: "tank-hydrolox-upper",
            category: "tank",
            name: "Hidrojen Üst Tankı (35t)",
            dryMass: 1.2,
            cost: 2.8,
            buildTime: 28,
            reliability: 0.98
        );

        public static readonly DesignComponent BusRoverLight = new DesignComponent(
            id: "bus-rover-light",
            category: "bus",
            name: "Hafif Gezgin Şasisi",
            dryMass: 0.35,
            cost: 5.5,
            buildTime: 35,
            reliability: 0.96,
            powerConsumption: 0.25,
            thermalLoad: 0.15
        );

        public static readonly DesignComponent BusRoverHeavy = new DesignComponent(
            id: "bus-rover-heavy",
            category: "bus",
            name: "Ağır Görev Gezgin Şasisi",
            dryMass: 0.85,
            cost: 10.5,
            buildTime: 50,
            reliability: 0.98,
            powerConsumption: 0.55,
            thermalLoad: 0.30
        );

        public static readonly DesignComponent PowerSolarBasic = new DesignComponent(
            id: "power-solar-array-basic",
            category: "power",
            name: "Katlanabilir Güneş Paneli",
            dryMass: 0.12,
            cost: 2.2,
            buildTime: 15,
            reliability: 0.94,
            powerGeneration: 1.1
        );

        public static readonly DesignComponent PowerSolarAdvanced = new DesignComponent(
            id: "power-solar-array-advanced",
            category: "power",
            name: "Gelişmiş Güneş Paneli",
            dryMass: 0.18,
            cost: 4.5,
            buildTime: 25,
            reliability: 0.97,
            powerGeneration: 2.4
        );

        public static readonly DesignComponent ThermalMli = new DesignComponent(
            id: "thermal-insulation-mli",
            category: "thermal",
            name: "MLI Çok Katmanlı İzolasyon",
            dryMass: 0.06,
            cost: 1.0,
            buildTime: 10,
            reliability: 0.99,
            thermalDissipation: 0.6
        );

        public static readonly DesignComponent CommsOmni = new DesignComponent(
            id: "comms-omni-antenna",
            category: "comms",
            name: "Düşük Kazançlı Omni Anten",
            dryMass: 0.03,
            cost: 0.9,
            buildTime: 12,
            reliability: 0.99,
            downlinkRate: 1.5,
            powerConsumption: 0.08
        );

        public static readonly DesignComponent AdcsReactionWheels = new DesignComponent(
            id: "adcs-reaction-wheels",
            category: "adcs",
            name: "Tepki Tekerlekleri & Yıldız İzler",
            dryMass: 0.06,
            cost: 1.8,
            buildTime: 18,
            reliability: 0.97,
            powerConsumption: 0.12
        );

        public static readonly DesignComponent EdlLunarSkycrane = new DesignComponent(
            id: "edl-lunar-skycrane",
            category: "edl",
            name: "Ay Gökyüzü Vinci (Skycrane)",
            dryMass: 0.65,
            cost: 8.5,
            buildTime: 45,
            reliability: 0.93,
            powerConsumption: 0.15
        );

        public static readonly DesignComponent EdlAirbag = new DesignComponent(
            id: "edl-airbag-cushion",
            category: "edl",
            name: "Darbe Sönümleyici Hava Yastığı",
            dryMass: 0.30,
            cost: 3.8,
            buildTime: 22,
            reliability: 0.88
        );

        public static readonly DesignComponent InstPanoramicCamera = new DesignComponent(
            id: "inst-panoramic-camera",
            category: "instrument",
            name: "Panoramik Kamera Paketi",
            dryMass: 0.04,
            cost: 2.8,
            buildTime: 25,
            reliability: 0.97,
            dataRate: 3.2,
            powerConsumption: 0.06
        );

        public static readonly DesignComponent InstAlphaSpectrometer = new DesignComponent(
            id: "inst-alpha-spectrometer",
            category: "instrument",
            name: "Alfa X-Işını Spektrometresi",
            dryMass: 0.07,
            cost: 4.8,
            buildTime: 35,
            reliability: 0.95,
            dataRate: 1.4,
            powerConsumption: 0.11
        );

        public static IReadOnlyList<DesignComponent> Modules { get; } = new[]
        {
            EngineKeroseneMk1,
            EngineKeroseneHeavy,
            EngineHydroloxUpper,
            TankKeroseneHeavy,
            TankKeroseneMedium,
            TankHydroloxUpper,
            BusRoverLight,
            BusRoverHeavy,
            PowerSolarBasic,
            PowerSolarAdvanced,
            ThermalMli,
            CommsOmni,
            AdcsReactionWheels,
            EdlLunarSkycrane,
            EdlAirbag,
            InstPanoramicCamera,
            InstAlphaSpectrometer
        };

        // ── Mission Profile Factory ─────────────────────────────────────────────────

        /// <summary>
        /// Creates the standard Moon Rover mission profile used for Design Review verification.
        /// </summary>
        public static MissionProfile CreateMissionProfile(double requiredDeltaV = TargetDeltaV)
        {
            return new MissionProfile(
                id: "mission-lunar-rover-standard",
                name: "Ay Yüzey Gezgini Görevi",
                targetDestinationId: DestinationId,
                targetMissionTypeId: MissionTypeId,
                requiredDeltaV: requiredDeltaV,
                minReliabilityThreshold: MinReliability,
                requiredLaunchPadLevel: 2,
                minDownlinkRate: 1.0,
                minDataStorage: 2.0,
                requiresEdl: true,
                requiresLifeSupport: false,
                requiredModuleCategories: new[] { "bus", "power", "edl" }
            );
        }

        /// <summary>
        /// Creates a standard validation context for evaluating lunar rover designs.
        /// </summary>
        public static ValidationContext CreateValidationContext(double requiredDeltaV = TargetDeltaV)
        {
            return new ValidationContext(
                missionProfile: CreateMissionProfile(requiredDeltaV),
                launchPad: LaunchPadInfo.Level2Medium
            );
        }

        // ── Blueprint Factories ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a complete, compliant Lunar Rover blueprint that passes all 8 Design Review validators.
        /// </summary>
        public static Blueprint CreatePassingBlueprint(MaterialSpec material = null, SupplierSpec supplier = null)
        {
            var mat = material ?? AluminumAlloy;
            var sup = supplier ?? HelionSupplier;

            var payload = new PayloadDesign(
                id: "payload-lunar-rover-mk1",
                name: "Ay Gezgini Faydalı Yükü",
                bus: BusRoverLight,
                power: PowerSolarBasic,
                thermal: ThermalMli,
                comms: CommsOmni,
                adcs: AdcsReactionWheels,
                edl: EdlLunarSkycrane,
                instruments: new[] { InstPanoramicCamera }
            );

            var stage1 = new StageDesign(
                id: "stage-1-booster",
                name: "Aşama 1 (Kalkış Güçlendirici)",
                engine: EngineKeroseneHeavy.WithSupplier(sup),
                tank: TankKeroseneHeavy.WithSupplier(sup).WithMaterial(mat),
                propellantMass: 140.0,
                structureMaterial: mat,
                stageSupplier: sup
            );

            var stage2 = new StageDesign(
                id: "stage-2-transfer",
                name: "Aşama 2 (Ay Transfer)",
                engine: EngineHydroloxUpper.WithSupplier(sup),
                tank: TankHydroloxUpper.WithSupplier(sup).WithMaterial(mat),
                propellantMass: 35.0,
                structureMaterial: mat,
                stageSupplier: sup
            );

            return new Blueprint(
                id: "blueprint-lunar-rover-passing",
                name: "Ay Gezgini (Rover Mk-1)",
                version: 1,
                targetDestinationId: DestinationId,
                targetMissionTypeId: MissionTypeId,
                payload: payload,
                stages: new[] { stage1, stage2 }
            );
        }

        /// <summary>
        /// Creates a Lunar Rover blueprint lacking the mandatory EDL module (fails Review with NO-GO).
        /// Equipping EDL converts it to a passing design.
        /// </summary>
        public static Blueprint CreateMissingEdlBlueprint(MaterialSpec material = null, SupplierSpec supplier = null)
        {
            var mat = material ?? AluminumAlloy;
            var sup = supplier ?? HelionSupplier;

            var payload = new PayloadDesign(
                id: "payload-lunar-rover-no-edl",
                name: "Ay Gezgini (EDL Eksik)",
                bus: BusRoverLight,
                power: PowerSolarBasic,
                thermal: ThermalMli,
                comms: CommsOmni,
                adcs: AdcsReactionWheels,
                edl: null, // Intentionally missing EDL
                instruments: new[] { InstPanoramicCamera }
            );

            var stage1 = new StageDesign(
                id: "stage-1-booster",
                name: "Aşama 1",
                engine: EngineKeroseneHeavy.WithSupplier(sup),
                tank: TankKeroseneHeavy.WithSupplier(sup).WithMaterial(mat),
                propellantMass: 140.0,
                structureMaterial: mat,
                stageSupplier: sup
            );

            var stage2 = new StageDesign(
                id: "stage-2-transfer",
                name: "Aşama 2",
                engine: EngineHydroloxUpper.WithSupplier(sup),
                tank: TankHydroloxUpper.WithSupplier(sup).WithMaterial(mat),
                propellantMass: 35.0,
                structureMaterial: mat,
                stageSupplier: sup
            );

            return new Blueprint(
                id: "blueprint-lunar-rover-no-edl",
                name: "Ay Gezgini (EDL Eksik)",
                version: 1,
                targetDestinationId: DestinationId,
                targetMissionTypeId: MissionTypeId,
                payload: payload,
                stages: new[] { stage1, stage2 }
            );
        }

        /// <summary>
        /// Creates a Lunar Rover blueprint with insufficient propellant / Delta-V (fails Review with Delta-V deficit).
        /// </summary>
        public static Blueprint CreateLowDeltaVBlueprint(MaterialSpec material = null, SupplierSpec supplier = null)
        {
            var mat = material ?? AluminumAlloy;
            var sup = supplier ?? HelionSupplier;

            var payload = new PayloadDesign(
                id: "payload-lunar-rover-low-dv",
                name: "Ay Gezgini",
                bus: BusRoverLight,
                power: PowerSolarBasic,
                thermal: ThermalMli,
                comms: CommsOmni,
                adcs: AdcsReactionWheels,
                edl: EdlLunarSkycrane,
                instruments: new[] { InstPanoramicCamera }
            );

            // Single undersized stage with only 30t propellant -> Delta-V ~5000 m/s << 13500 m/s
            var stage1 = new StageDesign(
                id: "stage-1-undersized",
                name: "Aşama 1 (Yetersiz)",
                engine: EngineKeroseneMk1.WithSupplier(sup),
                tank: TankKeroseneMedium.WithSupplier(sup).WithMaterial(mat),
                propellantMass: 30.0,
                structureMaterial: mat,
                stageSupplier: sup
            );

            return new Blueprint(
                id: "blueprint-lunar-rover-low-dv",
                name: "Ay Gezgini (Yetersiz Menzil)",
                version: 1,
                targetDestinationId: DestinationId,
                targetMissionTypeId: MissionTypeId,
                payload: payload,
                stages: new[] { stage1 }
            );
        }
    }
}
