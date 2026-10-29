using System;
using System.IO;
using System.Collections.Generic;
using Apogee.Design;

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "") {
    if (ok) { pass++; Console.WriteLine($"  ✓ {name}"); }
    else    { fail++; Console.WriteLine($"  ✗ {name}  [{detail}]"); }
}

Console.WriteLine("=== APOGEE M1-06 Blueprint Evaluator Validation ===\n");

// 1. DesignComponent Supplier & Material multipliers
{
    var supplier = new SupplierSpec("kestrel", "Kestrel", costMultiplier: 1.2, deliveryMultiplier: 1.5, reliabilityMultiplier: 0.9);
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

    Check("1a. EffectiveDryMass = 8.0", Math.Abs(comp.EffectiveDryMass - 8.0) < 1e-4, $"got={comp.EffectiveDryMass}");
    Check("1b. EffectiveCost = 12.0", Math.Abs(comp.EffectiveCost - 12.0) < 1e-4, $"got={comp.EffectiveCost}");
    Check("1c. EffectiveBuildTime = 39", comp.EffectiveBuildTime == 39, $"got={comp.EffectiveBuildTime}");
    Check("1d. EffectiveReliability = 0.855", Math.Abs(comp.EffectiveReliability - 0.855) < 1e-4, $"got={comp.EffectiveReliability}");
}

// 2. StageDesign
{
    var engine = new DesignComponent("eng", "engine", dryMass: 2.0, cost: 4.0, buildTime: 30, reliability: 0.98, isp: 310);
    var tank = new DesignComponent("tank", "tank", dryMass: 3.0, cost: 2.0, buildTime: 20, reliability: 0.99);
    var stage = new StageDesign("stage-1", engine: engine, tank: tank, propellantMass: 50.0);

    Check("2a. Stage DryMass = 5.0", Math.Abs(stage.DryMass - 5.0) < 1e-4);
    Check("2b. Stage WetMass = 55.0", Math.Abs(stage.WetMass - 55.0) < 1e-4);
    Check("2c. Stage Cost = 6.0", Math.Abs(stage.Cost - 6.0) < 1e-4);
    Check("2d. Stage BuildTime = 30", stage.BuildTimeDays == 30);
    Check("2e. Stage Reliability", Math.Abs(stage.Reliability - (0.98 * 0.99)) < 1e-4);

    var rStage = stage.ToRocketStage();
    Check("2f. ToRocketStage", rStage.Isp == 310 && rStage.DryMass == 5.0 && rStage.WetMass == 55.0);
}

// 3. PayloadDesign
{
    var bus = new DesignComponent("bus-1", "bus", dryMass: 1.0, cost: 2.0, powerConsumption: 0.5, thermalLoad: 0.4, reliability: 0.99);
    var solar = new DesignComponent("solar-1", "power", dryMass: 0.2, cost: 1.0, powerGeneration: 2.0, reliability: 0.98);
    var radiator = new DesignComponent("rad-1", "thermal", dryMass: 0.1, cost: 0.5, thermalDissipation: 1.5, reliability: 0.99);
    var comms = new DesignComponent("comm-1", "comms", dryMass: 0.1, cost: 1.0, powerConsumption: 0.3, downlinkRate: 20.0, dataStorage: 100.0, reliability: 0.98);
    var cam = new DesignComponent("cam-1", "instrument", dryMass: 0.05, cost: 1.5, powerConsumption: 0.2, dataRate: 15.0, reliability: 0.97);

    var payload = new PayloadDesign("p", bus: bus, power: solar, thermal: radiator, comms: comms, instruments: new[] { cam });

    Check("3a. Payload DryMass = 1.45", Math.Abs(payload.DryMass - 1.45) < 1e-4);
    Check("3b. Payload Cost = 6.0", Math.Abs(payload.Cost - 6.0) < 1e-4);
    Check("3c. Payload PowerGen = 2.0", Math.Abs(payload.PowerGeneration - 2.0) < 1e-4);
    Check("3d. Payload PowerCon = 1.0", Math.Abs(payload.PowerConsumption - 1.0) < 1e-4);
    Check("3e. Payload ThermalDiss = 1.5", Math.Abs(payload.ThermalDissipation - 1.5) < 1e-4);
    Check("3f. Payload ThermalLoad = 0.4", Math.Abs(payload.ThermalLoad - 0.4) < 1e-4);
    Check("3g. Payload DataRate = 15.0", Math.Abs(payload.DataRate - 15.0) < 1e-4);
    Check("3h. Payload Storage = 100.0", Math.Abs(payload.DataStorage - 100.0) < 1e-4);
    Check("3i. Payload Downlink = 20.0", Math.Abs(payload.DownlinkRate - 20.0) < 1e-4);
}

// 4. BlueprintEvaluator full test
{
    var bus = new DesignComponent("bus-1", "bus", dryMass: 1.0, cost: 3.0, buildTime: 25, powerConsumption: 0.8, powerGeneration: 2.0, thermalLoad: 0.5, thermalDissipation: 1.0, reliability: 0.99);
    var inst = new DesignComponent("radar-1", "instrument", dryMass: 0.5, cost: 5.0, buildTime: 40, powerConsumption: 0.6, dataRate: 12.0, dataStorage: 50.0, downlinkRate: 10.0, reliability: 0.96);
    var payload = new PayloadDesign("sat-payload", bus: bus, instruments: new[] { inst });

    var s1Engine = new DesignComponent("s1-eng", "engine", dryMass: 5.0, cost: 10.0, buildTime: 60, reliability: 0.97, isp: 300.0);
    var s1Tank = new DesignComponent("s1-tank", "tank", dryMass: 15.0, cost: 5.0, buildTime: 45, reliability: 0.99);
    var stage1 = new StageDesign("stage-1", engine: s1Engine, tank: s1Tank, propellantMass: 180.0);

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

    Check("4a. Blueprint DryMass = 27.3", Math.Abs(stats.DryMass - 27.3) < 1e-4, $"got={stats.DryMass}");
    Check("4b. Blueprint PropellantMass = 215.0", Math.Abs(stats.PropellantMass - 215.0) < 1e-4, $"got={stats.PropellantMass}");
    Check("4c. Blueprint WetMass = 242.3", Math.Abs(stats.WetMass - 242.3) < 1e-4, $"got={stats.WetMass}");
    Check("4d. Blueprint Cost = 33.0", Math.Abs(stats.Cost - 33.0) < 1e-4, $"got={stats.Cost}");
    Check("4e. Blueprint BuildTime = 70", stats.BuildTimeDays == 70, $"got={stats.BuildTimeDays}");
    double expectedRel = 0.99 * 0.96 * 0.995 * 0.97 * 0.99 * 0.98 * 0.99;
    Check("4f. Blueprint Reliability", Math.Abs(stats.Reliability - expectedRel) < 1e-4, $"got={stats.Reliability} exp={expectedRel}");
    Check("4g. PowerMargin = 0.6", Math.Abs(stats.PowerMargin - 0.6) < 1e-4, $"got={stats.PowerMargin}");
    Check("4h. ThermalMargin = 0.5", Math.Abs(stats.ThermalMargin - 0.5) < 1e-4, $"got={stats.ThermalMargin}");
    Check("4i. NetDeltaV > 6000", stats.NetDeltaV > 6000.0, $"got={stats.NetDeltaV}");
}

// 5. Breakdown checks
{
    var bus = new DesignComponent("bus-1", "bus", dryMass: 2.0, cost: 5.0);
    var eng = new DesignComponent("eng-1", "engine", dryMass: 3.0, cost: 10.0, isp: 300);
    var stage = new StageDesign("s1", engine: eng, propellantMass: 20.0);

    var bp = new Blueprint("simple-bp", "Simple", payload: new PayloadDesign("p", bus: bus), stages: new[] { stage });
    var stats = BlueprintEvaluator.Evaluate(bp);

    var massBreakdown = stats.GetBreakdown(StatType.DryMass);
    Check("5a. Mass Breakdown count = 2", massBreakdown.Entries.Count == 2);
    Check("5b. Mass Breakdown final = 5.0", Math.Abs(massBreakdown.FinalValue - 5.0) < 1e-4);

    var costBreakdown = stats.GetBreakdown(StatType.Cost);
    Check("5c. Cost Breakdown count = 2", costBreakdown.Entries.Count == 2);
    Check("5d. Cost Breakdown final = 15.0", Math.Abs(costBreakdown.FinalValue - 15.0) < 1e-4);
}

Console.WriteLine($"\n=== Results: {pass} PASS  {fail} FAIL ===");
if (fail > 0) Environment.Exit(1);
