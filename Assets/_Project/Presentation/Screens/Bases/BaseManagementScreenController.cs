using System;
using System.Collections.Generic;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Bases
{
    /// <summary>
    /// UI Toolkit screen controller for Strategic Base Management (ScreenId.BaseManagement = 11).
    /// Complies with GDD §6.1, §14.1 (#10 Üs Yönetimi), and M9-05.
    /// Provides facilities listing, upgrade actions, and new facility construction.
    /// </summary>
    public sealed class BaseManagementScreenController : ScreenControllerBase<BaseManagementViewModel>
    {
        public override ScreenId ScreenId => ScreenId.BaseManagement;

        // Top HUD
        private Label _titleLabel;
        private Label _budgetLabel;
        private Label _upkeepLabel;
        private Label _countLabel;
        private Button _backBtn;

        // Facilities List
        private ScrollView _facilitiesList;

        // Construction Panel
        private VisualElement _regionOptionsContainer;
        private VisualElement _baseTypeOptionsContainer;
        private Label _costPreviewLabel;
        private Button _buildBtn;
        private readonly Dictionary<string, Button> _regionButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BaseType, Button> _typeButtons = new();

        // Footer
        private Label _statusLabel;

        public BaseManagementScreenController(BaseManagementViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-base-management");
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

            _backBtn = new Button(() => ViewModel.BackCommand.Execute()) { text = "<- Geri (Harita)"};
            _backBtn.style.marginRight = 16;
            _backBtn.style.paddingLeft = 12;
            _backBtn.style.paddingRight = 12;
            _backBtn.style.paddingTop = 6;
            _backBtn.style.paddingBottom = 6;
            _backBtn.style.backgroundColor = new Color(0.15f, 0.20f, 0.30f);
            _backBtn.style.color = Color.white;
            leftHeader.Add(_backBtn);

            _titleLabel = new Label("ÜS YÖNETİMİ VE ALTYAPI");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            leftHeader.Add(_titleLabel);
            header.Add(leftHeader);

            var rightHeader = new VisualElement();
            rightHeader.style.flexDirection = FlexDirection.Row;
            rightHeader.style.alignItems = Align.Center;

            _budgetLabel = CreatePill("0 M$", new Color(0.4f, 0.9f, 0.5f));
            _upkeepLabel = CreatePill("Bakım: 0 M$/Ay", new Color(0.95f, 0.75f, 0.35f));
            _countLabel = CreatePill("0 Tesis", new Color(0.4f, 0.8f, 1f));
            rightHeader.Add(_budgetLabel);
            rightHeader.Add(_upkeepLabel);
            rightHeader.Add(_countLabel);
            header.Add(rightHeader);

            root.Add(header);

            // ── MAIN SPLIT: FACILITIES & CONSTRUCTION ───────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            // Left: Facilities ScrollView
            var leftCol = new VisualElement();
            leftCol.style.flexGrow = 1;
            leftCol.style.paddingLeft = 16;
            leftCol.style.paddingRight = 16;
            leftCol.style.paddingTop = 14;
            leftCol.style.paddingBottom = 14;

            var listHeader = new Label("KURULU TESİSLER");
            listHeader.style.fontSize = 12;
            listHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            listHeader.style.color = new Color(0.4f, 0.8f, 1f);
            listHeader.style.marginBottom = 10;
            leftCol.Add(listHeader);

            _facilitiesList = new ScrollView();
            _facilitiesList.style.flexGrow = 1;
            leftCol.Add(_facilitiesList);
            body.Add(leftCol);

            // Right: Construction Panel
            var rightCol = new VisualElement();
            rightCol.style.width = 340;
            rightCol.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            rightCol.style.borderLeftWidth = 1;
            rightCol.style.borderLeftColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            rightCol.style.paddingLeft = 16;
            rightCol.style.paddingRight = 16;
            rightCol.style.paddingTop = 14;
            rightCol.style.paddingBottom = 14;
            rightCol.style.justifyContent = Justify.SpaceBetween;

            var constructTop = new VisualElement();

            var constructTitle = new Label("YENİ TESİS İNŞA ET");
            constructTitle.style.fontSize = 12;
            constructTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            constructTitle.style.color = new Color(0.4f, 0.8f, 1f);
            constructTitle.style.marginBottom = 8;
            constructTop.Add(constructTitle);

            var regLabel = new Label("Bölge Seçimi:");
            regLabel.style.fontSize = 11;
            regLabel.style.color = new Color(0.7f, 0.85f, 1f);
            regLabel.style.marginBottom = 4;
            constructTop.Add(regLabel);

            _regionOptionsContainer = new VisualElement();
            _regionOptionsContainer.style.flexDirection = FlexDirection.Row;
            _regionOptionsContainer.style.flexWrap = Wrap.Wrap;
            _regionOptionsContainer.style.marginBottom = 12;
            constructTop.Add(_regionOptionsContainer);

            var typeLabel = new Label("Tesis Türü:");
            typeLabel.style.fontSize = 11;
            typeLabel.style.color = new Color(0.7f, 0.85f, 1f);
            typeLabel.style.marginBottom = 4;
            constructTop.Add(typeLabel);

            _baseTypeOptionsContainer = new VisualElement();
            _baseTypeOptionsContainer.style.marginBottom = 12;
            constructTop.Add(_baseTypeOptionsContainer);

            _costPreviewLabel = new Label("İnşaat Maliyeti: 0 M$");
            _costPreviewLabel.style.fontSize = 12;
            _costPreviewLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _costPreviewLabel.style.color = new Color(0.4f, 0.9f, 0.5f);
            _costPreviewLabel.style.marginTop = 8;
            constructTop.Add(_costPreviewLabel);

            rightCol.Add(constructTop);

            var constructBottom = new VisualElement();
            _buildBtn = new Button(() => ViewModel.BuildSelectedCommand.Execute()) { text = "Tesis İnşa Et"};
            _buildBtn.style.height = 36;
            _buildBtn.style.backgroundColor = new Color(0.18f, 0.50f, 0.35f);
            _buildBtn.style.color = Color.white;
            _buildBtn.style.fontSize = 13;
            _buildBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            constructBottom.Add(_buildBtn);
            rightCol.Add(constructBottom);

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

            RenderRegionButtons();
            RenderBaseTypeButtons();
            RenderFacilities();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
            });

            ViewModel.TotalMonthlyMaintenance.Subscribe(up =>
            {
                if (_upkeepLabel != null) _upkeepLabel.text = $"Bakım: {up:F1} M$/Ay";
            });

            ViewModel.TotalBasesCount.Subscribe(c =>
            {
                if (_countLabel != null) _countLabel.text = $"{c} Tesis";
            });

            ViewModel.SelectedBuildCost.Subscribe(cost =>
            {
                if (_costPreviewLabel != null) _costPreviewLabel.text = $"İnşaat Maliyeti: {cost:F1} M$";
            });

            ViewModel.Facilities.Subscribe(_ => RenderFacilities());
            ViewModel.AvailableRegions.Subscribe(_ => RenderRegionButtons());
            ViewModel.SelectedRegionId.Subscribe(_ => HighlightRegionButtons());
            ViewModel.SelectedBaseType.Subscribe(_ => HighlightTypeButtons());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void RenderRegionButtons()
        {
            if (_regionOptionsContainer == null || ViewModel == null) return;
            _regionOptionsContainer.Clear();
            _regionButtons.Clear();

            var regions = ViewModel.AvailableRegions.Value;
            if (regions == null) return;

            foreach (var reg in regions)
            {
                string rId = reg;
                var btn = new Button(() =>
                {
                    ViewModel.SetRegionForConstruction(rId);
                    HighlightRegionButtons();
                }) { text = FormatRegionName(rId) };

                btn.style.marginRight = 4;
                btn.style.marginBottom = 4;
                btn.style.paddingLeft = 8;
                btn.style.paddingRight = 8;
                btn.style.paddingTop = 4;
                btn.style.paddingBottom = 4;
                btn.style.fontSize = 11;
                btn.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f);
                btn.style.color = new Color(0.85f, 0.9f, 1f);

                _regionButtons[rId] = btn;
                _regionOptionsContainer.Add(btn);
            }

            HighlightRegionButtons();
        }

        private void HighlightRegionButtons()
        {
            string sel = ViewModel?.SelectedRegionId.Value ?? string.Empty;
            foreach (var kvp in _regionButtons)
            {
                bool isSel = string.Equals(kvp.Key, sel, StringComparison.OrdinalIgnoreCase);
                kvp.Value.style.backgroundColor = isSel ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.12f, 0.16f, 0.24f);
                kvp.Value.style.unityFontStyleAndWeight = isSel ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RenderBaseTypeButtons()
        {
            if (_baseTypeOptionsContainer == null || ViewModel == null) return;
            _baseTypeOptionsContainer.Clear();
            _typeButtons.Clear();

            var types = new[] { BaseType.LaunchPad, BaseType.Factory, BaseType.ResearchCenter, BaseType.GroundStation, BaseType.TestFacility };

            foreach (var t in types)
            {
                BaseType curType = t;
                var btn = new Button(() =>
                {
                    ViewModel.SetBaseTypeForConstruction(curType);
                    HighlightTypeButtons();
                }) { text = BaseFacilityItemViewModel.GetBaseTypeName(curType) };

                btn.style.marginBottom = 4;
                btn.style.paddingLeft = 10;
                btn.style.paddingRight = 10;
                btn.style.paddingTop = 5;
                btn.style.paddingBottom = 5;
                btn.style.fontSize = 11;
                btn.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f);
                btn.style.color = new Color(0.85f, 0.9f, 1f);

                _typeButtons[curType] = btn;
                _baseTypeOptionsContainer.Add(btn);
            }

            HighlightTypeButtons();
        }

        private void HighlightTypeButtons()
        {
            BaseType sel = ViewModel?.SelectedBaseType.Value ?? BaseType.LaunchPad;
            foreach (var kvp in _typeButtons)
            {
                bool isSel = kvp.Key == sel;
                kvp.Value.style.backgroundColor = isSel ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.12f, 0.16f, 0.24f);
                kvp.Value.style.unityFontStyleAndWeight = isSel ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RenderFacilities()
        {
            if (_facilitiesList == null || ViewModel == null) return;
            _facilitiesList.Clear();

            var facilities = ViewModel.Facilities.Value;
            if (facilities == null || facilities.Count == 0)
            {
                var emptyLabel = new Label("Henüz kurulmuş bir üs veya tesis bulunmamaktadır.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _facilitiesList.Add(emptyLabel);
                return;
            }

            foreach (var facility in facilities)
            {
                var card = CreateFacilityCard(facility);
                _facilitiesList.Add(card);
            }
        }

        private VisualElement CreateFacilityCard(BaseFacilityItemViewModel facility)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;

            var left = new VisualElement();
            var title = new Label($"{GetBaseIcon(facility.Type)} {facility.TypeName} ({FormatRegionName(facility.RegionId)})");
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.98f, 1f);
            left.Add(title);

            var meta = new Label($"Seviye {facility.Level}/{facility.MaxLevel}  ·  Kapasite: {facility.CapacityRating:F0}  ·  Bakım: {facility.MonthlyMaintenanceCost:F1} M$/Ay");
            meta.style.fontSize = 11;
            meta.style.color = new Color(0.6f, 0.75f, 0.9f);
            left.Add(meta);
            card.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            if (facility.CanUpgrade)
            {
                var upBtn = new Button(() => ViewModel.UpgradeFacility(facility.Id))
                {
                    text = $"Yükselt ({facility.NextUpgradeCost:F1} M$)"
                };
                upBtn.style.backgroundColor = new Color(0.15f, 0.35f, 0.65f);
                upBtn.style.color = Color.white;
                upBtn.style.paddingLeft = 8;
                upBtn.style.paddingRight = 8;
                upBtn.style.paddingTop = 4;
                upBtn.style.paddingBottom = 4;
                upBtn.style.fontSize = 11;
                right.Add(upBtn);
            }
            else
            {
                var maxLabel = new Label("Maks Seviye");
                maxLabel.style.fontSize = 11;
                maxLabel.style.color = new Color(0.35f, 0.85f, 0.55f);
                maxLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                right.Add(maxLabel);
            }

            card.Add(right);
            return card;
        }

        private static string GetBaseIcon(BaseType type) => type switch
        {
            BaseType.LaunchPad => "▲",
            BaseType.Factory => "■",
            BaseType.ResearchCenter => "◆",
            BaseType.GroundStation => "●",
            BaseType.TestFacility => "▼",
            _ => "●"
        };

        private static string FormatRegionName(string regionId) => regionId switch
        {
            "cape_peninsula" => "Cape Peninsula",
            "kourou" => "Kourou",
            "baikonur" => "Baykonur",
            "tanegashima" => "Tanegashima",
            "sriharikota" => "Sriharikota",
            "mahia" => "Mahia Peninsula",
            "andoya" => "Andøya",
            "alrisha_plateau" => "Alrisha Yaylası",
            _ => regionId
        };

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
