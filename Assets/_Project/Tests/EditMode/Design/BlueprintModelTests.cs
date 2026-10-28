using System;
using System.Collections.Generic;
using NUnit.Framework;
using Apogee.Design;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for Blueprint data model and derived stat evaluation.
    /// Complies with GDD §8.1, §8.2, §19.11, and §21 M1-06.
    /// </summary>
    [TestFixture]
    public class BlueprintModelTests
    {
        private const double Tol = 1e-4;

        [Test]
        public void DesignComponent_AppliesSupplierAndMaterialMultipliers()
        {
            // Supplier: Cost x1.2, Delivery x1.5, Reliability x0.9
            var supplier = new SupplierSpec("kestrel", "Kestrel", costMultiplier: 1.2, deliveryMultiplier: 1.5, reliabilityMultiplier: 0.9);
            // Material: Mass x0.8, Cost x2.0, BuildTime x1.3
            var material = new MaterialSpec("carbon", "Carbon", massMultiplier: 0.8, costMultiplier: 2.0, buildTimeMultiplier: 1.3);

            var comp = new DesignComponent(
                id: "test-tank",
                category: "tank",
                name: "Advanced Tank",
                dryMass: 10.0,
                cost: 5.0,
                buildTime: 20,
                reliability: 0.95,
                supplier: supplier,
                material: material);

            // Effective dry mass = 10.0 * 0.8 = 8.0
            Assert.AreEqual(8.0, comp.EffectiveDryMass, Tol);
            // Effective cost = 5.0 * 1.2 * 2.0 = 12.0
            Assert.AreEqual(12.0, comp.EffectiveCost, Tol);
            // Effective build time = ceil(20 * 1.5 * 1.3) = ceil(39.0) = 39
            Assert.AreEqual(39, comp.EffectiveBuildTime);
            // Effective reliability = 0.95 * 0.9 = 0.855
            Assert.AreEqual(0.855, comp.EffectiveReliability, Tol);
        }

        [Test]
        public void StageDesign_AggregatesDryMassAndCost()
        {
            var engine = new DesignComponent("eng", "engine", dryMass: 2.0, cost: 4.0, buildTime: 30, reliability: 0.98, isp: 310);
            var tank = new DesignComponent("tank", "tank", dryMass: 3.0, cost: 2.0, buildTime: 20, reliability: 0.99);

            var stage = new StageDesign(
                id: "stage-1",
                engine: engine,
                tank: tank,
                propellantMass: 50.0);

            Assert.AreEqual(5.0, stage.DryMass, Tol);
            Assert.AreEqual(55.0, stage.WetMass, Tol);
            Assert.AreEqual(6.0, stage.Cost, Tol);
            Assert.AreEqual(30, stage.BuildTimeDays);
            Assert.AreEqual(0.98 * 0.99, stage.Reliability, Tol);

            var rocketStage = stage.ToRocketStage();
            Assert.AreEqual("stage-1", rocketStage.Id);
            Assert.AreEqual(310.0, rocketStage.Isp, Tol);
            Assert.AreEqual(5.0, rocketStage.DryMass, Tol);
            Assert.AreEqual(55.0, rocketStage.WetMass, Tol);
        }

        [Test]
        public void PayloadDesign_CalculatesSubsystemsAndScienceInstruments()
        {
            var bus = new DesignComponent("bus-1", "bus", dryMass: 1.0, cost: 2.0, powerConsumption: 0.5, thermalLoad: 0.4, reliability: 0.99);
            var solar = new DesignComponent("solar-1", "power", dryMass: 0.2, cost: 1.0, powerGeneration: 2.0, reliability: 0.98);
            var radiator = new DesignComponent("rad-1", "thermal", dryMass: 0.1, cost: 0.5, thermalDissipation: 1.5, reliability: 0.99);
            var comms = new DesignComponent("comm-1", "comms", dryMass: 0.1, cost: 1.0, powerConsumption: 0.3, downlinkRate: 20.0, dataStorage: 100.0, reliability: 0.98);
            var cam = new DesignComponent("cam-1", "instrument", dryMass: 0.05, cost: 1.5, powerConsumption: 0.2, dataRate: 15.0, reliability: 0.97);

            var payload = new PayloadDesign(
                id: "lunar-orbiter-payload",
                bus: bus,
                power: solar,
                thermal: radiator,
                comms: comms,
                instruments: new[] { cam });

            double expectedDry = 1.0 + 0.2 + 0.1 + 0.1 + 0.05;
            double expectedCost = 2.0 + 1.0 + 0.5 + 1.0 + 1.5;

            Assert.AreEqual(expectedDry, payload.DryMass, Tol);
            Assert.AreEqual(expectedCost, payload.Cost, Tol);
            Assert.AreEqual(2.0, payload.PowerGeneration, Tol);
            Assert.AreEqual(0.5 + 0.3 + 0.2, payload.PowerConsumption, Tol);
            Assert.AreEqual(0.4, payload.ThermalLoad, Tol);
            Assert.AreEqual(1.5, payload.ThermalDissipation, Tol);
            Assert.AreEqual(15.0, payload.DataRate, Tol);
            Assert.AreEqual(100.0, payload.DataStorage, Tol);
            Assert.AreEqual(20.0, payload.DownlinkRate, Tol);
            Assert.AreEqual(0.99 * 0.98 * 0.99 * 0.98 * 0.97, payload.Reliability, Tol);
        }

        [Test]
        public void BlueprintEvaluator_FullVehicle_ComputesAllDerivedStats()
        {
            // Create Payload
            var bus = new DesignComponent("bus-1", "bus", dryMass: 1.0, cost: 3.0, buildTime: 25, powerConsumption: 0.8, powerGeneration: 2.0, thermalLoad: 0.5, thermalDissipation: 1.0, reliability: 0.99);
            var inst = new DesignComponent("radar-1", "instrument", dryMass: 0.5, cost: 5.0, buildTime: 40, powerConsumption: 0.6, dataRate: 12.0, dataStorage: 50.0, downlinkRate: 10.0, reliability: 0.96);
            var payload = new PayloadDesign("sat-payload", bus: bus, instruments: new[] { inst });

            // Create Stage 1
            var s1Engine = new DesignComponent("s1-eng", "engine", dryMass: 5.0, cost: 10.0, buildTime: 60, reliability: 0.97, isp: 300.0);
            var s1Tank = new DesignComponent("s1-tank", "tank", dryMass: 15.0, cost: 5.0, buildTime: 45, reliability: 0.99);
            var stage1 = new StageDesign("stage-1", engine: s1Engine, tank: s1Tank, propellantMass: 180.0);

            // Create Stage 2
            var s2Engine = new DesignComponent("s2-eng", "engine", dryMass: 1.5, cost: 6.0, buildTime: 50, reliability: 0.98, isp: 420.0);
            var s2Tank = new DesignComponent("s2-tank", "tank", dryMass: 3.5, cost: 3.0, buildTime: 35, reliability: 0.99);
            var stage2 = new StageDesign("stage-2", engine: s2Engine, tank: s2Tank, propellantMass: 35.0);

            var fairing = new DesignComponent("fairing", "structure", dryMass: 0.8, cost: 1.0, buildTime: 15, reliability: 0.995);

            var blueprint = new Blueprint(
                id: "bp-moon-orbiter",
                name: "Moon Explorer I",
                version: 1,
                targetDestinationId: "moon",
                targetMissionTypeId: "orbiter",
                payload: payload,
                stages: new[] { stage1, stage2 },
                fairingAdapter: fairing);

            var stats = BlueprintEvaluator.Evaluate(blueprint, DeltaVLossProfile.None);

            // 1. Dry Mass Check
            // Payload (1.5) + Fairing (0.8) + Stage 1 (20.0) + Stage 2 (5.0) = 27.3t
            Assert.AreEqual(27.3, stats.DryMass, Tol);

            // 2. Propellant Check
            // Stage 1 (180.0) + Stage 2 (35.0) = 215.0t
            Assert.AreEqual(215.0, stats.PropellantMass, Tol);

            // 3. Wet Mass Check
            // 27.3 + 215.0 = 242.3t
            Assert.AreEqual(242.3, stats.WetMass, Tol);

            // 4. Cost Check
            // Payload (3.0 + 5.0 = 8.0) + Fairing (1.0) + Stage 1 (10.0 + 5.0 = 15.0) + Stage 2 (6.0 + 3.0 = 9.0) = 33.0 M$
            Assert.AreEqual(33.0, stats.Cost, Tol);

            // 5. Build Time Check
            // Max component time is Stage 1 engine: 60 days
            // Integration time: 2 stages * 5 days = 10 days
            // Total = 70 days
            Assert.AreEqual(70, stats.BuildTimeDays);

            // 6. Reliability Check
            // bus(0.99) * inst(0.96) * fairing(0.995) * s1Eng(0.97) * s1Tank(0.99) * s2Eng(0.98) * s2Tank(0.99)
            double expectedRel = 0.99 * 0.96 * 0.995 * 0.97 * 0.99 * 0.98 * 0.99;
            Assert.AreEqual(expectedRel, stats.Reliability, Tol);

            // 7. Power Check
            Assert.AreEqual(2.0, stats.PowerGeneration, Tol);
            Assert.AreEqual(1.4, stats.PowerConsumption, Tol);
            Assert.AreEqual(0.6, stats.PowerMargin, Tol); // 2.0 - 1.4

            // 8. Thermal Check
            Assert.AreEqual(0.5, stats.ThermalLoad, Tol);
            Assert.AreEqual(1.0, stats.ThermalDissipation, Tol);
            Assert.AreEqual(0.5, stats.ThermalMargin, Tol); // 1.0 - 0.5

            // 9. Comms/Data Check
            Assert.AreEqual(12.0, stats.DataRate, Tol);
            Assert.AreEqual(50.0, stats.DataStorage, Tol);
            Assert.AreEqual(10.0, stats.DownlinkRate, Tol);

            // 10. Delta-V Check
            Assert.IsNotNull(stats.DeltaVResult);
            Assert.Greater(stats.NetDeltaV, 6000.0);
            Assert.AreEqual(stats.NetDeltaV, stats.IdealDeltaV, Tol); // Since profile is None
        }

        [Test]
        public void BlueprintStats_ProvidesValidStatBreakdown()
        {
            var bus = new DesignComponent("bus-1", "bus", dryMass: 2.0, cost: 5.0);
            var eng = new DesignComponent("eng-1", "engine", dryMass: 3.0, cost: 10.0, isp: 300);
            var stage = new StageDesign("s1", engine: eng, propellantMass: 20.0);

            var bp = new Blueprint("simple-bp", "Simple", payload: new PayloadDesign("p", bus: bus), stages: new[] { stage });
            var stats = BlueprintEvaluator.Evaluate(bp);

            var massBreakdown = stats.GetBreakdown(StatType.DryMass);
            Assert.AreEqual(StatType.DryMass, massBreakdown.Type);
            Assert.AreEqual(5.0, massBreakdown.FinalValue, Tol);
            Assert.AreEqual(2, massBreakdown.Entries.Count); // Bus and Stage 1

            var costBreakdown = stats.GetBreakdown(StatType.Cost);
            Assert.AreEqual(15.0, costBreakdown.FinalValue, Tol);
            Assert.AreEqual(2, costBreakdown.Entries.Count);
        }

        [Test]
        public void Evaluate_NullBlueprint_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => BlueprintEvaluator.Evaluate(null));
        }
    }
}
