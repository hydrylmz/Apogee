using System;
using System.Collections.Generic;
using Apogee.Core.Education;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Glossary
{
    /// <summary>
    /// UI Toolkit screen controller for the Glossary / Knowledge Base Screen (ScreenId.Glossary = 17).
    /// Inherits from ScreenControllerBase to integrate with IScreenNavigator.
    /// </summary>
    public sealed class GlossaryScreenController : ScreenControllerBase<GlossaryViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Glossary;

        private TextField _searchField;
        private VisualElement _categoryBar;
        private ScrollView _termsListContainer;
        private Label _detailTermLabel;
        private Label _detailPlainTitleLabel;
        private Label _detailCategoryBadge;
        private Label _detailSummaryLabel;
        private Label _detailExplanationLabel;
        private Label _detailWhyItMattersLabel;
        private Button _backButton;

        public GlossaryScreenController(GlossaryViewModel viewModel) : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-glossary");

            // Top Header Bar
            var headerBar = new VisualElement();
            headerBar.AddToClassList("glossary-header-bar");
            headerBar.style.flexDirection = FlexDirection.Row;
            headerBar.style.justifyContent = Justify.SpaceBetween;
            headerBar.style.alignItems = Align.Center;
            headerBar.style.paddingTop = 16;
            headerBar.style.paddingBottom = 16;
            headerBar.style.paddingLeft = 24;
            headerBar.style.paddingRight = 24;
            headerBar.style.borderBottomWidth = 1;
            headerBar.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.8f);

            var titleLabel = new Label("APOGEE SÖZLÜK & BİLGİ BANKASI");
            titleLabel.style.fontSize = 20;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.9f, 0.95f, 1f);

            _backButton = new Button(() => ViewModel?.CloseCommand?.Execute()) { text = "<- Kapat / Geri Dön"};
            _backButton.AddToClassList("apogee-btn");
            _backButton.AddToClassList("apogee-btn--secondary");

            headerBar.Add(titleLabel);
            headerBar.Add(_backButton);
            root.Add(headerBar);

            // Filter & Search Row
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;
            searchRow.style.paddingLeft = 24;
            searchRow.style.paddingRight = 24;
            searchRow.style.paddingTop = 12;
            searchRow.style.paddingBottom = 12;
            searchRow.style.backgroundColor = new Color(0.12f, 0.15f, 0.22f, 0.9f);

            _searchField = new TextField { label = "Terim Ara:" };
            _searchField.style.flexGrow = 1;
            _searchField.RegisterValueChangedCallback(evt => ViewModel?.SetSearchQuery(evt.newValue));

            _categoryBar = new VisualElement();
            _categoryBar.style.flexDirection = FlexDirection.Row;
            _categoryBar.style.marginLeft = 16;

            searchRow.Add(_searchField);
            searchRow.Add(_categoryBar);
            root.Add(searchRow);

            // Main 2-Column Split
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.paddingLeft = 24;
            mainContent.style.paddingRight = 24;
            mainContent.style.paddingTop = 16;
            mainContent.style.paddingBottom = 16;

            // Left: Term List ScrollView
            var leftPanel = new VisualElement();
            leftPanel.style.width = 380;
            leftPanel.style.marginRight = 16;
            leftPanel.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.95f);
            leftPanel.style.borderTopWidth = 1;
            leftPanel.style.borderBottomWidth = 1;
            leftPanel.style.borderLeftWidth = 1;
            leftPanel.style.borderRightWidth = 1;
            leftPanel.style.borderTopColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            leftPanel.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            leftPanel.style.borderLeftColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            leftPanel.style.borderRightColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            leftPanel.style.borderTopLeftRadius = 6;
            leftPanel.style.borderTopRightRadius = 6;
            leftPanel.style.borderBottomLeftRadius = 6;
            leftPanel.style.borderBottomRightRadius = 6;
            leftPanel.style.paddingTop = 8;
            leftPanel.style.paddingBottom = 8;
            leftPanel.style.paddingLeft = 8;
            leftPanel.style.paddingRight = 8;

            _termsListContainer = new ScrollView(ScrollViewMode.Vertical);
            _termsListContainer.style.flexGrow = 1;
            leftPanel.Add(_termsListContainer);
            mainContent.Add(leftPanel);

            // Right: Detail Panel
            var detailPanel = new VisualElement();
            detailPanel.style.flexGrow = 1;
            detailPanel.style.backgroundColor = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            detailPanel.style.borderTopWidth = 1;
            detailPanel.style.borderBottomWidth = 1;
            detailPanel.style.borderLeftWidth = 1;
            detailPanel.style.borderRightWidth = 1;
            detailPanel.style.borderTopColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            detailPanel.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            detailPanel.style.borderLeftColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            detailPanel.style.borderRightColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            detailPanel.style.borderTopLeftRadius = 6;
            detailPanel.style.borderTopRightRadius = 6;
            detailPanel.style.borderBottomLeftRadius = 6;
            detailPanel.style.borderBottomRightRadius = 6;
            detailPanel.style.paddingTop = 20;
            detailPanel.style.paddingBottom = 20;
            detailPanel.style.paddingLeft = 24;
            detailPanel.style.paddingRight = 24;

            _detailTermLabel = new Label();
            _detailTermLabel.style.fontSize = 24;
            _detailTermLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailTermLabel.style.color = new Color(1f, 0.8f, 0.2f);

            _detailPlainTitleLabel = new Label();
            _detailPlainTitleLabel.style.fontSize = 16;
            _detailPlainTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailPlainTitleLabel.style.color = new Color(0.7f, 0.85f, 1f);
            _detailPlainTitleLabel.style.marginTop = 4;

            _detailCategoryBadge = new Label();
            _detailCategoryBadge.style.alignSelf = Align.FlexStart;
            _detailCategoryBadge.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.6f);
            _detailCategoryBadge.style.color = new Color(0.8f, 0.95f, 1f);
            _detailCategoryBadge.style.paddingLeft = 8;
            _detailCategoryBadge.style.paddingRight = 8;
            _detailCategoryBadge.style.paddingTop = 2;
            _detailCategoryBadge.style.paddingBottom = 2;
            _detailCategoryBadge.style.borderTopLeftRadius = 4;
            _detailCategoryBadge.style.borderTopRightRadius = 4;
            _detailCategoryBadge.style.borderBottomLeftRadius = 4;
            _detailCategoryBadge.style.borderBottomRightRadius = 4;
            _detailCategoryBadge.style.marginTop = 8;
            _detailCategoryBadge.style.marginBottom = 16;

            var summaryHeader = new Label("ÖZET") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.5f, 0.7f, 0.9f), marginBottom = 4 } };
            _detailSummaryLabel = new Label();
            _detailSummaryLabel.style.fontSize = 14;
            _detailSummaryLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            _detailSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailSummaryLabel.style.marginBottom = 16;

            var expHeader = new Label("DETAYLI AÇIKLAMA") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.5f, 0.7f, 0.9f), marginBottom = 4 } };
            _detailExplanationLabel = new Label();
            _detailExplanationLabel.style.fontSize = 13;
            _detailExplanationLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            _detailExplanationLabel.style.whiteSpace = WhiteSpace.Normal;
            _detailExplanationLabel.style.marginBottom = 16;

            var whyHeader = new Label("NEDEN ÖNEMLİ? (NEDEN-SONUÇ)") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = new Color(1f, 0.6f, 0.3f), marginBottom = 4 } };
            _detailWhyItMattersLabel = new Label();
            _detailWhyItMattersLabel.style.fontSize = 13;
            _detailWhyItMattersLabel.style.color = new Color(1f, 0.9f, 0.8f);
            _detailWhyItMattersLabel.style.whiteSpace = WhiteSpace.Normal;

            detailPanel.Add(_detailTermLabel);
            detailPanel.Add(_detailPlainTitleLabel);
            detailPanel.Add(_detailCategoryBadge);
            detailPanel.Add(summaryHeader);
            detailPanel.Add(_detailSummaryLabel);
            detailPanel.Add(expHeader);
            detailPanel.Add(_detailExplanationLabel);
            detailPanel.Add(whyHeader);
            detailPanel.Add(_detailWhyItMattersLabel);

            mainContent.Add(detailPanel);
            root.Add(mainContent);

            return root;
        }

        protected override void OnInitialized()
        {
            if (ViewModel == null) return;

            ViewModel.Categories.Subscribe(categories =>
            {
                _categoryBar.Clear();
                foreach (var cat in categories)
                {
                    var btn = new Button(() => ViewModel.SelectCategory(cat)) { text = cat };
                    btn.AddToClassList("apogee-tab-btn");
                    btn.style.marginRight = 4;
                    _categoryBar.Add(btn);
                }
            });

            ViewModel.FilteredTerms.Subscribe(terms =>
            {
                _termsListContainer.Clear();
                foreach (var item in terms)
                {
                    var card = new Button(() => ViewModel.SelectTerm(item));
                    card.style.flexDirection = FlexDirection.Column;
                    card.style.alignItems = Align.FlexStart;
                    card.style.paddingTop = 8;
                    card.style.paddingBottom = 8;
                    card.style.paddingLeft = 10;
                    card.style.paddingRight = 10;
                    card.style.marginBottom = 6;
                    card.style.backgroundColor = new Color(0.15f, 0.18f, 0.25f, 0.8f);
                    card.style.borderTopWidth = 1;
                    card.style.borderBottomWidth = 1;
                    card.style.borderLeftWidth = 1;
                    card.style.borderRightWidth = 1;
                    card.style.borderTopColor = new Color(0.25f, 0.3f, 0.4f, 0.4f);
                    card.style.borderBottomColor = new Color(0.25f, 0.3f, 0.4f, 0.4f);
                    card.style.borderLeftColor = new Color(0.25f, 0.3f, 0.4f, 0.4f);
                    card.style.borderRightColor = new Color(0.25f, 0.3f, 0.4f, 0.4f);
                    card.style.borderTopLeftRadius = 4;
                    card.style.borderTopRightRadius = 4;
                    card.style.borderBottomLeftRadius = 4;
                    card.style.borderBottomRightRadius = 4;

                    var nameLbl = new Label(item.Term);
                    nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    nameLbl.style.color = new Color(0.9f, 0.95f, 1f);

                    var descLbl = new Label(item.PlainTitle);
                    descLbl.style.fontSize = 11;
                    descLbl.style.color = new Color(0.6f, 0.75f, 0.9f);

                    card.Add(nameLbl);
                    card.Add(descLbl);
                    _termsListContainer.Add(card);
                }
            });

            ViewModel.SelectedTerm.Subscribe(item =>
            {
                if (item != null)
                {
                    _detailTermLabel.text = item.Term;
                    _detailPlainTitleLabel.text = item.PlainTitle;
                    _detailCategoryBadge.text = item.Category;
                    _detailSummaryLabel.text = item.Summary;
                    _detailExplanationLabel.text = item.DetailedExplanation;
                    _detailWhyItMattersLabel.text = item.WhyItMatters;
                }
                else
                {
                    _detailTermLabel.text = "Terim Seçilmedi";
                    _detailPlainTitleLabel.text = "";
                    _detailCategoryBadge.text = "";
                    _detailSummaryLabel.text = "Listeden incelemek istediğiniz bir terime tıklayın.";
                    _detailExplanationLabel.text = "";
                    _detailWhyItMattersLabel.text = "";
                }
            });
        }

        protected override void OnShown(object context)
        {
            if (context is string termId && ViewModel != null)
            {
                ViewModel.SelectTermById(termId);
            }
        }
    }
}
