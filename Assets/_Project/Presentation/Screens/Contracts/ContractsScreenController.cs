using System;
using System.Collections.Generic;
using Apogee.Strategy;
using Apogee.UI.Core.Controls;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Contracts
{
    /// <summary>
    /// UI Toolkit screen controller for Strategic Supplier Relations and Contracts
    /// (ScreenId.Contracts = 9). Complies with GDD §9.1, §14.1 (#8 Şirketler/Sözleşmeler),
    /// and M9-05. "Dosya klasörü" identity: a walnut contract desk holding supplier
    /// business cards on the left, incoming offer letters with product photos on the
    /// top right, and stamped archive folders for signed contracts below. Signing
    /// goes through a paper SignatureDialog; cancellation through a destructive
    /// ConfirmDialog with the explicit refund/relationship outcome.
    /// </summary>
    public sealed class ContractsScreenController : ScreenControllerBase<ContractsViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Contracts;

        // Palette mirrors the .theme-contract-desk USS tokens (kept as C# constants
        // for inline styles, same convention as RadarScopeView / AssemblyStandView).
        private static readonly Color DeskColor = new(0.133f, 0.082f, 0.063f);       // #221510 walnut
        private static readonly Color BrassColor = new(0.922f, 0.796f, 0.518f);      // #EBCB84
        private static readonly Color BrassInkColor = new(0.643f, 0.463f, 0.122f);   // #A4761F on paper
        private static readonly Color ParchmentColor = new(0.941f, 0.902f, 0.824f);  // #F0E6D2
        private static readonly Color InkSoftColor = new(0.431f, 0.361f, 0.263f);    // #6E5C43
        private static readonly Color InkMutedColor = new(0.549f, 0.478f, 0.369f);   // #8C7A5E
        private static readonly Color StampGreenColor = new(0.345f, 0.455f, 0.263f); // #587443
        private static readonly Color StampAmberColor = new(0.663f, 0.463f, 0.122f); // #A9761F
        private static readonly Color SealRedColor = new(0.612f, 0.231f, 0.180f);    // #9C3B2E

        // Offer product photos (drop-in: Assets/Resources/Contracts/Photos/<name>.jpg).

        // HUD / lists
        private ScreenNavBar _navBar;
        private Label _budgetLabel;
        private Label _riskLabel;
        private Label _activeCountLabel;
        private ScrollView _companiesList;
        private ScrollView _offersList;
        private ScrollView _contractsList;
        private Label _offersCountLabel;
        private Label _contractsCountLabel;

        private VisualElement _root;

        public ContractsScreenController(ContractsViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            _root = new VisualElement();
            _root.AddToClassList("screen-root");
            _root.AddToClassList("theme-contract-desk");
            _root.style.backgroundColor = DeskColor;
            _root.style.flexDirection = FlexDirection.Column;

            // Walnut plank backdrop behind all desk furniture.
            var backdrop = new ContractDeskBackdrop();
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0;
            backdrop.style.top = 0;
            backdrop.style.right = 0;
            backdrop.style.bottom = 0;
            _root.Add(backdrop);

            // ── 1. Desk header: title + HUD pills ────────────────────────────────
            _root.Add(BuildHeader());

            // ── 2. Desk body: business cards (left) + letters/archive (right) ────
            var body = new VisualElement();
            body.AddToClassList("screen-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.flexBasis = 0;
            body.style.minHeight = 0;

            body.Add(BuildBusinessCardColumn());
            body.Add(BuildLettersColumn());

            _root.Add(body);

            // ── 3. Shared icon nav bar — docked to the bottom like every panel ───
            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.Contracts);
            _root.Add(_navBar);

            return _root;
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

        // ── Builders ────────────────────────────────────────────────────────────

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("contract-desk-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;

            var titleBox = new VisualElement();
            var title = new Label("SÖZLEŞME MASASI");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ParchmentColor;
            titleBox.Add(title);

            var subtitle = new Label("Tedarikçi İlişkileri & Sözleşme Arşivi · Satın Alma Dairesi");
            subtitle.style.fontSize = 9;
            subtitle.style.color = InkMutedColor;
            titleBox.Add(subtitle);
            header.Add(titleBox);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            _budgetLabel = BuildHudPill(header, IconType.Budget, "0.0 M$",
                "Kampanya bütçesi",
                "İmzalanan sözleşmelerin maliyeti anında buradan düşülür.");

            _riskLabel = BuildHudPill(header, IconType.Risk, "Risk: —",
                "Tek kaynak riski",
                "Aktif sözleşmeleriniz ne kadar tek tedarikçiye bağlı.");

            _activeCountLabel = BuildHudPill(header, IconType.Contracts, "0 Aktif",
                "Aktif sözleşmeler",
                "Teslim bekleyen tedarik sözleşmesi sayısı.");

            return header;
        }

        private Label BuildHudPill(VisualElement host, IconType icon, string initialText, string tooltipTitle, string tooltipContent)
        {
            var pill = new VisualElement();
            pill.AddToClassList("contract-hud-pill");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.paddingLeft = 10;
            pill.style.paddingRight = 10;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.marginLeft = 10;

            var iconHost = new VisualElement();
            iconHost.style.width = 14;
            iconHost.style.height = 14;
            iconHost.style.marginRight = 6;
            iconHost.Add(IconElement.Create(icon, 13f));
            pill.Add(iconHost);

            var label = new Label(initialText);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.pickingMode = PickingMode.Ignore;
            pill.Add(label);

            pill.AddManipulator(new TooltipManipulator(new TooltipData(tooltipTitle, tooltipContent)));
            host.Add(pill);
            return label;
        }

        private VisualElement BuildBusinessCardColumn()
        {
            var column = new VisualElement();
            column.AddToClassList("contract-column-board");
            column.style.width = 344;
            column.style.flexShrink = 0;
            column.style.marginLeft = 12;
            column.style.marginTop = 12;
            column.style.marginBottom = 12;
            column.style.paddingLeft = 10;
            column.style.paddingRight = 10;
            column.style.paddingTop = 10;
            column.style.paddingBottom = 10;
            column.style.flexDirection = FlexDirection.Column;

            var columnHeader = new Label("TEDARİKÇİ KARTVİZİTLERİ");
            columnHeader.AddToClassList("contract-column-header");
            columnHeader.style.fontSize = 12;
            columnHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            columnHeader.style.marginBottom = 8;
            column.Add(columnHeader);

            _companiesList = new ScrollView();
            _companiesList.style.flexGrow = 1;
            column.Add(_companiesList);

            return column;
        }

        private VisualElement BuildLettersColumn()
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexDirection = FlexDirection.Column;
            column.style.marginLeft = 10;
            column.style.marginRight = 12;
            column.style.marginTop = 12;
            column.style.marginBottom = 12;

            // ── Incoming offer letters (top half) ────────────────────────────────
            var offersWrap = new VisualElement();
            offersWrap.AddToClassList("contract-column-board");
            offersWrap.style.height = Length.Percent(52);
            offersWrap.style.paddingLeft = 10;
            offersWrap.style.paddingRight = 10;
            offersWrap.style.paddingTop = 10;
            offersWrap.style.paddingBottom = 10;
            offersWrap.style.flexDirection = FlexDirection.Column;

            var offersHeaderRow = new VisualElement();
            offersHeaderRow.style.flexDirection = FlexDirection.Row;
            offersHeaderRow.style.alignItems = Align.Center;
            offersHeaderRow.style.marginBottom = 8;

            var offersHeader = new Label("GELEN TEKLİF MEKTUPLARI");
            offersHeader.AddToClassList("contract-column-header");
            offersHeader.style.fontSize = 12;
            offersHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            offersHeaderRow.Add(offersHeader);

            var offersSpacer = new VisualElement();
            offersSpacer.style.flexGrow = 1;
            offersHeaderRow.Add(offersSpacer);

            _offersCountLabel = new Label("0 teklif");
            _offersCountLabel.style.fontSize = 10;
            _offersCountLabel.style.color = BrassColor;
            offersHeaderRow.Add(_offersCountLabel);

            offersWrap.Add(offersHeaderRow);

            _offersList = new ScrollView();
            _offersList.style.flexGrow = 1;
            offersWrap.Add(_offersList);

            column.Add(offersWrap);

            // ── Contract archive (bottom half) ───────────────────────────────────
            var archiveWrap = new VisualElement();
            archiveWrap.AddToClassList("contract-column-board");
            archiveWrap.style.flexGrow = 1;
            archiveWrap.style.marginTop = 8;
            archiveWrap.style.paddingLeft = 10;
            archiveWrap.style.paddingRight = 10;
            archiveWrap.style.paddingTop = 10;
            archiveWrap.style.paddingBottom = 10;
            archiveWrap.style.flexDirection = FlexDirection.Column;

            var archiveHeaderRow = new VisualElement();
            archiveHeaderRow.style.flexDirection = FlexDirection.Row;
            archiveHeaderRow.style.alignItems = Align.Center;
            archiveHeaderRow.style.marginBottom = 8;

            var archiveHeader = new Label("SÖZLEŞME ARŞİVİ");
            archiveHeader.AddToClassList("contract-column-header");
            archiveHeader.style.fontSize = 12;
            archiveHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            archiveHeaderRow.Add(archiveHeader);

            var archiveSpacer = new VisualElement();
            archiveSpacer.style.flexGrow = 1;
            archiveHeaderRow.Add(archiveSpacer);

            _contractsCountLabel = new Label("0 dosya");
            _contractsCountLabel.style.fontSize = 10;
            _contractsCountLabel.style.color = BrassColor;
            archiveHeaderRow.Add(_contractsCountLabel);

            archiveWrap.Add(archiveHeaderRow);

            _contractsList = new ScrollView();
            _contractsList.style.flexGrow = 1;
            archiveWrap.Add(_contractsList);

            column.Add(archiveWrap);

            return column;
        }

        // ── Observables ─────────────────────────────────────────────────────────

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
                RenderOffers(); // sign availability depends on the budget
            });

            ViewModel.RiskLevelText.Subscribe(r =>
            {
                if (_riskLabel != null)
                {
                    _riskLabel.text = $"{r}";
                    _riskLabel.style.color = GetRiskColor(r);
                }
            });

            ViewModel.ActiveContractsCount.Subscribe(c =>
            {
                if (_activeCountLabel != null) _activeCountLabel.text = $"{c} Aktif";
            });

            ViewModel.Companies.Subscribe(_ => RenderCompanies());
            ViewModel.AvailableOffers.Subscribe(_ => RenderOffers());
            ViewModel.Contracts.Subscribe(_ => RenderContracts());
            ViewModel.SelectedCompany.Subscribe(_ =>
            {
                RenderCompanies();
                RenderOffers();
            });

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_navBar != null && _navBar.StatusLabel != null) _navBar.StatusLabel.text = msg ?? string.Empty;
            });
        }

        private static Color GetRiskColor(string riskText)
        {
            if (string.IsNullOrEmpty(riskText)) return BrassColor;
            if (riskText.Contains("KRİTİK") || riskText.Contains("YÜKSEK")) return SealRedColor;
            if (riskText.Contains("ORTA")) return StampAmberColor;
            return StampGreenColor;
        }

        // ── Business cards ──────────────────────────────────────────────────────

        private void RenderCompanies()
        {
            if (_companiesList == null || ViewModel == null) return;
            _companiesList.Clear();

            var comps = ViewModel.Companies.Value;
            if (comps == null || comps.Count == 0)
            {
                _companiesList.Add(CreateEmptyState("Tedarikçi kartviziti yok.\nKampanya başladığında tedarikçiler masaya gelir."));
                return;
            }

            var selectedId = ViewModel.SelectedCompany.Value?.Id;
            foreach (var comp in comps)
            {
                _companiesList.Add(CreateBusinessCard(comp, string.Equals(comp.Id, selectedId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private VisualElement CreateBusinessCard(SupplierCompanyItemViewModel comp, bool selected)
        {
            var card = new VisualElement();
            card.AddToClassList("contract-bizcard");
            if (selected)
            {
                card.AddToClassList("contract-bizcard--selected");
            }
            card.style.marginBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;

            // Brand stripe (child element, so the selected border stays class-driven).
            var stripe = new VisualElement();
            stripe.style.position = Position.Absolute;
            stripe.style.left = 0;
            stripe.style.top = 0;
            stripe.style.bottom = 0;
            stripe.style.width = 4;
            stripe.style.borderTopRightRadius = 2;
            stripe.style.borderBottomRightRadius = 2;
            stripe.style.backgroundColor = ParseBrandColor(comp.BrandColor);
            stripe.pickingMode = PickingMode.Ignore;
            card.Add(stripe);

            // Header row: monogram + name + relationship stamp.
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var monogram = new Label(comp.Name.Length > 0 ? comp.Name[..1].ToUpperInvariant() : "?");
            monogram.AddToClassList("contract-bizcard__mark");
            monogram.style.width = 24;
            monogram.style.height = 24;
            monogram.style.fontSize = 13;
            monogram.style.backgroundColor = ParseBrandColor(comp.BrandColor);
            monogram.style.marginRight = 8;
            headerRow.Add(monogram);

            var name = new Label(comp.Name);
            name.AddToClassList("paper-title");
            name.style.fontSize = 13;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(name);

            var headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1;
            headerRow.Add(headerSpacer);

            var relStamp = new Label($"İLİŞKİ %{comp.RelationshipScore * 100:F0}");
            relStamp.AddToClassList("contract-rel-stamp");
            relStamp.AddToClassList(GetRelationshipStampClass(comp.RelationshipScore));
            relStamp.style.fontSize = 9;
            relStamp.style.paddingLeft = 6;
            relStamp.style.paddingRight = 6;
            relStamp.style.paddingTop = 2;
            relStamp.style.paddingBottom = 2;
            headerRow.Add(relStamp);

            card.Add(headerRow);

            // Ledger lines on the card.
            AddCardLine(card, "Güçlü", comp.Strengths, StampGreenColor);
            AddCardLine(card, "Zayıf", comp.Weaknesses, SealRedColor);
            if (!string.Equals(comp.SpecialCapabilities, "Yok", StringComparison.Ordinal))
            {
                AddCardLine(card, "Özellik", comp.SpecialCapabilities, BrassInkColor);
            }

            if (comp.ActiveContractsCount > 0)
            {
                var activeRow = new VisualElement();
                activeRow.style.flexDirection = FlexDirection.Row;
                activeRow.style.alignItems = Align.Center;
                activeRow.style.marginTop = 3;

                var iconHost = new VisualElement();
                iconHost.style.width = 12;
                iconHost.style.height = 12;
                iconHost.style.marginRight = 4;
                iconHost.Add(IconElement.Create(IconType.Contracts, 11f));
                activeRow.Add(iconHost);

                var activeLabel = new Label($"{comp.ActiveContractsCount} aktif sözleşme");
                activeLabel.AddToClassList("paper-meta");
                activeLabel.style.fontSize = 10;
                activeLabel.pickingMode = PickingMode.Ignore;
                activeRow.Add(activeLabel);

                card.Add(activeRow);
            }

            card.AddManipulator(new TooltipManipulator(new TooltipData(
                comp.Name,
                $"İlişki %{comp.RelationshipScore * 100:F0}. " +
                "İyi çalışma ilişkiyi yükseltir ve daha iyi şartlar getirir; iptaller ilişkiyi düşürür." +
                (selected ? " Kartviziti seçili: teklif mektupları vurgulanır." : " Tıklayınca teklifleri vurgulanır."))));

            card.RegisterCallback<ClickEvent>(_ => ViewModel.SelectCompany(comp.Id));

            return card;
        }

        private static void AddCardLine(VisualElement card, string key, string value, Color keyColor)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.marginTop = 2;

            var keyLabel = new Label(key);
            keyLabel.style.fontSize = 10;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.color = keyColor;
            keyLabel.style.marginRight = 4;
            keyLabel.pickingMode = PickingMode.Ignore;
            line.Add(keyLabel);

            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 10;
            valueLabel.style.color = InkSoftColor;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            valueLabel.style.flexGrow = 1;
            valueLabel.pickingMode = PickingMode.Ignore;
            line.Add(valueLabel);

            card.Add(line);
        }

        // ── Offer letters ───────────────────────────────────────────────────────

        private void RenderOffers()
        {
            if (_offersList == null || ViewModel == null) return;
            _offersList.Clear();

            var offers = ViewModel.AvailableOffers.Value;
            if (offers == null || offers.Count == 0)
            {
                if (_offersCountLabel != null) _offersCountLabel.text = "0 teklif";
                _offersList.Add(CreateEmptyState("Masada bekleyen teklif yok.\nTedarikçilerden yeni teklif geldiğinde mektubu buraya düşer."));
                return;
            }

            if (_offersCountLabel != null) _offersCountLabel.text = $"{offers.Count} teklif";

            var selectedId = ViewModel.SelectedCompany.Value?.Id;
            foreach (var offer in offers)
            {
                bool focused = selectedId != null && string.Equals(offer.SupplierId, selectedId, StringComparison.OrdinalIgnoreCase);
                _offersList.Add(CreateOfferLetter(offer, focused));
            }
        }

        private VisualElement CreateOfferLetter(ContractOfferItemViewModel offer, bool focused)
        {
            var letter = new VisualElement();
            letter.AddToClassList("paper-sheet");
            letter.AddToClassList("contract-offer-letter");
            if (focused)
            {
                letter.AddToClassList("contract-offer-letter--focused");
            }
            letter.style.flexDirection = FlexDirection.Row;
            letter.style.marginBottom = 8;

            // ── Product photo slot (drop-in Resources/Contracts/Photos/…) ────────
            letter.Add(BuildOfferPhotoSlot(offer));

            // ── Letter body ──────────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.paddingLeft = 10;
            body.style.paddingRight = 10;
            body.style.paddingTop = 7;
            body.style.paddingBottom = 8;

            var letterHead = new VisualElement();
            letterHead.style.flexDirection = FlexDirection.Row;
            letterHead.style.alignItems = Align.Center;

            var letterKind = new Label("TEKLİF MEKTUBU");
            letterKind.AddToClassList("paper-muted");
            letterKind.style.fontSize = 9;
            letterHead.Add(letterKind);

            var headSpacer = new VisualElement();
            headSpacer.style.flexGrow = 1;
            letterHead.Add(headSpacer);

            var supplierTag = new Label(offer.SupplierName);
            supplierTag.AddToClassList("paper-brass");
            supplierTag.style.fontSize = 10;
            supplierTag.style.unityFontStyleAndWeight = FontStyle.Bold;
            letterHead.Add(supplierTag);

            body.Add(letterHead);

            var itemName = new Label(offer.ItemName);
            itemName.AddToClassList("paper-title");
            itemName.style.fontSize = 13;
            itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            itemName.style.marginTop = 1;
            body.Add(itemName);

            var meta = new Label($"Kategori: {GetCategoryName(offer.Category)}  ·  Güvenilirlik x{offer.ReliabilityMultiplier:F2} ({ReliabilityNote(offer.ReliabilityMultiplier)})");
            meta.AddToClassList("paper-meta");
            meta.style.fontSize = 10;
            meta.style.marginTop = 1;
            body.Add(meta);

            var signButton = new ActionButton(
                IconType.Sign,
                "İmzala",
                () => OpenSignatureDialog(offer),
                $"İmzalanırsa {offer.BaseCost:F1} M$ çekilir; {offer.ItemName} {offer.DeliveryDays} gün içinde teslim edilir ve sözleşme arşive dosyalanır.")
            {
                style = { marginTop = 5 }
            };
            signButton.SetVariant("primary");
            signButton.SetPrice($"{offer.BaseCost:F1} M$");
            signButton.SetDuration($"{offer.DeliveryDays} gün");

            double budget = ViewModel.Budget.Value;
            signButton.SetAvailable(
                budget >= offer.BaseCost,
                budget >= offer.BaseCost
                    ? null
                    : $"Bütçe yetersiz: {offer.BaseCost:F1} M$ gerekli, kasada {budget:F1} M$ var.");

            body.Add(signButton);

            letter.Add(body);
            return letter;
        }

        private VisualElement BuildOfferPhotoSlot(ContractOfferItemViewModel offer)
        {
            string resourcePath = GetOfferPhotoResource(offer.Category);
            Texture2D tex = resourcePath != null ? LoadPhoto(resourcePath) : null;

            var slot = new VisualElement();
            slot.AddToClassList("contract-offer-photo");
            slot.style.width = 118;
            slot.style.flexShrink = 0;
            slot.style.position = Position.Relative;
            slot.style.overflow = Overflow.Hidden;

            var preview = new VisualElement();
            preview.style.position = Position.Absolute;
            preview.style.left = 0;
            preview.style.top = 0;
            preview.style.right = 0;
            preview.style.bottom = 0;
            if (tex != null)
            {
                preview.style.backgroundImage = new StyleBackground(tex);
                preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            preview.pickingMode = PickingMode.Ignore;
            slot.Add(preview);

            if (tex == null)
            {
                var placeholder = new Label(
                    "ÜRÜN FOTOĞRAFI BEKLENİYOR\n(Resources/" +
                    (resourcePath != null ? $"{resourcePath}.jpg" : "Contracts/Photos/<kategori>.jpg") + ")");
                placeholder.AddToClassList("contract-offer-photo__placeholder");
                placeholder.style.fontSize = 8;
                placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                placeholder.style.whiteSpace = WhiteSpace.Normal;
                placeholder.style.position = Position.Absolute;
                placeholder.style.left = 2;
                placeholder.style.right = 2;
                placeholder.style.top = 0;
                placeholder.style.bottom = 0;
                placeholder.pickingMode = PickingMode.Ignore;
                slot.Add(placeholder);
            }
            else
            {
                var caption = new Label(GetOfferPhotoCaption(offer.Category));
                caption.AddToClassList("contract-offer-photo__caption");
                caption.style.fontSize = 8;
                caption.style.whiteSpace = WhiteSpace.Normal;
                caption.style.position = Position.Absolute;
                caption.style.left = 0;
                caption.style.right = 0;
                caption.style.bottom = 0;
                caption.style.paddingLeft = 4;
                caption.style.paddingRight = 4;
                caption.style.paddingTop = 2;
                caption.style.paddingBottom = 2;
                caption.pickingMode = PickingMode.Ignore;
                slot.Add(caption);
            }

            return slot;
        }

        /// <summary>
        /// Opens the paper signing dialog for <paramref name="offer"/>. Public so
        /// EditMode tests can drive the sign flow without synthesizing click events.
        /// </summary>
        public void OpenSignatureDialog(ContractOfferItemViewModel offer)
        {
            string resourcePath = GetOfferPhotoResource(offer.Category);
            Texture2D tex = resourcePath != null ? LoadPhoto(resourcePath) : null;

            SignatureDialog.Show(
                RootElement,
                ViewModel,
                offer,
                photoCaption: GetOfferPhotoCaption(offer.Category),
                photoTexture: tex,
                onSign: () => ViewModel.SignContract(offer));
        }

        // ── Archive folders ─────────────────────────────────────────────────────

        private void RenderContracts()
        {
            if (_contractsList == null || ViewModel == null) return;
            _contractsList.Clear();

            var contracts = ViewModel.Contracts.Value;
            if (contracts == null || contracts.Count == 0)
            {
                if (_contractsCountLabel != null) _contractsCountLabel.text = "0 dosya";
                _contractsList.Add(CreateEmptyState("Arşiv boş — henüz dosyalanmış sözleşme yok.\nÜstteki teklif mektuplarını imzalayıp mühürleyin."));
                return;
            }

            if (_contractsCountLabel != null) _contractsCountLabel.text = $"{contracts.Count} dosya";

            foreach (var contract in contracts)
            {
                _contractsList.Add(CreateArchiveFolder(contract));
            }
        }

        private VisualElement CreateArchiveFolder(SupplierContractItemViewModel contract)
        {
            var folder = new VisualElement();
            folder.AddToClassList("contract-folder");
            if (contract.Status == ContractStatus.Cancelled)
            {
                folder.AddToClassList("contract-folder--cancelled");
            }
            folder.style.marginBottom = 8;

            // Folder tab with the contract reference.
            var tab = new Label($"DOSYA #{contract.Id.ToUpperInvariant()}");
            tab.AddToClassList("contract-folder__tab");
            tab.style.fontSize = 8;
            tab.style.paddingLeft = 8;
            tab.style.paddingRight = 8;
            tab.style.paddingTop = 2;
            tab.style.alignSelf = Align.FlexStart;
            tab.pickingMode = PickingMode.Ignore;
            folder.Add(tab);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.alignItems = Align.Center;
            body.style.paddingLeft = 10;
            body.style.paddingRight = 10;
            body.style.paddingTop = 6;
            body.style.paddingBottom = 8;

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.marginRight = 10;

            var title = new Label($"{contract.ItemName} — {contract.SupplierName}");
            title.AddToClassList("paper-title");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            left.Add(title);

            var meta = new Label($"{contract.CostMUSD:F1} M$  ·  Hedef: {contract.DeliveryDate}");
            meta.AddToClassList("paper-meta");
            meta.style.fontSize = 10;
            meta.style.marginTop = 1;
            left.Add(meta);

            body.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            var stamp = new Label(GetStampText(contract.Status));
            stamp.AddToClassList("contract-stamp");
            stamp.AddToClassList(GetStampClass(contract.Status));
            stamp.style.fontSize = 9;
            stamp.style.paddingLeft = 8;
            stamp.style.paddingRight = 8;
            stamp.style.paddingTop = 2;
            stamp.style.paddingBottom = 2;
            stamp.style.rotate = new Rotate(new Angle(-4f, AngleUnit.Degree));
            right.Add(stamp);

            if (contract.CanCancel)
            {
                var refund = contract.CostMUSD * 0.5;
                var cancelBtn = new ActionButton(
                    IconType.Cancel,
                    "İptal Et",
                    () => ConfirmCancelContract(contract),
                    $"İptal edilirse ödenen {contract.CostMUSD:F1} M$'ın yarısı ({refund:F1} M$) iade edilir; {contract.SupplierName} ile ilişki puanı düşer.")
                {
                    style = { marginLeft = 10 }
                };
                cancelBtn.SetVariant("danger");
                right.Add(cancelBtn);
            }

            body.Add(right);
            folder.Add(body);

            folder.AddManipulator(new TooltipManipulator(new TooltipData(
                $"{contract.StatusName} — {contract.ItemName}",
                contract.Status == ContractStatus.PendingDelivery
                    ? $"{contract.SupplierName} kalem teslim kuyruğunda. Teslimden önce iptal edilebilir: tutarın yarısı iade edilir, ilişki puanı düşer."
                    : $"Arşiv dosyası: {contract.StatusName.ToLowerInvariant()} sözleşme.")));

            return folder;
        }

        /// <summary>
        /// Opens the destructive cancel confirmation for a pending contract.
        /// Public so EditMode tests can drive the cancel flow like ProductionStandTests.
        /// </summary>
        public void ConfirmCancelContract(SupplierContractItemViewModel contract)
        {
            double refund = contract.CostMUSD * 0.5;
            ConfirmDialog.Show(
                RootElement,
                "Sözleşmeyi İptal Et",
                $"{contract.ItemName} ({contract.SupplierName}) sözleşmesi iptal edilir. " +
                $"Ödenen {contract.CostMUSD:F1} M$'ın yarısı ({refund:F1} M$) bütçeye iade edilir; " +
                $"{contract.SupplierName} ile ilişki puanı %5 düşer.",
                $"İptal Et · {refund:F1} M$ iade",
                () => ViewModel.CancelContract(contract.Id),
                destructive: true);
        }

        // ── Shared helpers ──────────────────────────────────────────────────────

        private static VisualElement CreateEmptyState(string text)
        {
            var empty = new Label(text);
            empty.AddToClassList("paper-empty");
            empty.style.fontSize = 11;
            empty.style.whiteSpace = WhiteSpace.Normal;
            empty.style.marginTop = 18;
            return empty;
        }

        private static Color ParseBrandColor(string brandColor)
        {
            if (!string.IsNullOrEmpty(brandColor) && ColorUtility.TryParseHtmlString(brandColor, out Color parsed))
            {
                return parsed;
            }
            return new Color(0.643f, 0.463f, 0.122f); // default brass
        }

        private static string GetRelationshipStampClass(double relationship) => relationship switch
        {
            >= 0.60 => "contract-rel-stamp--good",
            >= 0.40 => "contract-rel-stamp--mid",
            _ => "contract-rel-stamp--bad"
        };

        private static string GetStampText(ContractStatus status) => status switch
        {
            ContractStatus.PendingDelivery => "TESLİM BEKLENİYOR",
            ContractStatus.Fulfilled => "TESLİM EDİLDİ",
            ContractStatus.Cancelled => "İPTAL EDİLDİ",
            _ => status.ToString().ToUpperInvariant()
        };

        private static string GetStampClass(ContractStatus status) => status switch
        {
            ContractStatus.PendingDelivery => "contract-stamp--pending",
            ContractStatus.Fulfilled => "contract-stamp--fulfilled",
            ContractStatus.Cancelled => "contract-stamp--cancelled",
            _ => "contract-stamp--pending"
        };

        private static string ReliabilityNote(double multiplier) => multiplier switch
        {
            >= 1.10 => "yüksek güvenilirlik, pahalı",
            >= 0.98 => "standart güvenilirlik",
            _ => "gecikme riski yüksek"
        };

        private static string GetCategoryName(string category) => category switch
        {
            "engine" => "Motor",
            "tank" => "Yakıt Tankı",
            "cabin" => "Kabin",
            "structure" => "Yapı",
            "avionics" => "Aviyonik",
            "science" => "Bilim",
            _ => category ?? "Genel"
        };

        private static string GetOfferPhotoResource(string category) => category switch
        {
            "engine" => "Contracts/Photos/offer_engine",
            "tank" => "Contracts/Photos/offer_tank",
            "cabin" => "Contracts/Photos/offer_cabin",
            "structure" => "Contracts/Photos/offer_structure",
            "avionics" => "Contracts/Photos/offer_avionics",
            "science" => "Contracts/Photos/offer_science",
            _ => null
        };

        private static string GetOfferPhotoCaption(string category) => category switch
        {
            "engine" => "Sıvı yakıtlı ana motor — itki odası ve nozul üretimi · Kaynak: NASA",
            "tank" => "Kriyojenik yakıt tankı bölümleri gövde hattında · Kaynak: NASA",
            "cabin" => "Mürettebat kapsülü kabin montajı · Kaynak: NASA",
            "structure" => "Kompozit gövde segmenti üretimi · Kaynak: NASA",
            "avionics" => "Aviyonik rafı — uçuş bilgisayarı ve haberleşme kartları · Kaynak: NASA",
            "science" => "Bilim yükü — spektrometre sensör paketi · Kaynak: NASA",
            _ => "Tedarik kalem üretimi · Kaynak: NASA"
        };

        // Photo cache (same drop-in convention as CampaignMap.GetPhotoTexture).
        private static readonly Dictionary<string, Texture2D> PhotoCache = new();

        private static Texture2D LoadPhoto(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;

            if (PhotoCache.TryGetValue(resourcePath, out Texture2D cached))
            {
                return cached != null ? cached : null;
            }

            var tex = Resources.Load<Texture2D>(resourcePath);
            PhotoCache[resourcePath] = tex;
            return tex != null ? tex : null;
        }
    }
}
