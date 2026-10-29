using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Apogee.Design;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for M1-08: Launch Capacity Calculator.
    /// Validates that <see cref="LaunchCapacityCalculator"/> and the
    /// associated <see cref="PayloadCapacityValidator"/> produce correct
    /// and consistent results against LEO and lunar mission profiles.
    ///
    /// GDD §8.1 — "Fırlatıcı kapasitesi: Roketin hedef yörüngeye taşıyabileceği kütle"
    /// GDD §21 M1-08 — "Testler; Mars/Ay profilleriyle tutarlılık."
    /// </summary>
    [TestFixture]
    public class LaunchCapacityTests
    {
        private const double Tol = 5.0;   // m/s tolerance for ΔV checks
        private const double MassTol = 0.05; // t tolerance for mass checks

        // ── Reference profiles (GDD §16.1) ──────────────────────────────────────
        // Typical values used throughout these tests.
        // LEO: ~9 400 m/s total budget; ascent losses ~1 500 m/s already included.
        private static readonly DeltaVLossProfile LeoProfile =
            new DeltaVLossProfile("LEO Test", stage1LossMetersPerSecond: 1500.0);

        // Lunar transfer (no return): ~13 500 m/s total budget including ascent losses.
        private static readonly DeltaVLossProfile LunarProfile =
            new DeltaVLossProfile("Lunar Test", stage1LossMetersPerSecond: 1500.0);

        // In-space: used when stages are already in orbit (no stage-1 losses).
        private static readonly DeltaVLossProfile InSpaceProfile = DeltaVLossProfile.None;

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a single-stage RocketStage with given Isp, propellant mass, and dry mass.
        /// </summary>
        private static RocketStage Stage(string id, double isp, double dryMass, double propellantMass) =>
            new RocketStage(id, isp, dryMass, dryMass + propellantMass);

        /// <summary>
        /// Returns the ideal ΔV for a single stage + payload using the rocket equation.
        /// </summary>
        private static double IdealDv(double isp, double dryMass, double propellantMass, double payloadMass)
        {
            double wet = dryMass + propellantMass + payloadMass;
            double dry = dryMass + payloadMass;
            return isp * 9.80665 * Math.Log(wet / dry);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 1. LaunchCapacityCalculator — Core Logic
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void Calculator_ZeroActualPayload_MaxPayloadIsPositive_WhenRocketHasCapacity()
        {
            // A capable single-stage rocket: Isp=320s, dry=10t, prop=100t
            var stages = new[] { Stage("S1", 320.0, 10.0, 100.0) };
            double reqDv = 8000.0; // well within rocket's zero-payload ΔV

            var result = LaunchCapacityCalculator.Calculate(stages, reqDv, InSpaceProfile, actualPayloadMass: 0.0);

            Assert.Greater(result.MaxPayloadMass, 0.0, "A capable rocket must report positive payload capacity.");
            Assert.That(result.NetDeltaVAtMaxPayload, Is.EqualTo(reqDv).Within(Tol),
                "ΔV at max payload must match required ΔV within solver tolerance.");
        }

        [Test]
        public void Calculator_ActualPayloadWithinCapacity_IsWithinCapacity()
        {
            // Rocket: Isp=320s, dry=10t, prop=150t → large capacity
            var stages = new[] { Stage("S1", 320.0, 10.0, 150.0) };
            const double reqDv = 7500.0;
            const double actualPayload = 5.0; // small spacecraft

            var result = LaunchCapacityCalculator.Calculate(stages, reqDv, InSpaceProfile, actualPayload);

            Assert.IsTrue(result.IsWithinCapacity, "5 t payload should be within the computed capacity.");
            Assert.Greater(result.MaxPayloadMass, actualPayload, "Max payload must exceed actual.");
            Assert.Greater(result.CapacityMargin, 0.0, "Margin must be positive.");
        }

        [Test]
        public void Calculator_HeavyPayload_ExceedsCapacity()
        {
            // Weak rocket: Isp=270s, dry=5t, prop=30t → low capacity
            var stages = new[] { Stage("S1", 270.0, 5.0, 30.0) };
            const double reqDv = 7000.0;
            const double actualPayload = 20.0; // extremely heavy spacecraft

            var result = LaunchCapacityCalculator.Calculate(stages, reqDv, InSpaceProfile, actualPayload);

            Assert.IsFalse(result.IsWithinCapacity, "20 t payload must exceed the computed capacity.");
            Assert.Less(result.CapacityMargin, 0.0, "Margin must be negative when overloaded.");
        }

        [Test]
        public void Calculator_RocketCannotMeetDvRequirementAtZeroPayload_ReturnsZeroCapacity()
        {
            // Tiny rocket: Isp=250s, dry=5t, prop=5t — ΔV at zero payload ≈ 1700 m/s
            var stages = new[] { Stage("S1", 250.0, 5.0, 5.0) };
            const double reqDv = 9400.0; // LEO — far exceeds rocket's total ΔV

            var result = LaunchCapacityCalculator.Calculate(stages, reqDv, InSpaceProfile, actualPayloadMass: 2.0);

            Assert.AreEqual(0.0, result.MaxPayloadMass, MassTol,
                "When rocket cannot meet ΔV at zero payload, max payload must be 0.");
            Assert.IsFalse(result.IsWithinCapacity);
        }

        [Test]
        public void Calculator_ZeroRequiredDv_ReturnsActualPayloadAsCapacity_NoConstraint()
        {
            var stages = new[] { Stage("S1", 300.0, 10.0, 80.0) };
            const double actualPayload = 3.5;

            var result = LaunchCapacityCalculator.Calculate(stages, requiredDeltaV: 0.0, InSpaceProfile, actualPayload);

            Assert.IsTrue(result.IsWithinCapacity, "No ΔV requirement means any payload fits.");
            Assert.AreEqual(0.0, result.RequiredDeltaV, 1e-9);
        }

        [Test]
        public void Calculator_Determinism_SameInputSameResult()
        {
            var stages = new[] { Stage("S1", 310.0, 8.0, 90.0), Stage("S2", 345.0, 2.0, 15.0) };
            const double reqDv = 9400.0;
            const double payload = 2.0;

            var r1 = LaunchCapacityCalculator.Calculate(stages, reqDv, LeoProfile, payload);
            var r2 = LaunchCapacityCalculator.Calculate(stages, reqDv, LeoProfile, payload);

            Assert.AreEqual(r1.MaxPayloadMass, r2.MaxPayloadMass, 1e-12,
                "Calculator must be deterministic: same inputs must produce identical results.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 2. LEO Profile Consistency (GDD §16.1: ~9 400 m/s including ascent losses)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void LEO_MediumRocket_MaxPayloadIsPlausible()
        {
            // Approximate a medium-class LEO launcher:
            //   Stage-1: Isp=310s, dry=15t, prop=200t
            //   Stage-2: Isp=345s, dry=5t, prop=40t
            // With 1500 m/s ascent loss, net ΔV at 0 payload should be well above 9400 m/s.
            var stages = new List<RocketStage>
            {
                Stage("S1", 310.0, 15.0, 200.0),
                Stage("S2", 345.0, 5.0,  40.0)
            };
            const double reqDv = 9400.0;

            var result = LaunchCapacityCalculator.Calculate(stages, reqDv, LeoProfile, actualPayloadMass: 5.0);

            Assert.IsTrue(result.IsWithinCapacity, "5 t should fit within medium-class LEO capacity.");
            Assert.That(result.NetDeltaVAtMaxPayload, Is.EqualTo(reqDv).Within(Tol),
                "ΔV at max payload should match required LEO ΔV within solver tolerance.");
            // A medium-class launcher should be able to lift at least several tonnes to LEO.
            Assert.Greater(result.MaxPayloadMass, 3.0, "Medium LEO rocket capacity should be > 3 t.");
        }

        [Test]
        public void LEO_SmallRocket_MaxPayloadLessThanMedium()
        {
            // Small-class launcher: much less propellant
            var smallStages = new List<RocketStage>
            {
                Stage("S1", 290.0, 5.0, 40.0),
                Stage("S2", 320.0, 1.5, 8.0)
            };
            var mediumStages = new List<RocketStage>
            {
                Stage("S1", 310.0, 15.0, 200.0),
                Stage("S2", 345.0, 5.0,  40.0)
            };
            const double reqDv = 9400.0;

            var smallResult  = LaunchCapacityCalculator.Calculate(smallStages,  reqDv, LeoProfile, 0.5);
            var mediumResult = LaunchCapacityCalculator.Calculate(mediumStages, reqDv, LeoProfile, 0.5);

            Assert.Less(smallResult.MaxPayloadMass, mediumResult.MaxPayloadMass,
                "Small rocket must deliver less payload than medium-class to LEO.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 3. Lunar Profile Consistency (GDD §16.1: ~13 500 m/s to lunar surface)
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void Lunar_LargeRocket_HasLessCapacityThanLEO_SameRocket()
        {
            // The same rocket stack should carry less payload to the Moon than to LEO
            // because the lunar trajectory requires more ΔV.
            var stages = new List<RocketStage>
            {
                Stage("S1", 315.0, 20.0, 250.0),
                Stage("S2", 350.0, 6.0,  60.0)
            };

            var leoResult   = LaunchCapacityCalculator.Calculate(stages, 9400.0,  LeoProfile,   actualPayloadMass: 5.0);
            var lunarResult = LaunchCapacityCalculator.Calculate(stages, 13500.0, LunarProfile, actualPayloadMass: 5.0);

            Assert.Less(lunarResult.MaxPayloadMass, leoResult.MaxPayloadMass,
                "Lunar trajectory requires more ΔV → less payload capacity than LEO for the same rocket.");
        }

        [Test]
        public void Lunar_WeakRocket_CannotReachLunarDv()
        {
            // A rocket that can reach LEO but not TLI
            var stages = new[] { Stage("S1", 310.0, 10.0, 80.0) };
            // At zero payload: ΔV ≈ 310 * 9.80665 * ln(90/10) ≈ 6855 m/s (no losses) → way below 13500
            var result = LaunchCapacityCalculator.Calculate(stages, 13500.0, LunarProfile, actualPayloadMass: 2.0);

            Assert.AreEqual(0.0, result.MaxPayloadMass, MassTol,
                "A rocket that cannot reach lunar ΔV at zero payload must report 0 capacity.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 4. Latitude Advantage
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void LatitudeAdvantage_IncreasesEffectiveCapacity()
        {
            // The latitude advantage reduces effective ΔV requirement, so the rocket
            // needs less "fuel" to meet the mission → can carry more payload.
            // We model this by having two profiles with different latitude advantages
            // and verifying the higher-advantage profile yields more payload capacity.

            // NOTE: DeltaVLossProfile latitude advantage is reported by the calculator
            // but is NOT added to vehicle capacity (per DeltaVCalculator assumptions).
            // The MissionProfile's RequiredDeltaV already incorporates latitude savings
            // as a *lower* required number. We simulate this by giving the equatorial
            // launcher a lower required ΔV.

            var stages = new[] { Stage("S1", 310.0, 12.0, 120.0) };

            // Polar launch (no advantage): full 9 400 m/s required
            var polarResult = LaunchCapacityCalculator.Calculate(stages, 9400.0, LeoProfile, 3.0);

            // Equatorial launch: ~465 m/s advantage → effective requirement 8 935 m/s
            var equatorialResult = LaunchCapacityCalculator.Calculate(stages, 8935.0, LeoProfile, 3.0);

            Assert.Greater(equatorialResult.MaxPayloadMass, polarResult.MaxPayloadMass,
                "Equatorial launch (lower ΔV req) must yield higher payload capacity than polar launch.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 5. BlueprintEvaluator Integration — Capacity in BlueprintStats
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void Evaluator_WithRequiredDv_PopulatesLaunchCapacity()
        {
            var engine = new DesignComponent("eng", "engine", dryMass: 3.0, cost: 5.0, isp: 315.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 5.0, cost: 2.0);
            var bus    = new DesignComponent("bus",  "bus",   dryMass: 1.0, cost: 2.0);
            var solar  = new DesignComponent("sol",  "power", dryMass: 0.2, cost: 1.0);

            var stage   = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 80.0);
            var payload = new PayloadDesign("p",  bus: bus, power: solar);
            var bp      = new Blueprint("bp-1", "Test", payload: payload, stages: new[] { stage });

            const double reqDv = 9400.0;
            var stats = BlueprintEvaluator.Evaluate(bp, lossProfile: LeoProfile, requiredDeltaV: reqDv);

            Assert.IsNotNull(stats.LaunchCapacity,
                "BlueprintStats.LaunchCapacity must be populated when requiredDeltaV > 0.");
            Assert.Greater(stats.MaxPayloadCapacity, 0.0,
                "MaxPayloadCapacity must be positive for a capable rocket.");
            Assert.AreEqual(reqDv, stats.LaunchCapacity.RequiredDeltaV, 1.0);
        }

        [Test]
        public void Evaluator_WithoutRequiredDv_LaunchCapacityIsNull()
        {
            var engine = new DesignComponent("eng", "engine", dryMass: 3.0, cost: 5.0, isp: 315.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 5.0, cost: 2.0);
            var stage  = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 80.0);
            var bp     = new Blueprint("bp-2", "NoReq", payload: new PayloadDesign("p", bus: new DesignComponent("b", "bus")), stages: new[] { stage });

            // Default requiredDeltaV = 0 → no capacity calculation
            var stats = BlueprintEvaluator.Evaluate(bp);

            Assert.IsNull(stats.LaunchCapacity,
                "LaunchCapacity must be null when no required ΔV is provided.");
            Assert.AreEqual(0.0, stats.MaxPayloadCapacity, 1e-9);
        }

        [Test]
        public void Evaluator_MissionProfileOverload_PopulatesCapacity()
        {
            var engine = new DesignComponent("eng", "engine", dryMass: 3.0, cost: 5.0, isp: 310.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 5.0, cost: 2.0);
            var bus    = new DesignComponent("bus",  "bus",   dryMass: 1.0, cost: 2.0);
            var stage  = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 90.0);
            var bp     = new Blueprint("bp-mp", "MpTest", payload: new PayloadDesign("p", bus: bus), stages: new[] { stage });

            var mission = new MissionProfile("leo-mission", requiredDeltaV: 9400.0);
            var stats   = BlueprintEvaluator.Evaluate(bp, mission, LeoProfile);

            Assert.IsNotNull(stats.LaunchCapacity, "Evaluator(blueprint, missionProfile) must populate LaunchCapacity.");
            Assert.AreEqual(9400.0, stats.LaunchCapacity.RequiredDeltaV, 1.0);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 6. PayloadCapacityValidator Tests
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void PayloadCapacityValidator_NoCapacityResult_Skips()
        {
            // When evaluator did not compute capacity (no required ΔV), validator must be silent.
            var bp    = new Blueprint("bp-skip", "Skip", payload: new PayloadDesign("p", bus: new DesignComponent("b", "bus")));
            var stats = BlueprintEvaluator.Evaluate(bp); // No requiredDeltaV → LaunchCapacity == null

            var context   = new ValidationContext(new MissionProfile("m", requiredDeltaV: 0.0));
            var validator = new PayloadCapacityValidator();
            var issues    = validator.Validate(bp, stats, context).ToList();

            Assert.AreEqual(0, issues.Count, "Validator must produce no issues when capacity is not available.");
        }

        [Test]
        public void PayloadCapacityValidator_WithinCapacity_ReturnsInfo()
        {
            var engine = new DesignComponent("eng", "engine", dryMass: 3.0, cost: 5.0, isp: 320.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 5.0, cost: 2.0);
            var bus    = new DesignComponent("bus",  "bus",   dryMass: 1.0, cost: 2.0);
            var stage  = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 100.0);
            var bp     = new Blueprint("bp-ok", "OK", payload: new PayloadDesign("p", bus: bus), stages: new[] { stage });

            // Small satellite: payload dry mass = 1 t, rocket should have much more capacity
            var stats   = BlueprintEvaluator.Evaluate(bp, lossProfile: InSpaceProfile, requiredDeltaV: 7000.0);
            var context = new ValidationContext(new MissionProfile("m", requiredDeltaV: 7000.0));

            var validator = new PayloadCapacityValidator();
            var issues    = validator.Validate(bp, stats, context).ToList();

            Assert.IsFalse(issues.Any(i => i.IsError), "Should produce no error when within capacity.");
            Assert.IsTrue(issues.Any(i => i.RuleId == ValidationRuleId.PayloadCapacity),
                "Should emit at least one capacity issue (Info or Warning).");
        }

        [Test]
        public void PayloadCapacityValidator_ExceedsCapacity_ReturnsError()
        {
            // Weak rocket: low propellant, low Isp
            var engine = new DesignComponent("eng", "engine", dryMass: 3.0, cost: 5.0, isp: 280.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 5.0, cost: 2.0);
            // Heavy spacecraft: 20 t dry mass
            var busHeavy = new DesignComponent("bus-h", "bus", dryMass: 20.0, cost: 10.0);
            var stage    = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 30.0);
            var bp       = new Blueprint("bp-over", "Overloaded", payload: new PayloadDesign("p", bus: busHeavy), stages: new[] { stage });

            // Required ΔV = 8 000 m/s; at 20 t payload the rocket will certainly not reach this
            var stats   = BlueprintEvaluator.Evaluate(bp, lossProfile: InSpaceProfile, requiredDeltaV: 8000.0);
            var context = new ValidationContext(new MissionProfile("m", requiredDeltaV: 8000.0));

            var validator = new PayloadCapacityValidator();
            var issues    = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.PayloadCapacity);
            Assert.IsNotNull(error, "Heavy payload beyond capacity must produce an error.");
            Assert.Greater(error.CurrentValue.Value, error.TargetValue.Value,
                "CurrentValue (actual) must exceed TargetValue (max capacity).");
            Assert.IsNotEmpty(error.Hint);
        }

        [Test]
        public void PayloadCapacityValidator_LowMargin_ReturnsWarning()
        {
            // Set up a rocket where the actual payload is just below the computed maximum.
            // We need: actual ≈ max * 0.96 (so margin ≈ 4% < 10% threshold → warning).

            var engine = new DesignComponent("eng", "engine", dryMass: 2.0, cost: 5.0, isp: 310.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 4.0, cost: 2.0);
            var stage  = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 60.0);

            // First, find the max payload with a small reference spacecraft
            var refBus = new DesignComponent("b", "bus", dryMass: 0.01);
            var refBp  = new Blueprint("ref", "Ref", payload: new PayloadDesign("p", bus: refBus), stages: new[] { stage });
            var refStats = BlueprintEvaluator.Evaluate(refBp, lossProfile: InSpaceProfile, requiredDeltaV: 8000.0);

            double maxCapacity = refStats.MaxPayloadCapacity;
            Assert.Greater(maxCapacity, 0.1, "Precondition: reference rocket must have positive capacity.");

            // Now build a spacecraft whose dry mass is ~97% of max capacity → margin ≈ 3%
            double targetPayload = maxCapacity * 0.97;
            var tightBus = new DesignComponent("b-tight", "bus", dryMass: targetPayload);
            var tightBp  = new Blueprint("bp-tight", "Tight",
                payload: new PayloadDesign("p", bus: tightBus), stages: new[] { stage });
            var tightStats = BlueprintEvaluator.Evaluate(tightBp, lossProfile: InSpaceProfile, requiredDeltaV: 8000.0);

            var context   = new ValidationContext(new MissionProfile("m", requiredDeltaV: 8000.0));
            var validator = new PayloadCapacityValidator();
            var issues    = validator.Validate(tightBp, tightStats, context).ToList();

            // With ~3% margin, we expect a Warning (threshold is 10%)
            var warning = issues.FirstOrDefault(i => i.IsWarning && i.RuleId == ValidationRuleId.PayloadCapacity);
            Assert.IsNotNull(warning,
                $"Payload at {targetPayload:0.###} t (≈97% of {maxCapacity:0.###} t max) should yield a warning. " +
                $"Actual margin: {tightStats.LaunchCapacity?.CapacityMargin:P2}");
        }

        [Test]
        public void PayloadCapacityValidator_RocketCannotMeetDvAtZeroPayload_ReturnsError()
        {
            // Tiny rocket that cannot even meet the ΔV with zero payload
            var engine = new DesignComponent("eng", "engine", dryMass: 1.0, cost: 1.0, isp: 250.0);
            var tank   = new DesignComponent("tank", "tank",  dryMass: 2.0, cost: 1.0);
            var bus    = new DesignComponent("bus",  "bus",   dryMass: 1.0, cost: 1.0);
            var stage  = new StageDesign("s1", engine: engine, tank: tank, propellantMass: 5.0);
            var bp     = new Blueprint("bp-tiny", "Tiny", payload: new PayloadDesign("p", bus: bus), stages: new[] { stage });

            // At zero payload ΔV ≈ 250 * 9.80665 * ln(8/3) ≈ 2416 m/s → far below 9400
            var stats   = BlueprintEvaluator.Evaluate(bp, lossProfile: LeoProfile, requiredDeltaV: 9400.0);
            var context = new ValidationContext(new MissionProfile("m", requiredDeltaV: 9400.0));

            var validator = new PayloadCapacityValidator();
            var issues    = validator.Validate(bp, stats, context).ToList();

            var error = issues.FirstOrDefault(i => i.IsError && i.RuleId == ValidationRuleId.PayloadCapacity);
            Assert.IsNotNull(error, "A rocket that cannot meet ΔV at zero payload must return an error.");
            Assert.IsNotEmpty(error.Hint);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 7. Argument Guard Tests
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void Calculator_NullStages_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                LaunchCapacityCalculator.Calculate(null, 9000.0, DeltaVLossProfile.None, 1.0));
        }

        [Test]
        public void Calculator_EmptyStages_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                LaunchCapacityCalculator.Calculate(new RocketStage[0], 9000.0, DeltaVLossProfile.None, 1.0));
        }

        [Test]
        public void Calculator_NullLossProfile_ThrowsArgumentNullException()
        {
            var stages = new[] { Stage("S1", 300.0, 5.0, 50.0) };
            Assert.Throws<ArgumentNullException>(() =>
                LaunchCapacityCalculator.Calculate(stages, 9000.0, null, 1.0));
        }

        [Test]
        public void Calculator_NegativeRequiredDv_ThrowsArgumentOutOfRangeException()
        {
            var stages = new[] { Stage("S1", 300.0, 5.0, 50.0) };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LaunchCapacityCalculator.Calculate(stages, -1.0, DeltaVLossProfile.None, 1.0));
        }

        [Test]
        public void Calculator_NegativeActualPayload_ThrowsArgumentOutOfRangeException()
        {
            var stages = new[] { Stage("S1", 300.0, 5.0, 50.0) };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LaunchCapacityCalculator.Calculate(stages, 8000.0, DeltaVLossProfile.None, actualPayloadMass: -1.0));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // 8. Multi-Stage Consistency
        // ══════════════════════════════════════════════════════════════════════════

        [Test]
        public void Calculator_TwoStages_MoreCapacityThanOneStage_SamePropellant()
        {
            // Two-stage rocket with the same total propellant achieves more ΔV than one-stage,
            // so it can carry more payload to the same required ΔV.
            var oneStage = new[] { Stage("S1", 310.0, 16.0, 120.0) }; // all propellant in one stage
            var twoStage = new[]
            {
                Stage("S1", 310.0, 12.0, 90.0),  // same total prop
                Stage("S2", 345.0, 4.0,  30.0)   // split across two stages
            };

            const double reqDv = 9000.0;

            var oneResult = LaunchCapacityCalculator.Calculate(oneStage, reqDv, InSpaceProfile, 1.0);
            var twoResult = LaunchCapacityCalculator.Calculate(twoStage, reqDv, InSpaceProfile, 1.0);

            Assert.Greater(twoResult.MaxPayloadMass, oneResult.MaxPayloadMass,
                "Two-stage rocket with stage separation advantage must deliver more payload than single stage.");
        }

        [Test]
        public void Calculator_HigherIsp_MoreCapacity()
        {
            // Higher Isp → more efficient → more payload capacity at same required ΔV.
            var lowIsp  = new[] { Stage("S1", 280.0, 10.0, 100.0) };
            var highIsp = new[] { Stage("S1", 350.0, 10.0, 100.0) };

            const double reqDv = 7000.0;

            var lowResult  = LaunchCapacityCalculator.Calculate(lowIsp,  reqDv, InSpaceProfile, 1.0);
            var highResult = LaunchCapacityCalculator.Calculate(highIsp, reqDv, InSpaceProfile, 1.0);

            Assert.Greater(highResult.MaxPayloadMass, lowResult.MaxPayloadMass,
                "Higher Isp engine must yield greater payload capacity for the same ΔV requirement.");
        }
    }
}
