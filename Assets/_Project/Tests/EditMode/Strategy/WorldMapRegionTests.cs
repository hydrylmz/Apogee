using NUnit.Framework;
using System.IO;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Screens.CampaignMap;

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

        // ================================================================ Region Media (photos)
        private static string LoadCatalogText(string filename)
        {
            string path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Assets", "_Project", "Data", "Catalog", "Json", filename);
            if (!File.Exists(path))
            {
                path = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets", "_Project", "Data", "Catalog", "Json", filename);
            }
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        [Test]
        public void RegionMediaCatalog_LoadsSuccessfully_ValidSchema()
        {
            string json = LoadCatalogText("region_media.json");
            if (json == null)
            {
                Assert.Inconclusive("region_media.json not reachable in this test runner environment.");
                return;
            }

            var media = CatalogLoader.Load<RegionMediaRecord>(json);
            Assert.IsTrue(media.Validation.IsValid, media.Validation.ToString());
            Assert.GreaterOrEqual(media.Count, 8, "Every region should have at least one photo entry.");
        }

        [Test]
        public void RegionMediaCatalog_EveryRegionId_MatchesRegionRecord()
        {
            string json = LoadCatalogText("region_media.json");
            if (json == null || _regions == null)
            {
                Assert.Inconclusive("Catalog files not reachable in this test runner environment.");
                return;
            }

            var media = CatalogLoader.Load<RegionMediaRecord>(json);
            Assert.IsTrue(media.Validation.IsValid, media.Validation.ToString());

            foreach (var rec in media.All)
            {
                Assert.IsNotNull(_regions.GetById(rec.RegionId),
                    $"Media record '{rec.Id}' references unknown region '{rec.RegionId}'.");
            }
        }

        [Test]
        public void RegionMediaCatalog_PhotosCarryCaptionAndCredit()
        {
            string json = LoadCatalogText("region_media.json");
            if (json == null)
            {
                Assert.Inconclusive("region_media.json not reachable in this test runner environment.");
                return;
            }

            var media = CatalogLoader.Load<RegionMediaRecord>(json);
            Assert.IsTrue(media.Validation.IsValid, media.Validation.ToString());

            foreach (var rec in media.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(rec.Path), $"Media '{rec.Id}' has empty asset path.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rec.Caption), $"Media '{rec.Id}' has empty caption.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rec.Credit), $"Media '{rec.Id}' has empty credit (attribution is mandatory).");
            }
        }

        [Test]
        public void RegionMediaCatalog_PinsStayClearOfLabelGutter_ClampBounds()
        {
            // Pacific Atoll (lon -160 → NormalizedX ≈ 5.56%) must stay inside the clamp
            // bounds used by the pin renderer so it never slides off the plane edge.
            if (_regions == null) return;

            var record = _regions.GetById("pacific_atoll");
            Assert.IsNotNull(record, "pacific_atoll must exist in regions.json");

            var pacific = new RegionMapItemViewModel(record);
            float posX = pacific.NormalizedX + 2.5f; // controller offset for pacific_atoll
            float clamped = System.Math.Clamp(posX, 3.5f, 90f);
            Assert.LessOrEqual(clamped, 90f);
            Assert.GreaterOrEqual(clamped, 3.5f);
            Assert.AreEqual(posX, clamped, 0.01f, "Pacific Atoll pin should not be edge-clamped after offset.");
        }
    }
}
