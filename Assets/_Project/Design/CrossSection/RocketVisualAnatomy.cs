using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Design
{
    /// <summary>
    /// Represents an individual geometric section or subsystem zone on the vehicle in the Hangar (GDD §7.2, §15.1).
    /// Pure C# model providing normalized proportions, dimensions in meters, slot mapping, and visual profile.
    /// </summary>
    public sealed class RocketAnatomySection
    {
        public string SectionId { get; }
        public VehicleSlotId? SlotId { get; }
        public CrossSectionPartType PartType { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public int StageIndex { get; } // -1 = Payload, 0 = Stage 1, 1 = Stage 2, 2 = Stage 3
        public DesignComponent Component { get; }
        public SupplierSpec Supplier { get; }
        public MaterialSpec Material { get; }
        public bool IsEmptySlot { get; }
        public bool IsRequired { get; }
        public bool IsInternalPayloadSubsystem { get; }

        /// <summary>
        /// Normalized vertical offset from bottom of stack (0.0 = engine base, 1.0 = rocket apex).
        /// </summary>
        public double NormalizedYBottom { get; }
        public double NormalizedHeight { get; }
        public double NormalizedYTop => NormalizedYBottom + NormalizedHeight;

        /// <summary>
        /// Normalized width / diameter of this section (0.0 to 1.0 relative to max booster diameter).
        /// </summary>
        public double NormalizedDiameter { get; }

        /// <summary>
        /// Physical estimated metrics in meters for realistic ruler display.
        /// </summary>
        public double HeightMeters { get; }
        public double DiameterMeters { get; }
        public double AltitudeFromGroundMeters { get; }

        public string SupplierColorHex { get; }
        public PartVisualProfile VisualProfile { get; }

        public RocketAnatomySection(
            string sectionId,
            VehicleSlotId? slotId,
            CrossSectionPartType partType,
            string title,
            string subtitle,
            int stageIndex,
            DesignComponent component,
            SupplierSpec supplier,
            MaterialSpec material,
            bool isEmptySlot,
            bool isRequired,
            bool isInternalPayloadSubsystem,
            double normalizedYBottom,
            double normalizedHeight,
            double normalizedDiameter,
            double heightMeters,
            double diameterMeters,
            double altitudeFromGroundMeters,
            string supplierColorHex,
            PartVisualProfile visualProfile)
        {
            SectionId = sectionId ?? throw new ArgumentNullException(nameof(sectionId));
            SlotId = slotId;
            PartType = partType;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            StageIndex = stageIndex;
            Component = component;
            Supplier = supplier;
            Material = material;
            IsEmptySlot = isEmptySlot;
            IsRequired = isRequired;
            IsInternalPayloadSubsystem = isInternalPayloadSubsystem;
            NormalizedYBottom = normalizedYBottom;
            NormalizedHeight = Math.Max(0.01, normalizedHeight);
            NormalizedDiameter = Math.Clamp(normalizedDiameter, 0.15, 1.0);
            HeightMeters = heightMeters;
            DiameterMeters = diameterMeters;
            AltitudeFromGroundMeters = altitudeFromGroundMeters;
            SupplierColorHex = supplierColorHex ?? CraftCrossSectionLayout.ColorDefault;
            VisualProfile = visualProfile ?? PartVisualProfile.FromComponent(component);
        }
    }

    /// <summary>
    /// Computes the complete hierarchical and dimensional anatomy of a rocket from a Blueprint (GDD §7.2, §14.3, §15.1).
    /// Pure C# (noEngineReferences=true) so it can be verified via unit tests and used across all presentation layers.
    /// </summary>
    public sealed class RocketVisualAnatomy
    {
        public IReadOnlyList<RocketAnatomySection> Sections { get; }
        public IReadOnlyList<RocketAnatomySection> PayloadSubsystems { get; }
        public double TotalHeightMeters { get; }
        public double MaxDiameterMeters { get; }
        public int StageCount { get; }
        public bool HasPayload { get; }
        public bool IsCrewedMission { get; }

        public RocketVisualAnatomy(
            IReadOnlyList<RocketAnatomySection> sections,
            IReadOnlyList<RocketAnatomySection> payloadSubsystems,
            double totalHeightMeters,
            double maxDiameterMeters,
            int stageCount,
            bool hasPayload,
            bool isCrewedMission)
        {
            Sections = sections ?? Array.Empty<RocketAnatomySection>();
            PayloadSubsystems = payloadSubsystems ?? Array.Empty<RocketAnatomySection>();
            TotalHeightMeters = Math.Max(1.0, totalHeightMeters);
            MaxDiameterMeters = Math.Max(0.5, maxDiameterMeters);
            StageCount = stageCount;
            HasPayload = hasPayload;
            IsCrewedMission = isCrewedMission;
        }

        /// <summary>
        /// Builds the visual anatomy for the given vehicle blueprint.
        /// Dynamically scales section heights and diameters from physical parameters (PropellantMass, Thrust, DryMass).
        /// </summary>
        public static RocketVisualAnatomy Build(Blueprint blueprint, bool forceCrewed = false)
        {
            if (blueprint == null)
            {
                return new RocketVisualAnatomy(Array.Empty<RocketAnatomySection>(), Array.Empty<RocketAnatomySection>(), 0, 0, 0, false, false);
            }

            var payload = blueprint.Payload;
            bool hasPayload = payload != null;

            // Check if mission is crewed:
            bool isCrewed = forceCrewed ||
                            (payload != null && (payload.Cabin != null || payload.LifeSupport != null || payload.AbortSystem != null));

            var stages = blueprint.Stages ?? Array.Empty<StageDesign>();
            int stageCount = stages.Count;

            // 1. Calculate physical metrics (meters) for stages
            // Standard booster diameter: Stage 1 = 3.8m, Stage 2 = 3.0m, Stage 3 = 2.4m, Fairing = 2.8m
            double boosterDia = 3.8;
            double stage2Dia = stageCount >= 2 ? 3.0 : boosterDia;
            double stage3Dia = stageCount >= 3 ? 2.4 : stage2Dia;
            double fairingDia = 2.8;

            // Base heights in meters derived from propellant mass and thrust
            // Stage 1
            double s1TankHeight = 12.0;
            double s1EngineHeight = 3.2;
            if (stageCount >= 1 && stages[0] != null)
            {
                var s1 = stages[0];
                double prop = s1.PropellantMass > 0 ? s1.PropellantMass : 100.0;
                s1TankHeight = Math.Clamp(prop * 0.11, 8.0, 24.0);
                double thrust = s1.Engine?.Thrust ?? 1200.0;
                s1EngineHeight = Math.Clamp(thrust * 0.0022, 2.5, 5.0);
            }

            // Stage 2
            double s2TankHeight = 0.0;
            double s2EngineHeight = 0.0;
            double interstage12Height = 1.2;
            if (stageCount >= 2 && stages[1] != null)
            {
                var s2 = stages[1];
                double prop = s2.PropellantMass > 0 ? s2.PropellantMass : 30.0;
                s2TankHeight = Math.Clamp(prop * 0.16, 4.0, 14.0);
                double thrust = s2.Engine?.Thrust ?? 200.0;
                s2EngineHeight = Math.Clamp(thrust * 0.004, 1.8, 3.5);
            }

            // Stage 3
            double s3TankHeight = 0.0;
            double s3EngineHeight = 0.0;
            double interstage23Height = stageCount >= 3 ? 1.0 : 0.0;
            if (stageCount >= 3 && stages[2] != null)
            {
                var s3 = stages[2];
                double prop = s3.PropellantMass > 0 ? s3.PropellantMass : 15.0;
                s3TankHeight = Math.Clamp(prop * 0.18, 3.0, 8.0);
                double thrust = s3.Engine?.Thrust ?? 100.0;
                s3EngineHeight = Math.Clamp(thrust * 0.005, 1.5, 3.0);
            }

            // Payload / Fairing bay height:
            double fairingConeHeight = 4.0;
            double fairingCylinderHeight = 4.5;
            if (isCrewed)
            {
                fairingCylinderHeight += 2.5; // space for cabin & life support
            }
            double abortTowerHeight = isCrewed ? 4.5 : 0.0;

            double finsHeight = 3.0;

            // Total height in meters
            double totalMeters = finsHeight * 0.4 + s1EngineHeight + s1TankHeight;
            if (stageCount >= 2) totalMeters += interstage12Height + s2EngineHeight + s2TankHeight;
            if (stageCount >= 3) totalMeters += interstage23Height + s3EngineHeight + s3TankHeight;
            totalMeters += fairingCylinderHeight + fairingConeHeight + abortTowerHeight;

            var sections = new List<RocketAnatomySection>();
            var internalSubsystems = new List<RocketAnatomySection>();

            double currentAlt = 0.0;

            // ── FINS & ENGINE BASE (Bottom) ──────────────────────────────────────────
            // Fins
            sections.Add(new RocketAnatomySection(
                sectionId: "section-aero-fins",
                slotId: null,
                partType: CrossSectionPartType.AeroFins,
                title: "Aerodinamik Kanatçıklar (Fins)",
                subtitle: "Atmosferik Denge ve Yön Kontrolü",
                stageIndex: 0,
                component: null,
                supplier: null,
                material: null,
                isEmptySlot: false,
                isRequired: false,
                isInternalPayloadSubsystem: false,
                normalizedYBottom: currentAlt / totalMeters,
                normalizedHeight: finsHeight / totalMeters,
                normalizedDiameter: 1.0,
                heightMeters: finsHeight,
                diameterMeters: boosterDia * 1.6,
                altitudeFromGroundMeters: currentAlt,
                supplierColorHex: CraftCrossSectionLayout.ColorDefault,
                visualProfile: new PartVisualProfile("fins", "fins")
            ));

            // Stage 1 Engine
            var s1EngineComp = stageCount >= 1 ? stages[0].Engine : null;
            var s1EngineSup = s1EngineComp?.Supplier;
            sections.Add(new RocketAnatomySection(
                sectionId: "section-stage1-engine",
                slotId: VehicleSlotId.Stage1Engine,
                partType: CrossSectionPartType.StageEngine,
                title: s1EngineComp != null ? s1EngineComp.Name : "Aşama 1 Motor Yuvası",
                subtitle: s1EngineComp != null ? $"{s1EngineComp.Thrust:F0} kN • Isp {s1EngineComp.Isp:F0}s" : "Boş Motor Yuvası",
                stageIndex: 0,
                component: s1EngineComp,
                supplier: s1EngineSup,
                material: s1EngineComp?.Material,
                isEmptySlot: s1EngineComp == null,
                isRequired: true,
                isInternalPayloadSubsystem: false,
                normalizedYBottom: currentAlt / totalMeters,
                normalizedHeight: s1EngineHeight / totalMeters,
                normalizedDiameter: boosterDia / boosterDia,
                heightMeters: s1EngineHeight,
                diameterMeters: boosterDia,
                altitudeFromGroundMeters: currentAlt,
                supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s1EngineSup?.Id),
                visualProfile: PartVisualProfile.FromComponent(s1EngineComp)
            ));
            currentAlt += s1EngineHeight;

            // Stage 1 Tank
            var s1TankComp = stageCount >= 1 ? stages[0].Tank : null;
            var s1TankSup = s1TankComp?.Supplier;
            sections.Add(new RocketAnatomySection(
                sectionId: "section-stage1-tank",
                slotId: VehicleSlotId.Stage1Tank,
                partType: CrossSectionPartType.StageTank,
                title: s1TankComp != null ? s1TankComp.Name : "Aşama 1 Yakıt Tankı Yuvası",
                subtitle: s1TankComp != null ? $"{stages[0].PropellantMass:F0}t Yakıt • {s1TankComp.EffectiveDryMass:F1}t Boş" : "Boş Tank Yuvası",
                stageIndex: 0,
                component: s1TankComp,
                supplier: s1TankSup,
                material: s1TankComp?.Material,
                isEmptySlot: s1TankComp == null,
                isRequired: true,
                isInternalPayloadSubsystem: false,
                normalizedYBottom: currentAlt / totalMeters,
                normalizedHeight: s1TankHeight / totalMeters,
                normalizedDiameter: boosterDia / boosterDia,
                heightMeters: s1TankHeight,
                diameterMeters: boosterDia,
                altitudeFromGroundMeters: currentAlt,
                supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s1TankSup?.Id),
                visualProfile: PartVisualProfile.FromComponent(s1TankComp, fillPercentage: 1.0)
            ));
            currentAlt += s1TankHeight;

            // ── STAGE 2 (if present) ────────────────────────────────────────────────
            if (stageCount >= 2)
            {
                // Interstage 1-2 Decoupler
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-interstage-1-2",
                    slotId: null,
                    partType: CrossSectionPartType.InterstageDecoupler,
                    title: "Aşama Dekuplörü 1/2",
                    subtitle: "Pnömatik Ayrılma Halkası",
                    stageIndex: 0,
                    component: null,
                    supplier: null,
                    material: null,
                    isEmptySlot: false,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: interstage12Height / totalMeters,
                    normalizedDiameter: (stage2Dia + boosterDia) * 0.5 / boosterDia,
                    heightMeters: interstage12Height,
                    diameterMeters: (stage2Dia + boosterDia) * 0.5,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ColorDefault,
                    visualProfile: new PartVisualProfile("decoupler-1-2", "structure")
                ));
                currentAlt += interstage12Height;

                // Stage 2 Engine
                var s2EngineComp = stages[1].Engine;
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-stage2-engine",
                    slotId: VehicleSlotId.Stage2Engine,
                    partType: CrossSectionPartType.StageEngine,
                    title: s2EngineComp != null ? s2EngineComp.Name : "Aşama 2 Motor Yuvası",
                    subtitle: s2EngineComp != null ? $"{s2EngineComp.Thrust:F0} kN • Isp {s2EngineComp.Isp:F0}s" : "İsteğe Bağlı 2. Aşama Motoru",
                    stageIndex: 1,
                    component: s2EngineComp,
                    supplier: s2EngineComp?.Supplier,
                    material: s2EngineComp?.Material,
                    isEmptySlot: s2EngineComp == null,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: s2EngineHeight / totalMeters,
                    normalizedDiameter: stage2Dia / boosterDia,
                    heightMeters: s2EngineHeight,
                    diameterMeters: stage2Dia,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s2EngineComp?.Supplier?.Id),
                    visualProfile: PartVisualProfile.FromComponent(s2EngineComp)
                ));
                currentAlt += s2EngineHeight;

                // Stage 2 Tank
                var s2TankComp = stages[1].Tank;
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-stage2-tank",
                    slotId: VehicleSlotId.Stage2Tank,
                    partType: CrossSectionPartType.StageTank,
                    title: s2TankComp != null ? s2TankComp.Name : "Aşama 2 Yakıt Tankı",
                    subtitle: s2TankComp != null ? $"{stages[1].PropellantMass:F0}t Yakıt • {s2TankComp.EffectiveDryMass:F1}t Boş" : "İsteğe Bağlı 2. Aşama Tankı",
                    stageIndex: 1,
                    component: s2TankComp,
                    supplier: s2TankComp?.Supplier,
                    material: s2TankComp?.Material,
                    isEmptySlot: s2TankComp == null,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: s2TankHeight / totalMeters,
                    normalizedDiameter: stage2Dia / boosterDia,
                    heightMeters: s2TankHeight,
                    diameterMeters: stage2Dia,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s2TankComp?.Supplier?.Id),
                    visualProfile: PartVisualProfile.FromComponent(s2TankComp, fillPercentage: 1.0)
                ));
                currentAlt += s2TankHeight;
            }

            // ── STAGE 3 (if present) ────────────────────────────────────────────────
            if (stageCount >= 3)
            {
                // Interstage 2-3 Decoupler
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-interstage-2-3",
                    slotId: null,
                    partType: CrossSectionPartType.InterstageDecoupler,
                    title: "Aşama Dekuplörü 2/3",
                    subtitle: "Ayrılma Sistemi",
                    stageIndex: 1,
                    component: null,
                    supplier: null,
                    material: null,
                    isEmptySlot: false,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: interstage23Height / totalMeters,
                    normalizedDiameter: (stage3Dia + stage2Dia) * 0.5 / boosterDia,
                    heightMeters: interstage23Height,
                    diameterMeters: (stage3Dia + stage2Dia) * 0.5,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ColorDefault,
                    visualProfile: new PartVisualProfile("decoupler-2-3", "structure")
                ));
                currentAlt += interstage23Height;

                // Stage 3 Engine
                var s3EngineComp = stages[2].Engine;
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-stage3-engine",
                    slotId: VehicleSlotId.Stage3Engine,
                    partType: CrossSectionPartType.StageEngine,
                    title: s3EngineComp != null ? s3EngineComp.Name : "Aşama 3 Motor Yuvası",
                    subtitle: s3EngineComp != null ? $"{s3EngineComp.Thrust:F0} kN • Isp {s3EngineComp.Isp:F0}s" : "İsteğe Bağlı 3. Aşama Motoru",
                    stageIndex: 2,
                    component: s3EngineComp,
                    supplier: s3EngineComp?.Supplier,
                    material: s3EngineComp?.Material,
                    isEmptySlot: s3EngineComp == null,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: s3EngineHeight / totalMeters,
                    normalizedDiameter: stage3Dia / boosterDia,
                    heightMeters: s3EngineHeight,
                    diameterMeters: stage3Dia,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s3EngineComp?.Supplier?.Id),
                    visualProfile: PartVisualProfile.FromComponent(s3EngineComp)
                ));
                currentAlt += s3EngineHeight;

                // Stage 3 Tank
                var s3TankComp = stages[2].Tank;
                sections.Add(new RocketAnatomySection(
                    sectionId: "section-stage3-tank",
                    slotId: VehicleSlotId.Stage3Tank,
                    partType: CrossSectionPartType.StageTank,
                    title: s3TankComp != null ? s3TankComp.Name : "Aşama 3 Yakıt Tankı",
                    subtitle: s3TankComp != null ? $"{stages[2].PropellantMass:F0}t Yakıt • {s3TankComp.EffectiveDryMass:F1}t Boş" : "İsteğe Bağlı 3. Aşama Tankı",
                    stageIndex: 2,
                    component: s3TankComp,
                    supplier: s3TankComp?.Supplier,
                    material: s3TankComp?.Material,
                    isEmptySlot: s3TankComp == null,
                    isRequired: false,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: s3TankHeight / totalMeters,
                    normalizedDiameter: stage3Dia / boosterDia,
                    heightMeters: s3TankHeight,
                    diameterMeters: stage3Dia,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(s3TankComp?.Supplier?.Id),
                    visualProfile: PartVisualProfile.FromComponent(s3TankComp, fillPercentage: 1.0)
                ));
                currentAlt += s3TankHeight;
            }

            // ── PAYLOAD FAIRING & INTERNAL CAVITY (Top) ─────────────────────────────
            double payloadBaseAlt = currentAlt;
            double fairingTotalHeight = fairingCylinderHeight + fairingConeHeight;

            // Outer Fairing Shell section (Exterior view)
            var fairingAdapter = blueprint.FairingAdapter;
            sections.Add(new RocketAnatomySection(
                sectionId: "section-fairing-shell",
                slotId: null,
                partType: CrossSectionPartType.FairingNose,
                title: "Aerodinamik Kaporta (Fairing Shell)",
                subtitle: fairingAdapter != null ? fairingAdapter.Name : "Kompozit Faydalı Yük Koruyucu",
                stageIndex: -1,
                component: fairingAdapter,
                supplier: fairingAdapter?.Supplier,
                material: fairingAdapter?.Material,
                isEmptySlot: false,
                isRequired: false,
                isInternalPayloadSubsystem: false,
                normalizedYBottom: payloadBaseAlt / totalMeters,
                normalizedHeight: fairingTotalHeight / totalMeters,
                normalizedDiameter: fairingDia / boosterDia,
                heightMeters: fairingTotalHeight,
                diameterMeters: fairingDia,
                altitudeFromGroundMeters: payloadBaseAlt,
                supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(fairingAdapter?.Supplier?.Id),
                visualProfile: new PartVisualProfile("fairing-shell", "fairing")
            ));

            // Internal Payload Subsystems (Cutaway / Subsystems view)
            // They live inside the cavity of the fairing [payloadBaseAlt .. payloadBaseAlt + fairingCylinderHeight]
            // We order them functionally: Bus (chassis) at bottom, then EDL, Thermal, Power, Comms, ADCS, Instruments, Cabin/LifeSupport at top
            var payloadSlots = new List<(VehicleSlotId slotId, CrossSectionPartType pType, string defaultTitle, bool isRequired, bool crewOnly)>
            {
                (VehicleSlotId.PayloadBus, CrossSectionPartType.PayloadBus, "Gövde Yapısı (Bus)", true, false),
                (VehicleSlotId.PayloadEdl, CrossSectionPartType.PayloadEdl, "İniş Sistemi (EDL)", false, false),
                (VehicleSlotId.PayloadThermal, CrossSectionPartType.PayloadThermal, "Isıl Kontrol (Thermal)", true, false),
                (VehicleSlotId.PayloadPower, CrossSectionPartType.PayloadPower, "Güç Sistemi (Power)", true, false),
                (VehicleSlotId.PayloadComms, CrossSectionPartType.PayloadComms, "Haberleşme (Comms)", true, false),
                (VehicleSlotId.PayloadAdcs, CrossSectionPartType.PayloadAdcs, "Yönelim Kontrol (ADCS)", true, false),
                (VehicleSlotId.PayloadInstrument1, CrossSectionPartType.PayloadInstrument, "Bilimsel Alet 1", false, false),
                (VehicleSlotId.PayloadInstrument2, CrossSectionPartType.PayloadInstrument, "Bilimsel Alet 2", false, false),
                (VehicleSlotId.PayloadCabin, CrossSectionPartType.PayloadCabin, "Mürettebat Kabini", false, true),
                (VehicleSlotId.PayloadLifeSupport, CrossSectionPartType.PayloadLifeSupport, "Yaşam Destek (ECLSS)", false, true),
            };

            var activePayloadSlots = payloadSlots.Where(p => !p.crewOnly || isCrewed).ToList();
            double subHeightMeters = fairingCylinderHeight / Math.Max(1, activePayloadSlots.Count);

            for (int i = 0; i < activePayloadSlots.Count; i++)
            {
                var entry = activePayloadSlots[i];
                var comp = blueprint.GetComponentInSlot(entry.slotId);
                double subAlt = payloadBaseAlt + (i * subHeightMeters);

                var subSection = new RocketAnatomySection(
                    sectionId: $"section-payload-{entry.slotId.ToString().ToLowerInvariant()}",
                    slotId: entry.slotId,
                    partType: entry.pType,
                    title: comp != null ? comp.Name : entry.defaultTitle,
                    subtitle: comp != null ? $"{comp.EffectiveDryMass:F2}t • {comp.Category}" : "Yuva Boş",
                    stageIndex: -1,
                    component: comp,
                    supplier: comp?.Supplier,
                    material: comp?.Material,
                    isEmptySlot: comp == null,
                    isRequired: entry.isRequired,
                    isInternalPayloadSubsystem: true,
                    normalizedYBottom: subAlt / totalMeters,
                    normalizedHeight: subHeightMeters / totalMeters,
                    normalizedDiameter: (fairingDia * 0.78) / boosterDia,
                    heightMeters: subHeightMeters,
                    diameterMeters: fairingDia * 0.78,
                    altitudeFromGroundMeters: subAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(comp?.Supplier?.Id),
                    visualProfile: PartVisualProfile.FromComponent(comp)
                );

                sections.Add(subSection);
                internalSubsystems.Add(subSection);
            }

            currentAlt += fairingTotalHeight;

            // Abort System Tower (at the apex, if crewed)
            if (isCrewed)
            {
                var abortComp = blueprint.GetComponentInSlot(VehicleSlotId.PayloadAbortSystem);
                var abortSection = new RocketAnatomySection(
                    sectionId: "section-payload-abortsystem",
                    slotId: VehicleSlotId.PayloadAbortSystem,
                    partType: CrossSectionPartType.PayloadAbortSystem,
                    title: abortComp != null ? abortComp.Name : "Fırlatma Kaçış Kulesi (Abort Tower)",
                    subtitle: abortComp != null ? $"{abortComp.EffectiveDryMass:F1}t Acil Kaçış Motoru" : "İnsanlı Güvenlik Kulesi",
                    stageIndex: -1,
                    component: abortComp,
                    supplier: abortComp?.Supplier,
                    material: abortComp?.Material,
                    isEmptySlot: abortComp == null,
                    isRequired: true,
                    isInternalPayloadSubsystem: false,
                    normalizedYBottom: currentAlt / totalMeters,
                    normalizedHeight: abortTowerHeight / totalMeters,
                    normalizedDiameter: 0.22,
                    heightMeters: abortTowerHeight,
                    diameterMeters: 0.8,
                    altitudeFromGroundMeters: currentAlt,
                    supplierColorHex: CraftCrossSectionLayout.ResolveSupplierColor(abortComp?.Supplier?.Id),
                    visualProfile: new PartVisualProfile("abort-tower", "abort-system", hasEscapeTower: true)
                );
                sections.Add(abortSection);
                internalSubsystems.Add(abortSection);
                currentAlt += abortTowerHeight;
            }

            return new RocketVisualAnatomy(
                sections: sections,
                payloadSubsystems: internalSubsystems,
                totalHeightMeters: totalMeters,
                maxDiameterMeters: boosterDia,
                stageCount: stageCount,
                hasPayload: hasPayload,
                isCrewedMission: isCrewed
            );
        }

        public RocketAnatomySection FindSectionBySlot(VehicleSlotId slotId)
        {
            return Sections.FirstOrDefault(s => s.SlotId == slotId);
        }
    }
}
