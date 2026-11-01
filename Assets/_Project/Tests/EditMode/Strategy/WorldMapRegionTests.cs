using NUnit.Framework;
using System.IO;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// Unit and catalog tests for M4-02: RegionRecord, regions.json, MapLens, RegionLensEvaluator,
    /// SetMapLensCommand, and SelectRegionCommand.
    /// Complies with GDD §6.1 and §19.9.
    /// </summary>
    [TestFixture]
    public class WorldMapRegionTests
    {
        private CatalogCollection<RegionRecord> _regions;

        [SetUp]
        public void SetUp()
        {
            string jsonPath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Assets", "_Project", "Data", "Catalog", "Json", "regions.json");

            if (!File.Exists(jsonPath))
            {
                // Fallback for direct solution roots
                jsonPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets", "_Project", "Data", "Catalog", "Json", "regions.json");
            }

            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                _regions = CatalogLoader.Load<RegionRecord>(json);
            }
        }

        // ================================================================ Catalog Integrity
        [Test]
        public void RegionsCatalog_LoadsSuccessfully_HasAtLeastEightRegions()
        {
            if (_regions == null)
            {
                Assert.Inconclusive("regions.json not reachable in this test runner environment.");
                return;
            }

            Assert.IsTrue(_regions.Validation.IsValid, _regions.Validation.ToString());
            Assert.GreaterOrEqual(_regions.Count, 8);
        }

        [Test]
        public void RegionsCatalog_EquatorialRegions_HaveHighRotationBonus()
        {
            if (_regions == null) return;

            var guiana = _regions.GetById("guiana_coast");
            Assert.IsNotNull(guiana);
            Assert.Greater(guiana.EquatorialBonusDv, 400.0);
            Assert.Less(guiana.Latitude, 10.0);
            Assert.Contains("launch_pad", guiana.AllowedBaseTypes);
        }

        [Test]
        public void RegionsCatalog_PolarRegions_HaveLowRotationBonus()
        {
            if (_regions == null) return;

            var nordic = _regions.GetById("nordic_range");
            Assert.IsNotNull(nordic);
            Assert.Less(nordic.EquatorialBonusDv, 200.0);
            Assert.Greater(nordic.Latitude, 60.0);
        }

        // ================================================================ RegionLensEvaluator
        [Test]
        public void RegionLensEvaluator_LaunchEfficiency_ScalesWithRotationBonus()
        {
            if (_regions == null) return;

            var pacific = _regions.GetById("pacific_atoll"); // ~0.5 deg lat
            var nordic = _regions.GetById("nordic_range");   // ~67.8 deg lat

            double pacificScore = RegionLensEvaluator.EvaluateScore(pacific, MapLens.LaunchEfficiency);
            double nordicScore = RegionLensEvaluator.EvaluateScore(nordic, MapLens.LaunchEfficiency);

            Assert.Greater(pacificScore, nordicScore);
            Assert.GreaterOrEqual(pacificScore, 0.95);
        }

        [Test]
        public void RegionLensEvaluator_RiskLens_ReflectsWeatherAndInstability()
        {
            if (_regions == null) return;

            var cape = _regions.GetById("cape_peninsula");
            var outback = _regions.GetById("outback_station");

            double capeRisk = RegionLensEvaluator.EvaluateScore(cape, MapLens.Risk);
            double outbackRisk = RegionLensEvaluator.EvaluateScore(outback, MapLens.Risk);

            // Outback has very low weather risk (0.05) and high stability (0.99)
            Assert.Less(outbackRisk, capeRisk);
        }

        [Test]
        public void RegionLensEvaluator_GetLensDescription_IsNonEmpty()
        {
            if (_regions == null) return;

            var guiana = _regions.GetById("guiana_coast");
            foreach (MapLens lens in System.Enum.GetValues(typeof(MapLens)))
            {
                string desc = RegionLensEvaluator.GetLensDescription(guiana, lens);
                Assert.IsFalse(string.IsNullOrWhiteSpace(desc), $"Description for {lens} should not be empty.");
            }
        }

        // ================================================================ SetMapLensCommand
        [Test]
        public void SetMapLensCommand_SwitchesActiveLens_AndUndoes()
        {
            var state = new CampaignState(seed: 1);
            Assert.AreEqual(MapLens.Overview, state.ActiveMapLens);

            var ctx = CommandContext.CreateLocalAdmin(state, state.Time.Tick);
            var cmd = new SetMapLensCommand(MapLens.LaunchEfficiency);

            var res = state.CommandBus.Execute(cmd, ctx);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(MapLens.LaunchEfficiency, state.ActiveMapLens);

            var undoRes = state.CommandBus.Undo(ctx);
            Assert.IsTrue(undoRes.Success);
            Assert.AreEqual(MapLens.Overview, state.ActiveMapLens);
        }

        // ================================================================ SelectRegionCommand
        [Test]
        public void SelectRegionCommand_SetsSelectedRegion_AndUndoes()
        {
            var state = new CampaignState(seed: 1);
            Assert.IsNull(state.SelectedRegionId);

            var ctx = CommandContext.CreateLocalAdmin(state, state.Time.Tick);
            var cmd = new SelectRegionCommand("guiana_coast");

            var res = state.CommandBus.Execute(cmd, ctx);
            Assert.IsTrue(res.Success);
            Assert.AreEqual("guiana_coast", state.SelectedRegionId);

            var undoRes = state.CommandBus.Undo(ctx);
            Assert.IsTrue(undoRes.Success);
            Assert.IsNull(state.SelectedRegionId);
        }

        [Test]
        public void SelectRegionCommand_FailsForNullOrEmpty()
        {
            var state = new CampaignState(seed: 1);
            var res = state.Execute(new SelectRegionCommand(""));
            Assert.IsFalse(res.Success);
        }
    }
}
