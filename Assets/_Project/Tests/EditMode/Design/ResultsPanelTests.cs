using System.Collections.Generic;
using System.Linq;
using Apogee.Design;
using NUnit.Framework;

namespace Apogee.Tests.EditMode.Design
{
    /// <summary>
    /// EditMode tests for M2-04 Results Panel (GDD §8.3, §7.4).
    ///
    /// Coverage:
    ///   - ResultBarViewModel: fill clamping, factory methods, status mapping
    ///   - ResultsPanelViewModel: bar generation from BlueprintStats (sans DeltaVResult)
    ///   - Integration: validator findings drive correct bar status from full Blueprint
    ///
    /// Pure C#, no UnityEngine references.
    /// </summary>
    [TestFixture]
    public class ResultsPanelTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a minimal <see cref="BlueprintStats"/> with no DeltaVResult.
        /// Useful for stat-only bar tests that don't require Δv calculation.
        /// </summary>
        private static BlueprintStats BuildStatsOnly(
            double wetMass      = 50,
            double reliability  = 0.96,
            double powerGen     = 2.0,
            double powerCons    = 1.0,
            double thermalDiss  = 1.5,
            double thermalLoad  = 1.0,
            double cost         = 45.0,
            int    buildTime    = 180)
        {
            var sc = new StatCollection();
            sc.SetBaseValue(StatType.WetMass,            wetMass);
            sc.SetBaseValue(StatType.DryMass,            wetMass * 0.3);
            sc.SetBaseValue(StatType.PropellantMass,     wetMass * 0.7);
            sc.SetBaseValue(StatType.Reliability,        reliability);
            sc.SetBaseValue(StatType.PowerGeneration,    powerGen);
            sc.SetBaseValue(StatType.PowerConsumption,   powerCons);
            sc.SetBaseValue(StatType.PowerMargin,        powerGen - powerCons);
            sc.SetBaseValue(StatType.ThermalDissipation, thermalDiss);
            sc.SetBaseValue(StatType.ThermalLoad,        thermalLoad);
            sc.SetBaseValue(StatType.ThermalMargin,      thermalDiss - thermalLoad);
            sc.SetBaseValue(StatType.Cost,               cost);
            sc.SetBaseValue(StatType.BuildTime,          buildTime);

            return new BlueprintStats(sc, deltaVResult: null, launchCapacity: null);
        }

        // ── ResultBarViewModel unit tests ──────────────────────────────────────────

        [Test]
        public void ResultBarViewModel_FillRatio_ClampedToZeroOne()
        {
            var bar = new ResultBarViewModel("k", "Label");

            bar.FillRatio = -0.5f;
            Assert.AreEqual(0f, bar.FillRatio, 1e-6f, "Negative fill should clamp to 0.");

            bar.FillRatio = 1.8f;
            Assert.AreEqual(1f, bar.FillRatio, 1e-6f, "Fill > 1 should clamp to 1.");

            bar.FillRatio = 0.6f;
            Assert.AreEqual(0.6f, bar.FillRatio, 1e-4f, "Valid fill in [0,1] should be unchanged.");
        }

        [Test]
        public void ResultBarViewModel_FactoryMethods_SetCorrectStatus()
        {
            var good = ResultBarViewModel.Good("k", "L", 0.9f, "val");
            var warn = ResultBarViewModel.Warn("k", "L", 0.5f, "val");
            var err  = ResultBarViewModel.Error("k", "L", 0.3f, "val");

            Assert.AreEqual(ResultBarStatus.Good,    good.Status);
            Assert.AreEqual(ResultBarStatus.Warning, warn.Status);
            Assert.AreEqual(ResultBarStatus.Error,   err.Status);
        }

        [Test]
        public void ResultBarViewModel_KeyLabel_StoredCorrectly()
        {
            var bar = new ResultBarViewModel("delta_v", "Menzil (Δv)");
            Assert.AreEqual("delta_v",      bar.Key);
            Assert.AreEqual("Menzil (Δv)", bar.Label);
        }

        [Test]
        public void ResultBarViewModel_EmptyKey_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => new ResultBarViewModel("", "Label"));
        }

        [Test]
        public void ResultBarViewModel_EmptyLabel_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => new ResultBarViewModel("key", ""));
        }

        // ── ResultsPanelViewModel — bar generation ─────────────────────────────────

        [Test]
        public void ResultsPanelViewModel_Build_ReturnsSevenBars()
        {
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);

            Assert.AreEqual(7, bars.Count, "Must produce exactly 7 bars.");
        }

        [Test]
        public void ResultsPanelViewModel_Build_AllBarsHaveNonEmptyKey()
        {
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);

            foreach (var bar in bars)
            {
                Assert.IsFalse(string.IsNullOrEmpty(bar.Key),   $"Bar key must not be empty. Label={bar.Label}");
                Assert.IsFalse(string.IsNullOrEmpty(bar.Label), $"Bar label must not be empty. Key={bar.Key}");
            }
        }

        [Test]
        public void ResultsPanelViewModel_Build_AllFillRatiosInRange()
        {
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);

            foreach (var bar in bars)
            {
                Assert.GreaterOrEqual(bar.FillRatio, 0f, $"Bar '{bar.Key}' fill < 0.");
                Assert.LessOrEqual(bar.FillRatio,    1f, $"Bar '{bar.Key}' fill > 1.");
            }
        }

        [Test]
        public void ResultsPanelViewModel_Build_AllBarsHaveNonEmptyValueText()
        {
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);

            foreach (var bar in bars)
            {
                Assert.IsFalse(string.IsNullOrEmpty(bar.ValueText),
                    $"Bar '{bar.Key}' must have a non-empty ValueText (GDD §8.3 — outcome shown to player).");
            }
        }

        [Test]
        public void ResultsPanelViewModel_Build_AllBarsHaveReasonText()
        {
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);

            foreach (var bar in bars)
            {
                Assert.IsFalse(string.IsNullOrEmpty(bar.ReasonText),
                    $"Bar '{bar.Key}' must have a non-empty ReasonText (GDD §13.2 — 'why?' tooltip).");
            }
        }

        [Test]
        public void ResultsPanelViewModel_DeltaVBar_NeutralWhenNoDeltaVResult()
        {
            // No DeltaVResult → no required value → should show raw value, not Error
            var stats = BuildStatsOnly();
            var bars  = ResultsPanelViewModel.Build(stats);
            var dvBar = bars.FirstOrDefault(b => b.Key == "delta_v");

            Assert.IsNotNull(dvBar, "delta_v bar must exist.");
            // With no required dv, it should be Neutral or Good (not Error)
            Assert.AreNotEqual(ResultBarStatus.Error, dvBar.Status,
                "With no required Δv, bar should not be Error.");
        }

        [Test]
        public void ResultsPanelViewModel_ReliabilityBar_GoodAboveThreshold()
        {
            var stats = BuildStatsOnly(reliability: 0.96);
            var bars  = ResultsPanelViewModel.Build(stats);
            var relBar = bars.FirstOrDefault(b => b.Key == "reliability");

            Assert.IsNotNull(relBar);
            Assert.AreEqual(ResultBarStatus.Good, relBar.Status, "Reliability 0.96 should be Good.");
        }

        [Test]
        public void ResultsPanelViewModel_ReliabilityBar_WarnInMiddleRange()
        {
            var stats = BuildStatsOnly(reliability: 0.88);
            var bars  = ResultsPanelViewModel.Build(stats);
            var relBar = bars.FirstOrDefault(b => b.Key == "reliability");

            Assert.IsNotNull(relBar);
            Assert.AreEqual(ResultBarStatus.Warning, relBar.Status, "Reliability 0.88 (< 0.92, ≥ 0.85) should be Warning.");
        }

        [Test]
        public void ResultsPanelViewModel_ReliabilityBar_ErrorWhenTooLow()
        {
            var stats = BuildStatsOnly(reliability: 0.80);
            var bars  = ResultsPanelViewModel.Build(stats);
            var relBar = bars.FirstOrDefault(b => b.Key == "reliability");

            Assert.IsNotNull(relBar);
            Assert.AreEqual(ResultBarStatus.Error, relBar.Status, "Reliability < 0.85 should be Error.");
        }

        [Test]
        public void ResultsPanelViewModel_PowerBar_GoodWithPositiveMargin()
        {
            var stats = BuildStatsOnly(powerGen: 2.0, powerCons: 1.0);
            var bars  = ResultsPanelViewModel.Build(stats);
            var pBar  = bars.FirstOrDefault(b => b.Key == "power");

            Assert.IsNotNull(pBar);
            Assert.AreEqual(ResultBarStatus.Good, pBar.Status, "Power margin > 10% of gen should be Good.");
        }

        [Test]
        public void ResultsPanelViewModel_PowerBar_ErrorWhenDeficit()
        {
            // gen=0.5, cons=1.5 → margin = -1.0 → Error
            var stats = BuildStatsOnly(powerGen: 0.5, powerCons: 1.5);
            var bars  = ResultsPanelViewModel.Build(stats);
            var pBar  = bars.FirstOrDefault(b => b.Key == "power");

            Assert.IsNotNull(pBar);
            Assert.AreEqual(ResultBarStatus.Error, pBar.Status, "Power deficit (margin < 0) should be Error.");
        }

        [Test]
        public void ResultsPanelViewModel_ThermalBar_GoodWhenDissipationSufficient()
        {
            var stats = BuildStatsOnly(thermalDiss: 1.5, thermalLoad: 1.0);
            var bars  = ResultsPanelViewModel.Build(stats);
            var tBar  = bars.FirstOrDefault(b => b.Key == "thermal");

            Assert.IsNotNull(tBar);
            Assert.AreEqual(ResultBarStatus.Good, tBar.Status, "Thermal margin 50% should be Good.");
        }

        [Test]
        public void ResultsPanelViewModel_ThermalBar_ErrorWhenDissipationInsufficient()
        {
            // diss=0.3, load=1.5 → margin = -1.2 → Error
            var stats = BuildStatsOnly(thermalDiss: 0.3, thermalLoad: 1.5);
            var bars  = ResultsPanelViewModel.Build(stats);
            var tBar  = bars.FirstOrDefault(b => b.Key == "thermal");

            Assert.IsNotNull(tBar);
            Assert.AreEqual(ResultBarStatus.Error, tBar.Status, "Thermal dissipation < load should be Error.");
        }

        [Test]
        public void ResultsPanelViewModel_CostBar_GoodBelowSoftCap()
        {
            var stats = BuildStatsOnly(cost: 40.0);
            var bars  = ResultsPanelViewModel.Build(stats);
            var cBar  = bars.FirstOrDefault(b => b.Key == "cost");

            Assert.IsNotNull(cBar);
            Assert.AreEqual(ResultBarStatus.Good, cBar.Status, "Cost $40M (well below $200M cap) should be Good.");
        }

        [Test]
        public void ResultsPanelViewModel_BuildTimeBar_GoodUnder730Days()
        {
            var stats = BuildStatsOnly(buildTime: 180);
            var bars  = ResultsPanelViewModel.Build(stats);
            var btBar = bars.FirstOrDefault(b => b.Key == "build_time");

            Assert.IsNotNull(btBar);
            Assert.AreEqual(ResultBarStatus.Good, btBar.Status, "Build time 180 days should be Good.");
        }

        [Test]
        public void ResultsPanelViewModel_NullStats_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => ResultsPanelViewModel.Build((BlueprintStats)null));
        }

        [Test]
        public void ResultsPanelViewModel_NullReport_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => ResultsPanelViewModel.Build((ValidationReport)null));
        }

        // ── Integration: full Blueprint → ValidationReport → ResultBars ────────────

        [Test]
        public void ResultsPanelViewModel_FromReport_ReflectsValidatorDeltaVError()
        {
            var catalog   = MinimalTestFixtures.BuildCatalog();
            var blueprint = MinimalTestFixtures.BuildBlueprint(catalog);

            // Mission requires 50 000 m/s — impossibly high → DeltaVValidator must fire Error
            var context = new ValidationContext(
                missionProfile: new MissionProfile(
                    id: "test-impossible",
                    requiredDeltaV: 50_000));

            var report = BlueprintValidator.Validate(blueprint, context);
            var bars   = ResultsPanelViewModel.Build(report);
            var dvBar  = bars.FirstOrDefault(b => b.Key == "delta_v");

            Assert.IsNotNull(dvBar, "delta_v bar must exist.");
            Assert.AreEqual(ResultBarStatus.Error, dvBar.Status,
                "delta_v bar should be Error when DeltaVValidator fires an error.");
        }

        [Test]
        public void ResultsPanelViewModel_FromReport_NoErrorsOnTrivialMission()
        {
            var catalog   = MinimalTestFixtures.BuildCatalog();
            var blueprint = MinimalTestFixtures.BuildBlueprint(catalog);

            // Trivially small required Δv → no validator should fire errors
            var context = new ValidationContext(
                missionProfile: new MissionProfile(
                    id: "test-easy",
                    requiredDeltaV: 100));

            var report  = BlueprintValidator.Validate(blueprint, context);
            var bars    = ResultsPanelViewModel.Build(report);
            var errBars = bars.Where(b => b.Status == ResultBarStatus.Error).ToList();

            Assert.AreEqual(0, errBars.Count,
                $"Trivial mission should produce no Error bars. Got errors on: {string.Join(", ", errBars.Select(b => b.Key))}");
        }

        [Test]
        public void ResultsPanelViewModel_FromReport_ContainsSevenBars()
        {
            var catalog   = MinimalTestFixtures.BuildCatalog();
            var blueprint = MinimalTestFixtures.BuildBlueprint(catalog);
            var context   = new ValidationContext();
            var report    = BlueprintValidator.Validate(blueprint, context);
            var bars      = ResultsPanelViewModel.Build(report);

            Assert.AreEqual(7, bars.Count, "Report-based build must also produce 7 bars.");
        }
    }

    // ── Minimal test fixtures ──────────────────────────────────────────────────

    /// <summary>
    /// Shared minimal blueprint fixtures for M2-04 tests.
    /// </summary>
    internal static class MinimalTestFixtures
    {
        public static List<DesignComponent> BuildCatalog()
        {
            return new List<DesignComponent>
            {
                new DesignComponent("engine-test", "engine", "Test Motor",
                    dryMass: 1.0, cost: 5.0, buildTime: 30, reliability: 0.95, isp: 310, thrust: 800),
                new DesignComponent("tank-test", "tank", "Test Tankı",
                    dryMass: 1.5, cost: 2.0, buildTime: 20, reliability: 0.99),
                new DesignComponent("bus-test", "bus", "Test Bus",
                    dryMass: 0.2, cost: 4.0, buildTime: 15, reliability: 0.97,
                    powerGeneration: 1.5, powerConsumption: 0.5),
                new DesignComponent("edl-test", "edl", "Test EDL",
                    dryMass: 0.5, cost: 3.0, buildTime: 20, reliability: 0.92),
            };
        }

        public static Blueprint BuildBlueprint(List<DesignComponent> catalog)
        {
            var engine = catalog.First(c => c.Id == "engine-test");
            var tank   = catalog.First(c => c.Id == "tank-test");
            var bus    = catalog.First(c => c.Id == "bus-test");
            var edl    = catalog.First(c => c.Id == "edl-test");

            var stage1 = new StageDesign("stage-1", "Aşama 1", engine, tank, propellantMass: 50.0);

            var payload = new PayloadDesign(
                "payload-test", "Test Yükü",
                bus:         bus,
                power:       null,
                thermal:     null,
                comms:       null,
                adcs:        null,
                edl:         edl,
                instruments: null);

            return new Blueprint(
                "blueprint-test", "Test Blueprint", version: 1,
                targetDestinationId: "moon", targetMissionTypeId: "rover",
                payload: payload, stages: new[] { stage1 });
        }
    }
}
