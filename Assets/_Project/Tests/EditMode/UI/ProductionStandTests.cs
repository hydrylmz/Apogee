using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Production;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// "Montaj Salonu" identity tests for the Production Queue screen (ADR-064):
    /// theme wiring, stamped work order ledger, conveyor pallets, assembly stand
    /// selection, NASA photo placeholder fallback, and the irreversible-cancel
    /// ConfirmDialog contract.
    /// </summary>
    [TestFixture]
    public class ProductionStandTests
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

        private static List<ProductionJob> CreateJobs()
        {
            return new List<ProductionJob>
            {
                new ProductionJob("JOB-VEH-001", "ATLAS-1", ProductionJobType.VehicleAssembly,
                    20, 40.0, new GameDate(0), 5.0, null, 0.0, ProductionJobStatus.InProgress),
                new ProductionJob("JOB-TST-002", "ATLAS-2 Kalifikasyon", ProductionJobType.SystemTesting,
                    25, 10.0, new GameDate(0), 3.0, null, 0.0, ProductionJobStatus.Queued),
                new ProductionJob("JOB-CMP-003", "AY-1 Modülü", ProductionJobType.ComponentFabrication,
                    12, 8.0, new GameDate(0), 2.0, null, 0.0, ProductionJobStatus.Completed),
            };
        }

        private static ProductionQueueScreenController CreateInitializedController(
            out ProductionQueueViewModel vm, out VisualElement host)
        {
            vm = new ProductionQueueViewModel(new MockNavigator());
            var controller = new ProductionQueueScreenController(vm);
            host = new VisualElement();
            controller.Initialize(host);
            return controller;
        }

        [Test]
        public void ViewModel_ExposesNavigator_ForSharedNavBar()
        {
            var navigator = new MockNavigator();
            var vm = new ProductionQueueViewModel(navigator);

            Assert.AreSame(navigator, vm.Navigator, "ADR-063: VM must expose the navigator to the controller.");
        }

        [Test]
        public void HangarFloor_RootHasThemeAndStandStructure()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            var root = controller.RootElement;

            Assert.IsTrue(root.ClassListContains("theme-hangar-floor"), "Screen root must carry the hangar floor theme.");
            Assert.IsNotNull(root.Q(className: "apogee-navbar"), "Shared ScreenNavBar must be present.");
            Assert.IsNotNull(root.Q(className: "floor-stand"), "Assembly stand panel must be present.");
            Assert.IsNotNull(root.Q(className: "floor-conveyor"), "Conveyor band must be present.");
            Assert.IsNotNull(root.Q(className: "floor-photo-frame"), "NASA photo card must be present.");

            var labels = root.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("MONTAJ SALONU")), "Hangar title must be rendered.");
            Assert.IsFalse(labels.Any(l => l.text.StartsWith("KAYNAK:")),
                "Photo attribution line must not be rendered (removed from the stand).");
        }

        [Test]
        public void Ledger_RendersStampedWorkOrderCardsAndConveyorPallets()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            var root = controller.RootElement;

            var cards = root.Query(className: "floor-workorder").ToList();
            Assert.AreEqual(3, cards.Count, "Ledger must render one stamped card per job.");

            var pallets = root.Query(className: "floor-pallet").ToList();
            Assert.AreEqual(2, pallets.Count, "Conveyor must carry only Queued + InProgress jobs.");

            var stamps = root.Query<Label>().ToList().Where(l => l.ClassListContains("floor-stamp")).ToList();
            Assert.IsTrue(stamps.Any(l => l.text == "ÜRETİMDE" && l.ClassListContains("floor-stamp--inprogress")),
                "In-progress job must carry the ÜRETİMDE stamp.");
            Assert.IsTrue(stamps.Any(l => l.text == "KUYRUKTA" && l.ClassListContains("floor-stamp--queued")),
                "Queued job must carry the KUYRUKTA stamp.");
            Assert.IsTrue(stamps.Any(l => l.text == "TAMAMLANDI" && l.ClassListContains("floor-stamp--completed")),
                "Completed job must carry the TAMAMLANDI stamp.");
        }

        [Test]
        public void SelectJobById_UpdatesAssemblyStandAndSelectionMarks()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            controller.SelectJobById("JOB-TST-002");

            var root = controller.RootElement;
            var labels = root.Query<Label>().ToList();

            Assert.IsTrue(labels.Any(l => l.text == "İŞ EMRİ #JOB-TST-002"),
                "Stand header must show the selected job's work order code.");
            Assert.IsTrue(labels.Any(l => l.text == "ATLAS-2 Kalifikasyon"),
                "Stand must show the selected job's vehicle name.");

            var selectedCards = root.Query(className: "floor-workorder--selected").ToList();
            Assert.AreEqual(1, selectedCards.Count, "Exactly one ledger card must be marked selected.");
            var selectedPallets = root.Query(className: "floor-pallet--selected").ToList();
            Assert.AreEqual(1, selectedPallets.Count, "Exactly one conveyor pallet must be marked selected.");
        }

        [Test]
        public void RequestCancelJob_OpensConfirmDialog_AndCancelClosesIt()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            controller.RequestCancelJob("JOB-VEH-001");

            var backdrop = controller.RootElement.Q(className: ConfirmDialog.BackdropClassName);
            Assert.IsNotNull(backdrop, "Irreversible cancel must open a ConfirmDialog (contract #2).");

            var dialog = (ConfirmDialog)backdrop;
            dialog.Cancel();

            Assert.IsNull(controller.RootElement.Q(className: ConfirmDialog.BackdropClassName),
                "Dialog must remove itself after cancelling.");
        }

        [Test]
        public void RequestCancelJob_ConfirmInvokesVmCancelWithoutCampaign()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            controller.RequestCancelJob("JOB-VEH-001");

            var dialog = (ConfirmDialog)controller.RootElement.Q(className: ConfirmDialog.BackdropClassName);
            Assert.DoesNotThrow(() => dialog.Confirm(),
                "Confirm must reach ViewModel.CancelJob and stay graceful without a bound campaign.");
            Assert.IsFalse(vm.CancelJob("JOB-VEH-001"), "No campaign bound — cancellation must be a no-op.");
            Assert.IsNull(controller.RootElement.Q(className: ConfirmDialog.BackdropClassName),
                "Dialog must remove itself after confirmation.");
        }

        [Test]
        public void PhotoCard_ShowsNasaPhotoForAssemblyJobs_AndHidesForNonAssemblyJobs()
        {
            var controller = CreateInitializedController(out var vm, out var host);
            vm.Jobs.Value = CreateJobs().Select(j => new ProductionJobItemViewModel(j)).ToList();

            var root = controller.RootElement;

            // Auto-selection picks the first InProgress job (VehicleAssembly) → photo card visible.
            var photoFrame = root.Q(className: "floor-photo-frame");
            Assert.IsNotNull(photoFrame);
            Assert.AreEqual(DisplayStyle.Flex, photoFrame.style.display.value,
                "VehicleAssembly jobs must show the NASA work-log photo card.");

            var placeholder = root.Query<Label>().ToList()
                .FirstOrDefault(l => l.text.Contains("NASA FOTOĞRAFI BEKLENİYOR"));
            Assert.IsNotNull(placeholder, "Placeholder label must exist in the photo frame.");

            var credit = root.Query<Label>().ToList().Any(l => l.text.StartsWith("KAYNAK:"));
            Assert.IsFalse(credit, "Attribution line must stay removed from the photo card.");

            // The NASA photos are committed under Assets/Resources/Production/Photos/ —
            // Resources.Load finds them, so the placeholder must hide in favor of the photo.
            // (If the files are ever removed, this fails loudly to flag the missing asset.)
            Assert.AreEqual(DisplayStyle.None, placeholder.style.display.value,
                "Photo files are present in Resources — placeholder must hide after a successful load.");

            // Select a SystemTesting job → photo card hides entirely.
            controller.SelectJobById("JOB-TST-002");
            Assert.AreEqual(DisplayStyle.None, photoFrame.style.display.value,
                "NASA photo card must hide for non VehicleAssembly jobs.");
        }
    }
}
