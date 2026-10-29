using System.Linq;
using Apogee.Design;
using NUnit.Framework;

namespace Apogee.Tests.EditMode.Design
{
    [TestFixture]
    public class CrossSectionTests
    {
        [Test]
        public void ThreeStageRocketWithPayload_AutoAssemblesAndSnapsSuccessfully()
        {
            // Arrange: Create a full 3-stage rocket blueprint with payload
            var supplierNordvik = new SupplierSpec("supplier-nordvik", "Nordvik Propulsion", 1.0, 1.0, 1.0);
            var supplierHelion = new SupplierSpec("supplier-helion", "Helion Dynamics", 0.9, 0.8, 0.95);
            var supplierKestrel = new SupplierSpec("supplier-kestrel", "Kestrel Aerospace", 1.2, 1.2, 0.99);

            var s3Engine = new DesignComponent("eng-s3", "engine", "S3 Vacuum Engine", 0.8, 2.0, 30, 0.98, isp: 350, thrust: 45, supplier: supplierNordvik);
            var s3Tank = new DesignComponent("tank-s3", "tank", "S3 Hydrolox Tank", 1.2, 1.5, 20, 0.99, supplier: supplierNordvik);
            var stage3 = new StageDesign("s3", "Stage 3", s3Engine, s3Tank, 12.0, stageSupplier: supplierNordvik);

            var s2Engine = new DesignComponent("eng-s2", "engine", "S2 Mid Engine", 1.5, 3.5, 45, 0.97, isp: 330, thrust: 200, supplier: supplierHelion);
            var s2Tank = new DesignComponent("tank-s2", "tank", "S2 Methalox Tank", 3.0, 2.5, 35, 0.99, supplier: supplierHelion);
            var stage2 = new StageDesign("s2", "Stage 2", s2Engine, s2Tank, 35.0, stageSupplier: supplierHelion);

            var s1Engine = new DesignComponent("eng-s1", "engine", "S1 Booster Engine", 4.0, 6.0, 60, 0.96, isp: 290, thrust: 850, supplier: supplierKestrel);
            var s1Tank = new DesignComponent("tank-s1", "tank", "S1 Kerolox Tank", 8.0, 5.0, 50, 0.99, supplier: supplierKestrel);
            var stage1 = new StageDesign("s1", "Stage 1", s1Engine, s1Tank, 120.0, stageSupplier: supplierKestrel);

            var bus = new DesignComponent("bus-rover", "bus", "Lunar Rover Bus", 0.4, 3.0, 30, 0.98);
            var power = new DesignComponent("pwr-solar", "power", "High-Eff Solar Panel", 0.1, 1.0, 15, 0.97, powerGeneration: 1.5);
            var comms = new DesignComponent("comms-xband", "comms", "X-Band Transceiver", 0.05, 0.8, 10, 0.99, downlinkRate: 25.0);
            var payload = new PayloadDesign("payload-rover", "Moon Explorer", bus: bus, power: power, comms: comms);

            var blueprint = new Blueprint(
                id: "bp-saturn-v-class",
                name: "3-Aşamalı Ay Roketi",
                payload: payload,
                stages: new[] { stage1, stage2, stage3 });

            // Act: Build cross-section layout
            var layout = CraftCrossSectionLayout.Build(blueprint);

            // Assert: Layout metadata
            Assert.IsNotNull(layout);
            Assert.AreEqual(3, layout.StageCount, "Stage count must be 3 for a 3-stage rocket.");
            Assert.IsTrue(layout.HasPayload, "Layout must indicate presence of payload.");
            Assert.IsNotEmpty(layout.Nodes, "Layout must produce cross-section nodes.");

            // Assert: Ordering (Top-down sequence: Fairing -> Instruments -> Bus -> Power -> Comms -> Decoupler -> Stage 3 -> Decoupler 2-3 -> Stage 2 -> Decoupler 1-2 -> Stage 1 -> Fins)
            var nodeTypes = layout.Nodes.Select(n => n.PartType).ToList();
            Assert.AreEqual(CrossSectionPartType.FairingNose, nodeTypes.First(), "First node must be Fairing/Nose.");
            Assert.AreEqual(CrossSectionPartType.AeroFins, nodeTypes.Last(), "Last node must be base Aero Fins.");

            // Verify Stage 3 presence in nodes
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 2 && n.PartType == CrossSectionPartType.StageTank), "Stage 3 Tank node must exist.");
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 2 && n.PartType == CrossSectionPartType.StageEngine), "Stage 3 Engine node must exist.");

            // Verify Stage 2 presence
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 1 && n.PartType == CrossSectionPartType.StageTank), "Stage 2 Tank node must exist.");
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 1 && n.PartType == CrossSectionPartType.StageEngine), "Stage 2 Engine node must exist.");

            // Verify Stage 1 presence
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 0 && n.PartType == CrossSectionPartType.StageTank), "Stage 1 Tank node must exist.");
            Assert.IsTrue(layout.Nodes.Any(n => n.StageIndex == 0 && n.PartType == CrossSectionPartType.StageEngine), "Stage 1 Engine node must exist.");

            // Verify Auto-Snap connections (all intermediate nodes must have connected top and bottom snaps)
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                var node = layout.Nodes[i];
                if (i > 0)
                {
                    Assert.IsTrue(node.TopSnap.IsConnected, $"Node at index {i} ({node.Title}) should have TopSnap connected.");
                }
                if (i < layout.Nodes.Count - 1)
                {
                    Assert.IsTrue(node.BottomSnap.IsConnected, $"Node at index {i} ({node.Title}) should have BottomSnap connected.");
                }
            }
        }

        [Test]
        public void EmptySlotHandling_CorrectlyMarksEmptySlots()
        {
            // Blueprint without Stage 2 modules
            var s1Engine = new DesignComponent("eng-1", "engine", "S1 Engine", 2.0, 3.0, 30, 0.95, isp: 300, thrust: 500);
            var stage1 = new StageDesign("s1", "Stage 1", engine: s1Engine, tank: null);
            var blueprint = new Blueprint("bp-partial", "Kısmi Montaj", stages: new[] { stage1 });

            var layout = CraftCrossSectionLayout.Build(blueprint);

            // Stage 1 Engine is equipped -> IsEmptySlot is false
            var engNode = layout.Nodes.FirstOrDefault(n => n.SlotId == VehicleSlotId.Stage1Engine);
            Assert.IsNotNull(engNode);
            Assert.IsFalse(engNode.IsEmptySlot);

            // Stage 1 Tank is not equipped -> IsEmptySlot is true
            var tankNode = layout.Nodes.FirstOrDefault(n => n.SlotId == VehicleSlotId.Stage1Tank);
            Assert.IsNotNull(tankNode);
            Assert.IsTrue(tankNode.IsEmptySlot);
        }

        [Test]
        public void SupplierColorResolution_MatchesGddSpecification()
        {
            Assert.AreEqual(CraftCrossSectionLayout.ColorHelion, CraftCrossSectionLayout.ResolveSupplierColor("supplier-helion-dynamics"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorKestrel, CraftCrossSectionLayout.ResolveSupplierColor("supplier-kestrel-aero"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorNordvik, CraftCrossSectionLayout.ResolveSupplierColor("supplier-nordvik"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorTarsus, CraftCrossSectionLayout.ResolveSupplierColor("supplier-tarsus"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorMeridian, CraftCrossSectionLayout.ResolveSupplierColor("supplier-meridian"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorAurelia, CraftCrossSectionLayout.ResolveSupplierColor("supplier-aurelia"));
            Assert.AreEqual(CraftCrossSectionLayout.ColorDefault, CraftCrossSectionLayout.ResolveSupplierColor("unknown-company"));
        }

        [Test]
        public void DynamicWidthProgression_PayloadNarrowerThanBooster()
        {
            var s1Engine = new DesignComponent("eng-s1", "engine", "Booster", 3.0, 5.0, 45, 0.95, isp: 300, thrust: 600);
            var stage1 = new StageDesign("s1", "Stage 1", engine: s1Engine);
            var blueprint = new Blueprint("bp-single", "Tek Aşamalı", stages: new[] { stage1 });

            var layout = CraftCrossSectionLayout.Build(blueprint);

            var fairingNode = layout.Nodes.First(n => n.PartType == CrossSectionPartType.FairingNose);
            var boosterTankNode = layout.Nodes.First(n => n.PartType == CrossSectionPartType.StageTank && n.StageIndex == 0);

            Assert.Less(fairingNode.RelativeWidth, boosterTankNode.RelativeWidth,
                "Fairing / payload width should be narrower than lower stage booster tank.");
        }
    }
}
