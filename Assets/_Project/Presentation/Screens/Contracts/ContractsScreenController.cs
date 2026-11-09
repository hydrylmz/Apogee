using System;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Contracts
{
    /// <summary>
    /// UI Toolkit screen controller for Strategic Supplier Relations and Contracts (ScreenId.Contracts = 9).
    /// Complies with GDD §9.1, §14.1 (#8 Şirketler/Sözleşmeler), and M9-05.
    /// Provides supplier company cards, single-source risk analysis, contract procurement, and delivery status.
    /// </summary>
    public sealed class ContractsScreenController : ScreenControllerBase<ContractsViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Contracts;

        // Top HUD
        private Label _titleLabel;
        private Label _budgetLabel;
        private Label _riskLabel;
        private Label _contractsCountLabel;
        private Button _backBtn;

        // Companies List
        private ScrollView _companiesList;

        // Offers List
        private ScrollView _offersList;

        // Active Contracts List
        private ScrollView _contractsList;

        // Footer
        private Label _statusLabel;

        public ContractsScreenController(ContractsViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-contracts");
            root.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // ── TOP HEADER ──────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var leftHeader = new VisualElement();
            leftHeader.style.flexDirection = FlexDirection.Row;
            leftHeader.style.alignItems = Align.Center;

            _backBtn = new Button(() => ViewModel.BackCommand.Execute()) { text = "← Geri (Harita)" };
            _backBtn.style.marginRight = 16;
            _backBtn.style.paddingLeft = 12;
            _backBtn.style.paddingRight = 12;
            _backBtn.style.paddingTop = 6;
            _backBtn.style.paddingBottom = 6;
            _backBtn.style.backgroundColor = new Color(0.15f, 0.20f, 0.30f);
            _backBtn.style.color = Color.white;
            leftHeader.Add(_backBtn);

            _titleLabel = new Label("💼 TEDARİKÇİLER VE SÖZLEŞMELER");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            leftHeader.Add(_titleLabel);
            header.Add(leftHeader);

            var rightHeader = new VisualElement();
            rightHeader.style.flexDirection = FlexDirection.Row;
            rightHeader.style.alignItems = Align.Center;

            _budgetLabel = CreatePill("💰 0 M$", new Color(0.4f, 0.9f, 0.5f));
            _riskLabel = CreatePill("🛡 Risk: Düşük", new Color(0.35f, 0.85f, 1f));
            _contractsCountLabel = CreatePill("📋 0 Sözleşme", new Color(0.85f, 0.75f, 0.35f));

            rightHeader.Add(_budgetLabel);
            rightHeader.Add(_riskLabel);
            rightHeader.Add(_contractsCountLabel);
            header.Add(rightHeader);

            root.Add(header);

            // ── MAIN BODY: 2-COLUMN SPLIT ───────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            // Left: Companies List
            var leftCol = new VisualElement();
            leftCol.style.width = 330;
            leftCol.style.paddingLeft = 16;
            leftCol.style.paddingRight = 12;
            leftCol.style.paddingTop = 14;
            leftCol.style.paddingBottom = 14;
            leftCol.style.backgroundColor = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            leftCol.style.borderRightWidth = 1;
            leftCol.style.borderRightColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var compHeader = new Label("KURGUSAL TEDARİKÇİ ŞİRKETLER");
            compHeader.style.fontSize = 12;
            compHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            compHeader.style.color = new Color(0.4f, 0.8f, 1f);
            compHeader.style.marginBottom = 10;
            leftCol.Add(compHeader);

            _companiesList = new ScrollView();
            _companiesList.style.flexGrow = 1;
            leftCol.Add(_companiesList);
            body.Add(leftCol);

            // Right: Offers & Contracts
            var rightCol = new VisualElement();
            rightCol.style.flexGrow = 1;
            rightCol.style.paddingLeft = 16;
            rightCol.style.paddingRight = 16;
            rightCol.style.paddingTop = 14;
            rightCol.style.paddingBottom = 14;

            // Offers Section
            var offersHeader = new Label("İMZALANABİLİR TEDARİK TEKLİFLERİ");
            offersHeader.style.fontSize = 12;
            offersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            offersHeader.style.color = new Color(0.4f, 0.8f, 1f);
            offersHeader.style.marginBottom = 8;
            rightCol.Add(offersHeader);

            _offersList = new ScrollView();
            _offersList.style.height = 180;
            _offersList.style.marginBottom = 12;
            rightCol.Add(_offersList);

            // Active Contracts Section
            var contractsHeader = new Label("SÖZLEŞMELER & TESLİMATLAR");
            contractsHeader.style.fontSize = 12;
            contractsHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            contractsHeader.style.color = new Color(0.4f, 0.8f, 1f);
            contractsHeader.style.marginBottom = 8;
            rightCol.Add(contractsHeader);

            _contractsList = new ScrollView();
            _contractsList.style.flexGrow = 1;
            rightCol.Add(_contractsList);

            body.Add(rightCol);
            root.Add(body);

            // ── FOOTER ──────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.paddingTop = 6;
            footer.style.paddingBottom = 6;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.15f, 0.20f, 0.30f);

            _statusLabel = new Label("Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.4f, 0.8f, 1f);
            footer.Add(_statusLabel);
            root.Add(footer);

            return root;
        }

        protected override void OnInitialized()
        {
            RegisterObservables();
        }

        protected override void OnShown(object context)
        {
            if (context is CampaignState campaign)
            {
                ViewModel?.BindCampaign(campaign);
            }
            else
            {
                ViewModel?.Refresh();
            }

            RenderCompanies();
            RenderOffers();
            RenderContracts();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"💰 {b:F1} M$";
            });

            ViewModel.RiskLevelText.Subscribe(r =>
            {
                if (_riskLabel != null) _riskLabel.text = $"🛡 {r}";
            });

            ViewModel.ActiveContractsCount.Subscribe(c =>
            {
                if (_contractsCountLabel != null) _contractsCountLabel.text = $"📋 {c} Aktif";
            });

            ViewModel.Companies.Subscribe(_ => RenderCompanies());
            ViewModel.AvailableOffers.Subscribe(_ => RenderOffers());
            ViewModel.Contracts.Subscribe(_ => RenderContracts());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void RenderCompanies()
        {
            if (_companiesList == null || ViewModel == null) return;
            _companiesList.Clear();

            var comps = ViewModel.Companies.Value;
            if (comps == null || comps.Count == 0)
            {
                var emptyLabel = new Label("Tedarikçi verisi bulunamadı.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _companiesList.Add(emptyLabel);
                return;
            }

            foreach (var comp in comps)
            {
                var card = CreateCompanyCard(comp);
                _companiesList.Add(card);
            }
        }

        private VisualElement CreateCompanyCard(SupplierCompanyItemViewModel comp)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 4;

            Color brandCol = new Color(0.3f, 0.6f, 0.9f);
            if (!string.IsNullOrEmpty(comp.BrandColor) && ColorUtility.TryParseHtmlString(comp.BrandColor, out Color parsed))
            {
                brandCol = parsed;
            }
            card.style.borderLeftColor = brandCol;

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;

            var nameLabel = new Label(comp.Name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.95f, 0.98f, 1f);
            topRow.Add(nameLabel);

            var relLabel = new Label($"İlişki: {comp.RelationshipPercent}");
            relLabel.style.fontSize = 11;
            relLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            relLabel.style.color = new Color(0.4f, 0.85f, 0.6f);
            topRow.Add(relLabel);
            card.Add(topRow);

            var strengthLabel = new Label($"Güçlü: {comp.Strengths}");
            strengthLabel.style.fontSize = 10;
            strengthLabel.style.color = new Color(0.6f, 0.85f, 0.7f);
            strengthLabel.style.marginTop = 2;
            card.Add(strengthLabel);

            var weakLabel = new Label($"Zayıf: {comp.Weaknesses}");
            weakLabel.style.fontSize = 10;
            weakLabel.style.color = new Color(0.85f, 0.65f, 0.6f);
            card.Add(weakLabel);

            if (comp.ActiveContractsCount > 0)
            {
                var activeLabel = new Label($"Aktif Sözleşmeler: {comp.ActiveContractsCount}");
                activeLabel.style.fontSize = 10;
                activeLabel.style.color = new Color(0.5f, 0.8f, 1f);
                activeLabel.style.marginTop = 2;
                card.Add(activeLabel);
            }

            return card;
        }

        private void RenderOffers()
        {
            if (_offersList == null || ViewModel == null) return;
            _offersList.Clear();

            var offers = ViewModel.AvailableOffers.Value;
            if (offers == null || offers.Count == 0)
            {
                var emptyLabel = new Label("Şu an mevcut tedarik teklifi yok.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _offersList.Add(emptyLabel);
                return;
            }

            foreach (var offer in offers)
            {
                var card = CreateOfferCard(offer);
                _offersList.Add(card);
            }
        }

        private VisualElement CreateOfferCard(ContractOfferItemViewModel offer)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 6;
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;

            var left = new VisualElement();
            var title = new Label(offer.ItemName);
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.98f, 1f);
            left.Add(title);

            var meta = new Label($"{offer.SupplierName}  ·  {offer.BaseCost:F1} M$  ·  Teslim: {offer.DeliveryDays} gün  ·  Güvenilirlik: x{offer.ReliabilityMultiplier:F2}");
            meta.style.fontSize = 10;
            meta.style.color = new Color(0.6f, 0.75f, 0.9f);
            left.Add(meta);
            card.Add(left);

            var signBtn = new Button(() => ViewModel.SignContract(offer)) { text = "✍ İmzala" };
            signBtn.style.backgroundColor = new Color(0.18f, 0.50f, 0.35f);
            signBtn.style.color = Color.white;
            signBtn.style.paddingLeft = 10;
            signBtn.style.paddingRight = 10;
            signBtn.style.paddingTop = 4;
            signBtn.style.paddingBottom = 4;
            signBtn.style.fontSize = 11;
            signBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(signBtn);

            return card;
        }

        private void RenderContracts()
        {
            if (_contractsList == null || ViewModel == null) return;
            _contractsList.Clear();

            var contracts = ViewModel.Contracts.Value;
            if (contracts == null || contracts.Count == 0)
            {
                var emptyLabel = new Label("Henüz imzalanmış bir tedarik sözleşmesi bulunmamaktadır.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _contractsList.Add(emptyLabel);
                return;
            }

            foreach (var contract in contracts)
            {
                var card = CreateContractCard(contract);
                _contractsList.Add(card);
            }
        }

        private VisualElement CreateContractCard(SupplierContractItemViewModel contract)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 6;
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;

            var left = new VisualElement();
            var title = new Label($"{contract.ItemName} ({contract.SupplierName})");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.98f, 1f);
            left.Add(title);

            var meta = new Label($"{contract.StatusName}  ·  Maliyet: {contract.CostMUSD:F1} M$  ·  Hedef Tarih: {contract.DeliveryDate}");
            meta.style.fontSize = 10;
            meta.style.color = new Color(0.6f, 0.75f, 0.9f);
            left.Add(meta);
            card.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            if (contract.CanCancel)
            {
                var cancelBtn = new Button(() => ViewModel.CancelContract(contract.Id)) { text = "İptal" };
                cancelBtn.style.backgroundColor = new Color(0.40f, 0.15f, 0.15f);
                cancelBtn.style.color = new Color(1f, 0.7f, 0.7f);
                cancelBtn.style.paddingLeft = 8;
                cancelBtn.style.paddingRight = 8;
                cancelBtn.style.paddingTop = 3;
                cancelBtn.style.paddingBottom = 3;
                cancelBtn.style.fontSize = 10;
                right.Add(cancelBtn);
            }
            card.Add(right);

            return card;
        }

        private static Label CreatePill(string text, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            label.style.marginLeft = 10;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            label.style.borderTopLeftRadius = 10;
            label.style.borderTopRightRadius = 10;
            label.style.borderBottomLeftRadius = 10;
            label.style.borderBottomRightRadius = 10;
            return label;
        }
    }
}
