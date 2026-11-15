using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Strategy;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Media;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Solar System Orrery Map (ScreenId.SolarMap = 4).
    /// Complies with GDD §6.2, §14.1 (#4 Güneş Sistemi Haritası), and §21 M9-06.
    /// Visual identity: "Planetaryum / Pirinç Astrolap" — a dark night-sky plate with
    /// brass astrolabe rings and degree ticks behind dynamic planet markers, and a
    /// planet catalog fiche (brass-framed museum plaque) carrying the NASA destination
    /// photo with its Turkish credit. Visualizes dynamic planetary orbits, real-time
    /// transfer window states, interplanetary flight telemetry, and in-flight
    /// spacecraft positions.
    /// </summary>
    public sealed class SolarMapScreenController : ScreenControllerBase<SolarMapViewModel>
    {
        public override ScreenId ScreenId => ScreenId.SolarMap;

        // Photo texture cache (ADR-054/063/066/068 pattern — Resources.Load + dictionary).
        private static readonly Dictionary<string, Texture2D> PhotoCache = new();

        // ── HUD Elements ──────────────────────────────────────────────────────
        private Label _agencyLabel;
        private Label _budgetLabel;
        private Label _reputationLabel;
        private Label _researchLabel;
        private Label _dateLabel;
        private readonly List<Button> _speedButtons = new();

        // ── Solar Orrery Elements ─────────────────────────────────────────────
        private VisualElement _orreryCanvas;
        private readonly Dictionary<string, VisualElement> _planetMarkers = new();
        private readonly List<VisualElement> _missionPins = new();

        // ── Left Collapsible Planet Selector Panel (gezegen fotoğraflı seçim) ──
        private VisualElement _planetPanel;
        private VisualElement _planetPanelBody;
        private Label _planetPanelToggleLabel;
        private readonly List<VisualElement> _planetPanelRows = new();
        private bool _planetPanelCollapsed;
        private const float PlanetPanelExpandedWidth = 222f;
        private const float PlanetPanelCollapsedWidth = 46f;

        // ── Video mode (geçici güneş sistemi videosu, fallback-protected) ──────
        // Astrolab plakası geçici olarak gunes_sistemi.mp4 videosuyla değiştirilir.
        // Sol gezegen paneli ve sağ fiche çalışmaya devam eder; FlightVideoMode
        // kapalıyken veya video arızasında eski orrery haritası geri gelir.
        private FlightVideoStage _videoStage;
        private bool _videoModeActive;

        // ── Planet Catalog Fiche Elements ─────────────────────────────────────
        private Label _targetNameLabel;
        private Label _targetTypeLabel;
        private VisualElement _fichePhotoSlot;
        private Label _fichePhotoCaption;
        private Label _fichePhotoFallback;
        private VisualElement _windowCard;
        private Label _windowBadge;
        private Label _windowDesc;
        private Label _deltaVLabel;
        private Label _transitTimeLabel;
        private Label _commDelayLabel;
        private VisualElement _hazardsContainer;
        private Button _designMissionBtn;

        // ── Active Missions Section ───────────────────────────────────────────
        private VisualElement _missionsContainer;

        // ── Footer ────────────────────────────────────────────────────────────
        private ScreenNavBar _navBar;
        private Button _mainMenuBtn;

        public SolarMapScreenController(SolarMapViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-solar-map");
            root.AddToClassList("theme-orrery"); // Planetaryum / brass astrolabe identity
            root.style.flexDirection = FlexDirection.Column;

            // ── TOP: OBSERVATORY HUD ──────────────────────────────────────────
            var hud = new VisualElement();
            hud.AddToClassList("orrery-header");
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.justifyContent = Justify.SpaceBetween;
            hud.style.alignItems = Align.Center;
            hud.style.paddingTop = 8;
            hud.style.paddingBottom = 8;
            hud.style.paddingLeft = 16;
            hud.style.paddingRight = 16;

            // Left: Agency Title
            _agencyLabel = new Label("Birleşik Yörünge Ajansı · Planetaryum");
            _agencyLabel.AddToClassList("apogee-panel-title");
            hud.Add(_agencyLabel);

            // Center: Resources Pill
            var resPill = new VisualElement();
            resPill.AddToClassList("orrery-hud-pill");
            resPill.style.flexDirection = FlexDirection.Row;
            resPill.style.alignItems = Align.Center;
            resPill.style.paddingTop = 4;
            resPill.style.paddingBottom = 4;
            resPill.style.paddingLeft = 14;
            resPill.style.paddingRight = 14;

            // Budget: banknote symbol — same icon as every other screen's budget readout.
            var budgetIcon = IconElement.Create(IconType.Budget, 13f);
            budgetIcon.style.color = new Color(0.43f, 0.78f, 0.55f);
            budgetIcon.style.marginRight = 6;
            resPill.Add(budgetIcon);
            _budgetLabel = new Label("0.0 M$");
            _budgetLabel.style.color = new Color(0.43f, 0.78f, 0.55f);
            _budgetLabel.style.fontSize = 12;
            _budgetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _budgetLabel.style.marginRight = 14;
            resPill.Add(_budgetLabel);

            // Reputation: star symbol.
            var repIcon = IconElement.Create(IconType.Reputation, 13f);
            repIcon.style.color = new Color(0.91f, 0.81f, 0.61f);
            repIcon.style.marginRight = 6;
            resPill.Add(repIcon);
            _reputationLabel = new Label("0.0");
            _reputationLabel.style.color = new Color(0.91f, 0.81f, 0.61f);
            _reputationLabel.style.fontSize = 12;
            _reputationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _reputationLabel.style.marginRight = 14;
            resPill.Add(_reputationLabel);

            // Research points: flask symbol.
            var rpIcon = IconElement.Create(IconType.ResearchPoints, 13f);
            rpIcon.style.color = new Color(0.62f, 0.79f, 0.88f);
            rpIcon.style.marginRight = 6;
            resPill.Add(rpIcon);
            _researchLabel = new Label("0.0 AP");
            _researchLabel.style.color = new Color(0.62f, 0.79f, 0.88f);
            _researchLabel.style.fontSize = 12;
            _researchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            resPill.Add(_researchLabel);

            resPill.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                "Kampanya Kaynakları",
                "Bütçe (M$): ajans kasası — harcamalar buradan ödenir. İtibar: saygınlık (0–100), yıllık bütçe tahsisatını belirler. AP: Araştırma Puanı — teknoloji ağacındaki düğümleri ilerletir.")));

            hud.Add(resPill);

            // Right: Time Controls
            var timeBox = new VisualElement();
            timeBox.style.flexDirection = FlexDirection.Row;
            timeBox.style.alignItems = Align.Center;

            _dateLabel = new Label("2031-01-01 · Gün 0");
            _dateLabel.style.fontSize = 12;
            _dateLabel.style.color = new Color(0.74f, 0.76f, 0.81f);
            _dateLabel.style.marginRight = 12;
            timeBox.Add(_dateLabel);

            var speedGroup = new VisualElement();
            speedGroup.style.flexDirection = FlexDirection.Row;

            _speedButtons.Add(CreateSpeedButton("II", () => ViewModel.PauseCommand.Execute()));
            _speedButtons.Add(CreateSpeedButton("1x", () => ViewModel.Speed1xCommand.Execute()));
            _speedButtons.Add(CreateSpeedButton("2x", () => ViewModel.Speed2xCommand.Execute()));
            _speedButtons.Add(CreateSpeedButton("3x", () => ViewModel.Speed3xCommand.Execute()));
            _speedButtons.Add(CreateSpeedButton("+1G", () => ViewModel.AdvanceDayCommand.Execute()));

            foreach (var btn in _speedButtons)
            {
                speedGroup.Add(btn);
            }
            timeBox.Add(speedGroup);

            hud.Add(timeBox);
            root.Add(hud);

            // ── MAIN VIEW: ASTROLABE PLATE (LEFT) + CATALOG FICHE (RIGHT) ─────
            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("screen-body");
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;
            mainContainer.style.flexShrink = 1;
            mainContainer.style.flexBasis = 0;
            mainContainer.style.minHeight = 0;

            // ── LEFT: COLLAPSIBLE PLANET SELECTOR PANEL (gezegen fotoğraflı) ──
            BuildPlanetPanel(mainContainer);

            // Astrolabe plate canvas
            _orreryCanvas = new VisualElement();
            _orreryCanvas.AddToClassList("orrery-canvas");
            _orreryCanvas.style.flexGrow = 1;
            _orreryCanvas.style.position = Position.Relative;
            _orreryCanvas.style.overflow = Overflow.Hidden;

            // Brass astrolabe backdrop: rings, degree ticks, star field.
            _orreryCanvas.Add(new OrreryBackdrop());

            // Sun at the plate center
            var sun = new VisualElement();
            sun.AddToClassList("orrery-sun");
            sun.style.position = Position.Absolute;
            sun.style.left = Length.Percent(50);
            sun.style.top = Length.Percent(50);
            sun.style.width = 36;
            sun.style.height = 36;
            sun.style.marginLeft = -18;
            sun.style.marginTop = -18;
            _orreryCanvas.Add(sun);

            var sunLabel = new Label("GÜNEŞ");
            sunLabel.AddToClassList("orrery-sun-label");
            sunLabel.style.position = Position.Absolute;
            sunLabel.style.left = Length.Percent(50);
            sunLabel.style.top = Length.Percent(50);
            sunLabel.style.marginLeft = -16;
            sunLabel.style.marginTop = 20;
            sunLabel.style.fontSize = 9;
            _orreryCanvas.Add(sunLabel);

            // ── VIDEO STAGE: plakanın üzerine biner; FlightVideoMode aktifken orrery
            //    haritası yerine geçici güneş sistemi videosu oynatılır. Video mode
            //    kapalıysa hiç açılmaz — eski görünüm aynen korunur (fallback).
            _videoStage = new FlightVideoStage();
            _videoStage.SetVisible(false);
            _videoStage.PlaybackFailed += OnVideoStageFailed;
            _orreryCanvas.Add(_videoStage);

            mainContainer.Add(_orreryCanvas);

            // ── RIGHT SIDE: PLANET CATALOG FICHE (müze plaketi) ───────────────
            var rightPanel = new VisualElement();
            rightPanel.style.width = 340;
            rightPanel.style.backgroundColor = new Color(0.055f, 0.075f, 0.125f, 0.985f);
            rightPanel.style.borderLeftWidth = 1;
            rightPanel.style.borderLeftColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.45f);
            rightPanel.style.paddingLeft = 14;
            rightPanel.style.paddingRight = 14;
            rightPanel.style.paddingTop = 12;
            rightPanel.style.paddingBottom = 12;

            var panelScroll = new ScrollView();
            panelScroll.style.flexGrow = 1;

            var fiche = new VisualElement();
            fiche.AddToClassList("orrery-fiche");
            fiche.style.paddingLeft = 12;
            fiche.style.paddingRight = 12;
            fiche.style.paddingTop = 10;
            fiche.style.paddingBottom = 10;
            fiche.style.marginBottom = 10;

            // Fiche header: catalog number + name
            var ficheHeader = new VisualElement();
            ficheHeader.AddToClassList("orrery-fiche-header");
            ficheHeader.style.marginBottom = 8;
            ficheHeader.style.paddingBottom = 6;

            var ficheCatNo = new Label("KATALOK NO: ORR-04");
            ficheCatNo.AddToClassList("orrery-fiche-sub");
            ficheCatNo.style.fontSize = 9;
            ficheCatNo.style.marginBottom = 2;
            ficheHeader.Add(ficheCatNo);

            _targetNameLabel = new Label("Mars");
            _targetNameLabel.AddToClassList("orrery-fiche-title");
            _targetNameLabel.style.fontSize = 20;
            ficheHeader.Add(_targetNameLabel);

            _targetTypeLabel = new Label("Karasal Gezegen · 1.52 AU");
            _targetTypeLabel.AddToClassList("orrery-fiche-sub");
            _targetTypeLabel.style.fontSize = 11;
            ficheHeader.Add(_targetTypeLabel);

            fiche.Add(ficheHeader);

            // ── Photo slot (NASA destination photo from destinations.json) ──
            _fichePhotoSlot = new VisualElement();
            _fichePhotoSlot.AddToClassList("orrery-fiche-photo");
            _fichePhotoSlot.style.height = 118;
            _fichePhotoSlot.style.marginBottom = 8;
            _fichePhotoSlot.style.overflow = Overflow.Hidden;
            fiche.Add(_fichePhotoSlot);

            // Transfer Window Card
            _windowCard = new VisualElement();
            _windowCard.AddToClassList("orrery-window-card");
            _windowCard.style.paddingLeft = 10;
            _windowCard.style.paddingRight = 10;
            _windowCard.style.paddingTop = 8;
            _windowCard.style.paddingBottom = 8;
            _windowCard.style.marginBottom = 10;

            var winTitleRow = new VisualElement();
            winTitleRow.style.flexDirection = FlexDirection.Row;
            winTitleRow.style.justifyContent = Justify.SpaceBetween;
            winTitleRow.style.alignItems = Align.Center;

            var winTitle = new Label("FIRLATMA PENCERESİ");
            winTitle.AddToClassList("orrery-section-label");
            winTitle.style.fontSize = 10;
            winTitleRow.Add(winTitle);

            _windowBadge = new Label("PENCERE AÇIK!");
            _windowBadge.AddToClassList("orrery-window-badge");
            _windowBadge.style.fontSize = 10;
            _windowBadge.style.paddingTop = 2;
            _windowBadge.style.paddingBottom = 2;
            _windowBadge.style.paddingLeft = 6;
            _windowBadge.style.paddingRight = 6;
            winTitleRow.Add(_windowBadge);
            _windowCard.Add(winTitleRow);

            _windowDesc = new Label("Hohmann transfer yörüngesi için en uygun fırlatma tarihi.");
            _windowDesc.style.fontSize = 11;
            _windowDesc.style.color = new Color(0.85f, 0.82f, 0.76f);
            _windowDesc.style.marginTop = 6;
            _windowCard.Add(_windowDesc);

            fiche.Add(_windowCard);

            // Telemetry plaque
            var statsBox = new VisualElement();
            statsBox.AddToClassList("orrery-plaque");
            statsBox.style.paddingLeft = 10;
            statsBox.style.paddingRight = 10;
            statsBox.style.paddingTop = 8;
            statsBox.style.paddingBottom = 8;
            statsBox.style.marginBottom = 10;

            _deltaVLabel = CreateStatRow(statsBox, "Gereken Toplam Δv", "18,900 m/s");
            _transitTimeLabel = CreateStatRow(statsBox, "Seyir Süresi", "180–270 gün");
            _commDelayLabel = CreateStatRow(statsBox, "Haberleşme Gecikmesi", "3.0–22.0 dk");

            fiche.Add(statsBox);

            // Environmental Hazards Section
            var hazTitle = new Label("ÇEVRESEL TEHLİKELER");
            hazTitle.AddToClassList("orrery-section-label");
            hazTitle.style.fontSize = 10;
            hazTitle.style.marginBottom = 6;
            fiche.Add(hazTitle);

            _hazardsContainer = new VisualElement();
            _hazardsContainer.style.flexDirection = FlexDirection.Row;
            _hazardsContainer.style.flexWrap = Wrap.Wrap;
            _hazardsContainer.style.marginBottom = 8;
            fiche.Add(_hazardsContainer);

            panelScroll.Add(fiche);

            // Active Interplanetary Missions Section (on the plate, below the fiche)
            var misTitle = new Label("AKTİF SEYİRDEKİ ARAÇLAR");
            misTitle.AddToClassList("orrery-section-label");
            misTitle.style.fontSize = 10;
            misTitle.style.marginBottom = 6;
            panelScroll.Add(misTitle);

            _missionsContainer = new VisualElement();
            _missionsContainer.style.marginBottom = 12;
            panelScroll.Add(_missionsContainer);

            rightPanel.Add(panelScroll);

            // Launch Mission Button
            _designMissionBtn = new Button(() => ViewModel.LaunchToSelectedDestinationCommand.Execute());
            _designMissionBtn.text = "BU HEDEFE GÖREV TASARLA";
            _designMissionBtn.AddToClassList("apogee-button");
            _designMissionBtn.AddToClassList("apogee-button--primary");
            _designMissionBtn.style.height = 40;
            _designMissionBtn.style.fontSize = 12;
            _designMissionBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            rightPanel.Add(_designMissionBtn);

            mainContainer.Add(rightPanel);
            root.Add(mainContainer);

            // ── BOTTOM: SHARED NAV + ANA MENÜ ─────────────────────────────────
            // Navbar added directly as the last child; the screen-specific
            // "Ana Menü" button rides in the bar's trailing slot so the footer
            // stays a single 42px bar identical to every other screen.
            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.SolarMap);

            _mainMenuBtn = new Button(() => ViewModel.GoToMainMenuCommand.Execute()) { text = "Ana Menü" };
            _mainMenuBtn.AddToClassList("orrery-menubtn");
            _mainMenuBtn.style.height = 26;
            _mainMenuBtn.style.fontSize = 11;
            _mainMenuBtn.style.marginLeft = 8;
            _mainMenuBtn.style.paddingLeft = 10;
            _mainMenuBtn.style.paddingRight = 10;
            _mainMenuBtn.AddManipulator(new TooltipManipulator(TooltipData.CreateSimple(
                "Ana Menü",
                "Kampanyadan çıkıp ana menü ekranına döner.")));
            _navBar.AddTrailingElement(_mainMenuBtn);

            root.Add(_navBar);

            return root;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            BindViewModel();
        }

        private void BindViewModel()
        {
            if (ViewModel == null) return;

            ViewModel.AgencyName.Subscribe(name =>
            {
                if (_agencyLabel != null)
                    _agencyLabel.text = $"{name} · Planetaryum";
            });

            ViewModel.DateDisplay.Subscribe(date =>
            {
                if (_dateLabel != null) _dateLabel.text = date;
            });

            ViewModel.Budget.Subscribe(val =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{val:F1} M$";
            });

            ViewModel.Reputation.Subscribe(val =>
            {
                if (_reputationLabel != null) _reputationLabel.text = $"{val:F0}";
            });

            ViewModel.ResearchPoints.Subscribe(val =>
            {
                if (_researchLabel != null) _researchLabel.text = $"{val:F0} AP";
            });

            ViewModel.CurrentSpeed.Subscribe(speed =>
            {
                HighlightSpeedButton(speed);
            });

            ViewModel.Bodies.Subscribe(bodies =>
            {
                PopulatePlanetMarkers(bodies);
                PopulatePlanetPanel(bodies);
            });

            ViewModel.SelectedBody.Subscribe(body =>
            {
                UpdateSelectedBodyView(body);
            });

            ViewModel.BriefingCard.Subscribe(card =>
            {
                UpdateBriefingCard(card);
            });

            ViewModel.ActiveMissions.Subscribe(missions =>
            {
                PopulateActiveMissions(missions);
            });

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                _navBar?.SetStatus(msg);
            });
        }

        protected override void OnShown(object context)
        {
            base.OnShown(context);

            if (context is string destinationId)
            {
                ViewModel?.SelectBody(destinationId);
            }

            ViewModel?.Refresh();

            // ── Video mode (geçici): orrery plakası yerine güneş sistemi videosu ──
            _videoModeActive = FlightVideoMode.IsActive;
            if (_videoModeActive)
            {
                if (!PlayMapVideo())
                {
                    _videoModeActive = false;
                }
            }
            _videoStage.SetVisible(_videoModeActive);
            if (_videoModeActive)
            {
                _videoStage.BringToFront();
            }
        }

        /// <summary>Güneş sistemi haritası videosunu oynatmaya çalışır; arızada legacy orrery'a düşer.</summary>
        private bool PlayMapVideo()
        {
            try
            {
                if (FlightVideoLibrary.Exists(FlightVideoClip.Cruise) &&
                    _videoStage.PlayClip(FlightVideoClip.Cruise, loop: true))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                FlightVideoMode.ReportPlaybackFailure(ex.Message);
            }
            OnVideoStageFailed("solar map clip unavailable");
            return false;
        }

        /// <summary>Video arızası → bu oturumda eski orrery haritası geri gelir (fallback).</summary>
        private void OnVideoStageFailed(string _)
        {
            if (!_videoModeActive) return;
            _videoModeActive = false;
            _videoStage.StopAndRelease();
            _videoStage.SetVisible(false);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            _videoStage?.StopAndRelease();
            _videoStage?.SetVisible(false);
            _videoModeActive = false;
        }

        // ── Left collapsible planet selector panel (gezegen seçimi + fotoğraflar) ─

        /// <summary>
        /// Sol taraftaki açılıp kapanabilir gezegen panelini kurar. Panel; gezegen
        /// fotoğrafı, adı ve tipini içeren seçim satırlarından oluşur. Astrolab
        /// üzerindeki gezegen işaretleri de çalışmaya devam eder (eklenmiştir, eskisi
        /// bozulmadı). Başlık düğmesi paneli daraltıp genişletir.
        /// </summary>
        private void BuildPlanetPanel(VisualElement parent)
        {
            _planetPanel = new VisualElement();
            _planetPanel.AddToClassList("orrery-planet-panel");
            _planetPanel.style.width = PlanetPanelExpandedWidth;
            _planetPanel.style.flexShrink = 0;
            _planetPanel.style.flexDirection = FlexDirection.Column;
            _planetPanel.style.backgroundColor = new Color(0.055f, 0.075f, 0.125f, 0.985f);
            _planetPanel.style.borderRightWidth = 1;
            _planetPanel.style.borderRightColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.45f);
            _planetPanel.style.paddingTop = 10;
            _planetPanel.style.paddingBottom = 10;
            _planetPanel.style.overflow = Overflow.Hidden;

            // Panel başlığı — tıklanınca açılır/kapanır
            var headerBtn = new Button(TogglePlanetPanel);
            headerBtn.AddToClassList("orrery-planet-panel__header");
            headerBtn.style.height = 30;
            headerBtn.style.marginLeft = 8;
            headerBtn.style.marginRight = 8;
            headerBtn.style.paddingLeft = 8;
            headerBtn.style.paddingRight = 8;
            headerBtn.style.fontSize = 11;
            headerBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
            headerBtn.style.color = new Color(0.91f, 0.81f, 0.61f);
            headerBtn.style.backgroundColor = new Color(0.10f, 0.13f, 0.20f, 0.9f);

            _planetPanelToggleLabel = new Label("◀ GEZEGENLER");
            headerBtn.Add(_planetPanelToggleLabel);
            _planetPanel.Add(headerBtn);

            // Panel gövdesi — gezegen satırları kaydırılabilir liste içinde
            _planetPanelBody = new ScrollView();
            _planetPanelBody.style.flexGrow = 1;
            _planetPanelBody.style.marginTop = 6;
            _planetPanelBody.style.marginLeft = 8;
            _planetPanelBody.style.marginRight = 8;
            _planetPanel.Add(_planetPanelBody);

            parent.Add(_planetPanel);
        }

        private void TogglePlanetPanel()
        {
            SetPlanetPanelCollapsed(!_planetPanelCollapsed);
        }

        private void SetPlanetPanelCollapsed(bool collapsed)
        {
            _planetPanelCollapsed = collapsed;
            _planetPanel.style.width = collapsed ? PlanetPanelCollapsedWidth : PlanetPanelExpandedWidth;
            _planetPanelBody.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _planetPanelToggleLabel.text = collapsed ? "▶" : "◀ GEZEGENLER";
        }

        /// <summary>ViewModel'deki gövde listesinden gezegen satırlarını oluşturur.</summary>
        private void PopulatePlanetPanel(IReadOnlyList<CelestialBodyItemViewModel> bodies)
        {
            if (_planetPanelBody == null || bodies == null) return;

            _planetPanelBody.Clear();
            _planetPanelRows.Clear();

            foreach (var body in bodies)
            {
                var row = CreatePlanetPanelRow(body);
                _planetPanelRows.Add(row);
                _planetPanelBody.Add(row);
            }

            UpdatePlanetPanelSelection(ViewModel.SelectedBody.Value);
        }

        private VisualElement CreatePlanetPanelRow(CelestialBodyItemViewModel body)
        {
            var row = new VisualElement();
            row.AddToClassList("orrery-planet-row");
            row.userData = body.Id;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.marginBottom = 6;
            SetBorderRadius(row.style, 8);
            row.style.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.75f);
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = Color.clear;

            // Gezegen fotoğrafı (NASA destination art) — yoksa renkli daire fallback
            var thumb = new VisualElement();
            thumb.style.width = 40;
            thumb.style.height = 40;
            SetBorderRadius(thumb.style, 20);
            thumb.style.marginRight = 8;
            thumb.style.flexShrink = 0;
            thumb.style.overflow = Overflow.Hidden;
            SetBorderWidth(thumb.style, 1);

            var tex = GetPhotoTexture(body.Record.Art);
            if (tex != null)
            {
                thumb.style.backgroundImage = new StyleBackground(tex);
                thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                thumb.style.borderTopColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.6f);
                thumb.style.borderRightColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.6f);
                thumb.style.borderBottomColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.6f);
                thumb.style.borderLeftColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.6f);
            }
            else
            {
                var fallbackColor = ColorUtility.TryParseHtmlString(body.ColorHex, out var col) ? col : Color.gray;
                thumb.style.backgroundColor = fallbackColor;
                thumb.style.borderTopColor = new Color(1f, 1f, 1f, 0.25f);
                thumb.style.borderRightColor = new Color(1f, 1f, 1f, 0.25f);
                thumb.style.borderBottomColor = new Color(1f, 1f, 1f, 0.25f);
                thumb.style.borderLeftColor = new Color(1f, 1f, 1f, 0.25f);

                var initial = new Label(char.ToUpperInvariant(body.Id[0]).ToString());
                initial.style.fontSize = 16;
                initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                initial.style.color = new Color(0.06f, 0.09f, 0.14f);
                initial.style.unityFontStyleAndWeight = FontStyle.Bold;
                thumb.Add(initial);
            }

            row.Add(thumb);

            // Metin sütunu: ad + tip/AU
            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;

            var nameLabel = new Label(body.Name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.95f, 0.94f, 0.89f);
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(nameLabel);

            var subLabel = new Label($"{body.BodyType} · {body.SemiMajorAxisAu:F2} AU");
            subLabel.style.fontSize = 9;
            subLabel.style.color = new Color(0.58f, 0.62f, 0.70f);
            subLabel.style.whiteSpace = WhiteSpace.NoWrap;
            subLabel.style.textOverflow = TextOverflow.Ellipsis;
            textCol.Add(subLabel);

            row.Add(textCol);

            row.RegisterCallback<ClickEvent>(evt =>
            {
                ViewModel?.SelectBody(body.Id);
                evt.StopPropagation();
            });

            return row;
        }

        /// <summary>Seçili gezegen satırını vurgular (diğer seçim yollarıyla senkron).</summary>
        private void UpdatePlanetPanelSelection(CelestialBodyItemViewModel body)
        {
            if (body == null) return;

            foreach (var row in _planetPanelRows)
            {
                bool isSelected = row.userData is string rowId &&
                                  string.Equals(rowId, body.Id, StringComparison.OrdinalIgnoreCase);

                if (isSelected)
                {
                    row.AddToClassList("orrery-planet-row--selected");
                    row.style.backgroundColor = new Color(0.16f, 0.22f, 0.34f, 0.95f);
                    if (ColorUtility.TryParseHtmlString(body.ColorHex, out var accent))
                    {
                        row.style.borderLeftColor = accent;
                    }
                }
                else
                {
                    row.RemoveFromClassList("orrery-planet-row--selected");
                    row.style.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.75f);
                    row.style.borderLeftColor = Color.clear;
                }
            }
        }

        // ── Planet markers on the astrolabe plate ─────────────────────────────

        private void PopulatePlanetMarkers(IReadOnlyList<CelestialBodyItemViewModel> bodies)
        {
            if (_orreryCanvas == null || bodies == null) return;

            // Clear old planet markers
            foreach (var kvp in _planetMarkers)
            {
                if (kvp.Value?.parent != null)
                {
                    _orreryCanvas.Remove(kvp.Value);
                }
            }
            _planetMarkers.Clear();

            foreach (var body in bodies)
            {
                var marker = CreatePlanetElement(body);
                _planetMarkers[body.Id] = marker;
                _orreryCanvas.Add(marker);
            }

            // Video modu aktifken video, işaretlerin üstünde kalmalı (harita yerine geçer)
            if (_videoModeActive)
            {
                _videoStage.BringToFront();
            }

            UpdateSelectedBodyView(ViewModel.SelectedBody.Value);
        }

        private VisualElement CreatePlanetElement(CelestialBodyItemViewModel body)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = Length.Percent(body.NormalizedX);
            container.style.top = Length.Percent(body.NormalizedY);
            container.style.marginLeft = -body.RadiusPx / 2;
            container.style.marginTop = -body.RadiusPx / 2;
            container.style.alignItems = Align.Center;

            // Glowing aura if window is open
            if (body.IsWindowOpen)
            {
                var aura = new VisualElement();
                aura.AddToClassList("orrery-planet-aura");
                aura.style.position = Position.Absolute;
                aura.style.width = body.RadiusPx + 16;
                aura.style.height = body.RadiusPx + 16;
                aura.style.marginLeft = -(body.RadiusPx + 16) / 2;
                aura.style.marginTop = -(body.RadiusPx + 16) / 2;
                container.Add(aura);
            }

            // Planet circle
            var planet = new VisualElement();
            planet.AddToClassList("orrery-planet");
            planet.style.width = body.RadiusPx;
            planet.style.height = body.RadiusPx;

            if (ColorUtility.TryParseHtmlString(body.ColorHex, out var col))
            {
                planet.style.backgroundColor = col;
            }
            else
            {
                planet.style.backgroundColor = Color.gray;
            }

            container.Add(planet);

            // Label tag
            var tag = new Label(body.Record.Id.ToUpperInvariant());
            tag.AddToClassList("orrery-planet-label");
            tag.style.fontSize = 9;
            tag.style.marginTop = 4;
            container.Add(tag);

            container.RegisterCallback<ClickEvent>(evt =>
            {
                ViewModel?.SelectBody(body.Id);
                evt.StopPropagation();
            });

            return container;
        }

        private void UpdateSelectedBodyView(CelestialBodyItemViewModel body)
        {
            if (body == null) return;

            if (_targetNameLabel != null)
                _targetNameLabel.text = body.Name;

            if (_targetTypeLabel != null)
                _targetTypeLabel.text = $"{body.BodyType} · {body.SemiMajorAxisAu:F2} AU";

            // Sol paneldeki seçim vurgusunu da senkronla
            UpdatePlanetPanelSelection(body);

            // Catalog photo on the fiche (NASA destination art + Turkish credit)
            if (_fichePhotoSlot != null)
            {
                _fichePhotoSlot.Clear();
                _fichePhotoCaption = null;
                _fichePhotoFallback = null;

                var tex = GetPhotoTexture(body.Record.Art);
                if (tex != null)
                {
                    var photo = new VisualElement();
                    photo.style.position = Position.Absolute;
                    photo.style.left = 0;
                    photo.style.top = 0;
                    photo.style.right = 0;
                    photo.style.bottom = 0;
                    photo.style.backgroundImage = new StyleBackground(tex);
                    photo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    _fichePhotoSlot.Add(photo);

                    if (!string.IsNullOrEmpty(body.Record.ArtCredit))
                    {
                        _fichePhotoCaption = new Label("Kaynak: NASA — " + body.Record.ArtCredit);
                        _fichePhotoCaption.AddToClassList("orrery-fiche-photo__caption");
                        _fichePhotoCaption.style.position = Position.Absolute;
                        _fichePhotoCaption.style.left = 0;
                        _fichePhotoCaption.style.right = 0;
                        _fichePhotoCaption.style.bottom = 0;
                        _fichePhotoCaption.style.fontSize = 8;
                        _fichePhotoCaption.style.whiteSpace = WhiteSpace.Normal;
                        _fichePhotoCaption.style.paddingLeft = 3;
                        _fichePhotoCaption.style.paddingRight = 3;
                        _fichePhotoCaption.style.paddingTop = 1;
                        _fichePhotoCaption.style.paddingBottom = 1;
                        _fichePhotoSlot.Add(_fichePhotoCaption);
                    }
                }
                else
                {
                    _fichePhotoFallback = new Label("FOTO BEKLENİYOR");
                    _fichePhotoFallback.AddToClassList("orrery-fiche-photo__fallback");
                    _fichePhotoFallback.style.position = Position.Absolute;
                    _fichePhotoFallback.style.left = 0;
                    _fichePhotoFallback.style.right = 0;
                    _fichePhotoFallback.style.top = 0;
                    _fichePhotoFallback.style.bottom = 0;
                    _fichePhotoFallback.style.fontSize = 10;
                    _fichePhotoSlot.Add(_fichePhotoFallback);
                }
            }

            // Update planet markers selection outline
            foreach (var kvp in _planetMarkers)
            {
                bool isSelected = string.Equals(kvp.Key, body.Id, StringComparison.OrdinalIgnoreCase);
                kvp.Value.style.scale = isSelected ? new Scale(new Vector3(1.25f, 1.25f, 1f)) : new Scale(Vector3.one);

                var disc = kvp.Value.Q(className: "orrery-planet");
                if (disc != null)
                {
                    if (isSelected)
                    {
                        disc.AddToClassList("orrery-planet--selected");
                    }
                    else
                    {
                        disc.RemoveFromClassList("orrery-planet--selected");
                    }
                }
            }
        }

        private void UpdateBriefingCard(DestinationBriefingCard card)
        {
            if (card == null) return;

            if (_windowBadge != null)
            {
                _windowBadge.text = card.WindowStatusText.ToUpperInvariant();
                if (card.IsWindowActive)
                {
                    _windowBadge.AddToClassList("orrery-window-badge--open");
                    _windowBadge.RemoveFromClassList("orrery-window-badge--closed");
                }
                else
                {
                    _windowBadge.AddToClassList("orrery-window-badge--closed");
                    _windowBadge.RemoveFromClassList("orrery-window-badge--open");
                }
            }

            if (_windowDesc != null)
            {
                _windowDesc.text = card.IsWindowActive
                    ? "Transfer penceresi açık! Fırlatma için ideal yakıt tasarrufu."
                    : $"Sonraki fırlatma penceresine {card.DaysUntilNextWindow} gün kaldı.";
            }

            if (_deltaVLabel != null)
                _deltaVLabel.text = $"{card.RequiredDeltaVMps:N0} m/s";

            if (_transitTimeLabel != null)
                _transitTimeLabel.text = card.TransitDurationText;

            if (_commDelayLabel != null)
                _commDelayLabel.text = card.CommDelayText;

            // Hazards
            if (_hazardsContainer != null)
            {
                _hazardsContainer.Clear();
                if (card.HazardTags != null && card.HazardTags.Count > 0)
                {
                    foreach (var h in card.HazardTags)
                    {
                        var badge = new Label(h);
                        badge.AddToClassList("orrery-hazard-chip");
                        badge.style.fontSize = 10;
                        badge.style.paddingLeft = 6;
                        badge.style.paddingRight = 6;
                        badge.style.paddingTop = 2;
                        badge.style.paddingBottom = 2;
                        badge.style.marginRight = 6;
                        badge.style.marginBottom = 6;
                        _hazardsContainer.Add(badge);
                    }
                }
                else
                {
                    var none = new Label("Önemli bir tehlike yok.");
                    none.AddToClassList("orrery-hazard-none");
                    none.style.fontSize = 11;
                    _hazardsContainer.Add(none);
                }
            }
        }

        // ── Active mission pins & list ────────────────────────────────────────

        private void PopulateActiveMissions(IReadOnlyList<ActiveMissionMarkerViewModel> missions)
        {
            if (_missionsContainer == null) return;

            // Clear old mission markers from the astrolabe plate
            foreach (var pin in _missionPins)
            {
                if (pin?.parent != null) pin.RemoveFromHierarchy();
            }
            _missionPins.Clear();

            _missionsContainer.Clear();

            if (missions == null || missions.Count == 0)
            {
                var empty = new Label("Aktif seyirde uzay aracı bulunmuyor.");
                empty.AddToClassList("orrery-empty");
                empty.style.fontSize = 11;
                _missionsContainer.Add(empty);
                return;
            }

            foreach (var m in missions)
            {
                // List Item (catalog slip)
                var item = new VisualElement();
                item.AddToClassList("orrery-mission-item");
                item.style.paddingLeft = 8;
                item.style.paddingRight = 8;
                item.style.paddingTop = 6;
                item.style.paddingBottom = 6;
                item.style.marginBottom = 6;

                var row1 = new VisualElement();
                row1.style.flexDirection = FlexDirection.Row;
                row1.style.justifyContent = Justify.SpaceBetween;

                var nameLbl = new Label($"{m.MissionName}");
                nameLbl.style.fontSize = 11;
                nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = new Color(0.95f, 0.94f, 0.89f);
                row1.Add(nameLbl);

                var progLbl = new Label(m.ProgressPercent);
                progLbl.style.fontSize = 11;
                progLbl.style.color = new Color(0.62f, 0.79f, 0.88f);
                row1.Add(progLbl);

                item.Add(row1);

                var row2 = new Label($"Hedef: {m.DestinationId.ToUpperInvariant()} · Kalan: {m.DaysRemaining} gün");
                row2.style.fontSize = 10;
                row2.style.color = new Color(0.54f, 0.58f, 0.66f);
                row2.style.marginTop = 2;
                item.Add(row2);

                _missionsContainer.Add(item);

                // Add Spacecraft Pin on the astrolabe plate
                if (_orreryCanvas != null)
                {
                    var craftPin = new VisualElement();
                    craftPin.AddToClassList("orrery-craft-pin");
                    craftPin.style.position = Position.Absolute;
                    craftPin.style.left = Length.Percent(m.NormalizedX);
                    craftPin.style.top = Length.Percent(m.NormalizedY);
                    craftPin.style.width = 16;
                    craftPin.style.height = 16;
                    craftPin.style.marginLeft = -8;
                    craftPin.style.marginTop = -8;

                    var pinLabel = new Label(m.MissionName);
                    pinLabel.style.fontSize = 8;
                    pinLabel.style.color = new Color(0.95f, 0.94f, 0.89f);
                    pinLabel.style.marginTop = 16;
                    pinLabel.style.marginLeft = -10;
                    craftPin.Add(pinLabel);

                    _orreryCanvas.Add(craftPin);
                    _missionPins.Add(craftPin);
                }
            }

            // Video modu aktifken video, araç pinlerinin de üstünde kalmalı
            if (_videoModeActive && _missionPins.Count > 0)
            {
                _videoStage.BringToFront();
            }
        }

        // ── Small builders ────────────────────────────────────────────────────

        private Label CreateStatRow(VisualElement parent, string title, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("orrery-plaque-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 4;

            var tLbl = new Label(title);
            tLbl.AddToClassList("orrery-plaque-row__label");
            tLbl.style.fontSize = 11;
            row.Add(tLbl);

            var vLbl = new Label(value);
            vLbl.style.fontSize = 11;
            vLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            vLbl.style.color = new Color(0.85f, 0.82f, 0.76f);
            row.Add(vLbl);

            parent.Add(row);
            return vLbl;
        }

        private Button CreateSpeedButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("orrery-speed-btn");
            btn.style.width = 34;
            btn.style.height = 24;
            btn.style.fontSize = 11;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            return btn;
        }

        private void HighlightSpeedButton(TimeSpeed speed)
        {
            SetBtnActive(_speedButtons[0], speed == TimeSpeed.Paused);
            SetBtnActive(_speedButtons[1], speed == TimeSpeed.Normal);
            SetBtnActive(_speedButtons[2], speed == TimeSpeed.Fast);
            SetBtnActive(_speedButtons[3], speed == TimeSpeed.VeryFast);
        }

        private static void SetBtnActive(Button btn, bool active)
        {
            if (btn == null) return;
            if (active)
            {
                btn.AddToClassList("orrery-speed-btn--active");
            }
            else
            {
                btn.RemoveFromClassList("orrery-speed-btn--active");
            }
        }

        // ── Photo loading (ADR-054/063/066/068 pattern) ───────────────────────

        private static Texture2D GetPhotoTexture(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
            {
                return null;
            }

            if (PhotoCache.TryGetValue(resourcesPath, out var cached))
            {
                return cached;
            }

            Texture2D tex = null;
            try
            {
                tex = Resources.Load<Texture2D>(resourcesPath);
            }
            catch
            {
                tex = null; // non-Unity harness / missing asset — fiche renders without photo
            }

            if (tex != null)
            {
                PhotoCache[resourcesPath] = tex;
            }

            return tex;
        }

        private static void SetBorderRadius(IStyle style, float radius)
        {
            style.borderTopLeftRadius = radius;
            style.borderTopRightRadius = radius;
            style.borderBottomLeftRadius = radius;
            style.borderBottomRightRadius = radius;
        }

        private static void SetBorderWidth(IStyle style, float width)
        {
            style.borderTopWidth = width;
            style.borderRightWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
        }
    }
}
