using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Computes the modular 2D vertical cross-section assembly layout for a vehicle blueprint (GDD §7.2, §14.3, §15.1).
    /// Auto-snaps stages (up to 3 stages) and spacecraft payload modules with CAD alignment and supplier striping.
    /// Acceptance criterion: 3-stage rocket + payload automatically assembled and snapped.
    /// </summary>
    public sealed class CraftCrossSectionLayout
    {
        public const string ColorHelion = "#f59e0b";
        public const string ColorKestrel = "#3b82f6";
        public const string ColorNordvik = "#06b6d4";
        public const string ColorTarsus = "#10b981";
        public const string ColorMeridian = "#8b5cf6";
        public const string ColorAurelia = "#eab308";
        public const string ColorDefault = "#38bdf8";
        public const string ColorEmptySlot = "#475569";

        public IReadOnlyList<CrossSectionNode> Nodes { get; }
        public int StageCount { get; }
        public bool HasPayload { get; }

        public CraftCrossSectionLayout(IReadOnlyList<CrossSectionNode> nodes, int stageCount, bool hasPayload)
        {
            Nodes = nodes ?? Array.Empty<CrossSectionNode>();
            StageCount = stageCount;
            HasPayload = hasPayload;
        }

        /// <summary>
        /// Generates the complete vertical cross-section layout from the given blueprint.
        /// Stacks payload at the top and rocket stages below in top-down sequence.
        /// </summary>
        public static CraftCrossSectionLayout Build(Blueprint blueprint)
        {
            if (blueprint == null)
            {
                return new CraftCrossSectionLayout(Array.Empty<CrossSectionNode>(), 0, false);
            }

            var rawNodes = new List<CrossSectionNode>();

            // ── 1. PAYLOAD SECTION (Top of stack) ───────────────────────────────────
            var payload = blueprint.Payload;
            bool hasPayload = payload != null;

            // Nose Cone / Fairing cover
            rawNodes.Add(new CrossSectionNode(
                id: "node-fairing-nose",
                slotId: null,
                partType: CrossSectionPartType.FairingNose,
                title: "Aerodinamik Kaporta (Fairing)",
                subtitle: blueprint.FairingAdapter != null ? blueprint.FairingAdapter.Name : "Standart Kompozit Koruma",
                stageIndex: -1,
                component: blueprint.FairingAdapter,
                relativeWidth: 0.55,
                relativeHeight: 0.65,
                supplierColorHex: ResolveSupplierColor(blueprint.FairingAdapter?.Supplier?.Id)
            ));

            // Scientific Instruments
            var inst1 = blueprint.GetComponentInSlot(VehicleSlotId.PayloadInstrument1);
            rawNodes.Add(new CrossSectionNode(
                id: "node-payload-inst1",
                slotId: VehicleSlotId.PayloadInstrument1,
                partType: CrossSectionPartType.PayloadInstrument,
                title: inst1 != null ? inst1.Name : "Bilimsel Alet 1 Yuvası",
                subtitle: inst1 != null ? $"{inst1.EffectiveDryMass:F1} t • {inst1.Category}" : "Sensör / Kamera yuvası",
                stageIndex: -1,
                component: inst1,
                supplier: inst1?.Supplier,
                isEmptySlot: inst1 == null,
                relativeWidth: 0.58,
                relativeHeight: 0.55,
                supplierColorHex: ResolveSupplierColor(inst1?.Supplier?.Id)
            ));

            var inst2 = blueprint.GetComponentInSlot(VehicleSlotId.PayloadInstrument2);
            if (inst2 != null || payload?.Instruments?.Count > 1)
            {
                rawNodes.Add(new CrossSectionNode(
                    id: "node-payload-inst2",
                    slotId: VehicleSlotId.PayloadInstrument2,
                    partType: CrossSectionPartType.PayloadInstrument,
                    title: inst2 != null ? inst2.Name : "Bilimsel Alet 2 Yuvası",
                    subtitle: inst2 != null ? $"{inst2.EffectiveDryMass:F1} t • {inst2.Category}" : "İkincil bilimsel alet",
                    stageIndex: -1,
                    component: inst2,
                    supplier: inst2?.Supplier,
                    isEmptySlot: inst2 == null,
                    relativeWidth: 0.58,
                    relativeHeight: 0.55,
                    supplierColorHex: ResolveSupplierColor(inst2?.Supplier?.Id)
                ));
            }

            // Payload Bus (Core chassis / electronics)
            var bus = blueprint.GetComponentInSlot(VehicleSlotId.PayloadBus);
            rawNodes.Add(new CrossSectionNode(
                id: "node-payload-bus",
                slotId: VehicleSlotId.PayloadBus,
                partType: CrossSectionPartType.PayloadBus,
                title: bus != null ? bus.Name : "Gövde Yapısı (Bus)",
                subtitle: bus != null ? $"{bus.EffectiveDryMass:F1} t • Aviyonik & Şasi" : "Montaj bekleyen gövde",
                stageIndex: -1,
                component: bus,
                supplier: bus?.Supplier,
                isEmptySlot: bus == null,
                relativeWidth: 0.62,
                relativeHeight: 0.85,
                supplierColorHex: ResolveSupplierColor(bus?.Supplier?.Id)
            ));

            // Spacecraft Subsystems: Power, Thermal, Comms, ADCS (Composite or individual)
            var power = blueprint.GetComponentInSlot(VehicleSlotId.PayloadPower);
            rawNodes.Add(new CrossSectionNode(
                id: "node-payload-power",
                slotId: VehicleSlotId.PayloadPower,
                partType: CrossSectionPartType.PayloadSubsystem,
                title: power != null ? power.Name : "Güç Sistemi (Power)",
                subtitle: power != null ? $"{power.PowerGeneration:F1} kW • Solar / Batarya" : "Güç ünitesi",
                stageIndex: -1,
                component: power,
                supplier: power?.Supplier,
                isEmptySlot: power == null,
                relativeWidth: 0.65,
                relativeHeight: 0.60,
                supplierColorHex: ResolveSupplierColor(power?.Supplier?.Id)
            ));

            var comms = blueprint.GetComponentInSlot(VehicleSlotId.PayloadComms);
            rawNodes.Add(new CrossSectionNode(
                id: "node-payload-comms",
                slotId: VehicleSlotId.PayloadComms,
                partType: CrossSectionPartType.PayloadSubsystem,
                title: comms != null ? comms.Name : "Haberleşme & ADCS",
                subtitle: comms != null ? $"{comms.DownlinkRate:F1} Mbps Downlink" : "İletişim & Yönelim",
                stageIndex: -1,
                component: comms,
                supplier: comms?.Supplier,
                isEmptySlot: comms == null,
                relativeWidth: 0.65,
                relativeHeight: 0.55,
                supplierColorHex: ResolveSupplierColor(comms?.Supplier?.Id)
            ));

            // EDL Module (if present or applicable)
            var edl = blueprint.GetComponentInSlot(VehicleSlotId.PayloadEdl);
            if (edl != null || (payload != null && payload.Edl != null))
            {
                var edlComp = edl ?? payload.Edl;
                rawNodes.Add(new CrossSectionNode(
                    id: "node-payload-edl",
                    slotId: VehicleSlotId.PayloadEdl,
                    partType: CrossSectionPartType.PayloadEdl,
                    title: edlComp != null ? edlComp.Name : "İniş Modülü (EDL)",
                    subtitle: edlComp != null ? $"{edlComp.EffectiveDryMass:F1} t • Isı Kalkanı / Retro" : "İniş sistemi",
                    stageIndex: -1,
                    component: edlComp,
                    supplier: edlComp?.Supplier,
                    isEmptySlot: edlComp == null,
                    relativeWidth: 0.68,
                    relativeHeight: 0.60,
                    supplierColorHex: ResolveSupplierColor(edlComp?.Supplier?.Id)
                ));
            }

            // Payload Adapter / Separation Decoupler
            rawNodes.Add(new CrossSectionNode(
                id: "node-decoupler-payload",
                slotId: null,
                partType: CrossSectionPartType.InterstageDecoupler,
                title: "Faydalı Yük Ayrılma Halkası",
                subtitle: "Pnömatik Dekuplör Adaptörü",
                stageIndex: -1,
                relativeWidth: 0.68,
                relativeHeight: 0.35,
                supplierColorHex: "#64748b"
            ));

            // ── 2. ROCKET STAGES (From Stage 3 down to Stage 1) ─────────────────────
            int stageCount = DetermineEffectiveStageCount(blueprint);

            // STAGE 3 (If present in blueprint)
            if (stageCount >= 3)
            {
                var s3Tank = blueprint.GetComponentInSlot(VehicleSlotId.Stage3Tank);
                var s3Engine = blueprint.GetComponentInSlot(VehicleSlotId.Stage3Engine);
                var s3 = blueprint.Stages.Count > 2 ? blueprint.Stages[2] : null;

                rawNodes.Add(new CrossSectionNode(
                    id: "node-stage3-tank",
                    slotId: VehicleSlotId.Stage3Tank,
                    partType: CrossSectionPartType.StageTank,
                    title: s3Tank != null ? s3Tank.Name : "Aşama 3 Yakıt Tankı",
                    subtitle: s3 != null ? $"{s3.PropellantMass:F1} t Yakıt • {s3.StructureMaterial?.Name ?? "Alüminyum"}" : "Üst aşama yakıt tankı",
                    stageIndex: 2,
                    component: s3Tank,
                    supplier: s3?.StageSupplier ?? s3Tank?.Supplier,
                    material: s3?.StructureMaterial,
                    isEmptySlot: s3Tank == null,
                    relativeWidth: 0.72,
                    relativeHeight: 0.90,
                    supplierColorHex: ResolveSupplierColor(s3?.StageSupplier?.Id ?? s3Tank?.Supplier?.Id)
                ));

                rawNodes.Add(new CrossSectionNode(
                    id: "node-stage3-engine",
                    slotId: VehicleSlotId.Stage3Engine,
                    partType: CrossSectionPartType.StageEngine,
                    title: s3Engine != null ? s3Engine.Name : "Aşama 3 Motoru",
                    subtitle: s3Engine != null ? $"{s3Engine.Isp:F0} s Isp • {s3Engine.Thrust:F0} kN" : "Vakum motoru yuvası",
                    stageIndex: 2,
                    component: s3Engine,
                    supplier: s3Engine?.Supplier,
                    isEmptySlot: s3Engine == null,
                    relativeWidth: 0.66,
                    relativeHeight: 0.70,
                    supplierColorHex: ResolveSupplierColor(s3Engine?.Supplier?.Id)
                ));

                rawNodes.Add(new CrossSectionNode(
                    id: "node-decoupler-2-3",
                    slotId: null,
                    partType: CrossSectionPartType.InterstageDecoupler,
                    title: "Kafes Dekuplör (Aşama 2-3)",
                    subtitle: "Ayrılma ve havalandırma interstage",
                    stageIndex: 2,
                    relativeWidth: 0.76,
                    relativeHeight: 0.35,
                    supplierColorHex: "#64748b"
                ));
            }

            // STAGE 2 (If stageCount >= 2)
            if (stageCount >= 2)
            {
                var s2Tank = blueprint.GetComponentInSlot(VehicleSlotId.Stage2Tank);
                var s2Engine = blueprint.GetComponentInSlot(VehicleSlotId.Stage2Engine);
                var s2 = blueprint.Stages.Count > 1 ? blueprint.Stages[1] : null;

                rawNodes.Add(new CrossSectionNode(
                    id: "node-stage2-tank",
                    slotId: VehicleSlotId.Stage2Tank,
                    partType: CrossSectionPartType.StageTank,
                    title: s2Tank != null ? s2Tank.Name : "Aşama 2 Yakıt Tankı",
                    subtitle: s2 != null ? $"{s2.PropellantMass:F1} t Yakıt • {s2.StructureMaterial?.Name ?? "Al-Li Alaşım"}" : "Orta aşama yakıt tankı",
                    stageIndex: 1,
                    component: s2Tank,
                    supplier: s2?.StageSupplier ?? s2Tank?.Supplier,
                    material: s2?.StructureMaterial,
                    isEmptySlot: s2Tank == null,
                    relativeWidth: 0.85,
                    relativeHeight: 1.10,
                    supplierColorHex: ResolveSupplierColor(s2?.StageSupplier?.Id ?? s2Tank?.Supplier?.Id)
                ));

                rawNodes.Add(new CrossSectionNode(
                    id: "node-stage2-engine",
                    slotId: VehicleSlotId.Stage2Engine,
                    partType: CrossSectionPartType.StageEngine,
                    title: s2Engine != null ? s2Engine.Name : "Aşama 2 Motoru",
                    subtitle: s2Engine != null ? $"{s2Engine.Isp:F0} s Isp • {s2Engine.Thrust:F0} kN" : "Üst aşama motor yuvası",
                    stageIndex: 1,
                    component: s2Engine,
                    supplier: s2Engine?.Supplier,
                    isEmptySlot: s2Engine == null,
                    relativeWidth: 0.75,
                    relativeHeight: 0.75,
                    supplierColorHex: ResolveSupplierColor(s2Engine?.Supplier?.Id)
                ));

                rawNodes.Add(new CrossSectionNode(
                    id: "node-decoupler-1-2",
                    slotId: null,
                    partType: CrossSectionPartType.InterstageDecoupler,
                    title: "Kafes Dekuplör (Aşama 1-2)",
                    subtitle: "Aşama ayırıcı piroteknik halka",
                    stageIndex: 1,
                    relativeWidth: 0.90,
                    relativeHeight: 0.35,
                    supplierColorHex: "#64748b"
                ));
            }

            // STAGE 1 (Always present as primary booster)
            var s1Tank = blueprint.GetComponentInSlot(VehicleSlotId.Stage1Tank);
            var s1Engine = blueprint.GetComponentInSlot(VehicleSlotId.Stage1Engine);
            var s1 = blueprint.Stages.Count > 0 ? blueprint.Stages[0] : null;

            rawNodes.Add(new CrossSectionNode(
                id: "node-stage1-tank",
                slotId: VehicleSlotId.Stage1Tank,
                partType: CrossSectionPartType.StageTank,
                title: s1Tank != null ? s1Tank.Name : "Aşama 1 Ana Yakıt Tankı",
                subtitle: s1 != null ? $"{s1.PropellantMass:F1} t Yakıt • {s1.StructureMaterial?.Name ?? "Birincil Yapı"}" : "Ana fırlatıcı tankı",
                stageIndex: 0,
                component: s1Tank,
                supplier: s1?.StageSupplier ?? s1Tank?.Supplier,
                material: s1?.StructureMaterial,
                isEmptySlot: s1Tank == null,
                relativeWidth: 1.00,
                relativeHeight: 1.40,
                supplierColorHex: ResolveSupplierColor(s1?.StageSupplier?.Id ?? s1Tank?.Supplier?.Id)
            ));

            rawNodes.Add(new CrossSectionNode(
                id: "node-stage1-engine",
                slotId: VehicleSlotId.Stage1Engine,
                partType: CrossSectionPartType.StageEngine,
                title: s1Engine != null ? s1Engine.Name : "Aşama 1 Ana Motor",
                subtitle: s1Engine != null ? $"{s1Engine.Thrust:F0} kN İtki • {s1Engine.Isp:F0} s Isp" : "Fırlatıcı ana motor kümesi",
                stageIndex: 0,
                component: s1Engine,
                supplier: s1Engine?.Supplier,
                isEmptySlot: s1Engine == null,
                relativeWidth: 0.92,
                relativeHeight: 0.90,
                supplierColorHex: ResolveSupplierColor(s1Engine?.Supplier?.Id)
            ));

            // Aerodynamic Base Fins
            rawNodes.Add(new CrossSectionNode(
                id: "node-aero-fins",
                slotId: null,
                partType: CrossSectionPartType.AeroFins,
                title: "Aerodinamik Kanatçıklar (Fins)",
                subtitle: "Fırlatma stabilite ve yön kontrol yüzeyleri",
                stageIndex: 0,
                relativeWidth: 1.15,
                relativeHeight: 0.40,
                supplierColorHex: "#475569"
            ));

            // ── 3. AUTOMATIC SNAP CONNECTION PASS ────────────────────────────────────
            // Auto-snaps all nodes along the vertical centerline (0.5).
            var snappedNodes = new List<CrossSectionNode>(rawNodes.Count);
            for (int i = 0; i < rawNodes.Count; i++)
            {
                bool isTop = (i == 0);
                bool isBottom = (i == rawNodes.Count - 1);

                var raw = rawNodes[i];
                var topSnap = new SnapPoint(0.5, 0.0, !isTop);
                var bottomSnap = new SnapPoint(0.5, 1.0, !isBottom);

                var snapped = new CrossSectionNode(
                    raw.Id,
                    raw.SlotId,
                    raw.PartType,
                    raw.Title,
                    raw.Subtitle,
                    raw.StageIndex,
                    raw.Component,
                    raw.Supplier,
                    raw.Material,
                    raw.IsEmptySlot,
                    raw.RelativeWidth,
                    raw.RelativeHeight,
                    raw.SupplierColorHex,
                    topSnap,
                    bottomSnap);

                snappedNodes.Add(snapped);
            }

            return new CraftCrossSectionLayout(snappedNodes, stageCount, hasPayload);
        }

        private static int DetermineEffectiveStageCount(Blueprint blueprint)
        {
            if (blueprint == null) return 1;

            int stagesInList = blueprint.Stages.Count;
            bool hasStage3Slots = blueprint.GetComponentInSlot(VehicleSlotId.Stage3Engine) != null ||
                                  blueprint.GetComponentInSlot(VehicleSlotId.Stage3Tank) != null;
            bool hasStage2Slots = blueprint.GetComponentInSlot(VehicleSlotId.Stage2Engine) != null ||
                                  blueprint.GetComponentInSlot(VehicleSlotId.Stage2Tank) != null;

            if (stagesInList >= 3 || hasStage3Slots) return 3;
            if (stagesInList >= 2 || hasStage2Slots) return 2;
            return Math.Max(1, stagesInList);
        }

        public static string ResolveSupplierColor(string supplierId)
        {
            if (string.IsNullOrEmpty(supplierId))
                return ColorDefault;

            var idLower = supplierId.ToLowerInvariant();
            if (idLower.Contains("helion")) return ColorHelion;
            if (idLower.Contains("kestrel")) return ColorKestrel;
            if (idLower.Contains("nordvik")) return ColorNordvik;
            if (idLower.Contains("tarsus")) return ColorTarsus;
            if (idLower.Contains("meridian")) return ColorMeridian;
            if (idLower.Contains("aurelia")) return ColorAurelia;

            return ColorDefault;
        }
    }
}
