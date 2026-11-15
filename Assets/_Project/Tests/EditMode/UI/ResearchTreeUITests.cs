using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Screens.Research;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// EditMode tests for the circuit-themed research tree screen (ScreenId.ResearchTree):
    /// HOI4-style layout solver (swim-lanes × depth columns), node-card status classes,
    /// NASA art/credit mapping on the item view model, and graph view element production.
    /// GDD §9.2, §14.1, §15; UI redesign ADR (Ar-Ge Ağacı).
    /// </summary>
    [TestFixture]
    public sealed class ResearchTreeUITests
    {
        // ── Catalog helpers (JSON round-trip honours private setters) ────────────

        private static ResearchNodeRecord MakeRecord(
            string id,
            string branch = "Propulsion",
            int tier = 1,
            double cost = 100,
            int durationDays = 60,
            double startCost = 0,
            string[] prerequisites = null,
            string[] unlocksModules = null,
            string art = null,
            string artCredit = null)
        {
            string preJson = prerequisites == null
                ? ""
                : string.Join(",", prerequisites.Select(p => $"\"{p}\""));
            string unlocksJson = unlocksModules == null
                ? ""
                : string.Join(",", unlocksModules.Select(m => $"\"{m}\""));
            string artJson = art == null ? "null" : $"\"{art}\"";
            string creditJson = artCredit == null ? "null" : $"\"{artCredit}\"";

            var json = $@"{{
                ""id"":""{id}"",
                ""nameKey"":""key.{id}.name"",
                ""branch"":""{branch}"",
                ""tier"":{tier},
                ""researchPointCost"":{cost},
                ""baseDurationDays"":{durationDays},
                ""startCostMUSD"":{startCost},
                ""prerequisites"":[{preJson}],
                ""unlocksModuleIds"":[{unlocksJson}],
                ""art"":{artJson},
                ""artCredit"":{creditJson}
            }}";

            return Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchNodeRecord>(json);
        }

        private static ResearchNodeItemViewModel MakeItem(
            string id,
            string branch = "Propulsion",
            ResearchStatus status = ResearchStatus.Available,
            int tier = 1,
            string[] prerequisites = null,
            string art = null,
            string artCredit = null)
        {
            var record = MakeRecord(id, branch, tier, prerequisites: prerequisites, art: art, artCredit: artCredit);
            var state = new ResearchNodeState(id, status);
            return new ResearchNodeItemViewModel(record, state, currentBudget: 0);
        }

        /// <summary>
        /// Synthetic mirror of the shipped tree shape: 8 branches × 3 tiers,
        /// each tier-2 node requiring the tier-1 root of its branch, each tier-3
        /// node requiring its branch's tier-2 node (longest chain = depth 2).
        /// </summary>
        private static List<ResearchNodeItemViewModel> BuildCatalog()
        {
            var items = new List<ResearchNodeItemViewModel>();
            foreach (var branch in ResearchTreeLayout.LaneOrder)
            {
                string prefix = branch.Substring(0, 3).ToLowerInvariant();
                items.Add(MakeItem($"{prefix}-t1", branch, tier: 1));
                items.Add(MakeItem($"{prefix}-t2", branch, tier: 2, prerequisites: new[] { $"{prefix}-t1" }));
                items.Add(MakeItem($"{prefix}-t3", branch, tier: 3, prerequisites: new[] { $"{prefix}-t2" }));
            }

            return items;
        }

        // ── 1. Layout solver ──────────────────────────────────────────────────────

        [Test]
        public void Solve_EveryNodeGetsExactlyOneSlot()
        {
            var items = BuildCatalog();

            var result = ResearchTreeLayout.Solve(items);

            Assert.AreEqual(items.Count, result.Slots.Count, "Slot count must equal node count.");
            foreach (var item in items)
            {
                Assert.IsTrue(result.Slots.ContainsKey(item.Id), $"Missing slot for {item.Id}");
            }
        }

        [Test]
        public void Solve_NodesNeverOverlap()
        {
            var items = BuildCatalog();

            var result = ResearchTreeLayout.Solve(items);

            var rects = result.Slots.Values.Select(s => s.Rect).ToList();
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    bool overlaps = rects[i].Overlaps(rects[j]);
                    Assert.IsFalse(overlaps,
                        $"Cards overlap: slot {i} {rects[i]} vs slot {j} {rects[j]}");
                }
            }
        }

        [Test]
        public void Solve_PrerequisiteDepthIncreasesColumnPosition()
        {
            var items = BuildCatalog();

            var result = ResearchTreeLayout.Solve(items);
            var adcs = result.Slots["adc-t1"];
            var adcs2 = result.Slots["adc-t2"];
            var adcs3 = result.Slots["adc-t3"];

            Assert.AreEqual(ResearchTreeLayout.LeftMargin, adcs.Rect.x, 0.5f, "Root nodes sit in column 0.");
            Assert.Greater(adcs2.Rect.x, adcs.Rect.x, "Tier-2 column must be right of tier-1.");
            Assert.Greater(adcs3.Rect.x, adcs2.Rect.x, "Tier-3 column must be right of tier-2.");
            Assert.AreEqual(2, result.MaxDepth, "3-tier chain ⇒ max depth 2 (3 columns).");
        }

        [Test]
        public void Solve_LanesFollowFixedOrderWithDisjointBands()
        {
            var items = BuildCatalog();

            var result = ResearchTreeLayout.Solve(items);

            Assert.AreEqual(ResearchTreeLayout.LaneOrder.Length, result.Lanes.Count);
            for (int i = 0; i < result.Lanes.Count; i++)
            {
                Assert.AreEqual(ResearchTreeLayout.LaneOrder[i], result.Lanes[i].branch,
                    $"Lane {i} must be {ResearchTreeLayout.LaneOrder[i]}.");
            }

            for (int i = 0; i < result.Lanes.Count - 1; i++)
            {
                float topEnd = result.Lanes[i].band.yMax;
                float nextStart = result.Lanes[i + 1].band.yMin;
                Assert.LessOrEqual(topEnd, nextStart + 0.01f, "Lane bands must not overlap vertically.");
            }

            // Cards of different lanes never share a band row.
            var adcs = result.Slots["adc-t1"];
            var comms = result.Slots["com-t1"];
            Assert.Less(adcs.Rect.yMax, comms.Rect.yMin, "ADCS cards must sit above Comms cards.");
        }

        [Test]
        public void Solve_CrossBranchPrerequisitesProduceEdges()
        {
            var items = BuildCatalog();
            // Structure reliability check requires a Propulsion result (real-tree pattern).
            items.Add(MakeItem("str-boosters", "Structure", tier: 4,
                prerequisites: new[] { "pro-t3" }));

            var result = ResearchTreeLayout.Solve(items);

            var edge = result.Edges.FirstOrDefault(e => e[0] == "pro-t3" && e[1] == "str-boosters");
            Assert.IsNotNull(edge, "Cross-branch prerequisite pair must become an edge.");

            // The edge really spans lanes.
            string sourceBranch = items.First(n => n.Id == "pro-t3").Branch;
            string targetBranch = items.First(n => n.Id == "str-boosters").Branch;
            Assert.AreNotEqual(sourceBranch, targetBranch, "Sanity: the pair is cross-branch.");
        }

        [Test]
        public void Solve_IsDeterministic()
        {
            var items = BuildCatalog();

            var a = ResearchTreeLayout.Solve(items);
            var b = ResearchTreeLayout.Solve(items);

            Assert.AreEqual(a.CanvasWidth, b.CanvasWidth);
            Assert.AreEqual(a.CanvasHeight, b.CanvasHeight);
            foreach (var item in items)
            {
                Assert.AreEqual(a.Slots[item.Id].Rect, b.Slots[item.Id].Rect,
                    $"Slot for {item.Id} must be identical across solves.");
            }
        }

        [Test]
        public void Solve_EmptyList_ReturnsPlaceholderWithoutThrowing()
        {
            Assert.DoesNotThrow(() => ResearchTreeLayout.Solve(new List<ResearchNodeItemViewModel>()));
            Assert.DoesNotThrow(() => ResearchTreeLayout.Solve(null));
        }

        // ── 2. Item view model: NASA art mapping ─────────────────────────────────

        [Test]
        public void ItemViewModel_MapsArtAndArtCreditFromRecord()
        {
            const string artPath = "Research/Art/prop-ion-electric";
            const string credit = "NASA — Deep Space 1 ion thrust (PIA04238), kamu malı";

            var item = MakeItem("prop-ion", art: artPath, artCredit: credit);

            Assert.AreEqual(artPath, item.Art);
            Assert.AreEqual(credit, item.ArtCredit);
        }

        [Test]
        public void ItemViewModel_MissingArtFields_StaysNull()
        {
            var item = MakeItem("adcs-base");

            Assert.IsNull(item.Art, "No art field in JSON ⇒ Art stays null (vector fallback).");
            Assert.IsNull(item.ArtCredit);
        }

        [Test]
        public void ItemViewModel_StatusAndProgress_ReflectNodeState()
        {
            var locked = MakeItem("n-locked", status: ResearchStatus.Locked);
            Assert.IsTrue(locked.IsLocked);
            Assert.IsFalse(locked.IsCompleted);

            var done = MakeItem("n-done", status: ResearchStatus.Completed);
            Assert.IsTrue(done.IsCompleted);
            Assert.GreaterOrEqual(done.ProgressFraction, 0f);
            Assert.LessOrEqual(done.ProgressFraction, 1f);
        }

        // ── 3. Node card state classes ────────────────────────────────────────────

        [Test]
        public void NodeCard_InitialStatus_RendersMatchingStateClass()
        {
            var locked = new ResearchTreeNodeCard(MakeItem("a", status: ResearchStatus.Locked), null, _ => { });
            Assert.IsTrue(locked.ClassListContains("circuit-node--locked"));

            var available = new ResearchTreeNodeCard(MakeItem("b", status: ResearchStatus.Available), null, _ => { });
            Assert.IsTrue(available.ClassListContains("circuit-node--available"));

            var running = new ResearchTreeNodeCard(MakeItem("c", status: ResearchStatus.InProgress), null, _ => { });
            Assert.IsTrue(running.ClassListContains("circuit-node--inprogress"));

            var done = new ResearchTreeNodeCard(MakeItem("d", status: ResearchStatus.Completed), null, _ => { });
            Assert.IsTrue(done.ClassListContains("circuit-node--completed"));
        }

        [Test]
        public void NodeCard_UpdateState_SwapsStateClasses()
        {
            var card = new ResearchTreeNodeCard(MakeItem("x", status: ResearchStatus.Locked), null, _ => { });
            Assert.IsTrue(card.ClassListContains("circuit-node--locked"));

            card.UpdateState(MakeItem("x", status: ResearchStatus.Completed));

            Assert.IsTrue(card.ClassListContains("circuit-node--completed"));
            Assert.IsFalse(card.ClassListContains("circuit-node--locked"), "Old state class must be removed.");
        }

        [Test]
        public void NodeCard_SelectionTogglesSelectedClass()
        {
            var card = new ResearchTreeNodeCard(MakeItem("x"), null, _ => { });

            card.SetSelected(true);
            Assert.IsTrue(card.ClassListContains("circuit-node--selected"));

            card.SetSelected(false);
            Assert.IsFalse(card.ClassListContains("circuit-node--selected"));
        }

        [Test]
        public void NodeCard_Dim_TogglesOpacity()
        {
            var card = new ResearchTreeNodeCard(MakeItem("x"), null, _ => { });

            card.SetDimmed(true);
            Assert.AreEqual(0.28f, card.style.opacity.value, 0.01f);

            card.SetDimmed(false);
            Assert.AreEqual(1f, card.style.opacity.value, 0.01f);
        }

        [Test]
        public void NodeCard_ExposesNodeId()
        {
            var card = new ResearchTreeNodeCard(MakeItem("prop-advanced-kerosene"), null, _ => { });
            Assert.AreEqual("prop-advanced-kerosene", card.NodeId);
        }

        [Test]
        public void NodeCard_NullItemOrNullCallback_Throws()
        {
            var item = MakeItem("x");
            Assert.Throws<ArgumentNullException>(() => new ResearchTreeNodeCard(null, null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ResearchTreeNodeCard(item, null, null));
        }

        [Test]
        public void NodeCard_WithoutTexture_UsesVectorFallbackNotThrowing()
        {
            // art == null exercises the icon fallback path (Resources.Load skipped).
            Assert.DoesNotThrow(() => new ResearchTreeNodeCard(MakeItem("x", art: "Research/Art/does-not-exist"), null, _ => { }));
        }

        // ── 4. Graph view ─────────────────────────────────────────────────────────

        [Test]
        public void GraphView_SetNodes_ProducesOneCardPerNode()
        {
            var items = BuildCatalog();
            var graph = new ResearchTreeGraphView();

            graph.SetNodes(items);

            var cards = graph.Query<ResearchTreeNodeCard>().ToList();
            Assert.AreEqual(items.Count, cards.Count, "One card element per research node.");
        }

        [Test]
        public void GraphView_SetNodes_EmptyOrNull_DoesNotThrow()
        {
            var graph = new ResearchTreeGraphView();

            Assert.DoesNotThrow(() => graph.SetNodes(null));
            Assert.AreEqual(0, graph.Query<ResearchTreeNodeCard>().ToList().Count);
        }

        [Test]
        public void GraphView_SetNodes_ReflectsSelectionOnCards()
        {
            var items = BuildCatalog();
            var graph = new ResearchTreeGraphView();
            graph.SetNodes(items);

            graph.SetSelected("adc-t2");

            var selected = graph.Query<ResearchTreeNodeCard>().ToList()
                .Where(c => c.ClassListContains("circuit-node--selected")).ToList();
            Assert.AreEqual(1, selected.Count, "Exactly one card is visually selected.");
            Assert.AreEqual("adc-t2", selected[0].NodeId);
        }

        [Test]
        public void GraphView_SetHighlightedBranch_DimsOtherBranchCards()
        {
            var items = BuildCatalog();
            var graph = new ResearchTreeGraphView();
            graph.SetNodes(items);

            graph.SetHighlightedBranch("ADCS");

            var byId = items.ToDictionary(n => n.Id, n => n);
            foreach (var card in graph.Query<ResearchTreeNodeCard>().ToList())
            {
                bool inBranch = string.Equals(byId[card.NodeId].Branch, "ADCS", StringComparison.OrdinalIgnoreCase);
                float opacity = card.style.opacity.value;
                Assert.AreEqual(inBranch ? 1f : 0.28f, opacity, 0.01f,
                    $"Card {card.NodeId} opacity must reflect branch highlight.");
            }

            // "Tümü" clears the dim.
            graph.SetHighlightedBranch("Tümü");
            foreach (var card in graph.Query<ResearchTreeNodeCard>().ToList())
            {
                Assert.AreEqual(1f, card.style.opacity.value, 0.01f);
            }
        }

        [Test]
        public void GraphView_Zoom_ClampsAndResets()
        {
            var graph = new ResearchTreeGraphView();

            graph.ZoomIn();
            graph.ZoomIn();
            Assert.Greater(graph.Zoom, 1f, "Zoom-in raises above 1.");

            graph.ZoomOut();
            graph.ZoomOut();
            graph.ZoomOut();
            graph.ZoomOut();
            graph.ZoomOut();
            Assert.LessOrEqual(graph.Zoom, 0.55f + 0.001f, "Zoom clamps at the minimum.");

            graph.ZoomReset();
            Assert.AreEqual(1f, graph.Zoom, 0.001f);
        }

        [Test]
        public void GraphView_StructuralChange_RebuildsCardsOnce()
        {
            var items = BuildCatalog();
            var graph = new ResearchTreeGraphView();
            graph.SetNodes(items);
            int firstCount = graph.Query<ResearchTreeNodeCard>().ToList().Count;

            // Status-only refresh: same node set, different statuses → no rebuild needed,
            // but the card count must stay stable either way.
            var refreshed = BuildCatalog().Select(i =>
                MakeItem(i.Id, i.Branch, ResearchStatus.Completed, i.Tier)).ToList();
            graph.SetNodes(refreshed);

            var cards = graph.Query<ResearchTreeNodeCard>().ToList();
            Assert.AreEqual(firstCount, cards.Count);
            Assert.IsTrue(cards.All(c => c.ClassListContains("circuit-node--completed")),
                "All cards must reflect the refreshed statuses.");
        }
    }
}
