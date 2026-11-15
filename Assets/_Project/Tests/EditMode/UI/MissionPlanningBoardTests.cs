using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Core.Localization;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Composer;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// "Görev Planlama Panosu" identity tests for the Mission Composer screen:
    /// flight-plan dossier theme wiring, folder tabs with active/done states,
    /// kraft destination and mission-type sheets with stamps and photo slots,
    /// objective checklist rows, and shipped catalog integrity (art/credit
    /// fields point at real files; locale keys exist). GDD §5, §14; UI redesign
    /// ADR (planning board).
    /// </summary>
    [TestFixture]
    public sealed class MissionPlanningBoardTests
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

        private CatalogCollection<DestinationRecord> _destinations;
        private CatalogCollection<MissionTypeRecord> _missionTypes;
        private CatalogCollection<ObjectiveRecord> _objectives;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(FindCatalogFile("destinations.json")));
            _missionTypes = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(FindCatalogFile("mission_types.json")));
            _objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(FindCatalogFile("objectives.json")));

            // Fresh empty locale table: missing keys must never crash the board;
            // GetLocalizedOr falls back to prettified ids.
            LocalizationManager.SetInstance(new LocalizationManager());
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LocalizationManager.SetInstance(null);
        }

        private MissionComposerViewModel CreateViewModel()
        {
            return new MissionComposerViewModel(
                navigator: new MockNavigator(),
                destinations: _destinations.All,
                missionTypes: _missionTypes.All,
                objectives: _objectives.All);
        }

        private MissionComposerScreenController CreateInitializedController(
            out MissionComposerViewModel vm, out VisualElement host)
        {
            vm = CreateViewModel();
            var controller = new MissionComposerScreenController(vm);
            host = new VisualElement();
            controller.Initialize(host);
            return controller;
        }

        // ── 1. Theme & structure ──────────────────────────────────────────────────

        [Test]
        public void PlanningBoard_RootHasThemeAndDossierStructure()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;

            Assert.IsTrue(root.ClassListContains("theme-planning-board"), "Screen root must carry the planning board theme.");
            Assert.IsNotNull(root.Q(className: "apogee-navbar"), "Shared ScreenNavBar must be present.");
            Assert.IsNotNull(root.Q(className: "planning-backdrop"), "Planning desk backdrop must be present.");
            Assert.IsNotNull(root.Q(className: "planning-board-header"), "Board header must be present.");
            Assert.IsNotNull(root.Q(className: "planning-tabbar"), "Folder tab bar must be present.");
            Assert.IsNotNull(root.Q(className: "planning-dossier"), "Mission dossier panel must be present.");

            var labels = root.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("MİSYON PLANLAMA PANOSU")), "Board title must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("Uçuş Planı Dosyası")), "Dossier subtitle must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("MİSYON DOSYASI")), "Dossier panel header must be rendered.");
        }

        [Test]
        public void PlanningBoard_DecorativeArtNeverBlocksPicking()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var backdrop = controller.RootElement.Q(className: "planning-backdrop");
            var patches = controller.RootElement.Query(className: "planning-patch").ToList();

            Assert.AreEqual(PickingMode.Ignore, backdrop.pickingMode, "Backdrop must not intercept clicks.");
            Assert.GreaterOrEqual(patches.Count, 2, "Patch badge must appear in header and dossier panel.");
            Assert.IsTrue(patches.All(p => p.pickingMode == PickingMode.Ignore), "Patch badges are decorative.");
        }

        // ── 2. Folder tabs ────────────────────────────────────────────────────────

        [Test]
        public void FolderTabs_FourSteps_ActiveAndDoneStates()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var tabs = controller.RootElement.Query(className: "planning-tab").ToList();
            Assert.AreEqual(4, tabs.Count, "The flight-plan file must have four folder tabs.");

            Assert.IsTrue(tabs[0].ClassListContains("planning-tab--active"), "First tab starts active.");
            Assert.IsFalse(tabs[1].ClassListContains("planning-tab--done"));

            vm.NextStep(); // -> step 1
            Assert.IsTrue(tabs[0].ClassListContains("planning-tab--done"), "Completed tab keeps the done state.");
            Assert.IsFalse(tabs[0].ClassListContains("planning-tab--active"));
            Assert.IsTrue(tabs[1].ClassListContains("planning-tab--active"));

            vm.GoToStep(ComposerStep.ScheduleAndBudget); // jump to last step
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(tabs[i].ClassListContains("planning-tab--done"), $"Tab {i} must be marked done.");
            }
            Assert.IsTrue(tabs[3].ClassListContains("planning-tab--active"));
        }

        // ── 3. Destination & mission-type sheets ──────────────────────────────────

        [Test]
        public void DestinationCards_RenderKraftSheetsWithSelectionStamp()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;
            var cards = root.Query(className: "planning-sheet").ToList();
            Assert.GreaterOrEqual(cards.Count, _destinations.All.Count(), "Every destination must render as a kraft sheet.");

            var selected = cards.FirstOrDefault(c => c.ClassListContains("planning-sheet--selected"));
            Assert.IsNotNull(selected, "The default destination (moon) must be pre-selected.");

            var stamps = root.Query<Label>().ToList();
            Assert.IsTrue(stamps.Any(l => l.text == "DOSYA SEÇİLDİ"), "Selected destination carries the red selection stamp.");
            Assert.IsTrue(stamps.Any(l => l.text.StartsWith("FORM MP-")), "Unselected destinations carry form-number stamps.");

            // Clicking another card re-points the stamp.
            var another = _destinations.All.First(d => d.Id != vm.SelectedDestination.Value.Id);
            vm.SelectDestination(another);
            var stampsAfter = root.Query<Label>().ToList();
            Assert.IsTrue(stampsAfter.Any(l => l.text == "DOSYA SEÇİLDİ"), "Stamp follows the new selection.");
        }

        [Test]
        public void MissionTypeCards_RenderSpecSheetsWithApprovalStamp()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;
            var sheetCards = root.Query(className: "planning-sheet").ToList();
            Assert.GreaterOrEqual(sheetCards.Count, _destinations.All.Count() + _missionTypes.All.Count(),
                "Destinations and mission types all render on kraft sheets.");

            var stamps = root.Query<Label>().ToList();
            Assert.IsTrue(stamps.Any(l => l.text == "ŞEMATİK ONAYLI"), "Selected mission type carries the approval stamp.");
            Assert.IsTrue(stamps.Any(l => l.text.StartsWith("SPEC ")), "Unselected mission types carry spec stamps.");
            Assert.IsTrue(stamps.Any(l => l.text == "İNSANLI" || l.text == "ROBOTİK"), "Crewed/robotic badge must render.");
        }

        // ── 4. Objective checklist ────────────────────────────────────────────────

        [Test]
        public void ObjectiveRows_ChecklistToggleSwapsCheckedClass()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            vm.NextStep(); // MissionType
            vm.NextStep(); // Objectives

            var rows = controller.RootElement.Query(className: "planning-check-row").ToList();
            Assert.Greater(rows.Count, 0, "Compatible objectives render as checklist rows.");

            var target = vm.CompatibleObjectives.Value.First();
            int before = vm.SelectedObjectives.Value?.Count ?? 0;
            vm.ToggleObjective(target);
            int after = vm.SelectedObjectives.Value?.Count ?? 0;

            Assert.AreEqual(before + 1, after, "Toggling a row must add the objective.");

            var checkedRow = controller.RootElement.Query(className: "planning-check-row--checked").ToList();
            Assert.Greater(checkedRow.Count, 0, "Checked rows carry the kraft check class.");
        }

        // ── 5. Photo slots degrade gracefully ─────────────────────────────────────

        [Test]
        public void PhotoSlots_RenderPhotoOrPlaceholder_NeverCrash()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var slots = controller.RootElement.Query(className: "planning-photo").ToList();
            Assert.GreaterOrEqual(slots.Count, _destinations.All.Count() + _missionTypes.All.Count(),
                "Every destination and mission-type sheet carries a photo slot.");

            foreach (var slot in slots)
            {
                bool hasFallback = slot.Q(className: "planning-photo__fallback") != null;
                bool hasPhotoChild = !hasFallback && slot.childCount > 0;
                Assert.IsTrue(hasFallback || hasPhotoChild,
                    "Each slot shows either the NASA photo or the waiting placeholder.");
            }

            // Source credits no longer overlay the photos (they obscured the
            // imagery): the credit lives on the hover tooltip and in
            // attribution_data.json, so no caption label may remain in the tree.
            var captions = controller.RootElement.Query<Label>().Where(l => l.text.StartsWith("Kaynak: NASA")).ToList();
            Assert.AreEqual(0, captions.Count,
                "Photo slots must not carry NASA credit caption overlays.");
        }

        // ── 6. Shipped catalog integrity ──────────────────────────────────────────

        [Test]
        public void ShippedCatalog_MissionTypes_CarryArtAndCredits()
        {
            Assert.AreEqual(5, _missionTypes.All.Count(), "Five mission architecture profiles expected.");

            foreach (var type in _missionTypes.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(type.Art), $"{type.Id} must carry a mission photo path.");
                Assert.IsFalse(string.IsNullOrEmpty(type.ArtCredit), $"{type.Id} must carry a Turkish NASA credit line.");
                StringAssert.StartsWith("Composer/Art/", type.Art, "Mission art must live in the composer resources folder.");
            }
        }

        [Test]
        public void ShippedCatalog_ArtPaths_PointAtExistingFiles()
        {
            foreach (var type in _missionTypes.All)
            {
                string file = FindResourcesFile(Path.Combine("Composer", "Art", type.Art.Substring("Composer/Art/".Length) + ".jpg"));
                Assert.IsNotNull(file, $"{type.Id} art '{type.Art}.jpg' must exist under Assets/Resources/Composer/Art/.");
            }

            foreach (var dest in _destinations.All)
            {
                if (string.IsNullOrEmpty(dest.Art))
                {
                    continue;
                }

                Assert.IsTrue(dest.Art.StartsWith("Destinations/Art/"), "Destination art must live in the destination resources folder.");
                string file = FindResourcesFile(Path.Combine("Destinations", "Art", dest.Art.Substring("Destinations/Art/".Length) + ".jpg"));
                Assert.IsNotNull(file, $"{dest.Id} art '{dest.Art}.jpg' must exist under Assets/Resources/Destinations/Art/.");
            }
        }

        [Test]
        public void ShippedCatalog_LocaleKeys_MissionTypeNamesExist()
        {
            string trJson = File.ReadAllText(FindCatalogFile("locale_tr.json"));
            string enJson = File.ReadAllText(FindCatalogFile("locale_en.json"));

            var tr = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(trJson);
            var en = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(enJson);

            foreach (var type in _missionTypes.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(type.NameKey), $"{type.Id} must declare a nameKey.");
                StringAssert.Contains(type.Id.ToUpperInvariant().Replace("_", ""), type.NameKey.Replace("_", ""),
                    $"Name key '{type.NameKey}' must address the mission type '{type.Id}'.");
                Assert.IsTrue(tr.ContainsKey(type.NameKey), $"tr locale must define {type.NameKey}.");
                Assert.IsTrue(en.ContainsKey(type.NameKey), $"en locale must define {type.NameKey}.");

                Assert.IsFalse(string.IsNullOrEmpty(type.DescKey), $"{type.Id} must declare a descKey.");
                Assert.IsTrue(tr.ContainsKey(type.DescKey), $"tr locale must define {type.DescKey}.");
                Assert.IsTrue(en.ContainsKey(type.DescKey), $"en locale must define {type.DescKey}.");
            }
        }
    }
}
