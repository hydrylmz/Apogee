using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Research;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// EditMode tests for the mission destination rail on the research tree screen:
    /// destination art/credit mapping, lock-state computation (required research +
    /// reputation), destination/branch filter exclusivity in the view model, graph
    /// required-node highlighting, and catalog data integrity (unlock ids must point
    /// at real research nodes). GDD §16.1, §19.1; UI redesign ADR (destination rail).
    /// </summary>
    [TestFixture]
    public sealed class DestinationRailTests
    {
        private sealed class MockNavigator : IScreenNavigator
        {
            public ScreenId CurrentScreenId { get; private set; }
            public IScreenController CurrentScreen => null;
            public bool CanGoBack => false;
            public IReadOnlyCollection<ScreenId> History { get; } = new List<ScreenId>();

            public event Action<ScreenId, ScreenId> OnScreenChanged;

            public void RegisterScreen(IScreenController screenController) { }
            public void UnregisterScreen(ScreenId screenId) { }

            public bool GoTo(ScreenId targetScreenId, object context = null, bool addToHistory = true)
            {
                CurrentScreenId = targetScreenId;
                return true;
            }

            public bool GoBack() => false;
            public void ClearHistory() { }
        }

        // ── Catalog helpers ───────────────────────────────────────────────────────

        private static string FindCatalogFile(string fileName)
        {
            string[] candidates =
            {
                Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project", "Data", "Catalog", "Json", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "_Project", "Data", "Catalog", "Json", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", fileName),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            Assert.Ignore($"Catalog file not found: {fileName}");
            return null;
        }

        private static DestinationRecord MakeRecord(
            string id,
            string[] unlockResearchIds = null,
            double unlockReputationMin = 0.0,
            string art = null,
            string artCredit = null,
            double commDelaySecondsMin = 180.0,
            double commDelaySecondsMax = 1320.0)
        {
            string idsJson = unlockResearchIds == null
                ? ""
                : string.Join(",", unlockResearchIds.Select(p => $"\"{p}\""));
            string artJson = art == null ? "null" : $"\"{art}\"";
            string creditJson = artCredit == null ? "null" : $"\"{artCredit}\"";

            var json = $@"{{
                ""id"":""{id}"",
                ""nameKey"":""DEST_{id.ToUpperInvariant()}_NAME"",
                ""descKey"":""DEST_{id.ToUpperInvariant()}_DESC"",
                ""bodyType"":""body"",
                ""dvAscent"":9400,
                ""transitDaysMin"":180,
                ""transitDaysMax"":270,
                ""commDelaySecondsMin"":{commDelaySecondsMin.ToString(CultureInfo.InvariantCulture)},
                ""commDelaySecondsMax"":{commDelaySecondsMax.ToString(CultureInfo.InvariantCulture)},
                ""windowPeriodDays"":780,
                ""hazards"":{{""radiation"":0.7,""dust"":0.6}},
                ""unlockResearchIds"":[{idsJson}],
                ""unlockReputationMin"":{unlockReputationMin.ToString(CultureInfo.InvariantCulture)},
                ""art"":{artJson},
                ""artCredit"":{creditJson}
            }}";

            return Newtonsoft.Json.JsonConvert.DeserializeObject<DestinationRecord>(json);
        }

        private static ResearchNodeItemViewModel MakeResearchItem(string id, string branch)
        {
            var json = $@"{{
                ""id"":""{id}"",
                ""nameKey"":""key.{id}.name"",
                ""branch"":""{branch}"",
                ""tier"":1,
                ""researchPointCost"":100,
                ""baseDurationDays"":60,
                ""startCostMUSD"":0,
                ""prerequisites"":[],
                ""unlocksModuleIds"":[]
            }}";

            var record = Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchNodeRecord>(json);
            return new ResearchNodeItemViewModel(record, new ResearchNodeState(id, ResearchStatus.Available), 0);
        }

        // ── 1. Record parsing ─────────────────────────────────────────────────────

        [Test]
        public void Record_ParsesArtAndCreditFields()
        {
            var record = MakeRecord("mars",
                unlockResearchIds: new[] { "edl-heat-shield-mk2", "prop-methane-reuse" },
                unlockReputationMin: 30.0,
                art: "Destinations/Art/mars",
                artCredit: "Mars küresel görünüm — Viking (PIA00407)");

            Assert.AreEqual("Destinations/Art/mars", record.Art);
            Assert.AreEqual("Mars küresel görünüm — Viking (PIA00407)", record.ArtCredit);
            Assert.AreEqual(2, record.UnlockResearchIds.Count);
            Assert.AreEqual(30.0, record.UnlockReputationMin, 0.001);
        }

        [Test]
        public void Record_MissingArtFields_ParseAsNull()
        {
            var record = MakeRecord("leo");

            Assert.IsNull(record.Art);
            Assert.IsNull(record.ArtCredit);
        }

        // ── 2. Lock state computation ─────────────────────────────────────────────

        [Test]
        public void LockState_NoRequirements_IsUnlockedFromStart()
        {
            var dest = new DestinationItemViewModel(MakeRecord("leo"));

            Assert.IsTrue(dest.IsUnlocked, "LEO must be open from campaign start.");
        }

        [Test]
        public void LockState_MissingResearch_IsLockedWithMissingIds()
        {
            var dest = new DestinationItemViewModel(MakeRecord("mars",
                unlockResearchIds: new[] { "edl-heat-shield-mk2", "prop-methane-reuse" },
                unlockReputationMin: 30.0));

            dest.UpdateLockState(new[] { "edl-heat-shield-mk2" }, currentReputation: 35.0);

            Assert.IsFalse(dest.IsUnlocked);
            Assert.IsTrue(dest.IsResearchMissing("prop-methane-reuse"));
            Assert.IsFalse(dest.IsResearchMissing("edl-heat-shield-mk2"));
            Assert.AreEqual(1, dest.MissingResearchIds.Count);
            StringAssert.Contains("Prop Methane Reuse", dest.BuildLockReason(), "Lock reason must name the missing technology.");
        }

        [Test]
        public void LockState_AllResearchDone_ButReputationShort_Locks()
        {
            var dest = new DestinationItemViewModel(MakeRecord("venus",
                unlockResearchIds: new[] { "edl-heat-shield-mk2" },
                unlockReputationMin: 20.0));

            dest.UpdateLockState(new[] { "edl-heat-shield-mk2" }, currentReputation: 10.0);

            Assert.IsFalse(dest.IsUnlocked);
            StringAssert.Contains("İtibar", dest.BuildLockReason());
            StringAssert.Contains("20", dest.BuildLockReason());
        }

        [Test]
        public void LockState_AllRequirementsMet_Unlocks()
        {
            var dest = new DestinationItemViewModel(MakeRecord("europa",
                unlockResearchIds: new[] { "power-rtg", "comms-laser", "edl-heat-shield-mk2" },
                unlockReputationMin: 50.0));

            dest.UpdateLockState(
                new[] { "power-rtg", "comms-laser", "edl-heat-shield-mk2" },
                currentReputation: 55.0);

            Assert.IsTrue(dest.IsUnlocked);
            Assert.AreEqual(0, dest.MissingResearchIds.Count);
        }

        [Test]
        public void LockState_RecomputeClearsPreviousMissingIds()
        {
            var dest = new DestinationItemViewModel(MakeRecord("mars",
                unlockResearchIds: new[] { "a", "b" }));

            dest.UpdateLockState(Array.Empty<string>(), 0.0);
            dest.UpdateLockState(new[] { "a", "b" }, 40.0);

            Assert.IsTrue(dest.IsUnlocked, "Second pass must fully clear stale missing ids.");
        }

        // ── 3. Display summaries ──────────────────────────────────────────────────

        [Test]
        public void TravelSummary_ContainsDvTransitAndDelay()
        {
            var dest = new DestinationItemViewModel(MakeRecord("mars"));

            string summary = dest.BuildTravelSummary();

            StringAssert.Contains("km/s", summary);
            StringAssert.Contains("180-270 gün", summary);
            StringAssert.Contains("dk", summary, "Long delays must render as minutes.");
        }

        [Test]
        public void DelaySpan_UnderAMinute_RendersSeconds()
        {
            var dest = new DestinationItemViewModel(MakeRecord(
                "leo", commDelaySecondsMin: 0.001, commDelaySecondsMax: 0.05));

            StringAssert.Contains("sn", dest.FormatDelaySpan());
        }

        [Test]
        public void HazardSummary_UsesTurkishLabelsAndPercent()
        {
            var dest = new DestinationItemViewModel(MakeRecord("mars"));

            string summary = dest.BuildHazardSummary();

            StringAssert.Contains("Radyasyon %70", summary);
            StringAssert.Contains("Toz %60", summary);
        }

        // ── 4. ViewModel wiring ───────────────────────────────────────────────────

        [Test]
        public void ViewModel_SetDestinations_BuildsRailWithLockStates()
        {
            var vm = new ResearchTreeViewModel(new MockNavigator());
            vm.SetDestinations(new[]
            {
                MakeRecord("leo"),
                MakeRecord("mars", unlockResearchIds: new[] { "edl-heat-shield-mk2", "prop-methane-reuse" }, unlockReputationMin: 30.0),
            });

            var rail = vm.Destinations.Value;
            Assert.AreEqual(2, rail.Count);
            Assert.IsTrue(rail[0].IsUnlocked, "No-requirement destination must be unlocked.");
            Assert.IsFalse(rail[1].IsUnlocked, "Without a campaign, required research cannot be complete.");
        }

        [Test]
        public void ViewModel_SelectDestination_TogglesAndResetsBranchFilter()
        {
            var vm = new ResearchTreeViewModel(new MockNavigator());
            vm.SetDestinations(new[] { MakeRecord("leo"), MakeRecord("mars") });
            vm.SelectBranch("Power");
            Assert.AreEqual("Power", vm.SelectedBranch.Value);

            vm.SelectDestination("mars");
            Assert.AreEqual("mars", vm.SelectedDestination.Value.Id);
            Assert.AreEqual("Tümü", vm.SelectedBranch.Value, "Destination filter must reset the branch dim.");

            vm.SelectDestination("mars");
            Assert.IsNull(vm.SelectedDestination.Value, "Clicking the same tab again must clear the filter.");
        }

        [Test]
        public void ViewModel_SelectBranch_ClearsDestinationFilter()
        {
            var vm = new ResearchTreeViewModel(new MockNavigator());
            vm.SetDestinations(new[] { MakeRecord("leo"), MakeRecord("venus") });

            vm.SelectDestination("venus");
            Assert.IsNotNull(vm.SelectedDestination.Value);

            vm.SelectBranch("EDL");
            Assert.IsNull(vm.SelectedDestination.Value, "Branch chips must clear the destination highlight.");
        }

        [Test]
        public void ViewModel_SelectDestination_UnknownIdKeepsSelection()
        {
            var vm = new ResearchTreeViewModel(new MockNavigator());
            vm.SetDestinations(new[] { MakeRecord("leo"), MakeRecord("moon") });

            vm.SelectDestination("leo");
            vm.SelectDestination("does-not-exist");

            Assert.AreEqual("leo", vm.SelectedDestination.Value.Id);
        }

        // ── 5. Graph highlighting ─────────────────────────────────────────────────

        [Test]
        public void Graph_SetHighlightedNodes_RingsRequiredAndDimsRest()
        {
            var items = new List<ResearchNodeItemViewModel>
            {
                MakeResearchItem("edl-shield", "EDL"),
                MakeResearchItem("prop-lox", "Propulsion"),
                MakeResearchItem("comms-ant", "Comms"),
            };

            var graph = new ResearchTreeGraphView();
            graph.SetNodes(items);
            graph.SetHighlightedNodes(new[] { "edl-shield", "prop-lox" }, "Mars");

            var cards = graph.Query<ResearchTreeNodeCard>().ToList();
            Assert.AreEqual(3, cards.Count);

            var required = cards.First(c => c.NodeId == "edl-shield");
            Assert.IsTrue(required.ClassListContains("circuit-node--required"), "Required node must carry the ring class.");
            Assert.AreEqual(1f, required.style.opacity.value, "Required node must not be dimmed.");

            var other = cards.First(c => c.NodeId == "comms-ant");
            Assert.IsFalse(other.ClassListContains("circuit-node--required"));
            Assert.AreEqual(0.28f, other.style.opacity.value, 0.001f, "Non-required nodes must dim like branch filters.");

            graph.SetHighlightedNodes(null);
            Assert.IsFalse(required.ClassListContains("circuit-node--required"), "Clearing the filter must remove the ring.");
            Assert.AreEqual(1f, other.style.opacity.value, "Clearing the filter must restore opacity.");
        }

        // ── 6. Shipped catalog integrity ──────────────────────────────────────────

        [Test]
        public void ShippedCatalog_DestinationsExistWithArtAndCredits()
        {
            string json = File.ReadAllText(FindCatalogFile("destinations.json"));
            var catalog = CatalogLoader.Load<DestinationRecord>(json);

            Assert.IsNotNull(catalog?.All);
            var ids = catalog.All.Select(d => d.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "leo", "moon", "mars", "venus", "europa" }, ids,
                "The rail must expose exactly the five mission destinations.");

            foreach (var dest in catalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(dest.Art), $"{dest.Id} must carry a destination photo.");
                Assert.IsFalse(string.IsNullOrEmpty(dest.ArtCredit), $"{dest.Id} must carry a Turkish NASA credit line.");
            }
        }

        [Test]
        public void ShippedCatalog_UnlockResearchIds_PointAtRealNodes()
        {
            string destinationsJson = File.ReadAllText(FindCatalogFile("destinations.json"));
            string researchJson = File.ReadAllText(FindCatalogFile("research_tree.json"));

            var destinations = CatalogLoader.Load<DestinationRecord>(destinationsJson).All;
            var research = Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchTreeCatalogProbe>(researchJson);

            var researchIds = new HashSet<string>(research.Records.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var dest in destinations)
            {
                foreach (var requiredId in dest.UnlockResearchIds)
                {
                    Assert.IsTrue(researchIds.Contains(requiredId),
                        $"{dest.Id} requires '{requiredId}' but research_tree.json has no such node id (stale reference).");
                }
            }
        }

        /// <summary>Minimal probe for deserializing research_tree.json node ids.</summary>
        private sealed class ResearchTreeCatalogProbe
        {
            [Newtonsoft.Json.JsonProperty("records")]
            public List<ResearchIdProbe> Records { get; set; }
        }

        private sealed class ResearchIdProbe
        {
            [Newtonsoft.Json.JsonProperty("id")]
            public string Id { get; set; }
        }
    }
}
