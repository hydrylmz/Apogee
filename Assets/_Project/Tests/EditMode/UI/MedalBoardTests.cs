using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Milestones;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Milestones;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// "Madalya Panosu" identity tests for the milestone board screen: theme
    /// wiring, decorative felt-board artwork that never blocks picking, ribboned
    /// medal cards with NASA photo + credit slots, status ribbon badges, the
    /// claim ("Madalyayı Tak") flow DOM, next-goal plaque binding, and shipped
    /// catalog integrity (art/credit → real files, prerequisites → real ids).
    /// GDD §4.4, §14; UI redesign ADR (medal board).
    /// </summary>
    [TestFixture]
    public sealed class MedalBoardTests
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

        private static string FindResourcesFile(string relativePath)
        {
            string[] candidates =
            {
                Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "Resources", relativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Resources", relativePath),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", relativePath),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static List<MilestoneRecord> LoadShippedMilestoneRecords()
        {
            string json = File.ReadAllText(FindCatalogFile("milestones.json"));
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<MilestoneRecord>>(json);
        }

        private CampaignState CreateCampaignWithMilestones()
        {
            var state = new CampaignState(seed: 1);
            state.InitialiseMilestones(LoadShippedMilestoneRecords());
            return state;
        }

        private MilestoneBoardScreenController CreateInitializedController(
            out MilestoneBoardViewModel vm, out VisualElement host)
        {
            vm = new MilestoneBoardViewModel(null);
            var controller = new MilestoneBoardScreenController(vm, new MockNavigator());
            host = new VisualElement();
            controller.Initialize(host);
            return controller;
        }

        // ── 1. Theme & structure ──────────────────────────────────────────────────

        [Test]
        public void MedalBoard_RootHasThemeAndStructure()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;

            Assert.IsTrue(root.ClassListContains("theme-medal-board"), "Screen root must carry the medal board theme.");
            Assert.IsNotNull(root.Q(className: "apogee-navbar"), "Shared ScreenNavBar must be present.");
            Assert.IsNotNull(root.Q(className: "medal-backdrop"), "Felt patch-board backdrop must be present.");
            Assert.IsNotNull(root.Q(className: "medal-patch"), "Medal patch badge (crest) must be present.");
            Assert.IsNotNull(root.Q(className: "medal-header"), "Ceremonial header must be present.");
            Assert.IsNotNull(root.Q(className: "medal-summary"), "Progress summary row must be present.");
            Assert.IsNotNull(root.Q(className: "medal-track"), "Felt progress track must be present.");
            Assert.IsNotNull(root.Q(className: "medal-track-fill"), "Progress track fill must be present.");
            Assert.IsNotNull(root.Q(className: "medal-board"), "The felt patch board (left column) must be present.");
            Assert.IsNotNull(root.Q(className: "medal-detail"), "The brass plaque (right column) must be present.");
            Assert.AreEqual("apogee-navbar", root[root.childCount - 1].GetClasses().FirstOrDefault(),
                "Footer navbar must be the LAST child of the root so it sits at the same position on every screen.");

            var labels = root.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("MADALYA PANOSU")), "Board title must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text == "SONRAKİ HEDEF"), "Next-goal plaque header must render.");
        }

        [Test]
        public void MedalBoard_DecorativeArtNeverBlocksPicking()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var backdrop = controller.RootElement.Q(className: "medal-backdrop");
            Assert.AreEqual(PickingMode.Ignore, backdrop.pickingMode,
                "Felt board artwork must not intercept clicks.");

            var patch = controller.RootElement.Q(className: "medal-patch");
            Assert.AreEqual(PickingMode.Ignore, patch.pickingMode,
                "Medal patch badge must not intercept clicks.");

            var navbar = controller.RootElement.Q(className: "apogee-navbar");
            Assert.IsNotNull(navbar, "Navbar must stay interactive on top of the artwork.");
        }

        // ── 2. Empty board (no campaign) ──────────────────────────────────────────

        [Test]
        public void MedalBoard_NoCampaign_ShowsEmptyBoardWithBaseline()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            Assert.AreEqual(0, controller.RootElement.Query(className: "medal-card").ToList().Count,
                "No medal cards without a bound campaign.");
            Assert.AreEqual("0 / 8", controller.RootElement.Q<Label>(className: "medal-count").text,
                "Baseline count text must be preserved.");
            Assert.AreEqual("-", controller.RootElement.Q<Label>(className: "medal-goal-title").text,
                "Plaque keeps the placeholder goal without a campaign.");
        }

        // ── 3. Campaign-bound cards ────────────────────────────────────────────────

        [Test]
        public void MedalBoard_CampaignBinds_EightMedalCards()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.BindCampaign(CreateCampaignWithMilestones());

            var cards = controller.RootElement.Query(className: "medal-card").ToList();
            Assert.AreEqual(8, cards.Count, "All shipped milestones must render as medal cards.");

            // Every card carries a photo slot with caption or placeholder.
            foreach (var card in cards)
            {
                var photo = card.Q(className: "medal-card-photo");
                Assert.IsNotNull(photo, "Each medal card needs its photo slot.");
                bool hasCaption = photo.Q(className: "medal-card-photo__caption") != null;
                bool hasFallback = photo.Q(className: "medal-card-photo__fallback") != null;
                Assert.IsTrue(hasCaption || hasFallback,
                    "Photo slot shows either the NASA credit caption or the waiting placeholder.");
            }

            // Day-0 statuses: first milestone available, the rest locked.
            var badges = controller.RootElement.Query<Label>(className: "medal-badge").ToList();
            Assert.AreEqual(8, badges.Count, "Each card carries a status ribbon.");
            Assert.AreEqual(1, badges.Count(b => b.text == "ÖNERİLEN HEDEF"),
                "Exactly one milestone is recommended at day 0 (the prerequisite-free first one).");
            Assert.AreEqual(7, badges.Count(b => b.text == "KİLİTLİ"), "The rest stay locked behind prerequisites.");

            // Claim buttons are hidden until a milestone is completed.
            var claimButtons = controller.RootElement.Query<Button>(className: "medal-claim").ToList();
            Assert.AreEqual(8, claimButtons.Count);
            Assert.IsTrue(claimButtons.All(b => b.style.display == DisplayStyle.None),
                "Claim buttons must be hidden while no milestone awaits its ceremony.");
        }

        [Test]
        public void MedalBoard_ProgressSummary_ReflectsViewModel()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            vm.CompletedCountText.Value = "3 / 8";
            vm.ProgressFraction.Value = 0.375f;

            Assert.AreEqual("3 / 8", controller.RootElement.Q<Label>(className: "medal-count").text);
            var fill = controller.RootElement.Q(className: "medal-track-fill");
            Assert.AreEqual(37.5f, fill.style.width.value.value, 0.01f,
                "Gold fill must follow the progress fraction.");
        }

        // ── 4. Plaque & banner bindings ─────────────────────────────────────────────

        [Test]
        public void MedalBoard_Plaque_FollowsNextGoalText()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            vm.ActiveGoalTitleText.Value = "Ay Yüzeyine İniş (Robotik)";
            vm.ActiveGoalDescriptionText.Value = "Yumuşak iniş gerçekleştir.";

            Assert.AreEqual("Ay Yüzeyine İniş (Robotik)",
                controller.RootElement.Q<Label>(className: "medal-goal-title").text);
            Assert.AreEqual("Yumuşak iniş gerçekleştir.",
                controller.RootElement.Q<Label>(className: "medal-goal-desc").text);
        }

        [Test]
        public void MedalBoard_CeremonyBanner_TogglesWithNotification()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var banner = controller.RootElement.Q(className: "medal-banner");
            Assert.AreEqual(DisplayStyle.None, banner.style.display, "Banner starts hidden.");

            vm.NotificationText.Value = "Madalya töreni tamamlandı!";
            vm.HasNotification.Value = true;

            Assert.AreEqual(DisplayStyle.Flex, banner.style.display);
            StringAssert.Contains("Madalya töreni",
                controller.RootElement.Q<Label>(className: "medal-banner-text").text);

            vm.HasNotification.Value = false;
            Assert.AreEqual(DisplayStyle.None, banner.style.display);
        }

        // ── 5. Claim flow DOM ───────────────────────────────────────────────────────

        [Test]
        public void MedalBoard_ClaimButton_AppearsOnCanClaim_AndClaimShowsCeremonyResult()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.BindCampaign(CreateCampaignWithMilestones());

            var firstItem = vm.MilestoneItems.OrderBy(i => i.OrderIndex).First();
            Assert.AreEqual(MilestoneStatus.Available, firstItem.Status.Value);

            // Simulate the campaign completing the milestone ceremony state.
            firstItem.CanClaim.Value = true;

            var firstCard = controller.RootElement.Query(className: "medal-card").First();
            var claimBtn = firstCard.Q<Button>(className: "medal-claim");
            Assert.AreEqual(DisplayStyle.Flex, claimBtn.style.display,
                "Claim button must appear when the milestone awaits its ceremony.");

            // The button delegate calls ViewModel.ClaimMilestone(id); invoking the
            // same entry point directly must degrade gracefully into the ceremony
            // banner (error text) when the command is not claimable, never throw.
            Assert.DoesNotThrow(() => vm.ClaimMilestone(firstItem.Id));

            var banner = controller.RootElement.Q(className: "medal-banner");
            Assert.AreEqual(DisplayStyle.Flex, banner.style.display,
                "Failed claims surface as a banner message.");
            StringAssert.Contains("Hata",
                controller.RootElement.Q<Label>(className: "medal-banner-text").text);
        }

        [Test]
        public void MedalBoard_CardStyle_ReactsToStatusChanges()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.BindCampaign(CreateCampaignWithMilestones());

            var firstItem = vm.MilestoneItems.OrderBy(i => i.OrderIndex).First();
            var firstCard = controller.RootElement.Query(className: "medal-card").First();

            Assert.IsFalse(firstCard.ClassListContains("medal-card--pending"));

            firstItem.Status.Value = MilestoneStatus.CompletedPendingClaim;

            Assert.IsTrue(firstCard.ClassListContains("medal-card--pending"),
                "Pending-claim milestones sit on the gold ceremony card.");
            Assert.IsFalse(firstCard.ClassListContains("medal-card--claimed"));

            firstItem.Status.Value = MilestoneStatus.Claimed;

            Assert.IsTrue(firstCard.ClassListContains("medal-card--claimed"),
                "Claimed milestones move to the laurel card.");

            var badge = firstCard.Q<Label>(className: "medal-badge");
            StringAssert.Contains("MADALYA TAKILDI", badge.text, "Claimed ribbon text must update.");
        }

        // ── 6. Shipped catalog integrity ─────────────────────────────────────────────

        [Test]
        public void ShippedCatalog_AllMilestonesCarryArtAndCredits()
        {
            var records = LoadShippedMilestoneRecords();
            Assert.AreEqual(8, records.Count, "Eight shipped milestones expected.");

            foreach (var rec in records)
            {
                Assert.IsFalse(string.IsNullOrEmpty(rec.Art), $"{rec.Id} must carry a photo path.");
                StringAssert.StartsWith("Milestones/Art/", rec.Art,
                    "Milestone art must live in the milestone resources folder.");
                Assert.IsTrue(rec.ArtCredit != null && rec.ArtCredit.Contains("NASA"),
                    $"{rec.Id} must carry a Turkish NASA credit line.");

                string file = FindResourcesFile(Path.Combine("Milestones", "Art", rec.Art.Substring("Milestones/Art/".Length) + ".jpg"));
                Assert.IsNotNull(file, $"{rec.Id} art '{rec.Art}.jpg' must exist under Assets/Resources/Milestones/Art/.");
            }
        }

        [Test]
        public void ShippedCatalog_PrerequisitesResolveToRealIds()
        {
            var records = LoadShippedMilestoneRecords();
            var ids = records.Select(r => r.Id).ToHashSet();

            foreach (var rec in records)
            {
                Assert.IsFalse(string.IsNullOrEmpty(rec.Title), $"{rec.Id} must have a title.");
                Assert.IsFalse(string.IsNullOrEmpty(rec.Description), $"{rec.Id} must have a description.");
                Assert.GreaterOrEqual(rec.OrderIndex, 1);

                foreach (var reqId in rec.RequiredMilestoneIds)
                {
                    Assert.IsTrue(ids.Contains(reqId),
                        $"{rec.Id} prerequisite '{reqId}' must resolve to a shipped milestone.");
                }
            }

            Assert.AreEqual(8, records.Select(r => r.OrderIndex).Distinct().Count(),
                "Order indices must be unique for deterministic board order.");
        }
    }
}
