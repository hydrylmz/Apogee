using System;
using System.Collections.Generic;
using Apogee.Data;
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

        // World Map Canvas (pan/zoom)
        private VisualElement _mapCanvas;
        private VisualElement _mapPlane;      // 2:1 geographic plane — pans & zooms as one unit
        private float _panX, _panY;           // translate offset (px, applied to _mapPlane)
        private float _zoom = 1f;             // scale factor for _mapPlane
        private bool _dragMoved;              // drag threshold exceeded → suppress pin click
        private Texture2D _worldMapTex;
        private IVisualElementScheduledItem _viewAnim;
        private const float ZoomMin = 0.8f;
        private const float ZoomMax = 5f;
        private const float DragThresholdPx = 6f;

        // Per-region manual nudges (normalized percentage points) to keep pins from
        // colliding with each other / edge labels. Applied on top of the raw projection.
        private static readonly Dictionary<string, (float dx, float dy)> PinOffsetLookup = new()
        {
            ["mediterranean_hub"] = (-4f, 6f),
            ["anatolian_plateau"] = (0f, -6f),
            ["steppe_cosmodrome"] = (6f, -4f),
            ["pacific_atoll"] = (2.5f, 0f), // keep clear of the left-edge latitude gutter
        };

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

        // Region Photos — floating popup anchored next to the selected pin on the map
        private VisualElement _photoPopup;
        private VisualElement _photoStrip;
        private VisualElement _photoPreview;
        private Label _photoCaptionLabel;
        private readonly Dictionary<string, Texture2D> _photoTexCache = new();
        private const float PhotoPopupWidth = 236f;

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

            _budgetLabel = CreateResourceTag("Bütçe:", "500 M$", new Color(0.4f, 0.9f, 0.5f));
            _reputationLabel = CreateResourceTag("İtibar:", "30/100", new Color(0.95f, 0.75f, 0.3f));
            _researchLabel = CreateResourceTag("Ar-Ge:", "0 RP", new Color(0.35f, 0.85f, 1f));
            _knowledgeLabel = CreateResourceTag("Bilgi:", "0 KP", new Color(0.8f, 0.6f, 1f));

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

            _pauseBtn = CreateSpeedButton("", () => ViewModel.PauseCommand.Execute());
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

            AddLensButton(lensBar, MapLens.Overview, "Genel Bakış");
            AddLensButton(lensBar, MapLens.LaunchEfficiency, "Fırlatma Verimi");
            AddLensButton(lensBar, MapLens.Logistics, "Lojistik");
            AddLensButton(lensBar, MapLens.CommsCoverage, "Haberleşme");
            AddLensButton(lensBar, MapLens.Research, "Ar-Ge");
            AddLensButton(lensBar, MapLens.Risk, "Risk");

            root.Add(lensBar);

            // ── MAIN SPLIT: WORLD MAP + REGION INSPECTOR ─────────────────────
            var mainSplit = new VisualElement();
            mainSplit.style.flexDirection = FlexDirection.Row;
            mainSplit.style.flexGrow = 1;

            // Map Canvas (visible window — overflow hidden)
            _mapCanvas = new VisualElement();
            _mapCanvas.style.flexGrow = 1;
            _mapCanvas.style.backgroundColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            _mapCanvas.style.position = Position.Relative;
            _mapCanvas.style.overflow = Overflow.Hidden;
            _mapCanvas.RegisterCallback<GeometryChangedEvent>(OnMapCanvasGeometry);

            // ── Pannable/zoomable plane: exactly 2:1 (equirectangular) ──────
            _mapPlane = new VisualElement();
            _mapPlane.style.position = Position.Absolute;
            _mapPlane.style.left = 0;
            _mapPlane.style.top = 0;
            _mapPlane.RegisterCallback<WheelEvent>(OnMapWheel, TrickleDown.TrickleDown);
            _mapPlane.RegisterCallback<PointerDownEvent>(OnMapPointerDown);
            _mapPlane.RegisterCallback<PointerMoveEvent>(OnMapPointerMove);
            _mapPlane.RegisterCallback<PointerUpEvent>(OnMapPointerUp);
            _mapCanvas.Add(_mapPlane);

            // Subtle world map backdrop (Natural Earth land, recolored to palette)
            _worldMapTex = LoadTexture("Map/world_map");
            if (_worldMapTex != null)
            {
                var mapBg = new VisualElement();
                mapBg.style.position = Position.Absolute;
                mapBg.style.left = 0;
                mapBg.style.top = 0;
                mapBg.style.width = Length.Percent(100);
                mapBg.style.height = Length.Percent(100);
                mapBg.style.backgroundImage = new StyleBackground(_worldMapTex);
                mapBg.pickingMode = PickingMode.Ignore;
                _mapPlane.Add(mapBg);
            }

            // Subtle Equator Line (0° latitude is Y=50%)
            var equatorLine = new VisualElement();
            equatorLine.style.position = Position.Absolute;
            equatorLine.style.left = 0;
            equatorLine.style.right = 0;
            equatorLine.style.top = Length.Percent(50);
            equatorLine.style.height = 1;
            equatorLine.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.4f);
            equatorLine.pickingMode = PickingMode.Ignore;
            _mapPlane.Add(equatorLine);

            var equatorLabel = new Label("EKVATOR (0°)");
            equatorLabel.style.position = Position.Absolute;
            equatorLabel.style.right = 12;
            equatorLabel.style.top = Length.Percent(50);
            equatorLabel.style.translate = new Translate(0, -9, 0);
            equatorLabel.style.fontSize = 9;
            equatorLabel.style.color = new Color(0.3f, 0.6f, 0.9f, 0.6f);
            equatorLabel.pickingMode = PickingMode.Ignore;
            _mapPlane.Add(equatorLabel);

            // Tactical Coordinate Grid (Parallels & Meridians)
            // Latitude labels live on the RIGHT edge so they never collide with the
            // leftmost region pin (Pacific Atoll) — fixes the overlap bug.
            float[] parallels = new[] { 16.7f, 33.3f, 66.7f, 83.3f };
            string[] parallelLabels = new[] { "60° K", "30° K", "30° G", "60° G" };
            for (int p = 0; p < parallels.Length; p++)
            {
                var pLine = new VisualElement();
                pLine.style.position = Position.Absolute;
                pLine.style.left = 0;
                pLine.style.right = 0;
                pLine.style.top = Length.Percent(parallels[p]);
                pLine.style.height = 1;
                pLine.style.backgroundColor = new Color(0.15f, 0.25f, 0.4f, 0.25f);
                pLine.pickingMode = PickingMode.Ignore;
                _mapPlane.Add(pLine);

                var pLbl = new Label(parallelLabels[p]);
                pLbl.style.position = Position.Absolute;
                pLbl.style.right = 12;
                pLbl.style.top = Length.Percent(parallels[p]);
                pLbl.style.translate = new Translate(0, -9, 0);
                pLbl.style.fontSize = 8;
                pLbl.style.color = new Color(0.25f, 0.45f, 0.7f, 0.5f);
                pLbl.pickingMode = PickingMode.Ignore;
                _mapPlane.Add(pLbl);
            }

            float[] meridians = new[] { 16.7f, 33.3f, 50.0f, 66.7f, 83.3f };
            string[] meridianLabels = new[] { "-120°", "-60°", "0° (Greenwich)", "+60°", "+120°" };
            for (int m = 0; m < meridians.Length; m++)
            {
                var mLine = new VisualElement();
                mLine.style.position = Position.Absolute;
                mLine.style.top = 0;
                mLine.style.bottom = 0;
                mLine.style.left = Length.Percent(meridians[m]);
                mLine.style.width = 1;
                mLine.style.backgroundColor = (meridians[m] == 50.0f)
                    ? new Color(0.2f, 0.45f, 0.7f, 0.35f)
                    : new Color(0.15f, 0.25f, 0.4f, 0.20f);
                mLine.pickingMode = PickingMode.Ignore;
                _mapPlane.Add(mLine);

                var mLbl = new Label(meridianLabels[m]);
                mLbl.style.position = Position.Absolute;
                mLbl.style.left = Length.Percent(meridians[m] + 0.5f);
                mLbl.style.bottom = 8;
                mLbl.style.fontSize = 8;
                mLbl.style.color = new Color(0.25f, 0.45f, 0.7f, 0.5f);
                mLbl.pickingMode = PickingMode.Ignore;
                _mapPlane.Add(mLbl);
            }

            // Reset view button (top-right of the visible canvas)
            var resetBtn = new Button(ResetMapView) { text = "Sıfırla" };
            resetBtn.style.position = Position.Absolute;
            resetBtn.style.top = 8;
            resetBtn.style.right = 8;
            resetBtn.style.height = 22;
            resetBtn.style.fontSize = 10;
            resetBtn.style.paddingLeft = 8;
            resetBtn.style.paddingRight = 8;
            resetBtn.style.borderTopLeftRadius = 4;
            resetBtn.style.borderTopRightRadius = 4;
            resetBtn.style.borderBottomLeftRadius = 4;
            resetBtn.style.borderBottomRightRadius = 4;
            resetBtn.style.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.92f);
            resetBtn.style.color = new Color(0.7f, 0.85f, 1f);
            resetBtn.style.borderTopWidth = 1;
            resetBtn.style.borderBottomWidth = 1;
            resetBtn.style.borderLeftWidth = 1;
            resetBtn.style.borderRightWidth = 1;
            resetBtn.style.borderTopColor = new Color(0.25f, 0.4f, 0.6f, 0.8f);
            resetBtn.style.borderBottomColor = new Color(0.25f, 0.4f, 0.6f, 0.8f);
            resetBtn.style.borderLeftColor = new Color(0.25f, 0.4f, 0.6f, 0.8f);
            resetBtn.style.borderRightColor = new Color(0.25f, 0.4f, 0.6f, 0.8f);
            _mapCanvas.Add(resetBtn);

            // Floating region photo popup — appears next to the clicked region's
            // pin instead of living inside the right inspector panel.
            CreatePhotoPopup();

            // Clicking empty map space dismisses the photo popup.
            _mapCanvas.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _mapCanvas || evt.target == _mapPlane) HidePhotoPopup();
            });

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
            scoreRow.style.alignItems = Align.Center;
            scoreRow.style.marginBottom = 6;

            var scoreTitle = new Label("Aktif Lens Uygunluğu:");
            scoreTitle.style.fontSize = 11;
            scoreTitle.style.color = new Color(0.7f, 0.85f, 1f);
            scoreTitle.style.flexShrink = 1;
            scoreTitle.style.whiteSpace = WhiteSpace.NoWrap;
            scoreRow.Add(scoreTitle);

            _lensScoreLabel = new Label("0%");
            _lensScoreLabel.style.fontSize = 12;
            _lensScoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _lensScoreLabel.style.color = new Color(0.35f, 0.95f, 0.65f);
            _lensScoreLabel.style.flexShrink = 0;
            _lensScoreLabel.style.marginLeft = 8;
            scoreRow.Add(_lensScoreLabel);
            scoreCard.Add(scoreRow);

            var barTrack = new VisualElement();
            barTrack.style.height = 6;
            barTrack.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            barTrack.style.borderTopLeftRadius = 3;
            barTrack.style.borderTopRightRadius = 3;
            barTrack.style.borderBottomLeftRadius = 3;
            barTrack.style.borderBottomRightRadius = 3;
            barTrack.style.marginBottom = 8;

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

            AddNavTab(navTabs, "Misyon Tasarla", () => ViewModel.GoToComposerCommand.Execute(), isAccent: true);
            AddNavTab(navTabs, "Atölye", () => ViewModel.GoToWorkshopCommand.Execute());
            AddNavTab(navTabs, "Güneş Sistemi", () => ViewModel.GoToSolarMapCommand.Execute());
            AddNavTab(navTabs, "Ar-Ge Ağacı", () => ViewModel.GoToResearchCommand.Execute());
            AddNavTab(navTabs, "Üretim", () => ViewModel.GoToProductionCommand.Execute());
            AddNavTab(navTabs, "Sözleşmeler", () => ViewModel.GoToContractsCommand.Execute());
            AddNavTab(navTabs, "Üsler", () => ViewModel.GoToBaseManagementCommand.Execute());
            AddNavTab(navTabs, "Hedefler", () => ViewModel.GoToMilestonesCommand.Execute());
            AddNavTab(navTabs, "Ana Menü", () => ViewModel.GoToMainMenuCommand.Execute());
            footer.Add(navTabs);

            _statusLabel = new Label("Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.4f, 0.8f, 1f);
            _statusLabel.style.marginLeft = 16;
            _statusLabel.style.marginRight = 10;
            _statusLabel.style.flexShrink = 0;
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
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
            });

            ViewModel.Reputation.Subscribe(r =>
            {
                if (_reputationLabel != null) _reputationLabel.text = $"{r:F0}/100";
            });

            ViewModel.ResearchPoints.Subscribe(rp =>
            {
                if (_researchLabel != null) _researchLabel.text = $"{rp:F0} RP";
            });

            ViewModel.KnowledgePoints.Subscribe(kp =>
            {
                if (_knowledgeLabel != null) _knowledgeLabel.text = $"{kp:F0} KP";
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

            // Re-render pins whenever the region list changes (e.g. after EnsureRegionsLoaded populates them)
            ViewModel.Regions.Subscribe(_ => RenderRegionPins());

            // Region photos (region_media.json) — re-render strip on selection change
            ViewModel.SelectedRegionMedia.Subscribe(_ => RenderPhotoStrip());
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

            // EnsureRegionsLoaded will update Regions.Value and the subscriber will re-trigger this method
            if (ViewModel.Regions.Value == null || ViewModel.Regions.Value.Count == 0)
            {
                ViewModel.EnsureRegionsLoaded();
                return; // subscriber will re-call RenderRegionPins when Regions.Value is populated
            }

            var regions = ViewModel.Regions.Value;
            if (regions == null) return;

            foreach (var r in regions)
            {
                var pin = CreateRegionPin(r);
                _regionPins[r.Id] = pin;
                _mapPlane.Add(pin);
            }
        }

        private VisualElement CreateRegionPin(RegionMapItemViewModel item)
        {
            var pin = new VisualElement();
            pin.style.position = Position.Absolute;

            float posX = item.NormalizedX;
            float posY = item.NormalizedY;
            if (PinOffsetLookup.TryGetValue(item.Id, out var off))
            {
                posX += off.dx;
                posY += off.dy;
            }

            // Geographic plane coordinates (percent of the 2:1 plane).
            // Clamp keeps pins on the plane; the left minimum keeps clear of the
            // right-edge-aligned latitude labels is no longer needed, but the
            // margin prevents pins from half-rendering off the antimeridian seam.
            pin.style.left = Length.Percent(Math.Clamp(posX, 3.5f, 90f));
            pin.style.top = Length.Percent(Math.Clamp(posY, 5f, 84f));
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

            // Hotspot dot — the "clickable point" affordance on the world map
            var dot = new VisualElement();
            dot.style.width = 10;
            dot.style.height = 10;
            dot.style.borderTopLeftRadius = 5;
            dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5;
            dot.style.borderBottomRightRadius = 5;
            dot.style.marginRight = 6;
            if (isSelected)
            {
                dot.style.backgroundColor = new Color(0.4f, 0.95f, 1f);
            }
            else
            {
                dot.style.backgroundColor = new Color(0.25f, 0.65f, 0.95f, 0.9f);
            }
            row.Add(dot);

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

                if (item.HasLaunchPad) baseIconsRow.Add(CreateMiniIcon(""));
                if (item.HasFactory) baseIconsRow.Add(CreateMiniIcon(""));
                if (item.HasResearchCenter) baseIconsRow.Add(CreateMiniIcon(""));
                if (item.HasGroundStation) baseIconsRow.Add(CreateMiniIcon(""));
                if (item.HasTestFacility) baseIconsRow.Add(CreateMiniIcon(""));

                pin.Add(baseIconsRow);
            }

            pin.RegisterCallback<ClickEvent>(_ =>
            {
                if (_dragMoved) return; // suppress click after a pan drag
                ViewModel.SelectRegion(item.Id);
                ZoomToRegion(item);
            });

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

        // ── Map Pan / Zoom / Animation ─────────────────────────────────────

        private void OnMapCanvasGeometry(GeometryChangedEvent evt)
        {
            var canvasRect = evt.newRect;
            if (canvasRect.width < 1 || canvasRect.height < 1 || _mapPlane == null) return;

            // Keep the geographic plane at exactly 2:1 (equirectangular) while
            // always covering the visible canvas.
            float planeW = Mathf.Max(canvasRect.width, canvasRect.height * 2f);
            float planeH = planeW / 2f;
            _mapPlane.style.width = planeW;
            _mapPlane.style.height = planeH;
            _mapPlane.style.left = (canvasRect.width - planeW) / 2f;
            _mapPlane.style.top = (canvasRect.height - planeH) / 2f;

            ClampPan();
            ApplyView();
        }

        private void OnMapWheel(WheelEvent evt)
        {
            evt.StopPropagation();

            float factor = evt.delta.y > 0f ? 1f / 1.12f : 1.12f;
            float newZoom = Mathf.Clamp(_zoom * factor, ZoomMin, ZoomMax);
            if (Mathf.Approximately(newZoom, _zoom)) return;

            // Zoom around the cursor: keep the plane-space point under the cursor fixed.
            var canvasRect = _mapCanvas.contentRect;
            var cursor = _mapPlane.WorldToLocal(evt.mousePosition);

            // Plane center in plane-local coordinates:
            var planeRect = _mapPlane.contentRect;
            var planeCenter = new Vector2(planeRect.width / 2f, planeRect.height / 2f);

            // translate' = translate + (cursor - planeCenter) * (zoom - newZoom)  [origin = center]
            float dx = (cursor.x - planeCenter.x) * (_zoom - newZoom) / _zoom;
            float dy = (cursor.y - planeCenter.y) * (_zoom - newZoom) / _zoom;

            _zoom = newZoom;
            _panX += dx;
            _panY += dy;
            ClampPan();
            ApplyView();
        }

        private Vector2 _pointerDownPos;

        private void OnMapPointerDown(PointerDownEvent evt)
        {
            _pointerDownPos = evt.position;
            _dragMoved = false;
        }

        private void OnMapPointerMove(PointerMoveEvent evt)
        {
            if (evt.pressedButtons == 0) return;

            float dx = evt.position.x - _pointerDownPos.x;
            float dy = evt.position.y - _pointerDownPos.y;
            if (!_dragMoved && (dx * dx + dy * dy) > DragThresholdPx * DragThresholdPx)
            {
                _dragMoved = true;
            }

            if (_dragMoved)
            {
                _panX += evt.deltaPosition.x;
                _panY += evt.deltaPosition.y;
                _pointerDownPos = evt.position;
                ClampPan();
                ApplyView();
            }
        }

        private void OnMapPointerUp(PointerUpEvent evt)
        {
            // No pointer capture used; drag state resets on the next press.
        }

        /// <summary>
        /// Keeps the (scaled) plane covering the visible canvas at all times —
        /// the map can never be panned completely off-screen.
        /// </summary>
        private void ClampPan()
        {
            if (_mapCanvas == null || _mapPlane == null) return;
            var canvasRect = _mapCanvas.contentRect;
            var planeRect = _mapPlane.contentRect;
            float scaledW = planeRect.width * _zoom;
            float scaledH = planeRect.height * _zoom;

            float maxX = Mathf.Max(0f, (scaledW - canvasRect.width) / 2f);
            float maxY = Mathf.Max(0f, (scaledH - canvasRect.height) / 2f);

            _panX = Mathf.Clamp(_panX, -maxX, maxX);
            _panY = Mathf.Clamp(_panY, -maxY, maxY);
        }

        private void ApplyView()
        {
            if (_mapPlane == null) return;
            _mapPlane.style.translate = new Translate(_panX, _panY, 0);
            _mapPlane.style.scale = new Scale(new Vector3(_zoom, _zoom, 1f));
            UpdatePhotoPopupPosition(); // keep the popup anchored to its pin while panning/zooming
        }

        /// <summary>Animates the view so the region's pin centers on the canvas at ~2.2x zoom.</summary>
        private void ZoomToRegion(RegionMapItemViewModel item)
        {
            if (_mapPlane == null || _mapCanvas == null) return;
            _viewAnim?.Pause();

            var planeRect = _mapPlane.contentRect;
            if (planeRect.width < 1f || planeRect.height < 1f) return;

            float targetZoom = Mathf.Max(2.2f, Mathf.Min(_zoom, ZoomMax));

            // Plane-space position of the region's geographic point:
            var q = new Vector2(
                (Math.Clamp(item.NormalizedX + (PinOffsetLookup.TryGetValue(item.Id, out var o) ? o.dx : 0f), 3.5f, 90f) / 100f) * planeRect.width,
                (Math.Clamp(item.NormalizedY + (PinOffsetLookup.TryGetValue(item.Id, out var o2) ? o2.dy : 0f), 5f, 84f) / 100f) * planeRect.height);
            var planeCenter = new Vector2(planeRect.width / 2f, planeRect.height / 2f);

            // want q rendered at canvas center: pan = -(q - planeCenter) * zoom  (origin = center)
            float targetPanX = -(q.x - planeCenter.x) * targetZoom;
            float targetPanY = -(q.y - planeCenter.y) * targetZoom;

            AnimateView(targetPanX, targetPanY, targetZoom);
        }

        private void ResetMapView()
        {
            _viewAnim?.Pause();
            AnimateView(0f, 0f, 1f);
        }

        private void AnimateView(float targetPanX, float targetPanY, float targetZoom)
        {
            ClampTarget(ref targetPanX, ref targetPanY, targetZoom);

            const long frameMs = 16L;
            const float duration = 0.28f;
            float startPanX = _panX, startPanY = _panY, startZoom = _zoom;
            float t = 0f;

            _viewAnim?.Pause();
            _viewAnim = _mapPlane.schedule.Execute(() =>
            {
                t += frameMs / 1000f;
                float u = Mathf.Clamp01(t / duration);
                u = 1f - (1f - u) * (1f - u); // ease-out quad

                _panX = Mathf.LerpUnclamped(startPanX, targetPanX, u);
                _panY = Mathf.LerpUnclamped(startPanY, targetPanY, u);
                _zoom = Mathf.LerpUnclamped(startZoom, targetZoom, u);

                ClampPan();
                ApplyView();

                if (u >= 1f) _viewAnim?.Pause();
            }).Every(frameMs);
        }

        private void ClampTarget(ref float panX, ref float panY, float zoom)
        {
            if (_mapCanvas == null || _mapPlane == null) return;
            var canvasRect = _mapCanvas.contentRect;
            var planeRect = _mapPlane.contentRect;
            float maxX = Mathf.Max(0f, (planeRect.width * zoom - canvasRect.width) / 2f);
            float maxY = Mathf.Max(0f, (planeRect.height * zoom - canvasRect.height) / 2f);
            panX = Mathf.Clamp(panX, -maxX, maxX);
            panY = Mathf.Clamp(panY, -maxY, maxY);
        }

        private static Texture2D LoadTexture(string resourcesPath)
        {
            try
            {
                return Resources.Load<Texture2D>(resourcesPath);
            }
            catch
            {
                return null; // non-Unity harness / missing asset — map renders without backdrop
            }
        }

        // ── Region Photo Popup (floating, anchored to the selected pin) ────

        private void CreatePhotoPopup()
        {
            _photoPopup = new VisualElement();
            _photoPopup.style.position = Position.Absolute;
            _photoPopup.style.width = PhotoPopupWidth;
            _photoPopup.style.backgroundColor = new Color(0.06f, 0.09f, 0.15f, 0.96f);
            _photoPopup.style.borderTopLeftRadius = 8;
            _photoPopup.style.borderTopRightRadius = 8;
            _photoPopup.style.borderBottomLeftRadius = 8;
            _photoPopup.style.borderBottomRightRadius = 8;
            _photoPopup.style.borderTopWidth = 1;
            _photoPopup.style.borderBottomWidth = 1;
            _photoPopup.style.borderLeftWidth = 1;
            _photoPopup.style.borderRightWidth = 1;
            _photoPopup.style.borderTopColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
            _photoPopup.style.borderBottomColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
            _photoPopup.style.borderLeftColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
            _photoPopup.style.borderRightColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
            _photoPopup.style.paddingTop = 10;
            _photoPopup.style.paddingBottom = 10;
            _photoPopup.style.paddingLeft = 12;
            _photoPopup.style.paddingRight = 12;
            _photoPopup.style.display = DisplayStyle.None;
            // Clicks inside the popup must not bubble to the map's dismiss handler.
            _photoPopup.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _mapCanvas.Add(_photoPopup);

            var photoHeader = new Label("GÖRÜNTÜLER");
            photoHeader.style.fontSize = 11;
            photoHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            photoHeader.style.color = new Color(0.5f, 0.75f, 1f);
            photoHeader.style.marginBottom = 6;
            _photoPopup.Add(photoHeader);

            _photoPreview = new VisualElement();
            _photoPreview.style.height = 130;
            _photoPreview.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1f);
            _photoPreview.style.borderTopLeftRadius = 6;
            _photoPreview.style.borderTopRightRadius = 6;
            _photoPreview.style.borderBottomLeftRadius = 6;
            _photoPreview.style.borderBottomRightRadius = 6;
            _photoPreview.style.marginBottom = 6;
            _photoPreview.style.overflow = Overflow.Hidden;
            _photoPreview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _photoPreview.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _photoPreview.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _photoPreview.style.display = DisplayStyle.None;
            _photoPopup.Add(_photoPreview);

            _photoCaptionLabel = new Label("");
            _photoCaptionLabel.style.fontSize = 10;
            _photoCaptionLabel.style.color = new Color(0.65f, 0.75f, 0.88f);
            _photoCaptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _photoCaptionLabel.style.marginBottom = 6;
            _photoCaptionLabel.style.display = DisplayStyle.None;
            _photoPopup.Add(_photoCaptionLabel);

            var stripScroll = new ScrollView(ScrollViewMode.Horizontal);
            stripScroll.style.height = 64;
            stripScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            stripScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _photoStrip = new VisualElement();
            _photoStrip.style.flexDirection = FlexDirection.Row;
            stripScroll.Add(_photoStrip);
            _photoPopup.Add(_photoStrip);
        }

        private void HidePhotoPopup()
        {
            if (_photoPopup != null) _photoPopup.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Places the photo popup next to the selected region's pin (canvas space),
        /// flipping to the pin's left side when it would overflow the right edge.
        /// Called from ApplyView so the popup tracks pan/zoom continuously.
        /// </summary>
        private void UpdatePhotoPopupPosition()
        {
            if (_photoPopup == null || _photoPopup.style.display == DisplayStyle.None) return;

            var item = ViewModel?.SelectedRegion.Value;
            if (item == null || _mapCanvas == null || _mapPlane == null)
            {
                HidePhotoPopup();
                return;
            }

            var canvasRect = _mapCanvas.contentRect;
            var planeRect = _mapPlane.contentRect;
            if (canvasRect.width < 1 || canvasRect.height < 1 || planeRect.width < 1 || planeRect.height < 1) return;

            // Same clamped projection the pin uses, in plane-local pixels.
            float px = Math.Clamp(item.NormalizedX + (PinOffsetLookup.TryGetValue(item.Id, out var off) ? off.dx : 0f), 3.5f, 90f);
            float py = Math.Clamp(item.NormalizedY + (PinOffsetLookup.TryGetValue(item.Id, out var off2) ? off2.dy : 0f), 5f, 84f);
            var q = new Vector2(px / 100f * planeRect.width, py / 100f * planeRect.height);

            // Plane point → canvas point (scale origin is the plane center).
            var planeCenter = new Vector2(planeRect.width / 2f, planeRect.height / 2f);
            float pinX = (canvasRect.width - planeRect.width) / 2f + planeCenter.x + (q.x - planeCenter.x) * _zoom + _panX;
            float pinY = (canvasRect.height - planeRect.height) / 2f + planeCenter.y + (q.y - planeCenter.y) * _zoom + _panY;

            var popRect = _photoPopup.layout;
            float popW = popRect.width > 0f ? popRect.width : PhotoPopupWidth + 26f;
            float popH = popRect.height > 0f ? popRect.height : 260f;

            float x = pinX + 18f;
            if (x + popW > canvasRect.width - 8f) x = pinX - popW - 18f; // flip to the left of the pin
            float y = pinY + 12f;

            _photoPopup.style.left = Mathf.Clamp(x, 8f, Mathf.Max(8f, canvasRect.width - popW - 8f));
            _photoPopup.style.top = Mathf.Clamp(y, 8f, Mathf.Max(8f, canvasRect.height - popH - 8f));
        }

        // ── Region Photo Strip ─────────────────────────────────────────────

        private void RenderPhotoStrip()
        {
            if (_photoStrip == null || _photoPopup == null || ViewModel == null) return;

            _photoStrip.Clear();

            var media = ViewModel.SelectedRegionMedia.Value;
            if (media == null || media.Count == 0)
            {
                // No imagery for this region → no popup at all.
                HidePhotoPopup();
                return;
            }

            RegionMediaRecord first = null;
            foreach (var rec in media)
            {
                if (rec == null || string.IsNullOrEmpty(rec.Path)) continue;
                if (first == null) first = rec;

                var tex = GetPhotoTexture(rec.Path);
                if (tex == null) continue;

                var thumb = new Button(() => ShowPhoto(rec));
                thumb.style.width = 108;
                thumb.style.height = 68;
                thumb.style.marginRight = 6;
                thumb.style.borderTopLeftRadius = 4;
                thumb.style.borderTopRightRadius = 4;
                thumb.style.borderBottomLeftRadius = 4;
                thumb.style.borderBottomRightRadius = 4;
                thumb.style.borderTopWidth = 1;
                thumb.style.borderBottomWidth = 1;
                thumb.style.borderLeftWidth = 1;
                thumb.style.borderRightWidth = 1;
                thumb.style.borderTopColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
                thumb.style.borderBottomColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
                thumb.style.borderLeftColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
                thumb.style.borderRightColor = new Color(0.25f, 0.4f, 0.6f, 0.9f);
                thumb.style.backgroundImage = new StyleBackground(tex);
                thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _photoStrip.Add(thumb);
            }

            if (first != null)
            {
                ShowPhoto(first);
                _photoPopup.style.display = DisplayStyle.Flex;
                // Position once the popup has been laid out.
                _photoPopup.schedule.Execute(UpdatePhotoPopupPosition);
            }
            else
            {
                HidePhotoPopup();
            }
        }

        private void ShowPhoto(RegionMediaRecord rec)
        {
            var tex = GetPhotoTexture(rec.Path);
            if (tex == null || _photoPreview == null) return;

            _photoPreview.style.display = DisplayStyle.Flex;
            _photoPreview.style.backgroundImage = new StyleBackground(tex);
            _photoCaptionLabel.style.display = DisplayStyle.Flex;
            _photoCaptionLabel.text = rec.IsRepresentative
                ? $"{rec.Caption}\nAtıf: {rec.Credit}"
                : $"{rec.Caption} · Atıf: {rec.Credit}";
        }

        private Texture2D GetPhotoTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_photoTexCache.TryGetValue(path, out var cached)) return cached;

            var tex = LoadTexture(path);
            if (tex != null) _photoTexCache[path] = tex;
            return tex;
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
                BaseType.LaunchPad => "",
                BaseType.Factory => "",
                BaseType.ResearchCenter => "",
                BaseType.GroundStation => "",
                BaseType.TestFacility => "",
                _ => ""
            };
        }
    }
}
