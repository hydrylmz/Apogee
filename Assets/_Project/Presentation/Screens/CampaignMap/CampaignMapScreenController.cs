using System;
using System.Collections.Generic;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.CampaignMap
{
    /// <summary>
    /// UI Toolkit screen controller for the Strategic World Map (ScreenId.CampaignMap = 3).
    /// Complies with GDD §6.1, §6.3, §14.1 (#3 Kampanya Ana Ekranı), and §21 M9-04.
    /// Provides global geographic map representation, region selection, lens evaluation,
    /// base markers, time controls, and direct navigation to management subpanels.
    /// </summary>
    public sealed class CampaignMapScreenController : ScreenControllerBase<CampaignMapViewModel>
    {
        public override ScreenId ScreenId => ScreenId.CampaignMap;

        // Top HUD
        private Label _agencyLabel;
        private Label _budgetLabel;
        private Label _reputationLabel;
        private Label _researchLabel;
        private Label _knowledgeLabel;
        private Label _dateLabel;
        private Button _pauseBtn;
        private Button _speed1xBtn;
        private Button _speed2xBtn;
        private Button _speed3xBtn;
        private Button _stepDayBtn;

        // Lens Bar
        private readonly Dictionary<MapLens, Button> _lensButtons = new();

        // World Map Canvas
        private VisualElement _mapCanvas;
        private readonly Dictionary<string, VisualElement> _regionPins = new();

        // Region Detail Panel
        private Label _regionNameLabel;
        private Label _regionCoordsLabel;
        private Label _lensScoreLabel;
        private VisualElement _lensScoreBarFill;
        private Label _lensDescLabel;
        private Label _rotBonusLabel;
        private Label _laborCostLabel;
        private Label _stabilityLabel;
        private Label _weatherRiskLabel;
        private Label _logisticsLabel;
        private Label _commCoverageLabel;
        private VisualElement _basesContainer;
        private VisualElement _allowedBasesContainer;

        // Status
        private Label _statusLabel;

        public CampaignMapScreenController(CampaignMapViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-campaign-map");
            root.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // ── TOP: STRATEGIC HUD ───────────────────────────────────────────
            var hud = new VisualElement();
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.justifyContent = Justify.SpaceBetween;
            hud.style.alignItems = Align.Center;
            hud.style.paddingTop = 8;
            hud.style.paddingBottom = 8;
            hud.style.paddingLeft = 16;
            hud.style.paddingRight = 16;
            hud.style.backgroundColor = new Color(0.06f, 0.09f, 0.15f, 0.98f);
            hud.style.borderBottomWidth = 1;
            hud.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            // Left: Agency Title
            _agencyLabel = new Label("BirleYik YrOnge Ajans  Komuta Masas");
            _agencyLabel.style.fontSize = 14;
            _agencyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _agencyLabel.style.color = new Color(0.9f, 0.95f, 1f);
            _agencyLabel.style.maxWidth = 260;
            _agencyLabel.style.overflow = Overflow.Hidden;
            _agencyLabel.style.textOverflow = TextOverflow.Ellipsis;
            _agencyLabel.style.flexShrink = 1;
            hud.Add(_agencyLabel);

            // Center: Resources Pill
            var resPill = new VisualElement();
            resPill.style.flexDirection = FlexDirection.Row;
            resPill.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.85f);
            resPill.style.paddingTop = 4;
            resPill.style.paddingBottom = 4;
            resPill.style.paddingLeft = 14;
            resPill.style.paddingRight = 14;
            resPill.style.borderTopLeftRadius = 14;
            resPill.style.borderTopRightRadius = 14;
            resPill.style.borderBottomLeftRadius = 14;
            resPill.style.borderBottomRightRadius = 14;

            _budgetLabel = CreateResourceTag("💰 Bütçe:", "500 M$", new Color(0.4f, 0.9f, 0.5f));
            _reputationLabel = CreateResourceTag("⭐ İtibar:", "30/100", new Color(0.95f, 0.75f, 0.3f));
            _researchLabel = CreateResourceTag("🔬 Ar-Ge:", "0 RP", new Color(0.35f, 0.85f, 1f));
            _knowledgeLabel = CreateResourceTag("💡 Bilgi:", "0 KP", new Color(0.8f, 0.6f, 1f));

            resPill.Add(_budgetLabel);
            resPill.Add(_reputationLabel);
            resPill.Add(_researchLabel);
            resPill.Add(_knowledgeLabel);
            hud.Add(resPill);

            // Right: Date & Time Speed controls
            var timeControls = new VisualElement();
            timeControls.style.flexDirection = FlexDirection.Row;
            timeControls.style.alignItems = Align.Center;

            _dateLabel = new Label("2031-01-01 (Gün 0)");
            _dateLabel.style.fontSize = 13;
            _dateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dateLabel.style.color = new Color(0.85f, 0.9f, 1f);
            _dateLabel.style.marginRight = 12;
            timeControls.Add(_dateLabel);

            _pauseBtn = CreateSpeedButton("⏸", () => ViewModel.PauseCommand.Execute());
            _speed1xBtn = CreateSpeedButton("1x", () => ViewModel.Speed1xCommand.Execute());
            _speed2xBtn = CreateSpeedButton("2x", () => ViewModel.Speed2xCommand.Execute());
            _speed3xBtn = CreateSpeedButton("3x", () => ViewModel.Speed3xCommand.Execute());
            _stepDayBtn = CreateSpeedButton("+1G", () => ViewModel.AdvanceDayCommand.Execute(), isSpecial: true);

            timeControls.Add(_pauseBtn);
            timeControls.Add(_speed1xBtn);
            timeControls.Add(_speed2xBtn);
            timeControls.Add(_speed3xBtn);
            timeControls.Add(_stepDayBtn);
            hud.Add(timeControls);

            root.Add(hud);

            // ── SUB-BAR: LENS SELECTOR ───────────────────────────────────────
            var lensBar = new VisualElement();
            lensBar.style.flexDirection = FlexDirection.Row;
            lensBar.style.alignItems = Align.Center;
            lensBar.style.paddingTop = 6;
            lensBar.style.paddingBottom = 6;
            lensBar.style.paddingLeft = 16;
            lensBar.style.backgroundColor = new Color(0.08f, 0.11f, 0.18f, 0.95f);
            lensBar.style.borderBottomWidth = 1;
            lensBar.style.borderBottomColor = new Color(0.15f, 0.2f, 0.3f, 0.8f);

            var lensTitle = new Label("STRATEJİK HARİTA LENSİ:");
            lensTitle.style.fontSize = 11;
            lensTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lensTitle.style.color = new Color(0.5f, 0.7f, 0.9f);
            lensTitle.style.marginRight = 10;
            lensBar.Add(lensTitle);

            AddLensButton(lensBar, MapLens.Overview, "🌐 Genel Bakış");
            AddLensButton(lensBar, MapLens.LaunchEfficiency, "🚀 Fırlatma Verimi");
            AddLensButton(lensBar, MapLens.Logistics, "📦 Lojistik");
            AddLensButton(lensBar, MapLens.CommsCoverage, "📡 Haberleşme");
            AddLensButton(lensBar, MapLens.Research, "🔬 Ar-Ge");
            AddLensButton(lensBar, MapLens.Risk, "⚠️ Risk");

            root.Add(lensBar);

            // ── MAIN SPLIT: WORLD MAP + REGION INSPECTOR ─────────────────────
            var mainSplit = new VisualElement();
            mainSplit.style.flexDirection = FlexDirection.Row;
            mainSplit.style.flexGrow = 1;

            // Map Canvas
            _mapCanvas = new VisualElement();
            _mapCanvas.style.flexGrow = 1;
            _mapCanvas.style.backgroundColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            _mapCanvas.style.position = Position.Relative;
            _mapCanvas.style.overflow = Overflow.Hidden;

            // Subtle Equator Line (0° latitude is Y=50%)
            var equatorLine = new VisualElement();
            equatorLine.style.position = Position.Absolute;
            equatorLine.style.left = 0;
            equatorLine.style.right = 0;
            equatorLine.style.top = Length.Percent(50);
            equatorLine.style.height = 1;
            equatorLine.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.4f);
            _mapCanvas.Add(equatorLine);

            var equatorLabel = new Label("EKVATOR (0°)");
            equatorLabel.style.position = Position.Absolute;
            equatorLabel.style.left = 12;
            equatorLabel.style.top = Length.Percent(50.5f);
            equatorLabel.style.fontSize = 9;
            equatorLabel.style.color = new Color(0.3f, 0.6f, 0.9f, 0.6f);
            _mapCanvas.Add(equatorLabel);

            mainSplit.Add(_mapCanvas);

            // Right: Region Inspector Panel
            var inspector = new VisualElement();
            inspector.style.width = 340;
            inspector.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            inspector.style.borderLeftWidth = 1;
            inspector.style.borderLeftColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            inspector.style.paddingTop = 14;
            inspector.style.paddingBottom = 14;
            inspector.style.paddingLeft = 16;
            inspector.style.paddingRight = 16;

            var inspectorHeader = new Label("BÖLGE DETAYI");
            inspectorHeader.style.fontSize = 11;
            inspectorHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            inspectorHeader.style.color = new Color(0.4f, 0.8f, 1f);
            inspector.Add(inspectorHeader);

            _regionNameLabel = new Label("Bölge Seçiniz");
            _regionNameLabel.style.fontSize = 20;
            _regionNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _regionNameLabel.style.color = new Color(0.95f, 0.98f, 1f);
            _regionNameLabel.style.marginTop = 2;
            inspector.Add(_regionNameLabel);

            _regionCoordsLabel = new Label("Koordinatlar: —");
            _regionCoordsLabel.style.fontSize = 11;
            _regionCoordsLabel.style.color = new Color(0.5f, 0.65f, 0.8f);
            _regionCoordsLabel.style.marginBottom = 12;
            inspector.Add(_regionCoordsLabel);

            // Lens Score Card
            var scoreCard = new VisualElement();
            scoreCard.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.9f);
            scoreCard.style.paddingTop = 8;
            scoreCard.style.paddingBottom = 8;
            scoreCard.style.paddingLeft = 10;
            scoreCard.style.paddingRight = 10;
            scoreCard.style.borderTopLeftRadius = 6;
            scoreCard.style.borderTopRightRadius = 6;
            scoreCard.style.borderBottomLeftRadius = 6;
            scoreCard.style.borderBottomRightRadius = 6;
            scoreCard.style.marginBottom = 12;

            var scoreRow = new VisualElement();
            scoreRow.style.flexDirection = FlexDirection.Row;
            scoreRow.style.justifyContent = Justify.SpaceBetween;

            var scoreTitle = new Label("Aktif Lens Uygunluğu:");
            scoreTitle.style.fontSize = 11;
            scoreTitle.style.color = new Color(0.7f, 0.85f, 1f);
            scoreRow.Add(scoreTitle);

            _lensScoreLabel = new Label("0%");
            _lensScoreLabel.style.fontSize = 12;
            _lensScoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _lensScoreLabel.style.color = new Color(0.35f, 0.95f, 0.65f);
            scoreRow.Add(_lensScoreLabel);
            scoreCard.Add(scoreRow);

            var barTrack = new VisualElement();
            barTrack.style.height = 6;
            barTrack.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            barTrack.style.borderTopLeftRadius = 3;
            barTrack.style.borderTopRightRadius = 3;
            barTrack.style.borderBottomLeftRadius = 3;
            barTrack.style.borderBottomRightRadius = 3;
            barTrack.style.marginTop = 4;
            barTrack.style.marginBottom = 6;

            _lensScoreBarFill = new VisualElement();
            _lensScoreBarFill.style.height = Length.Percent(100);
            _lensScoreBarFill.style.width = Length.Percent(0);
            _lensScoreBarFill.style.backgroundColor = new Color(0.35f, 0.85f, 0.6f);
            _lensScoreBarFill.style.borderTopLeftRadius = 3;
            _lensScoreBarFill.style.borderTopRightRadius = 3;
            _lensScoreBarFill.style.borderBottomLeftRadius = 3;
            _lensScoreBarFill.style.borderBottomRightRadius = 3;
            barTrack.Add(_lensScoreBarFill);
            scoreCard.Add(barTrack);

            _lensDescLabel = new Label("—");
            _lensDescLabel.style.fontSize = 11;
            _lensDescLabel.style.color = new Color(0.85f, 0.9f, 0.95f);
            _lensDescLabel.style.whiteSpace = WhiteSpace.Normal;
            scoreCard.Add(_lensDescLabel);
            inspector.Add(scoreCard);

            // Telemetry Rows
            var statsBox = new VisualElement();
            _rotBonusLabel = CreateStatRow(statsBox, "Ekvatoral Δv Bonusu:", "+0 m/s");
            _laborCostLabel = CreateStatRow(statsBox, "İşçilik Maliyeti:", "1.00x");
            _stabilityLabel = CreateStatRow(statsBox, "Siyasi İstikrar:", "100%");
            _weatherRiskLabel = CreateStatRow(statsBox, "Hava/İptal Riski:", "10%");
            _logisticsLabel = CreateStatRow(statsBox, "Lojistik Erişimi:", "100%");
            _commCoverageLabel = CreateStatRow(statsBox, "Kapsama Açısı:", "80%");
            inspector.Add(statsBox);

            // Active Bases Section
            var basesHeader = new Label("MEVCUT TESİSLER");
            basesHeader.style.fontSize = 11;
            basesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            basesHeader.style.color = new Color(0.5f, 0.75f, 1f);
            basesHeader.style.marginTop = 10;
            basesHeader.style.marginBottom = 4;
            inspector.Add(basesHeader);

            _basesContainer = new VisualElement();
            inspector.Add(_basesContainer);

            // Permitted Base Types
            var allowedHeader = new Label("İZİN VERİLEN İNŞAATLAR");
            allowedHeader.style.fontSize = 11;
            allowedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            allowedHeader.style.color = new Color(0.5f, 0.75f, 1f);
            allowedHeader.style.marginTop = 10;
            allowedHeader.style.marginBottom = 4;
            inspector.Add(allowedHeader);

            _allowedBasesContainer = new VisualElement();
            _allowedBasesContainer.style.flexDirection = FlexDirection.Row;
            _allowedBasesContainer.style.flexWrap = Wrap.Wrap;
            inspector.Add(_allowedBasesContainer);

            mainSplit.Add(inspector);
            root.Add(mainSplit);

            // ── BOTTOM: STRATEGIC NAVIGATION TOOLBAR ─────────────────────────
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            footer.style.paddingTop = 6;
            footer.style.paddingBottom = 6;
            footer.style.paddingLeft = 14;
            footer.style.paddingRight = 14;
            footer.style.backgroundColor = new Color(0.06f, 0.08f, 0.14f, 0.98f);
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var navTabs = new VisualElement();
            navTabs.style.flexDirection = FlexDirection.Row;

            AddNavTab(navTabs, "🚀 Misyon Tasarla", () => ViewModel.GoToComposerCommand.Execute(), isAccent: true);
            AddNavTab(navTabs, "🛠 Atölye", () => ViewModel.GoToWorkshopCommand.Execute());
            AddNavTab(navTabs, "🪐 Güneş Sistemi", () => ViewModel.GoToSolarMapCommand.Execute());
            AddNavTab(navTabs, "🔬 Ar-Ge Ağacı", () => ViewModel.GoToResearchCommand.Execute());
            AddNavTab(navTabs, "🏭 Üretim", () => ViewModel.GoToProductionCommand.Execute());
            AddNavTab(navTabs, "💼 Sözleşmeler", () => ViewModel.GoToContractsCommand.Execute());
            AddNavTab(navTabs, "🏗 Üsler", () => ViewModel.GoToBaseManagementCommand.Execute());
            AddNavTab(navTabs, "🏆 Hedefler", () => ViewModel.GoToMilestonesCommand.Execute());
            AddNavTab(navTabs, "🏠 Ana Menü", () => ViewModel.GoToMainMenuCommand.Execute());
            footer.Add(navTabs);

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
                ViewModel?.RefreshHUD();
                ViewModel?.RefreshRegions();
            }

            RenderRegionPins();
            UpdateInspector();
            UpdateSpeedButtons();
            UpdateLensButtons();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.DateDisplay.Subscribe(date =>
            {
                if (_dateLabel != null) _dateLabel.text = date ?? string.Empty;
            });

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"💰 {b:F1} M$";
            });

            ViewModel.Reputation.Subscribe(r =>
            {
                if (_reputationLabel != null) _reputationLabel.text = $"⭐ {r:F0}/100";
            });

            ViewModel.ResearchPoints.Subscribe(rp =>
            {
                if (_researchLabel != null) _researchLabel.text = $"🔬 {rp:F0} RP";
            });

            ViewModel.KnowledgePoints.Subscribe(kp =>
            {
                if (_knowledgeLabel != null) _knowledgeLabel.text = $"💡 {kp:F0} KP";
            });

            ViewModel.AgencyName.Subscribe(name =>
            {
                if (_agencyLabel != null) _agencyLabel.text = $"{name ?? "Ajans"} · Komuta Masası";
            });

            ViewModel.CurrentSpeed.Subscribe(_ => UpdateSpeedButtons());
            ViewModel.ActiveLens.Subscribe(_ =>
            {
                UpdateLensButtons();
                RenderRegionPins();
                UpdateInspector();
            });

            ViewModel.SelectedRegion.Subscribe(_ =>
            {
                RenderRegionPins();
                UpdateInspector();
            });

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void RenderRegionPins()
        {
            if (_mapCanvas == null || ViewModel == null) return;

            // Clear old pins
            foreach (var pin in _regionPins.Values)
            {
                pin.RemoveFromHierarchy();
            }
            _regionPins.Clear();

            var regions = ViewModel.Regions.Value;
            if (regions == null) return;

            foreach (var r in regions)
            {
                var pin = CreateRegionPin(r);
                _regionPins[r.Id] = pin;
                _mapCanvas.Add(pin);
            }
        }

        private VisualElement CreateRegionPin(RegionMapItemViewModel item)
        {
            var pin = new VisualElement();
            pin.style.position = Position.Absolute;
            float posX = item.NormalizedX;
            float posY = item.NormalizedY;
            if (item.Id == "mediterranean_hub") { posX -= 4f; posY += 6f; }
            else if (item.Id == "anatolian_plateau") { posY -= 6f; }
            else if (item.Id == "steppe_cosmodrome") { posX += 6f; posY -= 4f; }
            pin.style.left = Length.Percent(Math.Clamp(posX - 4f, 2f, 88f));
            pin.style.top = Length.Percent(Math.Clamp(posY - 3f, 4f, 88f));
            pin.style.paddingTop = 4;
            pin.style.paddingBottom = 4;
            pin.style.paddingLeft = 8;
            pin.style.paddingRight = 8;
            pin.style.borderTopLeftRadius = 6;
            pin.style.borderTopRightRadius = 6;
            pin.style.borderBottomLeftRadius = 6;
            pin.style.borderBottomRightRadius = 6;
            pin.style.borderTopWidth = 1;
            pin.style.borderBottomWidth = 1;
            pin.style.borderLeftWidth = 1;
            pin.style.borderRightWidth = 1;

            bool isSelected = item.IsSelected;
            if (isSelected)
            {
                pin.style.backgroundColor = new Color(0.12f, 0.35f, 0.55f, 0.95f);
                pin.style.borderTopColor = new Color(0.35f, 0.9f, 1f);
                pin.style.borderBottomColor = new Color(0.35f, 0.9f, 1f);
                pin.style.borderLeftColor = new Color(0.35f, 0.9f, 1f);
                pin.style.borderRightColor = new Color(0.35f, 0.9f, 1f);
            }
            else
            {
                pin.style.backgroundColor = new Color(0.08f, 0.12f, 0.20f, 0.90f);
                pin.style.borderTopColor = new Color(0.2f, 0.3f, 0.45f, 0.7f);
                pin.style.borderBottomColor = new Color(0.2f, 0.3f, 0.45f, 0.7f);
                pin.style.borderLeftColor = new Color(0.2f, 0.3f, 0.45f, 0.7f);
                pin.style.borderRightColor = new Color(0.2f, 0.3f, 0.45f, 0.7f);
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var nameLbl = new Label(item.Name);
            nameLbl.style.fontSize = 11;
            nameLbl.style.unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal;
            nameLbl.style.color = isSelected ? Color.white : new Color(0.85f, 0.9f, 0.98f);
            row.Add(nameLbl);

            var scoreBadge = new Label(item.LensScoreDisplay);
            scoreBadge.style.fontSize = 9;
            scoreBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            scoreBadge.style.marginLeft = 6;
            scoreBadge.style.paddingTop = 1;
            scoreBadge.style.paddingBottom = 1;
            scoreBadge.style.paddingLeft = 4;
            scoreBadge.style.paddingRight = 4;
            scoreBadge.style.borderTopLeftRadius = 3;
            scoreBadge.style.borderTopRightRadius = 3;
            scoreBadge.style.borderBottomLeftRadius = 3;
            scoreBadge.style.borderBottomRightRadius = 3;
            scoreBadge.style.backgroundColor = GetScoreBadgeColor(item.LensScore);
            scoreBadge.style.color = Color.white;
            row.Add(scoreBadge);
            pin.Add(row);

            // Base Icons row if any
            if (item.BaseCount > 0)
            {
                var baseIconsRow = new VisualElement();
                baseIconsRow.style.flexDirection = FlexDirection.Row;
                baseIconsRow.style.marginTop = 2;

                if (item.HasLaunchPad) baseIconsRow.Add(CreateMiniIcon("🚀"));
                if (item.HasFactory) baseIconsRow.Add(CreateMiniIcon("🏭"));
                if (item.HasResearchCenter) baseIconsRow.Add(CreateMiniIcon("🔬"));
                if (item.HasGroundStation) baseIconsRow.Add(CreateMiniIcon("📡"));
                if (item.HasTestFacility) baseIconsRow.Add(CreateMiniIcon("🧪"));

                pin.Add(baseIconsRow);
            }

            pin.RegisterCallback<ClickEvent>(_ => ViewModel.SelectRegion(item.Id));

            return pin;
        }

        private void UpdateInspector()
        {
            var item = ViewModel?.SelectedRegion.Value;
            if (item == null)
            {
                if (_regionNameLabel != null) _regionNameLabel.text = "Bölge Seçiniz";
                if (_regionCoordsLabel != null) _regionCoordsLabel.text = "Koordinatlar: —";
                if (_lensScoreLabel != null) _lensScoreLabel.text = "0%";
                if (_lensScoreBarFill != null) _lensScoreBarFill.style.width = Length.Percent(0);
                if (_lensDescLabel != null) _lensDescLabel.text = "—";
                return;
            }

            _regionNameLabel.text = item.Name;
            _regionCoordsLabel.text = $"Enlem: {item.Latitude:0.#}° | Boylam: {item.Longitude:0.#}°";

            double score = ViewModel.SelectedRegionScore.Value;
            _lensScoreLabel.text = $"{score * 100:F0}%";
            _lensScoreBarFill.style.width = Length.Percent((float)(score * 100));
            _lensScoreBarFill.style.backgroundColor = GetScoreBadgeColor(score);
            _lensDescLabel.text = ViewModel.SelectedRegionDescription.Value;

            _rotBonusLabel.text = $"+{item.Record.EquatorialBonusDv:0} m/s";
            _laborCostLabel.text = $"{item.Record.LaborCostMultiplier:0.##}x";
            _stabilityLabel.text = $"{item.Record.Stability * 100:0}%";
            _weatherRiskLabel.text = $"{item.Record.WeatherRisk * 100:0}%";
            _logisticsLabel.text = $"{item.Record.LogisticsAccessibility * 100:0}%";
            _commCoverageLabel.text = $"{item.Record.CommCoverageRating * 100:0}%";

            // Update Bases List
            _basesContainer.Clear();
            if (item.ActiveBases.Count == 0)
            {
                var emptyLbl = new Label("Bu bölgede henüz ajans tesisi yok.");
                emptyLbl.style.fontSize = 11;
                emptyLbl.style.color = new Color(0.5f, 0.6f, 0.7f);
                _basesContainer.Add(emptyLbl);
            }
            else
            {
                foreach (var b in item.ActiveBases)
                {
                    var baseCard = new VisualElement();
                    baseCard.style.flexDirection = FlexDirection.Row;
                    baseCard.style.justifyContent = Justify.SpaceBetween;
                    baseCard.style.paddingTop = 2;
                    baseCard.style.paddingBottom = 2;

                    var bName = new Label($"{GetBaseTypeIcon(b.Type)} {b.Type} (Lv {b.Level})");
                    bName.style.fontSize = 11;
                    bName.style.color = new Color(0.85f, 0.95f, 1f);
                    baseCard.Add(bName);

                    var bCost = new Label($"{b.MonthlyMaintenanceCost:F1} M$/ay");
                    bCost.style.fontSize = 11;
                    bCost.style.color = new Color(0.7f, 0.75f, 0.85f);
                    baseCard.Add(bCost);

                    _basesContainer.Add(baseCard);
                }
            }

            // Update Permitted Base Types
            _allowedBasesContainer.Clear();
            if (item.Record.AllowedBaseTypes != null)
            {
                foreach (var t in item.Record.AllowedBaseTypes)
                {
                    var tag = new Label(t.Replace('_', ' '));
                    tag.style.fontSize = 9;
                    tag.style.color = new Color(0.55f, 0.8f, 1f);
                    tag.style.backgroundColor = new Color(0.1f, 0.16f, 0.26f, 0.9f);
                    tag.style.paddingTop = 2;
                    tag.style.paddingBottom = 2;
                    tag.style.paddingLeft = 6;
                    tag.style.paddingRight = 6;
                    tag.style.marginRight = 4;
                    tag.style.marginBottom = 4;
                    tag.style.borderTopLeftRadius = 4;
                    tag.style.borderTopRightRadius = 4;
                    tag.style.borderBottomLeftRadius = 4;
                    tag.style.borderBottomRightRadius = 4;
                    _allowedBasesContainer.Add(tag);
                }
            }
        }

        private void UpdateSpeedButtons()
        {
            var speed = ViewModel?.CurrentSpeed.Value ?? TimeSpeed.Paused;

            SetButtonActive(_pauseBtn, speed == TimeSpeed.Paused);
            SetButtonActive(_speed1xBtn, speed == TimeSpeed.Normal);
            SetButtonActive(_speed2xBtn, speed == TimeSpeed.Fast);
            SetButtonActive(_speed3xBtn, speed == TimeSpeed.VeryFast);
        }

        private void UpdateLensButtons()
        {
            var activeLens = ViewModel?.ActiveLens.Value ?? MapLens.Overview;

            foreach (var kvp in _lensButtons)
            {
                SetButtonActive(kvp.Value, kvp.Key == activeLens);
            }
        }

        private void AddLensButton(VisualElement parent, MapLens lens, string label)
        {
            var btn = new Button { text = label };
            btn.style.height = 24;
            btn.style.fontSize = 11;
            btn.style.marginRight = 6;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.clicked += () => ViewModel.SetMapLens(lens);

            _lensButtons[lens] = btn;
            parent.Add(btn);
        }

        private static void SetButtonActive(Button btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive)
            {
                btn.style.backgroundColor = new Color(0.15f, 0.45f, 0.75f, 1f);
                btn.style.color = Color.white;
            }
            else
            {
                btn.style.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.9f);
                btn.style.color = new Color(0.7f, 0.8f, 0.9f);
            }
        }

        private static Button CreateSpeedButton(string label, Action onClick, bool isSpecial = false)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.height = 24;
            btn.style.fontSize = 11;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.marginLeft = 4;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = isSpecial ? new Color(0.18f, 0.35f, 0.25f) : new Color(0.10f, 0.14f, 0.22f);
            btn.style.color = isSpecial ? new Color(0.5f, 0.95f, 0.6f) : new Color(0.85f, 0.9f, 1f);
            return btn;
        }

        private static void AddNavTab(VisualElement parent, string label, Action onClick, bool isAccent = false)
        {
            var btn = new Button(onClick) { text = label };
            btn.style.height = 28;
            btn.style.fontSize = 11;
            btn.style.unityFontStyleAndWeight = isAccent ? FontStyle.Bold : FontStyle.Normal;
            btn.style.marginRight = 6;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.backgroundColor = isAccent ? new Color(0.12f, 0.42f, 0.68f) : new Color(0.10f, 0.14f, 0.22f);
            btn.style.color = isAccent ? Color.white : new Color(0.8f, 0.88f, 0.95f);
            parent.Add(btn);
        }

        private static Label CreateResourceTag(string prefix, string value, Color valueColor)
        {
            var lbl = new Label($"{prefix} {value}");
            lbl.style.fontSize = 12;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = valueColor;
            lbl.style.marginRight = 14;
            return lbl;
        }

        private static Label CreateStatRow(VisualElement parent, string title, string initialValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 2;
            row.style.marginBottom = 2;

            var tLbl = new Label(title);
            tLbl.style.fontSize = 11;
            tLbl.style.color = new Color(0.6f, 0.72f, 0.85f);
            row.Add(tLbl);

            var vLbl = new Label(initialValue);
            vLbl.style.fontSize = 11;
            vLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            vLbl.style.color = new Color(0.9f, 0.95f, 1f);
            row.Add(vLbl);

            parent.Add(row);
            return vLbl;
        }

        private static Label CreateMiniIcon(string emoji)
        {
            var lbl = new Label(emoji);
            lbl.style.fontSize = 10;
            lbl.style.marginRight = 2;
            return lbl;
        }

        private static Color GetScoreBadgeColor(double score)
        {
            if (score >= 0.75) return new Color(0.15f, 0.55f, 0.25f, 0.95f); // Green
            if (score >= 0.45) return new Color(0.65f, 0.55f, 0.15f, 0.95f); // Yellow / Amber
            return new Color(0.65f, 0.20f, 0.20f, 0.95f);                   // Red
        }

        private static string GetBaseTypeIcon(BaseType type)
        {
            return type switch
            {
                BaseType.LaunchPad => "🚀",
                BaseType.Factory => "🏭",
                BaseType.ResearchCenter => "🔬",
                BaseType.GroundStation => "📡",
                BaseType.TestFacility => "🧪",
                _ => "🏢"
            };
        }
    }
}
