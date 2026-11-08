using System.Collections.Generic;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Attribution
{
    /// <summary>
    /// UI Toolkit screen controller for the NASA Attribution Screen (ScreenId.Attribution = 23).
    /// Displays NASA data sources, open-source libraries, and audio/art credits.
    /// </summary>
    public sealed class AttributionScreenController : ScreenControllerBase<AttributionViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Attribution;

        private VisualElement _tabBar;
        private ScrollView   _listContainer;
        private Label        _detailTitleLabel;
        private Label        _detailSourceLabel;
        private Label        _detailDescLabel;
        private Label        _detailLicenseLabel;
        private Label        _detailUrlLabel;
        private Label        _detailCategoryBadge;
        private Button       _backButton;

        public AttributionScreenController(AttributionViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-attribution");

            // ── Header ───────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection    = FlexDirection.Row;
            header.style.justifyContent   = Justify.SpaceBetween;
            header.style.alignItems       = Align.Center;
            header.style.paddingTop       = 16;
            header.style.paddingBottom    = 16;
            header.style.paddingLeft      = 24;
            header.style.paddingRight     = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.8f);

            var titleBlock = new VisualElement();
            var titleLabel = new Label("APOGEE — VERİ KAYNAKLARI VE ATIFLAR");
            titleLabel.style.fontSize                 = 20;
            titleLabel.style.unityFontStyleAndWeight  = FontStyle.Bold;
            titleLabel.style.color                    = new Color(0.9f, 0.95f, 1f);

            var subLabel = new Label("NASA Space Apps Challenge 2026 — Proje APOGEE");
            subLabel.style.fontSize = 12;
            subLabel.style.color    = new Color(0.5f, 0.75f, 1f);
            subLabel.style.marginTop = 2;

            titleBlock.Add(titleLabel);
            titleBlock.Add(subLabel);

            _backButton = new Button(() => ViewModel?.CloseCommand?.Execute()) { text = "← Geri Dön" };
            _backButton.AddToClassList("apogee-btn");
            _backButton.AddToClassList("apogee-btn--secondary");

            header.Add(titleBlock);
            header.Add(_backButton);
            root.Add(header);

            // ── NASA Badge Row ────────────────────────────────────────────────
            var nasaBadge = new Label(
                "Bu oyun NASA Space Apps Challenge 2026 kapsamında geliştirilmiştir. " +
                "Kullanılan tüm NASA veri kaynakları kamuya açık (Public Domain) olup aşağıda listelenmiştir.");
            nasaBadge.style.backgroundColor = new Color(0.05f, 0.1f, 0.25f, 0.9f);
            nasaBadge.style.color           = new Color(0.6f, 0.85f, 1f);
            nasaBadge.style.paddingTop      = 10;
            nasaBadge.style.paddingBottom   = 10;
            nasaBadge.style.paddingLeft     = 24;
            nasaBadge.style.paddingRight    = 24;
            nasaBadge.style.whiteSpace      = WhiteSpace.Normal;
            nasaBadge.style.fontSize        = 13;
            root.Add(nasaBadge);

            // ── Tab Bar ───────────────────────────────────────────────────────
            var tabRow = new VisualElement();
            tabRow.style.flexDirection    = FlexDirection.Row;
            tabRow.style.paddingLeft      = 24;
            tabRow.style.paddingRight     = 24;
            tabRow.style.paddingTop       = 10;
            tabRow.style.paddingBottom    = 10;
            tabRow.style.backgroundColor  = new Color(0.12f, 0.15f, 0.22f, 0.9f);

            _tabBar = new VisualElement();
            _tabBar.style.flexDirection = FlexDirection.Row;
            tabRow.Add(_tabBar);
            root.Add(tabRow);

            // ── Main 2-Column Split ───────────────────────────────────────────
            var main = new VisualElement();
            main.style.flexDirection  = FlexDirection.Row;
            main.style.flexGrow       = 1;
            main.style.paddingLeft    = 24;
            main.style.paddingRight   = 24;
            main.style.paddingTop     = 16;
            main.style.paddingBottom  = 16;

            // Left: list
            var leftPanel = BuildPanel(380);
            _listContainer = new ScrollView(ScrollViewMode.Vertical);
            _listContainer.style.flexGrow = 1;
            leftPanel.Add(_listContainer);
            main.Add(leftPanel);

            // Right: detail
            var rightPanel = BuildPanel(0, flexGrow: 1);

            _detailTitleLabel = new Label();
            _detailTitleLabel.style.fontSize                = 20;
            _detailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailTitleLabel.style.color                   = new Color(1f, 0.8f, 0.2f);

            _detailCategoryBadge = new Label();
            _detailCategoryBadge.AddToClassList("apogee-badge");
            _detailCategoryBadge.style.alignSelf     = Align.FlexStart;
            _detailCategoryBadge.style.marginTop     = 6;
            _detailCategoryBadge.style.marginBottom  = 10;

            var sourceHeader = SectionHeader("KAYNAK");
            _detailSourceLabel = BodyLabel();

            var descHeader = SectionHeader("AÇIKLAMA");
            _detailDescLabel = BodyLabel();
            _detailDescLabel.style.marginBottom = 12;

            var licenseHeader = SectionHeader("LİSANS");
            _detailLicenseLabel = BodyLabel();
            _detailLicenseLabel.style.color = new Color(0.7f, 1f, 0.7f);

            var urlHeader = SectionHeader("URL");
            _detailUrlLabel = BodyLabel();
            _detailUrlLabel.style.color = new Color(0.4f, 0.8f, 1f);

            rightPanel.Add(_detailTitleLabel);
            rightPanel.Add(_detailCategoryBadge);
            rightPanel.Add(sourceHeader);
            rightPanel.Add(_detailSourceLabel);
            rightPanel.Add(descHeader);
            rightPanel.Add(_detailDescLabel);
            rightPanel.Add(licenseHeader);
            rightPanel.Add(_detailLicenseLabel);
            rightPanel.Add(urlHeader);
            rightPanel.Add(_detailUrlLabel);

            main.Add(rightPanel);
            root.Add(main);

            return root;
        }

        protected override void OnInitialized()
        {
            if (ViewModel == null) return;

            ViewModel.CategoryTabs.Subscribe(tabs =>
            {
                _tabBar.Clear();
                foreach (var tab in tabs)
                {
                    string capturedTab = tab;
                    var btn = new Button(() => ViewModel.SelectCategory(capturedTab)) { text = capturedTab };
                    btn.AddToClassList("apogee-tab-btn");
                    btn.style.marginRight = 4;
                    _tabBar.Add(btn);
                }
            });

            ViewModel.Items.Subscribe(items =>
            {
                _listContainer.Clear();
                foreach (var item in items)
                {
                    AttributionItemViewModel captured = item;
                    var card = new Button(() => ViewModel.SelectItem(captured));
                    card.style.flexDirection  = FlexDirection.Column;
                    card.style.alignItems     = Align.FlexStart;
                    card.style.paddingTop     = 8;
                    card.style.paddingBottom  = 8;
                    card.style.paddingLeft    = 10;
                    card.style.paddingRight   = 10;
                    card.style.marginBottom   = 6;
                    card.style.backgroundColor = new Color(0.15f, 0.18f, 0.25f, 0.8f);
                    SetBorder(card, new Color(0.25f, 0.3f, 0.4f, 0.4f));

                    var nameLbl = new Label(item.Title);
                    nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    nameLbl.style.color                   = new Color(0.9f, 0.95f, 1f);

                    var srcLbl = new Label(item.Source);
                    srcLbl.style.fontSize = 11;
                    srcLbl.style.color    = new Color(0.5f, 0.75f, 1f);

                    var catLbl = new Label(item.CategoryDisplay);
                    catLbl.style.fontSize = 10;
                    catLbl.style.color    = new Color(0.6f, 0.6f, 0.6f);

                    card.Add(nameLbl);
                    card.Add(srcLbl);
                    card.Add(catLbl);
                    _listContainer.Add(card);
                }
            });

            ViewModel.SelectedItem.Subscribe(item =>
            {
                if (item != null)
                {
                    _detailTitleLabel.text    = item.Title;
                    _detailCategoryBadge.text = item.CategoryDisplay;
                    _detailSourceLabel.text   = item.Source;
                    _detailDescLabel.text     = item.Description;
                    _detailLicenseLabel.text  = item.LicenseDisplay;
                    _detailUrlLabel.text      = string.IsNullOrEmpty(item.Url) ? "—" : item.Url;
                }
                else
                {
                    _detailTitleLabel.text    = "Kaynak Seçilmedi";
                    _detailCategoryBadge.text = "";
                    _detailSourceLabel.text   = "";
                    _detailDescLabel.text     = "Listeden bir atıf kaydı seçin.";
                    _detailLicenseLabel.text  = "";
                    _detailUrlLabel.text      = "";
                }
            });
        }

        protected override void OnShown(object context) { }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static VisualElement BuildPanel(int width, int flexGrow = 0)
        {
            var panel = new VisualElement();
            if (width > 0) panel.style.width = width;
            if (flexGrow > 0) panel.style.flexGrow = flexGrow;
            panel.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.95f);
            panel.style.paddingTop      = 16;
            panel.style.paddingBottom   = 16;
            panel.style.paddingLeft     = 16;
            panel.style.paddingRight    = 16;
            panel.style.marginRight     = width > 0 ? 16 : 0;
            SetBorder(panel, new Color(0.2f, 0.25f, 0.35f, 0.5f));
            return panel;
        }

        private static void SetBorder(VisualElement el, Color color, int radius = 6)
        {
            el.style.borderTopWidth    = 1;
            el.style.borderBottomWidth = 1;
            el.style.borderLeftWidth   = 1;
            el.style.borderRightWidth  = 1;
            el.style.borderTopColor    = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor   = color;
            el.style.borderRightColor  = color;
            el.style.borderTopLeftRadius     = radius;
            el.style.borderTopRightRadius    = radius;
            el.style.borderBottomLeftRadius  = radius;
            el.style.borderBottomRightRadius = radius;
        }

        private static Label SectionHeader(string text)
        {
            var lbl = new Label(text);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color                   = new Color(0.5f, 0.7f, 0.9f);
            lbl.style.marginTop               = 12;
            lbl.style.marginBottom            = 4;
            return lbl;
        }

        private static Label BodyLabel()
        {
            var lbl = new Label();
            lbl.style.fontSize    = 13;
            lbl.style.color       = new Color(0.85f, 0.85f, 0.85f);
            lbl.style.whiteSpace  = WhiteSpace.Normal;
            return lbl;
        }
    }
}
