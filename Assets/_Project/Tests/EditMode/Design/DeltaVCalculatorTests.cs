using System;
using System.Collections.Generic;
using NUnit.Framework;
using Apogee.Design;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="DeltaVCalculator"/>, <see cref="RocketStage"/>,
    /// <see cref="DeltaVLossProfile"/>, and <see cref="DeltaVCalculationResult"/>.
    ///
    /// GDD §20.1 — "Roket denklemi ve çok aşamalı Δv doğruluğu (bilinen örnek değerlerle)."
    /// GDD §20.5 — Test geçmedikçe görev tamamlanmış sayılmaz.
    ///
    /// Reference values are derived from the Tsiolkovsky equation:
    ///   Δv = Isp × 9.80665 × ln(wet / dry)
    /// using hand-calculated or well-known examples (Saturn V, Falcon 9 approximate).
    /// Tolerance: ±1 m/s for individual stage Δv (rounding from double precision is negligible).
    /// </summary>
    [TestFixture]
    public class DeltaVCalculatorTests
    {
        private const double G0 = DeltaVCalculator.G0; // 9.80665 m/s²
        private const double Tolerance = 1.0;          // m/s — accepted per GDD §8.5 simplification

        // ─── RocketStage construction ───────────────────────────────────────────────

        [Test]
        public void RocketStage_ValidConstruction_StoresProperties()
        {
            var stage = new RocketStage("S1", isp: 311.0, dryMass: 25.0, wetMass: 400.0);
            Assert.AreEqual("S1",  stage.Id);
            Assert.AreEqual(311.0, stage.Isp);
            Assert.AreEqual(25.0,  stage.DryMass);
            Assert.AreEqual(400.0, stage.WetMass);
            Assert.AreEqual(375.0, stage.PropellantMass, 1e-9);
        }

        [Test]
        public void RocketStage_WetMassEqualsDryMass_ZeroPropellant_Allowed()
        {
            // Edge case: stage with no propellant (structural only, 0 Δv).
            var stage = new RocketStage("S0", isp: 300.0, dryMass: 5.0, wetMass: 5.0);
            Assert.AreEqual(0.0, stage.PropellantMass, 1e-9);
        }

        [Test]
        public void RocketStage_InvalidIsp_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RocketStage("S1", isp: 0.0, dryMass: 10.0, wetMass: 20.0));
        }

        [Test]
        public void RocketStage_WetLessThanDry_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 10.0));
        }

        [Test]
        public void RocketStage_EmptyId_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new RocketStage("", isp: 300.0, dryMass: 10.0, wetMass: 20.0));
        }

        // ─── Single-stage Δv ─────────────────────────────────────────────────────

        [Test]
        public void Calculate_SingleStage_NoPayload_NoLosses_MatchesTsiolkovsky()
        {
            // Isp=311 s, dry=25t, wet=400t — approximate Falcon 9 first stage character.
            // Expected: 311 × 9.80665 × ln(400/25) = 311 × 9.80665 × ln(16)
            //         = 311 × 9.80665 × 2.77259 = 8 440 m/s  (approx.)
            double isp     = 311.0;
            double dryMass = 25.0;
            double wetMass = 400.0;
            double expected = isp * G0 * Math.Log(wetMass / dryMass);

            var stage = new RocketStage("S1", isp, dryMass, wetMass);
            var result = DeltaVCalculator.Calculate(new[] { stage }, payloadMass: 0.0);

            Assert.AreEqual(expected, result.TotalIdealDeltaV, Tolerance,
                $"Single-stage ideal Δv should be {expected:0.#} m/s");
            Assert.AreEqual(0.0, result.TotalLosses, 1e-9);
            Assert.AreEqual(expected, result.NetDeltaV, Tolerance);
        }

        [Test]
        public void Calculate_SingleStage_WithPayload_CorrectMassRatio()
        {
            // Payload adds to the dry mass side at burnout (must be lifted).
            // Stage: Isp=450, dry=10t, wet=110t  → propellant=100t
            // Payload=5t
            // effective wet = 110+5 = 115t, effective dry = 10+5 = 15t
            // ratio = 115/15 ≈ 7.667
            // Δv = 450 × 9.80665 × ln(7.667) ≈ 450 × 9.80665 × 2.0369 ≈ 8 981 m/s
            double isp     = 450.0;
            double dryMass = 10.0;
            double wetMass = 110.0;
            double payload = 5.0;
            double ratio    = (wetMass + payload) / (dryMass + payload);
            double expected = isp * G0 * Math.Log(ratio);

            var stage = new RocketStage("Upper", isp, dryMass, wetMass);
            var result = DeltaVCalculator.Calculate(new[] { stage }, payload, DeltaVLossProfile.None);

            Assert.AreEqual(1, result.StageCount);
            Assert.AreEqual(expected, result.TotalIdealDeltaV, Tolerance);
            Assert.AreEqual(ratio,    result.Stages[0].MassRatio, 1e-6,
                "Mass ratio should account for payload.");
        }

        [Test]
        public void Calculate_SingleStage_ZeroPropellant_ProducesZeroDeltaV()
        {
            // wet == dry → ratio = 1 → ln(1) = 0 → Δv = 0.
            var stage = new RocketStage("Empty", isp: 300.0, dryMass: 10.0, wetMass: 10.0);
            var result = DeltaVCalculator.Calculate(new[] { stage }, payloadMass: 0.0);
            Assert.AreEqual(0.0, result.TotalIdealDeltaV, 1e-9, "Zero propellant → zero Δv.");
        }

        // ─── Two-stage Δv ─────────────────────────────────────────────────────────

        [Test]
        public void Calculate_TwoStage_NoPayload_NoLosses_SumsCorrectly()
        {
            // Stage 1 (booster): Isp=300s, dry=20t, wet=200t
            // Stage 2 (upper):   Isp=450s, dry=5t,  wet=50t
            // Payload: 0t
            //
            // Stage-1 effective: wet = 200+50 = 250t, dry = 20+50 = 70t
            //   Δv₁ = 300 × 9.80665 × ln(250/70) = 300 × 9.80665 × 1.2730 ≈ 3 742 m/s
            // Stage-2 effective: wet = 50t, dry = 5t  (payload=0)
            //   Δv₂ = 450 × 9.80665 × ln(50/5) = 450 × 9.80665 × 2.3026 ≈ 10 153 m/s
            // Total ≈ 13 895 m/s

            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("S2", isp: 450.0, dryMass: 5.0,  wetMass: 50.0);

            double dv1 = 300.0 * G0 * Math.Log((200.0 + 50.0) / (20.0 + 50.0));
            double dv2 = 450.0 * G0 * Math.Log(50.0 / 5.0);
            double expected = dv1 + dv2;

            var result = DeltaVCalculator.Calculate(new[] { s1, s2 }, payloadMass: 0.0);

            Assert.AreEqual(2,        result.StageCount);
            Assert.AreEqual(expected, result.TotalIdealDeltaV, Tolerance,
                $"Two-stage ideal Δv should be {expected:0.#} m/s");
            Assert.AreEqual(dv1, result.Stages[0].IdealDeltaV, Tolerance, "Stage-1 Δv mismatch");
            Assert.AreEqual(dv2, result.Stages[1].IdealDeltaV, Tolerance, "Stage-2 Δv mismatch");
        }

        [Test]
        public void Calculate_TwoStage_WithPayload_StageOrderMatters()
        {
            // Validates that the calculator correctly stacks payload above upper stage,
            // and that reversing stage order (wrong configuration) changes the result.
            var s1 = new RocketStage("Booster", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("Upper",   isp: 450.0, dryMass: 5.0,  wetMass: 50.0);
            double payload = 10.0;

            var resultCorrect  = DeltaVCalculator.Calculate(new[] { s1, s2 }, payload);
            var resultReversed = DeltaVCalculator.Calculate(new[] { s2, s1 }, payload);

            Assert.AreNotEqual(resultCorrect.NetDeltaV, resultReversed.NetDeltaV,
                "Reversing stage order must produce a different Δv (staging order is significant).");
        }

        // ─── Three-stage Δv ───────────────────────────────────────────────────────

        [Test]
        public void Calculate_ThreeStage_WithPayload_NoLosses_MatchesExpected()
        {
            // Saturn V-inspired (simplified, not exact):
            //   S1 (S-IC):  Isp=263s, dry=130t, wet=2 300t
            //   S2 (S-II):  Isp=421s, dry=40t,  wet=490t
            //   S3 (S-IVB): Isp=421s, dry=13t,  wet=119t
            //   Payload (CSM+LM): 45t
            //
            // Suffix wet masses:
            //   above S1 = 490 + 119 + 45 = 654t  → S1 wet_eff=2300+654=2954, dry_eff=130+654=784
            //   above S2 = 119 + 45 = 164t         → S2 wet_eff=490+164=654,  dry_eff=40+164=204
            //   above S3 = 45t                      → S3 wet_eff=119+45=164,  dry_eff=13+45=58
            //
            // Δv₁ = 263 × 9.80665 × ln(2954/784) ≈ 263 × 9.80665 × 1.3261 ≈ 3 417 m/s
            // Δv₂ = 421 × 9.80665 × ln(654/204)  ≈ 421 × 9.80665 × 1.1659 ≈ 4 815 m/s
            // Δv₃ = 421 × 9.80665 × ln(164/58)   ≈ 421 × 9.80665 × 1.0395 ≈ 4 293 m/s
            // Total ≈ 12 525 m/s  (rough — simplified masses, not exact Saturn V)

            var s1 = new RocketStage("S-IC",  isp: 263.0, dryMass: 130.0, wetMass: 2300.0);
            var s2 = new RocketStage("S-II",  isp: 421.0, dryMass: 40.0,  wetMass: 490.0);
            var s3 = new RocketStage("S-IVB", isp: 421.0, dryMass: 13.0,  wetMass: 119.0);
            double payload = 45.0;

            double dv1 = 263.0 * G0 * Math.Log((2300.0 + 490.0 + 119.0 + payload) / (130.0 + 490.0 + 119.0 + payload));
            double dv2 = 421.0 * G0 * Math.Log((490.0  + 119.0 + payload)          / (40.0  + 119.0 + payload));
            double dv3 = 421.0 * G0 * Math.Log((119.0  + payload)                  / (13.0  + payload));
            double expected = dv1 + dv2 + dv3;

            var result = DeltaVCalculator.Calculate(new[] { s1, s2, s3 }, payload, DeltaVLossProfile.None);

            Assert.AreEqual(3,        result.StageCount);
            Assert.AreEqual(expected, result.TotalIdealDeltaV, Tolerance,
                $"Three-stage Saturn-V-like ideal Δv should be {expected:0.#} m/s");
            Assert.AreEqual(dv1, result.Stages[0].IdealDeltaV, Tolerance, "S-IC Δv mismatch");
            Assert.AreEqual(dv2, result.Stages[1].IdealDeltaV, Tolerance, "S-II Δv mismatch");
            Assert.AreEqual(dv3, result.Stages[2].IdealDeltaV, Tolerance, "S-IVB Δv mismatch");
        }

        // ─── Loss profile application ─────────────────────────────────────────────

        [Test]
        public void Calculate_WithLossProfile_ReducesNetDeltaV()
        {
            var s1 = new RocketStage("S1", isp: 311.0, dryMass: 25.0, wetMass: 400.0);
            var lossProfile = new DeltaVLossProfile("Earth LEO test", stage1LossMetersPerSecond: 1500.0);

            var resultNoLoss = DeltaVCalculator.Calculate(new[] { s1 }, 0.0, DeltaVLossProfile.None);
            var resultLoss   = DeltaVCalculator.Calculate(new[] { s1 }, 0.0, lossProfile);

            Assert.AreEqual(1500.0, resultLoss.TotalLosses, 1e-9,
                "Loss profile should apply 1500 m/s to Stage-1.");
            Assert.AreEqual(resultNoLoss.TotalIdealDeltaV - 1500.0, resultLoss.NetDeltaV, Tolerance,
                "Net Δv = ideal − losses.");
            Assert.AreEqual(resultNoLoss.TotalIdealDeltaV, resultLoss.TotalIdealDeltaV, Tolerance,
                "Ideal Δv is not affected by losses — losses are a separate accounting.");
        }

        [Test]
        public void Calculate_TwoStage_LossesAppliedOnlyToStage1()
        {
            // Confirm that Stage-2 receives zero losses even with a loss profile.
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("S2", isp: 450.0, dryMass: 5.0,  wetMass: 50.0);
            var profile = new DeltaVLossProfile("Test", stage1LossMetersPerSecond: 1200.0);

            var result = DeltaVCalculator.Calculate(new[] { s1, s2 }, 0.0, profile);

            Assert.AreEqual(1200.0, result.Stages[0].LossesApplied, 1e-9, "Stage-1 losses should be 1200.");
            Assert.AreEqual(0.0,    result.Stages[1].LossesApplied, 1e-9, "Stage-2 losses should be 0.");
            Assert.AreEqual(1200.0, result.TotalLosses, 1e-9);
        }

        // ─── Mass accounting ──────────────────────────────────────────────────────

        [Test]
        public void Calculate_TotalWetMass_EqualsFullLaunchMass()
        {
            // Total wet = wet_S1 + wet_S2 + payload (entire stack at ignition).
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("S2", isp: 450.0, dryMass: 5.0,  wetMass: 50.0);
            double payload = 10.0;

            var result = DeltaVCalculator.Calculate(new[] { s1, s2 }, payload);

            Assert.AreEqual(200.0 + 50.0 + 10.0, result.TotalWetMass, 1e-9,
                "Total wet mass = all stage wet masses + payload.");
        }

        [Test]
        public void Calculate_TotalDryMass_IncludesPayload()
        {
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("S2", isp: 450.0, dryMass: 5.0,  wetMass: 50.0);
            double payload = 10.0;

            var result = DeltaVCalculator.Calculate(new[] { s1, s2 }, payload);

            Assert.AreEqual(20.0 + 5.0 + 10.0, result.TotalDryMass, 1e-9,
                "Total dry mass = stage dry masses + payload.");
            Assert.AreEqual(result.TotalWetMass - result.TotalDryMass, result.TotalPropellantMass, 1e-9);
        }

        // ─── Guard / validation ───────────────────────────────────────────────────

        [Test]
        public void Calculate_EmptyStageList_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                DeltaVCalculator.Calculate(new RocketStage[0], 0.0, DeltaVLossProfile.None));
        }

        [Test]
        public void Calculate_NegativePayload_Throws()
        {
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 10.0, wetMass: 100.0);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DeltaVCalculator.Calculate(new[] { s1 }, payloadMass: -1.0, DeltaVLossProfile.None));
        }

        [Test]
        public void Calculate_NullStages_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DeltaVCalculator.Calculate(null, 0.0, DeltaVLossProfile.None));
        }

        [Test]
        public void Calculate_NullLossProfile_Throws()
        {
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 10.0, wetMass: 100.0);
            Assert.Throws<ArgumentNullException>(() =>
                DeltaVCalculator.Calculate(new[] { s1 }, 0.0, null));
        }

        // ─── Static presets ───────────────────────────────────────────────────────

        [Test]
        public void DeltaVLossProfile_None_HasZeroLosses()
        {
            Assert.AreEqual(0.0, DeltaVLossProfile.None.Stage1LossMetersPerSecond, 1e-9);
            Assert.AreEqual(0.0, DeltaVLossProfile.None.LatitudeAdvantageMetersPerSecond, 1e-9);
        }

        [Test]
        public void DeltaVLossProfile_EarthToLeo_HasExpectedLosses()
        {
            Assert.AreEqual(1500.0, DeltaVLossProfile.EarthToLeo.Stage1LossMetersPerSecond, 1e-9);
        }

        [Test]
        public void DeltaVLossProfile_NegativeLoss_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DeltaVLossProfile("Bad", stage1LossMetersPerSecond: -100.0));
        }

        // ─── Convenience overload ─────────────────────────────────────────────────

        [Test]
        public void Calculate_ConvenienceOverload_NoLoss_MatchesExplicitNone()
        {
            var s1 = new RocketStage("S1", isp: 311.0, dryMass: 25.0, wetMass: 400.0);
            var r1 = DeltaVCalculator.Calculate(new[] { s1 }, 5.0);
            var r2 = DeltaVCalculator.Calculate(new[] { s1 }, 5.0, DeltaVLossProfile.None);

            Assert.AreEqual(r1.NetDeltaV, r2.NetDeltaV, 1e-9);
        }

        // ─── Determinism ─────────────────────────────────────────────────────────

        [Test]
        public void Calculate_SameInputs_AlwaysSameOutput()
        {
            // GDD §8.4: Deterministic — same design → same result.
            var s1 = new RocketStage("S1", isp: 300.0, dryMass: 20.0, wetMass: 200.0);
            var s2 = new RocketStage("S2", isp: 450.0, dryMass: 5.0,  wetMass: 50.0);
            var profile = DeltaVLossProfile.EarthToLeo;

            var r1 = DeltaVCalculator.Calculate(new[] { s1, s2 }, 10.0, profile);
            var r2 = DeltaVCalculator.Calculate(new[] { s1, s2 }, 10.0, profile);

            Assert.AreEqual(r1.NetDeltaV,        r2.NetDeltaV,        1e-9);
            Assert.AreEqual(r1.TotalIdealDeltaV, r2.TotalIdealDeltaV, 1e-9);
            Assert.AreEqual(r1.TotalLosses,      r2.TotalLosses,      1e-9);
        }
    }
}
