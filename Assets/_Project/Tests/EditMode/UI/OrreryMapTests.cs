using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.SolarMap;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// "Planetaryum / Pirinç Astrolap" identity tests for the Solar System map
    /// screen: theme wiring, astrolabe backdrop (rings + degree ticks + stars),
    /// planet markers on the plate, the brass-framed planet catalog fiche with
    /// NASA photo + credit, transfer-window badge states, active mission list,
    /// and shipped destination catalog integrity (art/credit → real files).
    /// GDD §6.2, §14; UI redesign ADR (orrery).
    /// </summary>
    [TestFixture]
    public sealed class OrreryMapTests
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

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(FindCatalogFile("destinations.json")));
        }

        private SolarMapViewModel CreateViewModel()
        {
            return new SolarMapViewModel(
                navigator: new MockNavigator(),
                campaign: null,
                destinationRecords: _destinations.All);
        }

        private SolarMapScreenController CreateInitializedController(
            out SolarMapViewModel vm, out VisualElement host)
        {
            vm = CreateViewModel();
            var controller = new SolarMapScreenController(vm);
            host = new VisualElement();
            controller.Initialize(host);
            return controller;
        }

        // ── 1. Theme & structure ──────────────────────────────────────────────────

        [Test]
        public void Orrery_RootHasThemeAndAstrolabeStructure()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;

            Assert.IsTrue(root.ClassListContains("theme-orrery"), "Screen root must carry the orrery theme.");
            Assert.IsNotNull(root.Q(className: "apogee-navbar"), "Shared ScreenNavBar must be present.");
            Assert.IsNotNull(root.Q(className: "orrery-backdrop"), "Brass astrolabe backdrop must be present.");
            Assert.IsNotNull(root.Q(className: "orrery-header"), "Observatory HUD header must be present.");
            Assert.IsNotNull(root.Q(className: "orrery-canvas"), "Astrolabe plate canvas must be present.");
            Assert.IsNotNull(root.Q(className: "orrery-fiche"), "Planet catalog fiche must be present.");
            Assert.IsNotNull(root.Q(className: "orrery-window-card"), "Transfer window card must be present.");
            Assert.AreEqual("apogee-navbar", root[root.childCount - 1].GetClasses().FirstOrDefault(),
                "Footer navbar must be the LAST child of the root so it sits at the same position on every screen.");
            Assert.IsNotNull(root.Q<Button>(className: "orrery-menubtn"), "Ana Menü affordance must stay available.");

            var labels = root.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("Planetaryum")), "Observatory title must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("KATALOK NO")), "Catalog number must be engraved on the fiche.");
            Assert.IsTrue(labels.Any(l => l.text == "FIRLATMA PENCERESİ"), "Transfer window card title must render.");
            Assert.IsTrue(labels.Any(l => l.text == "AKTİF SEYİRDEKİ ARAÇLAR"), "Active missions section must render.");
        }

        [Test]
        public void Orrery_DecorativeBackdropNeverBlocksPicking()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var backdrop = controller.RootElement.Q(className: "orrery-backdrop");
            Assert.AreEqual(PickingMode.Ignore, backdrop.pickingMode, "Astrolabe plate must not intercept clicks.");

            // Planet markers and the launch button must be clickable elements on top.
            Assert.Greater(controller.RootElement.Query(className: "orrery-planet").ToList().Count, 0);
            var launch = controller.RootElement.Q<Button>(className: "apogee-button--primary");
            Assert.IsNotNull(launch, "Design-mission launch button must exist.");
        }

        // ── 2. Planet markers ─────────────────────────────────────────────────────

        [Test]
        public void PlanetMarkers_AllDestinationsRenderOnPlate()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var markers = controller.RootElement.Query(className: "orrery-planet").ToList();
            Assert.AreEqual(_destinations.All.Count(), markers.Count,
                "Every destination body must render as a planet marker on the plate.");

            var markerLabels = controller.RootElement.Query<Label>(className: "orrery-planet-label").ToList();
            Assert.AreEqual(markers.Count, markerLabels.Count, "Each planet marker carries its id tag.");

            foreach (var body in vm.Bodies.Value)
            {
                Assert.That(body.NormalizedX, Is.InRange(0f, 100f), $"{body.Id} x position must be normalized.");
                Assert.That(body.NormalizedY, Is.InRange(0f, 100f), $"{body.Id} y position must be normalized.");
            }
        }

        [Test]
        public void SelectBody_UpdatesFicheAndMarkerSelection()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            vm.SelectBody("venus");

            Assert.AreEqual("venus", vm.SelectedBody.Value.Id, "SelectBody must re-point the selection.");
            var title = controller.RootElement.Q<Label>(className: "orrery-fiche-title");
            Assert.IsNotNull(title);
            Assert.AreEqual(vm.SelectedBody.Value.Name, title.text, "Fiche title must follow the selected body.");

            var markers = controller.RootElement.Query(className: "orrery-planet").ToList();
            var selected = markers.Where(m => m.ClassListContains("orrery-planet--selected")).ToList();
            Assert.AreEqual(1, selected.Count, "Exactly one planet carries the brass selection ring.");

            vm.SelectBody("mars");
            var selectedAfter = controller.RootElement.Query(className: "orrery-planet--selected").ToList();
            Assert.AreEqual(1, selectedAfter.Count, "Selection ring must move to the new body.");
        }

        // ── 3. Catalog fiche photo ────────────────────────────────────────────────

        [Test]
        public void FichePhoto_ShowsCreditCaptionOrPlaceholder()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var slot = controller.RootElement.Q(className: "orrery-fiche-photo");
            Assert.IsNotNull(slot, "Fiche photo slot must exist.");

            bool hasCaption = slot.Q(className: "orrery-fiche-photo__caption") != null;
            bool hasFallback = slot.Q(className: "orrery-fiche-photo__fallback") != null;
            Assert.IsTrue(hasCaption || hasFallback,
                "Photo slot shows either the NASA credit caption or the waiting placeholder.");

            // In the editor the shipped asset loads; the credit must then render.
            if (FindResourcesFile(Path.Combine("Destinations", "Art", "mars.jpg")) != null)
            {
                var captions = controller.RootElement.Query<Label>()
                    .Where(l => l.text.StartsWith("Kaynak: NASA")).ToList();
                Assert.GreaterOrEqual(captions.Count, 1,
                    "With shipped assets present, the fiche shows the Turkish NASA credit.");
            }
        }

        // ── 4. Transfer window card ───────────────────────────────────────────────

        [Test]
        public void WindowBadge_SwapsOpenClosedState()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var badge = controller.RootElement.Q<Label>(className: "orrery-window-badge");
            Assert.IsNotNull(badge, "Window badge must exist.");

            vm.SelectBody("mars"); // day 0: Mars window not open yet (first offset ~400 days)
            StringAssert.DoesNotContain("AÇIK", badge.text, "Mars window must be closed at day 0.");
            Assert.IsTrue(badge.ClassListContains("orrery-window-badge--closed"),
                "Closed windows carry the brass closed badge.");

            vm.SelectBody("leo"); // LEO has a nearly permanent window
            if (vm.SelectedBody.Value.IsWindowOpen)
            {
                Assert.IsTrue(badge.ClassListContains("orrery-window-badge--open"),
                    "Open windows carry the cyan open badge.");
                StringAssert.Contains("AÇIK", badge.text);
            }
            else
            {
                Assert.IsTrue(badge.ClassListContains("orrery-window-badge--closed"));
            }
        }

        // ── 5. Active missions list ───────────────────────────────────────────────

        [Test]
        public void ActiveMissions_EmptyWithoutCampaign()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var labels = controller.RootElement.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("Aktif seyirde uzay aracı bulunmuyor")),
                "Empty cruise state must render.");
            Assert.AreEqual(0, controller.RootElement.Query(className: "orrery-craft-pin").ToList().Count,
                "No craft pins without a campaign.");
        }

        // ── 6. Time controls ──────────────────────────────────────────────────────

        [Test]
        public void SpeedButtons_FiveControls_WithActiveHighlight()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var buttons = controller.RootElement.Query<Button>(className: "orrery-speed-btn").ToList();
            Assert.AreEqual(5, buttons.Count, "Pause, 1x, 2x, 3x and +1G controls must exist.");

            // Day 0 with no campaign → paused is highlighted.
            var active = buttons.Where(b => b.ClassListContains("orrery-speed-btn--active")).ToList();
            Assert.AreEqual(1, active.Count, "Exactly one speed control is highlighted.");
            Assert.AreEqual("II", active[0].text, "Paused control is highlighted by default.");
        }

        // ── 7. Shipped catalog integrity ──────────────────────────────────────────

        [Test]
        public void ShippedCatalog_AllDestinationsCarryArtAndCredits()
        {
            foreach (var dest in _destinations.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(dest.Art), $"{dest.Id} must carry a photo path.");
                Assert.IsFalse(string.IsNullOrEmpty(dest.ArtCredit), $"{dest.Id} must carry a Turkish NASA credit line.");
                StringAssert.StartsWith("Destinations/Art/", dest.Art,
                    "Destination art must live in the destination resources folder.");

                string file = FindResourcesFile(Path.Combine("Destinations", "Art", dest.Art.Substring("Destinations/Art/".Length) + ".jpg"));
                Assert.IsNotNull(file, $"{dest.Id} art '{dest.Art}.jpg' must exist under Assets/Resources/Destinations/Art/.");
            }
        }

        [Test]
        public void ShippedCatalog_BodyTelemetry_IsConsistent()
        {
            var vm = CreateViewModel();
            var bodies = vm.Bodies.Value;

            Assert.AreEqual(5, bodies.Count, "Five catalog bodies expected (LEO, Moon, Mars, Venus, Europa).");

            foreach (var body in bodies)
            {
                Assert.Greater(body.DvTotalMps, 0.0, $"{body.Id} must report a positive Δv budget.");
                Assert.Greater(body.SemiMajorAxisAu, 0.0, $"{body.Id} must report a positive semi-major axis.");
                Assert.IsFalse(string.IsNullOrEmpty(body.TransitDurationText));
                Assert.IsFalse(string.IsNullOrEmpty(body.CommDelayText));
            }
        }
    }
}
