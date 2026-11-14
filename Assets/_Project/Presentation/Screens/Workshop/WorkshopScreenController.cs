using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Apogee.Core.Audio;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Screen controller for the Design Workshop "Hangar" experience (GDD §7, §14.3, §15.1, ADR-054).
    /// Features:
    /// - High-fidelity 2D vector rocket illustration (HangarRocketCanvas) with 4 layers (Exterior, Cutaway, Subsystems, Schematic).
    /// - Section-based interaction with hover leader-lines, focus zoom, and two-way slot synchronization.
    /// - Slide-out module picker drawer (HangarPartDrawer) with cards, supplier badges, research locks, and ghost preview.
    /// - Vertical section rail (HangarSectionRail) with accessibility pips.
    /// - Large HUD with real-time telemetry and hayalet delta indicators.
    /// - Collapsible Engineering View panel (accessible via 'E' or toggle).
    /// - Full keyboard navigation ('1-9', 'Tab', 'Esc', 'Ctrl+Z/Y', 'E') and audio feedback.
    /// </summary>
    public class WorkshopScreenController : ScreenControllerBase<WorkshopViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Workshop;

        private readonly IAudioService _audioService;

        // Top Bar
        private Label _targetLabel;
        private Label _blueprintNameLabel;
        private Button _undoButton;
        private Button _redoButton;
        private Button _layerExteriorBtn;
        private Button _layerCutawayBtn;
        private Button _layerSubsystemsBtn;
        private Button _layerSchematicBtn;
        private Button _toggleEngineeringBtn;

        // Central Hangar Canvas & Sidebars
        private HangarRocketCanvas _canvas;
        private HangarPartDrawer _drawer;
        private HangarSectionRail _rail;
        private VisualElement _railSplitter;
        private VisualElement _engineeringPanel;
        private ScrollView _engineeringSlotsScroll;
        private bool _isEngineeringViewOpen = false;
        private bool _isEngineeringPanelMoved = false;

        // Bottom HUD Telemetry
        private Label _deltaVValueLabel;
        private Label _deltaVGhostLabel;
        private VisualElement _deltaVBarFill;
        private VisualElement _deltaVGhostBarFill;

        private Label _massValueLabel;
        private Label _massGhostLabel;

        private Label _costValueLabel;
        private Label _costGhostLabel;

        private Label _reliabilityValueLabel;
        private Label _reliabilityGhostLabel;

        private Label _powerMarginValueLabel;
        private Label _powerMarginGhostLabel;

        private Label _buildDaysValueLabel;

        private VisualElement _ghostSummaryBox;
        private Label _ghostSummaryTextLabel;

        private Button _reviewButton;

        public WorkshopScreenController(WorkshopViewModel viewModel, IAudioService audioService = null) : base(viewModel)
        {
            _audioService = audioService ?? (UnityEngine.Object.FindObjectOfType<Apogee.UI.Audio.UnityAudioService>() as IAudioService ?? new NullAudioService());
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-hangar-root");

            // ── 1. Top Bar (Toolbar, Layers, Navigation) ────────────────────────────
            var topBar = BuildTopBar();
            root.Add(topBar);

            // ── 2. Central Work Area (Rail, Canvas, Drawer, Engineering Panel) ──────
            var centerArea = new VisualElement();
            centerArea.style.flexDirection = FlexDirection.Row;
            centerArea.style.flexGrow = 1;
            centerArea.style.position = Position.Relative;

            // Left Miniature Section Rail
            _rail = new HangarSectionRail(ViewModel);
            _rail.SlotSelected += OnSlotSelectedFromRail;
            centerArea.Add(_rail);

            // Splitter Handle for resizing Section Rail
            _railSplitter = CreateRailSplitter(_rail);
            centerArea.Add(_railSplitter);

            // Center Interactive 2D Rocket Canvas
            _canvas = new HangarRocketCanvas();
            _canvas.SlotSelected += OnSlotSelectedFromCanvas;
            _canvas.SectionHovered += OnSectionHoveredFromCanvas;
            centerArea.Add(_canvas);

            // Right Slide-out Module Drawer
            _drawer = new HangarPartDrawer(ViewModel);
            _drawer.OptionHovered += OnDrawerOptionHovered;
            _drawer.OptionUnhovered += OnDrawerOptionUnhovered;
            _drawer.DrawerClosed += OnDrawerClosed;
            centerArea.Add(_drawer);

            // Collapsible Engineering View Panel (Default Hidden)
            _engineeringPanel = BuildEngineeringPanel();
            centerArea.Add(_engineeringPanel);

            root.Add(centerArea);

            // ── 3. Bottom HUD (Telemetry, Ghost Delta, Review CTA) ──────────────────
            var hud = BuildBottomHud();
            root.Add(hud);

            // Setup Keyboard Shortcuts
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            return root;
        }

        private VisualElement BuildTopBar()
        {
            var topBar = new VisualElement();
            topBar.AddToClassList("apogee-hangar-topbar");

            // Left: Title + Target Badge
            var leftGroup = new VisualElement();
            leftGroup.style.flexDirection = FlexDirection.Row;
            leftGroup.style.alignItems = Align.Center;

            var titleBox = new VisualElement();
            _blueprintNameLabel = new Label("APOGEE HANGAR");
            _blueprintNameLabel.AddToClassList("apogee-panel-title");
            _blueprintNameLabel.style.fontSize = 14;
            _blueprintNameLabel.style.color = new Color(0.20f, 0.75f, 0.95f);
            titleBox.Add(_blueprintNameLabel);

            var subTitle = new Label("Roket Atölyesi · Montaj & Kesit");
            subTitle.style.fontSize = 9;
            subTitle.style.color = new Color(0.40f, 0.52f, 0.68f);
            titleBox.Add(subTitle);
            leftGroup.Add(titleBox);

            _targetLabel = new Label("Hedef: Ay");
            _targetLabel.AddToClassList("apogee-badge");
            _targetLabel.AddToClassList("apogee-badge--cyan");
            _targetLabel.style.marginLeft = 12;
            _targetLabel.style.fontSize = 10;
            leftGroup.Add(_targetLabel);
            topBar.Add(leftGroup);

            // Center: Layer Modes & View Controls
            var centerGroup = new VisualElement();
            centerGroup.style.flexDirection = FlexDirection.Row;
            centerGroup.style.alignItems = Align.Center;

            _layerExteriorBtn = CreateLayerButton("Dış Görünüm", HangarLayerMode.Exterior);
            _layerCutawayBtn = CreateLayerButton("Kesit", HangarLayerMode.Cutaway, isActive: true);
            _layerSubsystemsBtn = CreateLayerButton("İç Sistemler", HangarLayerMode.Subsystems);
            _layerSchematicBtn = CreateLayerButton("Şematik", HangarLayerMode.Schematic);

            centerGroup.Add(_layerExteriorBtn);
            centerGroup.Add(_layerCutawayBtn);
            centerGroup.Add(_layerSubsystemsBtn);
            centerGroup.Add(_layerSchematicBtn);

            var resetViewBtn = new Button(() =>
            {
                _canvas.ResetView();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            resetViewBtn.text = "Tümünü Göster";
            resetViewBtn.AddToClassList("apogee-button");
            resetViewBtn.style.fontSize = 10;
            resetViewBtn.style.marginLeft = 10;
            centerGroup.Add(resetViewBtn);

            _toggleEngineeringBtn = new Button(ToggleEngineeringView);
            _toggleEngineeringBtn.text = "Mühendis (E)";
            _toggleEngineeringBtn.AddToClassList("apogee-button");
            _toggleEngineeringBtn.style.fontSize = 10;
            _toggleEngineeringBtn.style.marginLeft = 6;
            centerGroup.Add(_toggleEngineeringBtn);

            topBar.Add(centerGroup);

            // Right: Command & Navigation Buttons
            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            _undoButton = new Button(() =>
            {
                ViewModel.UndoCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            _undoButton.text = "<-";
            _undoButton.tooltip = "Geri Al (Ctrl+Z)";
            _undoButton.AddToClassList("apogee-button");
            _undoButton.style.fontSize = 12;
            _undoButton.style.paddingLeft = 8;
            _undoButton.style.paddingRight = 8;
            rightGroup.Add(_undoButton);

            _redoButton = new Button(() =>
            {
                ViewModel.RedoCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            _redoButton.text = "->";
            _redoButton.tooltip = "Yinele (Ctrl+Y)";
            _redoButton.AddToClassList("apogee-button");
            _redoButton.style.fontSize = 12;
            _redoButton.style.marginLeft = 4;
            _redoButton.style.paddingLeft = 8;
            _redoButton.style.paddingRight = 8;
            rightGroup.Add(_redoButton);

            var saveTemplateBtn = new Button(() =>
            {
                ViewModel.Templates.SaveCurrentCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            saveTemplateBtn.text = "Şablon";
            saveTemplateBtn.AddToClassList("apogee-button");
            saveTemplateBtn.style.fontSize = 10;
            saveTemplateBtn.style.marginLeft = 8;
            rightGroup.Add(saveTemplateBtn);

            var compareBtn = new Button(() =>
            {
                ViewModel.OpenComparisonCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            compareBtn.text = "Karşılaştır";
            compareBtn.AddToClassList("apogee-button");
            compareBtn.style.fontSize = 10;
            compareBtn.style.marginLeft = 4;
            rightGroup.Add(compareBtn);

            var composerBtn = new Button(() =>
            {
                ViewModel.GoToComposerCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            composerBtn.text = "Misyon";
            composerBtn.AddToClassList("apogee-button");
            composerBtn.AddToClassList("apogee-button--accent-amber");
            composerBtn.style.fontSize = 10;
            composerBtn.style.marginLeft = 8;
            rightGroup.Add(composerBtn);

            var mapBtn = new Button(() =>
            {
                ViewModel.Navigator?.GoTo(ScreenId.CampaignMap);
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            mapBtn.text = "Komuta";
            mapBtn.AddToClassList("apogee-button");
            mapBtn.style.fontSize = 10;
            mapBtn.style.marginLeft = 4;
            rightGroup.Add(mapBtn);

            var mainMenuBtn = new Button(() =>
            {
                ViewModel.Navigator?.GoTo(ScreenId.MainMenu);
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            mainMenuBtn.text = "Menü";
            mainMenuBtn.AddToClassList("apogee-button");
            mainMenuBtn.style.fontSize = 10;
            mainMenuBtn.style.marginLeft = 4;
            rightGroup.Add(mainMenuBtn);

            topBar.Add(rightGroup);

            return topBar;
        }

        private Button CreateLayerButton(string text, HangarLayerMode mode, bool isActive = false)
        {
            var btn = new Button(() =>
            {
                SetLayerMode(mode);
                _audioService?.PlaySfx(AudioEvent.TabSwitch);
            });
            btn.text = text;
            btn.AddToClassList("apogee-hangar-layer-btn");
            if (isActive) btn.AddToClassList("apogee-hangar-layer-btn--active");
            return btn;
        }

        private void SetLayerMode(HangarLayerMode mode)
        {
            _canvas.LayerMode = mode;

            _layerExteriorBtn?.RemoveFromClassList("apogee-hangar-layer-btn--active");
            _layerCutawayBtn?.RemoveFromClassList("apogee-hangar-layer-btn--active");
            _layerSubsystemsBtn?.RemoveFromClassList("apogee-hangar-layer-btn--active");
            _layerSchematicBtn?.RemoveFromClassList("apogee-hangar-layer-btn--active");

            switch (mode)
            {
                case HangarLayerMode.Exterior:
                    _layerExteriorBtn?.AddToClassList("apogee-hangar-layer-btn--active");
                    break;
                case HangarLayerMode.Cutaway:
                    _layerCutawayBtn?.AddToClassList("apogee-hangar-layer-btn--active");
                    break;
                case HangarLayerMode.Subsystems:
                    _layerSubsystemsBtn?.AddToClassList("apogee-hangar-layer-btn--active");
                    break;
                case HangarLayerMode.Schematic:
                    _layerSchematicBtn?.AddToClassList("apogee-hangar-layer-btn--active");
                    break;
            }
        }

        private VisualElement BuildBottomHud()
        {
            var hud = new VisualElement();
            hud.AddToClassList("apogee-hangar-hud");

            var metricsGroup = new VisualElement();
            metricsGroup.style.flexDirection = FlexDirection.Row;
            metricsGroup.style.alignItems = Align.Center;

            // 1. Delta-V
            var dvBox = new VisualElement();
            dvBox.AddToClassList("apogee-hangar-hud-metric");

            var dvHeader = new VisualElement();
            dvHeader.style.flexDirection = FlexDirection.Row;
            dvHeader.style.alignItems = Align.Center;

            var dvTitle = new Label("MENZİL (Δv)");
            dvTitle.style.fontSize = 10;
            dvTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            dvHeader.Add(dvTitle);

            _deltaVValueLabel = new Label("0 / 0 m/s");
            _deltaVValueLabel.style.fontSize = 11;
            _deltaVValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _deltaVValueLabel.style.color = Color.white;
            _deltaVValueLabel.style.marginLeft = 8;
            dvHeader.Add(_deltaVValueLabel);

            _deltaVGhostLabel = new Label(string.Empty);
            _deltaVGhostLabel.style.fontSize = 10;
            _deltaVGhostLabel.style.marginLeft = 4;
            dvHeader.Add(_deltaVGhostLabel);
            dvBox.Add(dvHeader);

            var dvBarContainer = new VisualElement();
            dvBarContainer.AddToClassList("apogee-bar-container");
            dvBarContainer.style.width = 160;
            dvBarContainer.style.height = 8;
            dvBarContainer.style.marginTop = 4;

            _deltaVBarFill = new VisualElement();
            _deltaVBarFill.AddToClassList("apogee-bar-fill");
            _deltaVBarFill.AddToClassList("apogee-bar-fill--green");
            dvBarContainer.Add(_deltaVBarFill);

            _deltaVGhostBarFill = new VisualElement();
            _deltaVGhostBarFill.style.position = Position.Absolute;
            _deltaVGhostBarFill.style.height = Length.Percent(100);
            _deltaVGhostBarFill.style.opacity = 0.5f;
            _deltaVGhostBarFill.style.backgroundColor = new Color(0.22f, 0.74f, 0.97f);
            dvBarContainer.Add(_deltaVGhostBarFill);

            dvBox.Add(dvBarContainer);
            metricsGroup.Add(dvBox);

            // 2. Mass
            var massBox = new VisualElement();
            massBox.AddToClassList("apogee-hangar-hud-metric");
            var massTitle = new Label("KÜTLE (ISLAK)");
            massTitle.style.fontSize = 10;
            massTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            massBox.Add(massTitle);

            var massValRow = new VisualElement();
            massValRow.style.flexDirection = FlexDirection.Row;
            _massValueLabel = new Label("0 t");
            _massValueLabel.style.fontSize = 12;
            _massValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _massValueLabel.style.color = Color.white;
            massValRow.Add(_massValueLabel);
            _massGhostLabel = new Label(string.Empty);
            _massGhostLabel.style.fontSize = 10;
            _massGhostLabel.style.marginLeft = 4;
            massValRow.Add(_massGhostLabel);
            massBox.Add(massValRow);
            metricsGroup.Add(massBox);

            // 3. Cost
            var costBox = new VisualElement();
            costBox.AddToClassList("apogee-hangar-hud-metric");
            var costTitle = new Label("MALİYET");
            costTitle.style.fontSize = 10;
            costTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            costBox.Add(costTitle);

            var costValRow = new VisualElement();
            costValRow.style.flexDirection = FlexDirection.Row;
            _costValueLabel = new Label("$0 M");
            _costValueLabel.style.fontSize = 12;
            _costValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _costValueLabel.style.color = Color.white;
            costValRow.Add(_costValueLabel);
            _costGhostLabel = new Label(string.Empty);
            _costGhostLabel.style.fontSize = 10;
            _costGhostLabel.style.marginLeft = 4;
            costValRow.Add(_costGhostLabel);
            costBox.Add(costValRow);
            metricsGroup.Add(costBox);

            // 4. Reliability
            var relBox = new VisualElement();
            relBox.AddToClassList("apogee-hangar-hud-metric");
            var relTitle = new Label("GÜVENİLİRLİK");
            relTitle.style.fontSize = 10;
            relTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            relBox.Add(relTitle);

            var relValRow = new VisualElement();
            relValRow.style.flexDirection = FlexDirection.Row;
            _reliabilityValueLabel = new Label("%0");
            _reliabilityValueLabel.style.fontSize = 12;
            _reliabilityValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _reliabilityValueLabel.style.color = Color.white;
            relValRow.Add(_reliabilityValueLabel);
            _reliabilityGhostLabel = new Label(string.Empty);
            _reliabilityGhostLabel.style.fontSize = 10;
            _reliabilityGhostLabel.style.marginLeft = 4;
            relValRow.Add(_reliabilityGhostLabel);
            relBox.Add(relValRow);
            metricsGroup.Add(relBox);

            // 5. Power
            var powerBox = new VisualElement();
            powerBox.AddToClassList("apogee-hangar-hud-metric");
            var powerTitle = new Label("GÜÇ DENGESİ");
            powerTitle.style.fontSize = 10;
            powerTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            powerBox.Add(powerTitle);

            var pwrValRow = new VisualElement();
            pwrValRow.style.flexDirection = FlexDirection.Row;
            _powerMarginValueLabel = new Label("0.0 kW");
            _powerMarginValueLabel.style.fontSize = 12;
            _powerMarginValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            pwrValRow.Add(_powerMarginValueLabel);
            _powerMarginGhostLabel = new Label(string.Empty);
            _powerMarginGhostLabel.style.fontSize = 10;
            _powerMarginGhostLabel.style.marginLeft = 4;
            pwrValRow.Add(_powerMarginGhostLabel);
            powerBox.Add(pwrValRow);
            metricsGroup.Add(powerBox);

            // 6. Build Time
            var timeBox = new VisualElement();
            timeBox.AddToClassList("apogee-hangar-hud-metric");
            var timeTitle = new Label("ÜRETİM");
            timeTitle.style.fontSize = 10;
            timeTitle.style.color = new Color(0.45f, 0.60f, 0.78f);
            timeBox.Add(timeTitle);
            _buildDaysValueLabel = new Label("0 gün");
            _buildDaysValueLabel.style.fontSize = 12;
            _buildDaysValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _buildDaysValueLabel.style.color = Color.white;
            timeBox.Add(_buildDaysValueLabel);
            metricsGroup.Add(timeBox);

            // Ghost Summary Box (appears in HUD)
            _ghostSummaryBox = new VisualElement();
            _ghostSummaryBox.AddToClassList("apogee-ghost-summary-box");
            _ghostSummaryBox.style.display = DisplayStyle.None;
            _ghostSummaryBox.style.marginLeft = 8;
            _ghostSummaryBox.style.paddingLeft = 8;
            _ghostSummaryBox.style.paddingRight = 8;
            _ghostSummaryBox.style.paddingTop = 4;
            _ghostSummaryBox.style.paddingBottom = 4;

            _ghostSummaryTextLabel = new Label(string.Empty);
            _ghostSummaryTextLabel.AddToClassList("apogee-ghost-summary-text");
            _ghostSummaryTextLabel.style.fontSize = 10;
            _ghostSummaryBox.Add(_ghostSummaryTextLabel);
            metricsGroup.Add(_ghostSummaryBox);

            hud.Add(metricsGroup);

            // Right CTA: "Tasarım İncelemesi ->"
            _reviewButton = new Button(() =>
            {
                ViewModel.GoToReviewCommand.Execute();
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            });
            _reviewButton.text = "Tasarım İncelemesi ->";
            _reviewButton.AddToClassList("apogee-button");
            _reviewButton.AddToClassList("apogee-button--primary");
            _reviewButton.style.paddingTop = 10;
            _reviewButton.style.paddingBottom = 10;
            _reviewButton.style.paddingLeft = 18;
            _reviewButton.style.paddingRight = 18;
            _reviewButton.style.fontSize = 13;
            _reviewButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            hud.Add(_reviewButton);

            return hud;
        }

        private VisualElement CreateRailSplitter(HangarSectionRail rail)
        {
            var splitter = new VisualElement();
            splitter.AddToClassList("apogee-hangar-rail-splitter");

            var grip = new VisualElement();
            grip.AddToClassList("apogee-splitter-grip--vertical");
            grip.style.width = 2;
            grip.style.height = 48;
            splitter.Add(grip);

            bool isDragging = false;
            float startX = 0f;
            float startWidth = 0f;

            splitter.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                isDragging = true;
                startX = evt.position.x;
                startWidth = rail.resolvedStyle.width > 0 ? rail.resolvedStyle.width : (rail.style.width.value.value > 0 ? rail.style.width.value.value : 160f);
                splitter.CapturePointer(evt.pointerId);
                splitter.AddToClassList("apogee-splitter-handle--dragging");
                evt.StopPropagation();
            });

            splitter.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isDragging) return;
                float deltaX = evt.position.x - startX;
                float newWidth = Mathf.Clamp(startWidth + deltaX, 130f, 450f);
                rail.style.width = newWidth;

                if (!_isEngineeringPanelMoved && _engineeringPanel != null)
                {
                    _engineeringPanel.style.left = newWidth + 8f;
                }
                evt.StopPropagation();
            });

            void EndDrag(int pointerId)
            {
                if (isDragging)
                {
                    isDragging = false;
                    splitter.ReleasePointer(pointerId);
                    splitter.RemoveFromClassList("apogee-splitter-handle--dragging");
                }
            }

            splitter.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isDragging)
                {
                    EndDrag(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            splitter.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isDragging)
                {
                    EndDrag(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            // Double click to reset to default rail width
            splitter.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    rail.style.width = 160f;
                    if (!_isEngineeringPanelMoved && _engineeringPanel != null)
                    {
                        _engineeringPanel.style.left = 168f;
                    }
                    evt.StopPropagation();
                }
            });

            return splitter;
        }

        private VisualElement BuildEngineeringPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("apogee-hangar-engineering-panel");
            panel.style.display = DisplayStyle.None;

            var header = new VisualElement();
            header.AddToClassList("apogee-hangar-engineering-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.28f, 0.40f);
            header.style.paddingBottom = 6;

            var title = new Label("MÜHENDİS GÖRÜNÜMÜ");
            title.AddToClassList("apogee-panel-title");
            title.style.fontSize = 12;
            title.style.color = new Color(0.20f, 0.75f, 0.95f);
            header.Add(title);

            var closeBtn = new Button(ToggleEngineeringView);
            closeBtn.text = "X";
            closeBtn.AddToClassList("apogee-button");
            closeBtn.style.width = 22;
            closeBtn.style.height = 22;
            closeBtn.style.paddingLeft = 0;
            closeBtn.style.paddingRight = 0;
            closeBtn.style.paddingTop = 0;
            closeBtn.style.paddingBottom = 0;
            header.Add(closeBtn);
            panel.Add(header);

            _engineeringSlotsScroll = new ScrollView(ScrollViewMode.Vertical);
            _engineeringSlotsScroll.style.flexGrow = 1;

            foreach (var slot in ViewModel.Slots)
            {
                var s = slot;
                var item = new VisualElement();
                item.AddToClassList("apogee-slot-item");
                item.style.marginBottom = 4;
                item.style.paddingTop = 4;
                item.style.paddingBottom = 4;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.alignItems = Align.Center;

                var l = new Label(s.DisplayName);
                l.style.fontSize = 10;
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.flexShrink = 0;
                l.style.marginRight = 8;
                row.Add(l);

                var eq = new Label();
                eq.style.fontSize = 10;
                eq.style.color = new Color(0.60f, 0.75f, 0.90f);
                eq.style.overflow = Overflow.Hidden;
                eq.style.textOverflow = TextOverflow.Ellipsis;
                eq.style.whiteSpace = WhiteSpace.NoWrap;
                eq.style.flexGrow = 1;
                eq.style.unityTextAlign = TextAnchor.MiddleRight;
                s.EquippedName.Subscribe(val => eq.text = val);
                row.Add(eq);
                item.Add(row);

                item.RegisterCallback<ClickEvent>(evt =>
                {
                    s.SelectSlotCommand.Execute();
                    _audioService?.PlaySfx(AudioEvent.ButtonClick);
                });

                _engineeringSlotsScroll.Add(item);
            }

            panel.Add(_engineeringSlotsScroll);

            SetupEngineeringPanelResizeAndDrag(panel, header, closeBtn);

            return panel;
        }

        private void SetupEngineeringPanelResizeAndDrag(VisualElement panel, VisualElement header, Button closeBtn)
        {
            // ── Header Dragging (Move window) ─────────────────────────────────
            bool isDraggingHeader = false;
            float dragStartX = 0f;
            float dragStartY = 0f;
            float startLeft = 0f;
            float startTop = 0f;

            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || evt.target == closeBtn || (closeBtn != null && closeBtn.Contains(evt.target as VisualElement))) return;
                isDraggingHeader = true;
                dragStartX = evt.position.x;
                dragStartY = evt.position.y;
                startLeft = panel.resolvedStyle.left > 0 ? panel.resolvedStyle.left : (panel.style.left.value.value > 0 ? panel.style.left.value.value : 168f);
                startTop = panel.resolvedStyle.top > 0 ? panel.resolvedStyle.top : (panel.style.top.value.value > 0 ? panel.style.top.value.value : 50f);
                header.CapturePointer(evt.pointerId);
                _isEngineeringPanelMoved = true;
                evt.StopPropagation();
            });

            header.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isDraggingHeader) return;
                float deltaX = evt.position.x - dragStartX;
                float deltaY = evt.position.y - dragStartY;
                panel.style.left = Mathf.Max(0f, startLeft + deltaX);
                panel.style.top = Mathf.Max(0f, startTop + deltaY);
                evt.StopPropagation();
            });

            void EndHeaderDrag(int pointerId)
            {
                if (isDraggingHeader)
                {
                    isDraggingHeader = false;
                    header.ReleasePointer(pointerId);
                }
            }

            header.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isDraggingHeader)
                {
                    EndHeaderDrag(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            header.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isDraggingHeader)
                {
                    EndHeaderDrag(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            // Double click header to reset to default position & dock next to rail
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2 && evt.target != closeBtn && (closeBtn == null || !closeBtn.Contains(evt.target as VisualElement)))
                {
                    _isEngineeringPanelMoved = false;
                    float railWidth = _rail != null && _rail.resolvedStyle.width > 0 ? _rail.resolvedStyle.width : 160f;
                    panel.style.left = railWidth + 8f;
                    panel.style.top = 50f;
                    panel.style.width = 360f;
                    panel.style.height = StyleKeyword.Null;
                    panel.style.bottom = 80f;
                    evt.StopPropagation();
                }
            });

            // ── Right Resize Handle (Width) ───────────────────────────────────
            var rightHandle = new VisualElement();
            rightHandle.AddToClassList("apogee-panel-resize-handle--right");
            panel.Add(rightHandle);

            bool isResizingRight = false;
            float resizeStartX = 0f;
            float startWidth = 0f;

            rightHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                isResizingRight = true;
                resizeStartX = evt.position.x;
                startWidth = panel.resolvedStyle.width > 0 ? panel.resolvedStyle.width : (panel.style.width.value.value > 0 ? panel.style.width.value.value : 360f);
                rightHandle.CapturePointer(evt.pointerId);
                rightHandle.AddToClassList("dragging");
                evt.StopPropagation();
            });

            rightHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isResizingRight) return;
                float deltaX = evt.position.x - resizeStartX;
                panel.style.width = Mathf.Clamp(startWidth + deltaX, 260f, 900f);
                evt.StopPropagation();
            });

            void EndRightResize(int pointerId)
            {
                if (isResizingRight)
                {
                    isResizingRight = false;
                    rightHandle.ReleasePointer(pointerId);
                    rightHandle.RemoveFromClassList("dragging");
                }
            }

            rightHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isResizingRight)
                {
                    EndRightResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            rightHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isResizingRight)
                {
                    EndRightResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            // ── Bottom Resize Handle (Height) ─────────────────────────────────
            var bottomHandle = new VisualElement();
            bottomHandle.AddToClassList("apogee-panel-resize-handle--bottom");
            panel.Add(bottomHandle);

            bool isResizingBottom = false;
            float resizeStartY = 0f;
            float startHeight = 0f;

            bottomHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                isResizingBottom = true;
                resizeStartY = evt.position.y;
                startHeight = panel.resolvedStyle.height > 0 ? panel.resolvedStyle.height : 450f;
                panel.style.bottom = StyleKeyword.Auto;
                panel.style.height = startHeight;
                bottomHandle.CapturePointer(evt.pointerId);
                bottomHandle.AddToClassList("dragging");
                evt.StopPropagation();
            });

            bottomHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isResizingBottom) return;
                float deltaY = evt.position.y - resizeStartY;
                panel.style.height = Mathf.Clamp(startHeight + deltaY, 180f, 1000f);
                evt.StopPropagation();
            });

            void EndBottomResize(int pointerId)
            {
                if (isResizingBottom)
                {
                    isResizingBottom = false;
                    bottomHandle.ReleasePointer(pointerId);
                    bottomHandle.RemoveFromClassList("dragging");
                }
            }

            bottomHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isResizingBottom)
                {
                    EndBottomResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            bottomHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isResizingBottom)
                {
                    EndBottomResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            // ── Corner Resize Handle (Width & Height) ─────────────────────────
            var cornerHandle = new VisualElement();
            cornerHandle.AddToClassList("apogee-panel-resize-handle--corner");
            var cornerGrip = new VisualElement();
            cornerGrip.AddToClassList("apogee-panel-corner-grip");
            cornerHandle.Add(cornerGrip);
            panel.Add(cornerHandle);

            bool isResizingCorner = false;
            float cornerStartX = 0f;
            float cornerStartY = 0f;
            float cornerStartW = 0f;
            float cornerStartH = 0f;

            cornerHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                isResizingCorner = true;
                cornerStartX = evt.position.x;
                cornerStartY = evt.position.y;
                cornerStartW = panel.resolvedStyle.width > 0 ? panel.resolvedStyle.width : (panel.style.width.value.value > 0 ? panel.style.width.value.value : 360f);
                cornerStartH = panel.resolvedStyle.height > 0 ? panel.resolvedStyle.height : 450f;
                panel.style.bottom = StyleKeyword.Auto;
                panel.style.height = cornerStartH;
                cornerHandle.CapturePointer(evt.pointerId);
                cornerHandle.AddToClassList("dragging");
                evt.StopPropagation();
            });

            cornerHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isResizingCorner) return;
                float deltaX = evt.position.x - cornerStartX;
                float deltaY = evt.position.y - cornerStartY;
                panel.style.width = Mathf.Clamp(cornerStartW + deltaX, 260f, 900f);
                panel.style.height = Mathf.Clamp(cornerStartH + deltaY, 180f, 1000f);
                evt.StopPropagation();
            });

            void EndCornerResize(int pointerId)
            {
                if (isResizingCorner)
                {
                    isResizingCorner = false;
                    cornerHandle.ReleasePointer(pointerId);
                    cornerHandle.RemoveFromClassList("dragging");
                }
            }

            cornerHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isResizingCorner)
                {
                    EndCornerResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            cornerHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isResizingCorner)
                {
                    EndCornerResize(evt.pointerId);
                    evt.StopPropagation();
                }
            });
        }

        private void ToggleEngineeringView()
        {
            _isEngineeringViewOpen = !_isEngineeringViewOpen;
            _engineeringPanel.style.display = _isEngineeringViewOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isEngineeringViewOpen)
            {
                if (!_isEngineeringPanelMoved)
                {
                    float railWidth = _rail != null && _rail.resolvedStyle.width > 0 ? _rail.resolvedStyle.width : (_rail != null && _rail.style.width.value.value > 0 ? _rail.style.width.value.value : 160f);
                    _engineeringPanel.style.left = railWidth + 8f;
                }
                _toggleEngineeringBtn?.AddToClassList("apogee-button--accent-amber");
                _audioService?.PlaySfx(AudioEvent.ModalOpen);
            }
            else
            {
                _toggleEngineeringBtn?.RemoveFromClassList("apogee-button--accent-amber");
                _audioService?.PlaySfx(AudioEvent.ModalClose);
            }
        }

        // ── Interaction Event Callbacks ─────────────────────────────────────────────

        private void OnSlotSelectedFromCanvas(VehicleSlotId? slotId)
        {
            if (slotId.HasValue)
            {
                ViewModel.SelectSlot(slotId.Value);
                _audioService?.PlaySfx(AudioEvent.ButtonClick);
            }
            else
            {
                _drawer.CloseDrawer();
            }
        }

        private void OnSlotSelectedFromRail(VehicleSlotId slotId)
        {
            _canvas.SetSelectedSlot(slotId);
            _audioService?.PlaySfx(AudioEvent.ButtonClick);
        }

        private void OnSectionHoveredFromCanvas(RocketAnatomySection section)
        {
            if (section != null)
            {
                _audioService?.PlaySfx(AudioEvent.ButtonHover);
            }
        }

        private void OnDrawerOptionHovered(VehicleSlotId? slotId, DesignComponent component)
        {
            _canvas.SetGhostPreview(slotId, component);
            _audioService?.PlaySfx(AudioEvent.ButtonHover);
        }

        private void OnDrawerOptionUnhovered()
        {
            _canvas.SetGhostPreview(null, null);
        }

        private void OnDrawerClosed()
        {
            _canvas.SetSelectedSlot(null);
            _canvas.SetGhostPreview(null, null);
            _audioService?.PlaySfx(AudioEvent.ModalClose);
        }

        // ── Keyboard Shortcuts ──────────────────────────────────────────────────────

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Esc: Deselect or Close Drawer
            if (evt.keyCode == KeyCode.Escape)
            {
                _drawer.CloseDrawer();
                _canvas.SetSelectedSlot(null);
                _canvas.ResetView();
                if (_isEngineeringViewOpen) ToggleEngineeringView();
                evt.StopPropagation();
            }
            // Tab: Cycle Layer Mode
            else if (evt.keyCode == KeyCode.Tab)
            {
                int nextMode = ((int)_canvas.LayerMode + 1) % 4;
                SetLayerMode((HangarLayerMode)nextMode);
                _audioService?.PlaySfx(AudioEvent.TabSwitch);
                evt.StopPropagation();
            }
            // 'E': Toggle Engineering View
            else if (evt.keyCode == KeyCode.E)
            {
                ToggleEngineeringView();
                evt.StopPropagation();
            }
            // Ctrl+Z: Undo
            else if (evt.keyCode == KeyCode.Z && evt.ctrlKey)
            {
                if (ViewModel.UndoCommand.CanExecute())
                {
                    ViewModel.UndoCommand.Execute();
                    _audioService?.PlaySfx(AudioEvent.ButtonClick);
                }
                evt.StopPropagation();
            }
            // Ctrl+Y: Redo
            else if (evt.keyCode == KeyCode.Y && evt.ctrlKey)
            {
                if (ViewModel.RedoCommand.CanExecute())
                {
                    ViewModel.RedoCommand.Execute();
                    _audioService?.PlaySfx(AudioEvent.ButtonClick);
                }
                evt.StopPropagation();
            }
            // 1-9: Select Slot Shortcut
            else if (evt.keyCode >= KeyCode.Alpha1 && evt.keyCode <= KeyCode.Alpha9)
            {
                int index = evt.keyCode - KeyCode.Alpha1;
                if (index < ViewModel.Slots.Count)
                {
                    var slot = ViewModel.Slots[index];
                    ViewModel.SelectSlot(slot.SlotId);
                    _canvas.SetSelectedSlot(slot.SlotId);
                    _audioService?.PlaySfx(AudioEvent.ButtonClick);
                    evt.StopPropagation();
                }
            }
        }

        // ── Bindings ────────────────────────────────────────────────────────────────

        protected override void OnInitialized()
        {
            ViewModel.BlueprintName.Subscribe(val => _blueprintNameLabel.text = val);
            ViewModel.TargetDestination.Subscribe(val => _targetLabel.text = $"Hedef: {val}");

            // Undo / Redo states
            ViewModel.CanUndo.Subscribe(canUndo => _undoButton.SetEnabled(canUndo));
            ViewModel.CanRedo.Subscribe(canRedo => _redoButton.SetEnabled(canRedo));

            // Sync Anatomy to Canvas and Rail
            ViewModel.Anatomy.Subscribe(anatomy =>
            {
                if (anatomy != null)
                {
                    _canvas.SetAnatomy(anatomy);
                    _rail.Refresh();
                }
            });

            // Sync Selected Slot to Canvas
            ViewModel.SelectedSlot.Subscribe(slot =>
            {
                _canvas.SetSelectedSlot(slot?.SlotId);
            });

            // Telemetry Subscriptions
            ViewModel.TotalDeltaV.Subscribe(val =>
            {
                _deltaVValueLabel.text = string.Format(CultureInfo.InvariantCulture, "{0:N0} / {1:N0} m/s", val, ViewModel.TargetDeltaV.Value);
                float ratio = (float)(val / Math.Max(1.0, ViewModel.TargetDeltaV.Value));
                _deltaVBarFill.style.width = Length.Percent(Math.Min(100f, ratio * 100f));
            });

            ViewModel.TotalMassTons.Subscribe(val =>
                _massValueLabel.text = string.Format(CultureInfo.InvariantCulture, "{0:F1} t", val));

            ViewModel.TotalCostMillions.Subscribe(val =>
                _costValueLabel.text = string.Format(CultureInfo.InvariantCulture, "${0:F1} M", val));

            ViewModel.ReliabilityPercent.Subscribe(val =>
                _reliabilityValueLabel.text = string.Format(CultureInfo.InvariantCulture, "%{0:F1}", val));

            ViewModel.PowerMarginKw.Subscribe(val =>
            {
                _powerMarginValueLabel.text = val >= 0
                    ? string.Format(CultureInfo.InvariantCulture, "+{0:F1} kW", val)
                    : string.Format(CultureInfo.InvariantCulture, "{0:F1} kW", val);
                _powerMarginValueLabel.style.color = val >= 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });

            ViewModel.MaxBuildDays.Subscribe(val =>
                _buildDaysValueLabel.text = string.Format(CultureInfo.InvariantCulture, "{0} gün", val));

            // Hayalet Delta Subscriptions (GDD §7.3)
            ViewModel.GhostDelta.IsActive.Subscribe(active =>
            {
                _ghostSummaryBox.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                _deltaVGhostBarFill.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

                if (!active)
                {
                    _deltaVGhostLabel.text = string.Empty;
                    _massGhostLabel.text = string.Empty;
                    _costGhostLabel.text = string.Empty;
                    _reliabilityGhostLabel.text = string.Empty;
                    _powerMarginGhostLabel.text = string.Empty;
                }
            });

            ViewModel.GhostDelta.SummaryText.Subscribe(summary =>
            {
                _ghostSummaryTextLabel.text = summary;
            });

            ViewModel.GhostDelta.DeltaDeltaV.Subscribe(diff =>
            {
                if (!ViewModel.GhostDelta.IsActive.Value || Math.Abs(diff) < 0.1)
                {
                    _deltaVGhostLabel.text = string.Empty;
                    return;
                }

                _deltaVGhostLabel.text = diff > 0
                    ? string.Format(CultureInfo.InvariantCulture, "(+{0:N0})", diff)
                    : string.Format(CultureInfo.InvariantCulture, "({0:N0})", diff);
                _deltaVGhostLabel.style.color = diff > 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);

                double previewTotal = ViewModel.TotalDeltaV.Value + diff;
                float ratio = (float)(previewTotal / Math.Max(1.0, ViewModel.TargetDeltaV.Value));
                _deltaVGhostBarFill.style.width = Length.Percent(Math.Min(100f, ratio * 100f));
            });

            ViewModel.GhostDelta.DeltaMassTons.Subscribe(diff =>
            {
                if (!ViewModel.GhostDelta.IsActive.Value || Math.Abs(diff) < 0.01)
                {
                    _massGhostLabel.text = string.Empty;
                    return;
                }

                _massGhostLabel.text = diff > 0
                    ? string.Format(CultureInfo.InvariantCulture, "(+{0:F2}t)", diff)
                    : string.Format(CultureInfo.InvariantCulture, "({0:F2}t)", diff);
                _massGhostLabel.style.color = diff < 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });

            ViewModel.GhostDelta.DeltaCostMillions.Subscribe(diff =>
            {
                if (!ViewModel.GhostDelta.IsActive.Value || Math.Abs(diff) < 0.01)
                {
                    _costGhostLabel.text = string.Empty;
                    return;
                }

                _costGhostLabel.text = diff > 0
                    ? string.Format(CultureInfo.InvariantCulture, "(+${0:F1}M)", diff)
                    : string.Format(CultureInfo.InvariantCulture, "(-${0:F1}M)", Math.Abs(diff));
                _costGhostLabel.style.color = diff < 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.98f, 0.75f, 0.18f);
            });

            ViewModel.GhostDelta.DeltaReliabilityPercent.Subscribe(diff =>
            {
                if (!ViewModel.GhostDelta.IsActive.Value || Math.Abs(diff) < 0.05)
                {
                    _reliabilityGhostLabel.text = string.Empty;
                    return;
                }

                _reliabilityGhostLabel.text = diff > 0
                    ? string.Format(CultureInfo.InvariantCulture, "(+%{0:F1})", diff)
                    : string.Format(CultureInfo.InvariantCulture, "(-%{0:F1})", Math.Abs(diff));
                _reliabilityGhostLabel.style.color = diff > 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });

            ViewModel.GhostDelta.DeltaPowerMarginKw.Subscribe(diff =>
            {
                if (!ViewModel.GhostDelta.IsActive.Value || Math.Abs(diff) < 0.01)
                {
                    _powerMarginGhostLabel.text = string.Empty;
                    return;
                }

                _powerMarginGhostLabel.text = diff > 0
                    ? string.Format(CultureInfo.InvariantCulture, "(+{0:F1}kW)", diff)
                    : string.Format(CultureInfo.InvariantCulture, "({0:F1}kW)", diff);
                _powerMarginGhostLabel.style.color = diff > 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });
        }
    }
}
