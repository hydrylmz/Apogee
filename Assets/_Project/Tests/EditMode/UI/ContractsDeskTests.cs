using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// "Sözleşme Masası" identity tests for the Contracts screen: wooden desk +
    /// warm paper theme wiring, supplier business cards with relationship stamps,
    /// offer letters with product photo placeholders and ActionButton sign
    /// contract, the paper SignatureDialog flow, and the explicit-outcome
    /// ConfirmDialog contract for cancellations (GDD §9.1, §14).
    /// </summary>
    [TestFixture]
    public class ContractsDeskTests
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

        private CatalogCollection<SupplierRecord> _catalog;

        [OneTimeSetUp]
        public void Setup()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            }
            if (!File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "suppliers.json");
            }

            string json = File.ReadAllText(path);
            _catalog = CatalogLoader.Load<SupplierRecord>(json);
            Assert.IsTrue(_catalog.Validation.IsValid, "Suppliers catalog must be valid.");
        }

        private static ContractsScreenController CreateInitializedController(
            out ContractsViewModel vm, out VisualElement host)
        {
            vm = new ContractsViewModel(new MockNavigator());
            var controller = new ContractsScreenController(vm);
            host = new VisualElement();
            controller.Initialize(host);
            return controller;
        }

        private ContractsScreenController CreateControllerWithCampaign(
            out ContractsViewModel vm, out VisualElement host, out CampaignState campaign)
        {
            campaign = new CampaignState();
            campaign.InitialiseSuppliers(_catalog.All);

            vm = new ContractsViewModel(new MockNavigator());
            var controller = new ContractsScreenController(vm);
            host = new VisualElement();
            controller.Initialize(host);

            vm.BindCampaign(campaign);
            return controller;
        }

        [Test]
        public void ViewModel_ExposesNavigator_ForSharedNavBar()
        {
            var navigator = new MockNavigator();
            var vm = new ContractsViewModel(navigator);

            Assert.AreSame(navigator, vm.Navigator, "Navigator must be exposed for the shared ScreenNavBar.");
        }

        [Test]
        public void ContractDesk_RootHasThemeAndDeskStructure()
        {
            var controller = CreateInitializedController(out var vm, out var host);

            var root = controller.RootElement;

            Assert.IsTrue(root.ClassListContains("theme-contract-desk"), "Screen root must carry the contract desk theme.");
            Assert.IsNotNull(root.Q(className: "apogee-navbar"), "Shared ScreenNavBar must be present.");
            Assert.IsNotNull(root.Q(className: "contract-desk-backdrop"), "Walnut plank backdrop must be present.");
            Assert.IsNotNull(root.Q(className: "contract-desk-header"), "Desk header bar must be present.");

            var boards = root.Query(className: "contract-column-board").ToList();
            Assert.AreEqual(3, boards.Count, "Business card column, offers board and archive board must exist.");

            var labels = root.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("SÖZLEŞME MASASI")), "Desk title must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("TEDARİKÇİ KARTVİZİTLERİ")), "Business card column header must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("GELEN TEKLİF MEKTUPLARI")), "Offer letters header must be rendered.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("SÖZLEŞME ARŞİVİ")), "Archive header must be rendered.");
        }

        [Test]
        public void BusinessCards_RenderWithStampsAndSelectionMark()
        {
            var controller = CreateControllerWithCampaign(out var vm, out var host, out var campaign);

            var root = controller.RootElement;
            var cards = root.Query(className: "contract-bizcard").ToList();
            Assert.GreaterOrEqual(cards.Count, 6, "All catalog suppliers must render as business cards.");

            var relStamps = root.Query<Label>().ToList().Where(l => l.text.StartsWith("İLİŞKİ %")).ToList();
            Assert.GreaterOrEqual(relStamps.Count, cards.Count, "Every business card must carry a relationship stamp.");

            // Select the second supplier → exactly one selected card.
            var secondId = vm.Companies.Value[1].Id;
            vm.SelectCompany(secondId);

            var selected = root.Query(className: "contract-bizcard--selected").ToList();
            Assert.AreEqual(1, selected.Count, "Exactly one business card must be marked selected.");

            // Offers from the selected supplier get the focused letter mark.
            var focusedLetters = root.Query(className: "contract-offer-letter--focused").ToList();
            Assert.GreaterOrEqual(focusedLetters.Count, 1, "Selecting a supplier must highlight its offer letters.");
        }

        [Test]
        public void OfferLetters_RenderPhotoSlotAndSignButton()
        {
            var controller = CreateControllerWithCampaign(out var vm, out var host, out var campaign);

            var root = controller.RootElement;

            var letters = root.Query(className: "contract-offer-letter").ToList();
            Assert.GreaterOrEqual(letters.Count, 6, "Every standard supplier offer must render as a letter.");

            var photoSlots = root.Query(className: "contract-offer-photo").ToList();
            Assert.AreEqual(letters.Count, photoSlots.Count, "Every offer letter must carry a product photo slot.");

            var slotOverlays = root.Query<Label>().ToList()
                .Where(l => l.ClassListContains("contract-offer-photo__placeholder")
                         || l.ClassListContains("contract-offer-photo__caption")).ToList();
            Assert.GreaterOrEqual(slotOverlays.Count, 1,
                "Every photo slot must show either the drop-in placeholder or the loaded-photo caption.");

            var signButtons = root.Query<Label>().ToList().Where(l => l.text == "İmzala").ToList();
            Assert.AreEqual(letters.Count, signButtons.Count, "Every letter must offer an İmzala action.");

            // With a healthy budget no sign button is disabled; with an empty
            // budget every button must expose its reason (interaction contract #3).
            Assert.IsFalse(root.Query(className: "apogee-action-btn--disabled").ToList().Any(),
                "Default budget must afford at least one offer — no disabled sign buttons expected.");

            vm.Budget.Value = 0.0;
            Assert.GreaterOrEqual(root.Query(className: "apogee-action-btn--disabled").ToList().Count, letters.Count,
                "Empty budget must disable every sign button with a reason.");
        }

        [Test]
        public void SignFlow_OpensSignatureDialog_AndFilesTheContract()
        {
            var controller = CreateControllerWithCampaign(out var vm, out var host, out var campaign);

            var offer = vm.AvailableOffers.Value[0];
            double budgetBefore = vm.Budget.Value;

            controller.OpenSignatureDialog(offer);

            var backdrop = controller.RootElement.Q(className: SignatureDialog.BackdropClassName);
            Assert.IsNotNull(backdrop, "Signing must open the paper SignatureDialog.");

            var labels = controller.RootElement.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text.Contains("TEDARİK SÖZLEŞMESİ")), "Dialog must present the contract sheet title.");
            Assert.IsTrue(labels.Any(l => l.text.Contains("İmzala ve Mühürle")), "Confirm button must name the sign action.");

            var dialog = (SignatureDialog)backdrop;
            dialog.Sign();

            Assert.IsNull(controller.RootElement.Q(className: SignatureDialog.BackdropClassName),
                "Dialog must remove itself after signing.");

            var folders = controller.RootElement.Query(className: "contract-folder").ToList();
            Assert.AreEqual(1, folders.Count, "Signed contract must be filed into the archive as a folder.");

            var stamps = controller.RootElement.Query<Label>().ToList()
                .Where(l => l.ClassListContains("contract-stamp")).ToList();
            Assert.IsTrue(stamps.Any(l => l.text == "TESLİM BEKLENİYOR"), "Fresh contract must carry the pending stamp.");

            Assert.AreEqual(budgetBefore - offer.BaseCost, vm.Budget.Value, 1e-4,
                "Signing must deduct the offer cost from the budget.");
        }

        [Test]
        public void SignatureDialog_ConfirmDisabledWhenBudgetInsufficient()
        {
            var controller = CreateControllerWithCampaign(out var vm, out var host, out var campaign);
            vm.Budget.Value = 0.0;

            var offer = vm.AvailableOffers.Value[0];
            controller.OpenSignatureDialog(offer);

            var confirm = controller.RootElement.Q<Button>(className: "contract-sign-confirm");
            Assert.IsNotNull(confirm, "Dialog must carry the İmzala ve Mühürle button.");
            Assert.IsFalse(confirm.enabledSelf, "Confirm must be disabled when the budget cannot afford the offer.");
            Assert.IsTrue(confirm.ClassListContains("contract-sign-confirm--disabled"),
                "Disabled confirm must carry the dimmed style.");
            Assert.IsFalse(string.IsNullOrEmpty(confirm.tooltip), "Disabled confirm must explain why (interaction contract #3).");

            ((SignatureDialog)controller.RootElement.Q(className: SignatureDialog.BackdropClassName)).Dismiss();
            Assert.IsNull(controller.RootElement.Q(className: SignatureDialog.BackdropClassName),
                "Dialog must remove itself after dismissing.");
        }

        [Test]
        public void CancelFlow_OpensConfirmDialog_AndStampsCancelled()
        {
            var controller = CreateControllerWithCampaign(out var vm, out var host, out var campaign);

            var offer = vm.AvailableOffers.Value[0];
            Assert.IsTrue(vm.SignContract(offer), "Contract must sign cleanly with the default budget.");
            Assert.AreEqual(1, vm.Contracts.Value.Count, "One active contract must exist after signing.");

            var contract = vm.Contracts.Value[0];
            controller.ConfirmCancelContract(contract);

            var backdrop = controller.RootElement.Q(className: ConfirmDialog.BackdropClassName);
            Assert.IsNotNull(backdrop, "Cancellation must open a ConfirmDialog (contract #2).");

            var message = controller.RootElement.Query<Label>().ToList()
                .FirstOrDefault(l => l.ClassListContains("apogee-confirm-message"));
            Assert.IsNotNull(message);
            StringAssert.Contains("iade", message.text, "Dialog must state the refund outcome explicitly.");
            StringAssert.Contains("ilişki", message.text, "Dialog must state the relationship penalty explicitly.");

            var dialog = (ConfirmDialog)backdrop;
            dialog.Confirm();

            var stamps = controller.RootElement.Query<Label>().ToList()
                .Where(l => l.ClassListContains("contract-stamp")).ToList();
            Assert.IsTrue(stamps.Any(l => l.text == "İPTAL EDİLDİ"), "Cancelled contract must carry the cancelled stamp.");
            Assert.IsNotNull(controller.RootElement.Q(className: "contract-folder--cancelled"),
                "Cancelled folder must carry the dimmed archive style.");
        }

        [Test]
        public void StatusNames_AreStampReady_WithoutGlyphPrefixes()
        {
            Assert.AreEqual("Teslim Bekleniyor", SupplierContractItemViewModel.GetStatusName(ContractStatus.PendingDelivery));
            Assert.AreEqual("Teslim Edildi", SupplierContractItemViewModel.GetStatusName(ContractStatus.Fulfilled));
            Assert.AreEqual("İptal Edildi", SupplierContractItemViewModel.GetStatusName(ContractStatus.Cancelled));
        }
    }
}
