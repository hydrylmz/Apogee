using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Localization;
using Apogee.Data;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Core.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Mission Composer wizard (GDD §5.1 - §5.5, §14.1 item 5, §21 M3-04).
    /// Visual identity: "Görev Planlama Panosu" — a flight-plan dossier on a planning
    /// desk (kraft folder sheets, perforated form sections, ink-blue chart lines,
    /// red stamps and a mission patch badge). Implements a 4-step wizard:
    ///   1. Hedef Seçimi (Destination)
    ///   2. Misyon Tipi (Mission Type)
    ///   3. Bilimsel ve Operasyonel Amaçlar (Objectives Multi-Select)
    ///   4. Takvim, Bütçe ve İsim (Schedule, Budget & Name)
    /// Integrates live <see cref="MissionClaimCardView"/> and telemetry preview.
    /// </summary>
    public class MissionComposerScreenController : ScreenControllerBase<MissionComposerViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MissionComposer;

        private readonly List<IDisposable> _subscriptions = new();

        // Photo texture cache (ADR-054/063/066 pattern — Resources.Load + dictionary).
        private static readonly Dictionary<string, Texture2D> PhotoCache = new();

        // ── Header & Breadcrumbs ──────────────────────────────────────────────────
        private ScreenNavBar _navBar;
        private VisualElement _stepTabsContainer;
        private readonly List<Button> _stepTabButtons = new();

        // ── Budget Selection Buttons ──────────────────────────────────────────────
        private Button _bUnlimited;
        private Button _b50;
        private Button _b100;
        private Button _b250;

        // ── Step Content Panels ───────────────────────────────────────────────────
        private VisualElement _stepContainerDestination;
        private VisualElement _stepContainerMissionType;
        private VisualElement _stepContainerObjectives;
        private VisualElement _stepContainerSchedule;

        private VisualElement _destinationCardsGrid;
        private VisualElement _missionTypeCardsGrid;
        private VisualElement _objectivesListContainer;
        private Label _objectiveCountLabel;

        // Schedule controls
        private readonly List<VisualElement> _windowCards = new();
        private TextField _missionNameField;
        private Label _windowDetailLabel;

        // ── Footer Wizard Controls ────────────────────────────────────────────────
        private Button _prevButton;
        private Button _nextButton;
        private Button _finalizeButton;
        private Label _stepIndicatorLabel;

        // ── Right Sidebar Preview ─────────────────────────────────────────────────
        private MissionClaimCardView _claimCardView;
        private Label _previewTargetLabel;
        private Label _previewObjectivesLabel;
        private Label _previewDeltaVLabel;
        private VisualElement _previewDeltaVBarFill;
        private Label _previewPadLabel;
        private Label _previewReliabilityLabel;
        private Label _previewTransitLabel;
        private Label _previewCommDelayLabel;
        private Label _previewWindowLabel;

        public MissionComposerScreenController(MissionComposerViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.AddToClassList("composer-screen-root");
            root.AddToClassList("screen-root");
            root.AddToClassList("theme-planning-board"); // Flight-plan dossier identity
            // NOTE: no root padding — the shared ScreenNavBar must sit flush with
            // the bottom edge exactly like every other screen; interior spacing
            // lives on the split area below.

            // Desk backdrop (decorative, never blocks picking)
            root.Add(new PlanningBoardBackdrop());

            // ── 1. Top Header Strip ────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("planning-board-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingTop = 6;
            header.style.paddingBottom = 6;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.marginBottom = 8;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var patch = new PlanningPatchBadge(30f);
            patch.style.marginRight = 10;
            titleRow.Add(patch);

            var titleCol = new VisualElement();
            var title = new Label("MİSYON PLANLAMA PANOSU");
            title.AddToClassList("apogee-panel-title");
            titleCol.Add(title);

            var subtitle = new Label("Uçuş Planı Dosyası — Hedef, Görev Tipi, Bilimsel Amaçlar ve Takvim");
            subtitle.AddToClassList("composer-subtitle");
            titleCol.Add(subtitle);
            titleRow.Add(titleCol);

            header.Add(titleRow);

            var hudPill = new Label("FORM NO: MP-4 · REV A");
            hudPill.AddToClassList("planning-hud-pill");
            hudPill.style.fontSize = 11;
            hudPill.style.paddingTop = 3;
            hudPill.style.paddingBottom = 3;
            hudPill.style.paddingLeft = 10;
            hudPill.style.paddingRight = 10;
            header.Add(hudPill);

            root.Add(header);

            // ── 2. Step Tabs (Folder Tabs) ─────────────────────────────────────────
            _stepTabsContainer = new VisualElement();
            _stepTabsContainer.AddToClassList("composer-step-bar");
            _stepTabsContainer.AddToClassList("planning-tabbar");
            BuildStepTabs(_stepTabsContainer);
            root.Add(_stepTabsContainer);

            // ── 3. Main Split Area (Left: Wizard Step Views, Right: Live Preview) ───
            var splitArea = new VisualElement();
            splitArea.AddToClassList("composer-split-area");
            splitArea.AddToClassList("screen-body");
            splitArea.style.flexDirection = FlexDirection.Row;
            splitArea.style.flexGrow = 1;
            splitArea.style.flexShrink = 1;
            splitArea.style.flexBasis = 0;
            splitArea.style.minHeight = 0;
            splitArea.style.paddingLeft = 20;
            splitArea.style.paddingRight = 20;
            splitArea.style.paddingBottom = 8;

            // Left Wizard Column
            var wizardCol = new VisualElement();
            wizardCol.AddToClassList("composer-wizard-column");
            wizardCol.style.flexGrow = 1;
            wizardCol.style.flexBasis = Length.Percent(65);
            wizardCol.style.marginRight = 12;

            _stepContainerDestination = BuildDestinationStep();
            _stepContainerMissionType = BuildMissionTypeStep();
            _stepContainerObjectives = BuildObjectivesStep();
            _stepContainerSchedule = BuildScheduleStep();

            wizardCol.Add(_stepContainerDestination);
            wizardCol.Add(_stepContainerMissionType);
            wizardCol.Add(_stepContainerObjectives);
            wizardCol.Add(_stepContainerSchedule);

            splitArea.Add(wizardCol);

            // Right Sidebar Column (Claims + Preview)
            var sidebarCol = new VisualElement();
            sidebarCol.AddToClassList("composer-sidebar-column");
            sidebarCol.style.flexBasis = Length.Percent(35);
            sidebarCol.style.flexShrink = 0;

            BuildSidebar(sidebarCol);
            splitArea.Add(sidebarCol);

            root.Add(splitArea);

            // ── 4. Footer Bar ──────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.AddToClassList("composer-footer-bar");
            BuildFooter(footer);
            root.Add(footer);

            // ── 5. Shared nav bar (replaces ad-hoc header buttons) ─────────────────
            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.MissionComposer);
            root.Add(_navBar);

            return root;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Bind Claim ViewModel
            _claimCardView.Bind(ViewModel.ClaimViewModel);

            // Wire Subscriptions
            _subscriptions.Add(ViewModel.CurrentStep.Subscribe(OnStepChanged));
            _subscriptions.Add(ViewModel.SelectedDestination.Subscribe(_ => RefreshAllViews()));
            _subscriptions.Add(ViewModel.SelectedMissionType.Subscribe(_ => RefreshAllViews()));
            _subscriptions.Add(ViewModel.SelectedObjectives.Subscribe(_ => RefreshObjectivesSelection()));
            _subscriptions.Add(ViewModel.CompatibleObjectives.Subscribe(_ => RefreshCompatibleObjectivesList()));
            _subscriptions.Add(ViewModel.SelectedWindowIndex.Subscribe(_ => RefreshWindowSelection()));
            _subscriptions.Add(ViewModel.MissionName.Subscribe(name =>
            {
                if (_missionNameField != null && _missionNameField.value != name)
                {
                    _missionNameField.SetValueWithoutNotify(name);
                }
            }));
            _subscriptions.Add(ViewModel.CanGoNext.Subscribe(can => _nextButton?.SetEnabled(can)));
            _subscriptions.Add(ViewModel.CanGoPrevious.Subscribe(can => _prevButton?.SetEnabled(can)));
            _subscriptions.Add(ViewModel.CanFinalize.Subscribe(can => _finalizeButton?.SetEnabled(can)));

            RefreshAllViews();
            OnStepChanged(ViewModel.CurrentStep.Value);
        }

        protected override void OnShown(object context)
        {
            base.OnShown(context);

            if (context is string destinationId && !string.IsNullOrEmpty(destinationId))
            {
                ViewModel.SelectDestinationById(destinationId);
            }
            else if (context is DestinationRecord destRecord)
            {
                ViewModel.SelectDestination(destRecord);
            }

            RefreshAllViews();
            OnStepChanged(ViewModel.CurrentStep.Value);
        }

        public override void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub?.Dispose();
            }
            _subscriptions.Clear();

            _claimCardView?.Dispose();
            _claimCardView = null;

            base.Dispose();
        }

        // ── View Builders ─────────────────────────────────────────────────────────

        private void BuildStepTabs(VisualElement container)
        {
            container.Clear();
            _stepTabButtons.Clear();

            string[] stepTitles = { "1. HEDEF", "2. MİSYON TİPİ", "3. BİLİMSEL AMAÇLAR", "4. TAKVİM & BÜTÇE" };

            for (int i = 0; i < stepTitles.Length; i++)
            {
                int stepIndex = i;
                var tab = new Button(() => ViewModel.GoToStep((ComposerStep)stepIndex));
                tab.text = stepTitles[i];
                tab.AddToClassList("composer-step-tab");
                tab.AddToClassList("planning-tab");

                _stepTabButtons.Add(tab);
                container.Add(tab);
            }
        }

        private VisualElement BuildDestinationStep()
        {
            var panel = new VisualElement();
            panel.AddToClassList("apogee-panel");
            panel.AddToClassList("composer-step-panel");

            var guide = new Label("Varış Hedefini Seçin. Hedefin yerçekimi ve mesafesi gereken menzili, pencere aralığını ve tehlikeleri belirler.");
            guide.AddToClassList("composer-step-guide");
            panel.Add(guide);

            _destinationCardsGrid = new ScrollView(ScrollViewMode.Vertical);
            _destinationCardsGrid.AddToClassList("composer-cards-grid");
            panel.Add(_destinationCardsGrid);

            return panel;
        }

        private VisualElement BuildMissionTypeStep()
        {
            var panel = new VisualElement();
            panel.AddToClassList("apogee-panel");
            panel.AddToClassList("composer-step-panel");

            var guide = new Label("Misyon Tipini Belirleyin. Seçilen mimari, araç üzerinde zorunlu modülleri (EDL, Kabin, Yaşam Destek) ve asgari güvenilirlik eşiğini türetir.");
            guide.AddToClassList("composer-step-guide");
            panel.Add(guide);

            _missionTypeCardsGrid = new ScrollView(ScrollViewMode.Vertical);
            _missionTypeCardsGrid.AddToClassList("composer-cards-grid");
            panel.Add(_missionTypeCardsGrid);

            return panel;
        }

        private VisualElement BuildObjectivesStep()
        {
            var panel = new VisualElement();
            panel.AddToClassList("apogee-panel");
            panel.AddToClassList("composer-step-panel");

            var guide = new Label("Bilimsel ve Operasyonel Amaçlar (Çoklu Seçim). Hedefe özel bilimsel hedefleri seçin; her hedef gereken aletleri ve başarı kriterini belirler.");
            guide.AddToClassList("composer-step-guide");
            panel.Add(guide);

            // Action row
            var actionRow = new VisualElement();
            actionRow.AddToClassList("composer-objectives-action-row");
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.marginBottom = 8;

            var selectAllBtn = new Button(() => ViewModel.SelectAllCompatibleObjectives()) { text = "Tümünü Seç" };
            selectAllBtn.AddToClassList("apogee-button");
            selectAllBtn.style.fontSize = 11;
            selectAllBtn.style.marginRight = 6;
            actionRow.Add(selectAllBtn);

            var clearBtn = new Button(() => ViewModel.ClearSelectedObjectives()) { text = "Temizle" };
            clearBtn.AddToClassList("apogee-button");
            clearBtn.style.fontSize = 11;
            actionRow.Add(clearBtn);

            _objectiveCountLabel = new Label("0 Amaç Seçildi");
            _objectiveCountLabel.AddToClassList("claim-metric-pill");
            _objectiveCountLabel.AddToClassList("claim-metric-pill--reward");
            _objectiveCountLabel.style.marginLeft = StyleKeyword.Auto;
            actionRow.Add(_objectiveCountLabel);

            panel.Add(actionRow);

            _objectivesListContainer = new ScrollView(ScrollViewMode.Vertical);
            _objectivesListContainer.AddToClassList("composer-objectives-list");
            panel.Add(_objectivesListContainer);

            return panel;
        }

        private VisualElement BuildScheduleStep()
        {
            var panel = new VisualElement();
            panel.AddToClassList("apogee-panel");
            panel.AddToClassList("composer-step-panel");

            var guide = new Label("Fırlatma Penceresi, Bütçe Tavanı ve Misyon Tanımı.");
            guide.AddToClassList("composer-step-guide");
            panel.Add(guide);

            var scroll = new ScrollView(ScrollViewMode.Vertical);

            // Section 1: Launch Window
            var winSectionTitle = new Label("FIRLATMA PENCERESİ TERCİHİ");
            winSectionTitle.AddToClassList("composer-section-title");
            scroll.Add(winSectionTitle);

            _windowCards.Clear();
            var winGroup = new VisualElement();
            winGroup.style.flexDirection = FlexDirection.Column;
            winGroup.style.marginBottom = 12;

            string[] winTitles = { "Pencere 0: En Yakın Pencere (Hemen Fırlatma)", "Pencere 1: Sonraki Pencere (+1 Periyot, Dengeli)", "Pencere 2: Daha Sonraki Pencere (Uzun Hazırlık)" };
            string[] winDescs = { "Tasarım ve üretim hemen başlamalıdır; araştırma için süre dardır.", "Araştırma ve üretim kuyruğuna zaman kazandırır; optimum hazırlık.", "Gelişmiş teknolojilerin araştırılması için geniş takvim marjı sağlar." };

            for (int i = 0; i < 3; i++)
            {
                int wIdx = i;
                var card = new VisualElement();
                card.AddToClassList("composer-window-option");
                card.AddToClassList("planning-window-form");

                // Whole card is the click target — the title button alone was too
                // small / finicky to hit (users also click the description & padding).
                card.RegisterCallback<ClickEvent>(_ => ViewModel.SetWindowIndex(wIdx));

                var titleLabel = new Label(winTitles[i]);
                titleLabel.AddToClassList("composer-window-option-title");
                titleLabel.AddToClassList("planning-sheet-title");
                card.Add(titleLabel);

                var desc = new Label(winDescs[i]);
                desc.AddToClassList("composer-window-option-desc");
                desc.AddToClassList("planning-sheet-meta");
                card.Add(desc);

                _windowCards.Add(card);
                winGroup.Add(card);
            }
            scroll.Add(winGroup);

            _windowDetailLabel = new Label("-");
            _windowDetailLabel.AddToClassList("claim-summary-text");
            _windowDetailLabel.style.marginBottom = 16;
            scroll.Add(_windowDetailLabel);

            // Section 2: Budget Ceiling
            var budgetSectionTitle = new Label("İÇ HEDEF: BÜTÇE TAVANI (OPSİYONEL)");
            budgetSectionTitle.AddToClassList("composer-section-title");
            scroll.Add(budgetSectionTitle);

            var budgetRow = new VisualElement();
            budgetRow.style.flexDirection = FlexDirection.Row;
            budgetRow.style.marginBottom = 16;

            var bUnlimited = new Button(() => { ViewModel.SetBudgetCeiling(null); RefreshBudgetSelection(null); }) { text = "Standart (Limitsiz)"};
            bUnlimited.AddToClassList("apogee-button");
            bUnlimited.style.marginRight = 6;
            budgetRow.Add(bUnlimited);

            var b50 = new Button(() => { ViewModel.SetBudgetCeiling(50.0); RefreshBudgetSelection(50.0); }) { text = "50 M$ Hedef" };
            b50.AddToClassList("apogee-button");
            b50.style.marginRight = 6;
            budgetRow.Add(b50);

            var b100 = new Button(() => { ViewModel.SetBudgetCeiling(100.0); RefreshBudgetSelection(100.0); }) { text = "100 M$ Hedef" };
            b100.AddToClassList("apogee-button");
            b100.style.marginRight = 6;
            budgetRow.Add(b100);

            var b250 = new Button(() => { ViewModel.SetBudgetCeiling(250.0); RefreshBudgetSelection(250.0); }) { text = "250 M$ Hedef" };
            b250.AddToClassList("apogee-button");
            budgetRow.Add(b250);

            _bUnlimited = bUnlimited;
            _b50        = b50;
            _b100       = b100;
            _b250       = b250;
            RefreshBudgetSelection(null); // Varsayılan: Standart seçili

            scroll.Add(budgetRow);

            // Section 3: Mission Name
            var nameSectionTitle = new Label("MİSYON ADI VE KODU");
            nameSectionTitle.AddToClassList("composer-section-title");
            scroll.Add(nameSectionTitle);

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            _missionNameField = new TextField();
            _missionNameField.AddToClassList("planning-ink-field");
            _missionNameField.style.flexGrow = 1;
            _missionNameField.style.marginRight = 8;
            _missionNameField.style.height = 36;
            _missionNameField.RegisterValueChangedCallback(evt => ViewModel.SetMissionName(evt.newValue));
            nameRow.Add(_missionNameField);

            var defaultNameBtn = new Button(() =>
            {
                var dest = ViewModel.SelectedDestination.Value;
                var type = ViewModel.SelectedMissionType.Value;
                if (dest != null && type != null)
                {
                    ViewModel.SetMissionName(string.Empty);
                    ViewModel.SelectDestination(dest); // Triggers auto-name
                }
            }) { text = "Varsayılan Ad" };
            defaultNameBtn.AddToClassList("apogee-button");
            nameRow.Add(defaultNameBtn);

            scroll.Add(nameRow);

            panel.Add(scroll);
            return panel;
        }

        private void BuildSidebar(VisualElement sidebar)
        {
            sidebar.Clear();

            // 1. Mission Claim Card View (GDD §5.3, ADR-016)
            _claimCardView = new MissionClaimCardView();
            sidebar.Add(_claimCardView);

            // 2. Mission dossier preview box (GDD §5.4) — "MİSYON DOSYASI"
            var dossierBox = new VisualElement();
            dossierBox.AddToClassList("planning-dossier");
            dossierBox.style.marginTop = 10;
            dossierBox.style.paddingLeft = 12;
            dossierBox.style.paddingRight = 12;
            dossierBox.style.paddingTop = 10;
            dossierBox.style.paddingBottom = 10;

            var pHeader = new VisualElement();
            pHeader.style.flexDirection = FlexDirection.Row;
            pHeader.style.alignItems = Align.Center;
            pHeader.style.justifyContent = Justify.SpaceBetween;
            pHeader.style.marginBottom = 8;
            pHeader.AddToClassList("planning-dossier-header");

            var pHeaderLeft = new VisualElement();
            pHeaderLeft.style.flexDirection = FlexDirection.Row;
            pHeaderLeft.style.alignItems = Align.Center;

            var dossierIcon = new PlanningPatchBadge(22f);
            dossierIcon.style.marginRight = 8;
            pHeaderLeft.Add(dossierIcon);

            var pTitle = new Label("MİSYON DOSYASI");
            pTitle.AddToClassList("claim-summary-title");
            pHeaderLeft.Add(pTitle);

            pHeader.Add(pHeaderLeft);

            _previewTargetLabel = new Label("AY · ROVER");
            _previewTargetLabel.AddToClassList("apogee-badge");
            _previewTargetLabel.AddToClassList("apogee-badge--cyan");
            pHeader.Add(_previewTargetLabel);
            dossierBox.Add(pHeader);

            // Delta-V Bar
            var dvRow = new VisualElement();
            dvRow.style.flexDirection = FlexDirection.Row;
            dvRow.style.justifyContent = Justify.SpaceBetween;
            dvRow.style.marginBottom = 2;

            var dvLabel = new Label("Gereken Menzil (Δv):");
            dvLabel.AddToClassList("claim-summary-text");
            dvRow.Add(dvLabel);

            _previewDeltaVLabel = new Label("13,500 m/s");
            _previewDeltaVLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            dvRow.Add(_previewDeltaVLabel);
            dossierBox.Add(dvRow);

            var barBg = new VisualElement();
            barBg.AddToClassList("results-bar-track");
            barBg.style.height = 8;
            barBg.style.marginBottom = 8;

            _previewDeltaVBarFill = new VisualElement();
            _previewDeltaVBarFill.AddToClassList("results-bar-fill");
            _previewDeltaVBarFill.AddToClassList("results-bar-fill--good");
            _previewDeltaVBarFill.style.width = Length.Percent(70);
            barBg.Add(_previewDeltaVBarFill);
            dossierBox.Add(barBg);

            // Metrics row
            var metricsRow = new VisualElement();
            metricsRow.style.flexDirection = FlexDirection.Row;
            metricsRow.style.flexWrap = Wrap.Wrap;
            metricsRow.style.marginBottom = 6;

            _previewPadLabel = CreateMetricPill("Rampa: Seviye 3");
            _previewReliabilityLabel = CreateMetricPill("Eşik: %50");
            _previewTransitLabel = CreateMetricPill("Seyir: 3 - 5 gün");
            _previewCommDelayLabel = CreateMetricPill("Gecikme: 1.3s");

            metricsRow.Add(_previewPadLabel);
            metricsRow.Add(_previewReliabilityLabel);
            metricsRow.Add(_previewTransitLabel);
            metricsRow.Add(_previewCommDelayLabel);
            dossierBox.Add(metricsRow);

            _previewObjectivesLabel = new Label("Seçili Amaç: 0 adet");
            _previewObjectivesLabel.AddToClassList("claim-summary-text");
            _previewObjectivesLabel.style.marginBottom = 4;
            dossierBox.Add(_previewObjectivesLabel);

            _previewWindowLabel = new Label("Pencere 0: Hemen");
            _previewWindowLabel.AddToClassList("claim-summary-text");
            dossierBox.Add(_previewWindowLabel);

            sidebar.Add(dossierBox);
        }

        private Label CreateMetricPill(string text)
        {
            var pill = new Label(text);
            pill.AddToClassList("claim-metric-pill");
            pill.AddToClassList("claim-metric-pill--reward");
            pill.style.marginRight = 4;
            pill.style.marginBottom = 4;
            return pill;
        }

        private void BuildFooter(VisualElement footer)
        {
            footer.Clear();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;

            _prevButton = new Button(() => ViewModel.PreviousStepCommand.Execute()) { text = "<- Önceki Adım"};
            _prevButton.AddToClassList("apogee-button");
            footer.Add(_prevButton);

            _stepIndicatorLabel = new Label("Adım 1 / 4: HEDEF SEÇİMİ");
            _stepIndicatorLabel.AddToClassList("composer-step-indicator-label");
            footer.Add(_stepIndicatorLabel);

            var rightActions = new VisualElement();
            rightActions.style.flexDirection = FlexDirection.Row;
            rightActions.style.alignItems = Align.Center;

            _nextButton = new Button(() => ViewModel.NextStepCommand.Execute()) { text = "Sonraki Adım ->"};
            _nextButton.AddToClassList("apogee-button");
            _nextButton.AddToClassList("apogee-button--accent-amber");
            rightActions.Add(_nextButton);

            _finalizeButton = new Button(() => ViewModel.FinalizeMissionCommand.Execute()) { text = "Uçuş Planını Damgala →"};
            _finalizeButton.AddToClassList("apogee-button");
            _finalizeButton.AddToClassList("apogee-button--primary");
            _finalizeButton.style.marginLeft = 8;
            rightActions.Add(_finalizeButton);

            footer.Add(rightActions);
        }

        // ── Dynamic Rendering & Refresh ───────────────────────────────────────────

        /// <summary>Bütçe butonlarından seçili olanı vurgular.</summary>
        private void RefreshBudgetSelection(double? selected)
        {
            void SetActive(Button btn, bool active)
            {
                if (active)
                    btn.AddToClassList("apogee-button--selected");
                else
                    btn.RemoveFromClassList("apogee-button--selected");
            }

            if (_bUnlimited == null) return;

            SetActive(_bUnlimited, selected == null);
            SetActive(_b50,        selected.HasValue && Math.Abs(selected.Value - 50.0)  < 0.001);
            SetActive(_b100,       selected.HasValue && Math.Abs(selected.Value - 100.0) < 0.001);
            SetActive(_b250,       selected.HasValue && Math.Abs(selected.Value - 250.0) < 0.001);
        }

        private void OnStepChanged(ComposerStep step)
        {
            // Toggle step folder-tab styles
            for (int i = 0; i < _stepTabButtons.Count; i++)
            {
                var tab = _stepTabButtons[i];
                tab.RemoveFromClassList("composer-step-tab--active");
                tab.RemoveFromClassList("composer-step-tab--completed");
                tab.RemoveFromClassList("planning-tab--active");
                tab.RemoveFromClassList("planning-tab--done");

                if (i == (int)step)
                {
                    tab.AddToClassList("composer-step-tab--active");
                    tab.AddToClassList("planning-tab--active");
                }
                else if (i < (int)step)
                {
                    tab.AddToClassList("composer-step-tab--completed");
                    tab.AddToClassList("planning-tab--done");
                }
            }

            // Toggle step containers visibility
            _stepContainerDestination.style.display = (step == ComposerStep.Destination) ? DisplayStyle.Flex : DisplayStyle.None;
            _stepContainerMissionType.style.display = (step == ComposerStep.MissionType) ? DisplayStyle.Flex : DisplayStyle.None;
            _stepContainerObjectives.style.display = (step == ComposerStep.Objectives) ? DisplayStyle.Flex : DisplayStyle.None;
            _stepContainerSchedule.style.display = (step == ComposerStep.ScheduleAndBudget) ? DisplayStyle.Flex : DisplayStyle.None;

            // Footer buttons state
            _prevButton.SetEnabled(ViewModel.CanGoPrevious.Value);
            _nextButton.SetEnabled(ViewModel.CanGoNext.Value);
            _finalizeButton.SetEnabled(ViewModel.CanFinalize.Value);

            _stepIndicatorLabel.text = $"Adım {(int)step + 1} / 4: {ViewModel.StepTitle.Value}";

            if (step == ComposerStep.ScheduleAndBudget)
            {
                _nextButton.style.display = DisplayStyle.None;
                _finalizeButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                _nextButton.style.display = DisplayStyle.Flex;
                _finalizeButton.style.display = DisplayStyle.None;
            }
        }

        private void RefreshAllViews()
        {
            RefreshDestinationCards();
            RefreshMissionTypeCards();
            RefreshCompatibleObjectivesList();
            RefreshObjectivesSelection();
            RefreshWindowSelection();
            RefreshPreviewSidebar();
        }

        private void RefreshDestinationCards()
        {
            if (_destinationCardsGrid == null) return;
            _destinationCardsGrid.Clear();

            var current = ViewModel.SelectedDestination.Value;

            foreach (var dest in ViewModel.Destinations)
            {
                bool isSelected = current != null && string.Equals(current.Id, dest.Id, StringComparison.OrdinalIgnoreCase);

                var card = new VisualElement();
                card.AddToClassList("composer-card");
                card.AddToClassList("planning-sheet");
                if (isSelected) card.AddToClassList("planning-sheet--selected");

                // ── Photo slot (NASA destination photo from destinations.json) ──
                var photoRow = new VisualElement();
                photoRow.style.flexDirection = FlexDirection.Row;
                photoRow.style.marginBottom = 8;

                var photoSlot = BuildPhotoSlot(dest.Art, dest.ArtCredit, 108, 80);
                photoRow.Add(photoSlot);

                var textCol = new VisualElement();
                textCol.style.flexGrow = 1;
                textCol.style.marginLeft = 10;

                // Header row: name + Δv badge
                var rowTop = new VisualElement();
                rowTop.style.flexDirection = FlexDirection.Row;
                rowTop.style.alignItems = Align.Center;
                rowTop.style.justifyContent = Justify.SpaceBetween;

                var nameLabel = new Label(GetDestinationDisplayName(dest));
                nameLabel.AddToClassList("composer-card-title");
                nameLabel.AddToClassList("planning-sheet-title");
                rowTop.Add(nameLabel);

                var dvBadge = new Label($"{dest.DvTotal / 1000.0:F1} km/s");
                dvBadge.AddToClassList("apogee-badge");
                dvBadge.AddToClassList("apogee-badge--cyan");
                // Δv rozeti sözlük kutusu: değerin ne anlama geldiğini sözlük
                // tarzında açıklar (GDD §13.2 eğitim hedefi).
                dvBadge.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                    "Δv — km/s (Delta-V / Menzil Bütçesi)",
                    "Görev aracının motorlarıyla toplamda kazanması gereken hız değişimi " +
                    "(yükseliş + transfer + varış + iniş + dönüş dahil). " +
                    "Değer yükseldikçe daha fazla yakıt ve itki kapasitesi gerekir; " +
                    "yetersizse araç hedefe ulaşamaz.")));
                rowTop.Add(dvBadge);
                textCol.Add(rowTop);

                var descLabel = new Label(dest.BodyType == "orbit" ? "Yörünge Görevi" : "Gök Cismi (Yüzey)");
                descLabel.AddToClassList("composer-card-desc");
                descLabel.AddToClassList("planning-sheet-meta");
                textCol.Add(descLabel);

                var infoRow = new VisualElement();
                infoRow.style.flexDirection = FlexDirection.Row;
                infoRow.style.flexWrap = Wrap.Wrap;

                var transitPill = new Label($"Seyir: {dest.TransitDaysMin}-{dest.TransitDaysMax}g");
                transitPill.AddToClassList("claim-metric-pill");
                transitPill.AddToClassList("claim-metric-pill--reward");
                infoRow.Add(transitPill);

                var delayPill = new Label(dest.CommDelaySecondsMax < 60 ? $"Sinyal: {dest.CommDelaySecondsMax:F1}s" : $"Sinyal: {dest.CommDelaySecondsMax / 60:F1}dk");
                delayPill.AddToClassList("claim-metric-pill");
                delayPill.AddToClassList("claim-metric-pill--reward");
                infoRow.Add(delayPill);

                textCol.Add(infoRow);
                photoRow.Add(textCol);
                card.Add(photoRow);

                // Selection stamp row
                var stampRow = new VisualElement();
                stampRow.style.flexDirection = FlexDirection.Row;
                stampRow.style.justifyContent = Justify.FlexEnd;

                var stamp = new Label(isSelected ? "DOSYA SEÇİLDİ" : "FORM MP-" + dest.Id.ToUpperInvariant());
                stamp.AddToClassList("planning-stamp");
                stamp.AddToClassList(isSelected ? "planning-stamp--selected" : "planning-stamp--info");
                stamp.style.fontSize = 10;
                stamp.style.paddingTop = 2;
                stamp.style.paddingBottom = 2;
                stamp.style.paddingLeft = 8;
                stamp.style.paddingRight = 8;
                stampRow.Add(stamp);
                card.Add(stampRow);

                card.RegisterCallback<ClickEvent>(_ => ViewModel.SelectDestination(dest));
                _destinationCardsGrid.Add(card);
            }
        }

        private void RefreshMissionTypeCards()
        {
            if (_missionTypeCardsGrid == null) return;
            _missionTypeCardsGrid.Clear();

            var current = ViewModel.SelectedMissionType.Value;

            foreach (var type in ViewModel.MissionTypes)
            {
                bool isSelected = current != null && string.Equals(current.Id, type.Id, StringComparison.OrdinalIgnoreCase);

                var card = new VisualElement();
                card.AddToClassList("composer-card");
                card.AddToClassList("planning-sheet");
                if (isSelected) card.AddToClassList("planning-sheet--selected");

                var bodyRow = new VisualElement();
                bodyRow.style.flexDirection = FlexDirection.Row;

                // ── Photo slot (NASA mission-architecture photo) ──
                var photoSlot = BuildPhotoSlot(type.Art, type.ArtCredit, 108, 80);
                bodyRow.Add(photoSlot);

                var textCol = new VisualElement();
                textCol.style.flexGrow = 1;
                textCol.style.marginLeft = 10;

                var rowTop = new VisualElement();
                rowTop.style.flexDirection = FlexDirection.Row;
                rowTop.style.alignItems = Align.Center;
                rowTop.style.justifyContent = Justify.SpaceBetween;

                var nameLabel = new Label(GetMissionTypeDisplayName(type));
                nameLabel.AddToClassList("composer-card-title");
                nameLabel.AddToClassList("planning-sheet-title");
                rowTop.Add(nameLabel);

                var crewPill = new Label(type.IsCrewed ? "İNSANLI" : "ROBOTİK");
                crewPill.AddToClassList("apogee-badge");
                crewPill.AddToClassList(type.IsCrewed ? "apogee-badge--amber" : "apogee-badge--cyan");
                rowTop.Add(crewPill);
                textCol.Add(rowTop);

                var relLabel = new Label($"Asgari Güvenilirlik Eşiği: %{type.MinReliabilityThreshold * 100:0}");
                relLabel.AddToClassList("composer-card-desc");
                relLabel.AddToClassList("planning-sheet-meta");
                textCol.Add(relLabel);

                var modulesRow = new VisualElement();
                modulesRow.style.flexDirection = FlexDirection.Row;
                modulesRow.style.flexWrap = Wrap.Wrap;

                foreach (var mod in type.RequiredModuleCategories)
                {
                    var modPill = new Label(mod);
                    modPill.AddToClassList("claim-metric-pill");
                    modPill.AddToClassList("claim-metric-pill--risk");
                    // Sözlük tooltipsi: kısaltma (bus/power/comms/…) üzerine gelince
                    // terimin sözlük anlamı açılır (GDD §13.2, §8.3).
                    string meaning = GetModuleCategoryGlossary(mod, out string termTitle);
                    modPill.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                        termTitle,
                        meaning)));
                    modulesRow.Add(modPill);
                }

                textCol.Add(modulesRow);
                bodyRow.Add(textCol);
                card.Add(bodyRow);

                // Selection stamp row
                var stampRow = new VisualElement();
                stampRow.style.flexDirection = FlexDirection.Row;
                stampRow.style.justifyContent = Justify.FlexEnd;

                var stamp = new Label(isSelected ? "ŞEMATİK ONAYLI" : "SPEC " + type.Id.ToUpperInvariant());
                stamp.AddToClassList("planning-stamp");
                stamp.AddToClassList(isSelected ? "planning-stamp--ok" : "planning-stamp--info");
                stamp.style.fontSize = 10;
                stamp.style.paddingTop = 2;
                stamp.style.paddingBottom = 2;
                stamp.style.paddingLeft = 8;
                stamp.style.paddingRight = 8;
                stampRow.Add(stamp);
                card.Add(stampRow);

                card.RegisterCallback<ClickEvent>(_ => ViewModel.SelectMissionType(type));
                _missionTypeCardsGrid.Add(card);
            }
        }

        private void RefreshCompatibleObjectivesList()
        {
            if (_objectivesListContainer == null) return;
            _objectivesListContainer.Clear();

            var compatible = ViewModel.CompatibleObjectives.Value;
            var selected = ViewModel.SelectedObjectives.Value;

            if (compatible == null || compatible.Count == 0)
            {
                var emptyMsg = new Label("Seçili hedef ve misyon tipi için uygun bilimsel amaç bulunamadı.");
                emptyMsg.AddToClassList("composer-card-desc");
                emptyMsg.AddToClassList("planning-empty");
                _objectivesListContainer.Add(emptyMsg);
                return;
            }

            foreach (var obj in compatible)
            {
                bool isChecked = selected != null && selected.Any(s => string.Equals(s.Id, obj.Id, StringComparison.OrdinalIgnoreCase));

                var item = new VisualElement();
                item.AddToClassList("composer-objective-item");
                item.AddToClassList("planning-check-row");
                if (isChecked) item.AddToClassList("planning-check-row--checked");

                // ── Photo slot (NASA archive photo from objectives.json) ────────
                // Same kraft-photo treatment as the destination and mission-type
                // cards, so every step of the wizard reads identically.
                var photoRow = new VisualElement();
                photoRow.style.flexDirection = FlexDirection.Row;
                photoRow.style.flexGrow = 1;

                var photoSlot = BuildPhotoSlot(obj.Art, obj.ArtCredit, 108, 80);
                photoRow.Add(photoSlot);

                var contentCol = new VisualElement();
                contentCol.style.flexGrow = 1;
                contentCol.style.flexShrink = 1;
                contentCol.style.marginLeft = 10;

                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.alignItems = Align.Center;

                var checkIndicator = new Label(isChecked ? "[x]": "[ ]");
                checkIndicator.style.fontSize = 16;
                checkIndicator.style.marginRight = 8;
                checkIndicator.style.color = isChecked ? new Color(0.243f, 0.561f, 0.769f) : new Color(0.39f, 0.44f, 0.54f);
                topRow.Add(checkIndicator);

                var title = new Label(GetObjectiveDisplayName(obj));
                title.AddToClassList("composer-card-title");
                title.AddToClassList("planning-sheet-title");
                topRow.Add(title);

                var pointsPill = new Label($"+{obj.ScienceValue} Bilim / +{obj.DifficultyPoints} Zorluk");
                pointsPill.AddToClassList("claim-metric-pill");
                pointsPill.AddToClassList("claim-metric-pill--reward");
                pointsPill.style.marginLeft = StyleKeyword.Auto;
                topRow.Add(pointsPill);

                contentCol.Add(topRow);

                var metaRow = new VisualElement();
                metaRow.style.flexDirection = FlexDirection.Row;
                metaRow.style.flexWrap = Wrap.Wrap;
                metaRow.style.marginTop = 4;

                var dataPill = new Label($"Veri: ≥{obj.MinimumDataGb} GB");
                dataPill.AddToClassList("claim-metric-pill");
                dataPill.AddToClassList("claim-metric-pill--reward");
                metaRow.Add(dataPill);

                var durPill = new Label($"Süre: ≥{obj.MinimumDurationDays} gün");
                durPill.AddToClassList("claim-metric-pill");
                durPill.AddToClassList("claim-metric-pill--reward");
                metaRow.Add(durPill);

                if (obj.RequiredInstrumentCategories != null)
                {
                    foreach (var inst in obj.RequiredInstrumentCategories)
                    {
                        var instPill = new Label($"Alet: {GetInstrumentDisplayName(inst)}");
                        instPill.AddToClassList("claim-metric-pill");
                        instPill.AddToClassList("claim-metric-pill--risk");
                        instPill.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                            GetInstrumentDisplayName(inst),
                            $"Bu amaç, görev aracında \"{inst}\" kategorisinde bir bilimsel alet bulundurmayı zorunlu kılar.")));
                        metaRow.Add(instPill);
                    }
                }

                contentCol.Add(metaRow);
                photoRow.Add(contentCol);

                item.Add(photoRow);

                // ── Tooltip on hover via TooltipManipulator (GDD §8.3) ─────────────
                // Lazy factory so the tooltip reflects the checked state at hover time.
                item.AddManipulator(new TooltipManipulator(() =>
                {
                    bool checkedNow = ViewModel.SelectedObjectives.Value?.Any(s =>
                        string.Equals(s.Id, obj.Id, StringComparison.OrdinalIgnoreCase)) == true;

                    return new TooltipData(
                        GetObjectiveDisplayName(obj),
                        GetObjectiveDescription(obj),
                        GetObjectiveCriterionReason(obj),
                        $"+{obj.ScienceValue} Bilim / +{obj.DifficultyPoints} Zorluk",
                        true,
                        checkedNow ? TooltipSeverity.Success : TooltipSeverity.Info);
                }));

                item.RegisterCallback<ClickEvent>(_ => ViewModel.ToggleObjective(obj));
                _objectivesListContainer.Add(item);
            }
        }

        // ── Kraft photo slot (planning-photo, tooltip credit) ────────────────────

        /// <summary>
        /// Builds a photo slot on the kraft sheet: NASA photo with no overlay text
        /// (the source credit lives on the hover tooltip and attribution_data.json,
        /// keeping the imagery unobstructed). Missing files degrade gracefully to a
        /// "FOTO BEKLENİYOR" placeholder so tests never depend on asset presence.
        /// </summary>
        private static VisualElement BuildPhotoSlot(string artPath, string artCredit, float width, float height)
        {
            var slot = new VisualElement();
            slot.AddToClassList("planning-photo");
            slot.style.width = width;
            slot.style.height = height;
            slot.style.flexShrink = 0;
            slot.style.overflow = Overflow.Hidden;

            var tex = GetPhotoTexture(artPath);
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
                slot.Add(photo);

                slot.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                    "NASA arşiv görseli",
                    string.IsNullOrEmpty(artCredit) ? "Kaynak: NASA" : "Kaynak: NASA — " + artCredit)));
            }
            else
            {
                var fallback = new Label("FOTO BEKLENİYOR");
                fallback.AddToClassList("planning-photo__fallback");
                fallback.style.position = Position.Absolute;
                fallback.style.left = 0;
                fallback.style.right = 0;
                fallback.style.top = 0;
                fallback.style.bottom = 0;
                fallback.style.fontSize = 9;
                slot.Add(fallback);
            }

            return slot;
        }

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
                tex = null; // non-Unity harness / missing asset — cards render without photo
            }

            if (tex != null)
            {
                PhotoCache[resourcesPath] = tex;
            }

            return tex;
        }

        // ── Module Category Glossary (sözlük tooltipsi) ──────────────────────────

        /// <summary>
        /// Misyon tipi kartlarındaki modül kategorisi kısaltmalarının (bus, power,
        /// comms…) sözlük tarzı Türkçe açıklamaları. bus kaydı glossary.json'daki
        /// "term-bus" maddesiyle uyumludur; diğerleri oyun içi tasarım sözlüğünü
        /// (Workshop katalog kategorileri) izler.
        /// </summary>
        private static string GetModuleCategoryGlossary(string categoryId, out string termTitle)
        {
            string definition;
            switch (categoryId?.Trim().ToLowerInvariant())
            {
                case "bus":
                    termTitle = "bus — Spacecraft Bus (Uzay Aracı Gövdesi)";
                    definition = "Uzay aracının omurgası ve altyapı platformu: tüm alt sistemleri ve " +
                                 "bilimsel aletleri bir arada tutan, elektrik ile veri hatlarını " +
                                 "barındıran ana gövde. Bus olmadan sistemler birbirine bağlanamaz ve " +
                                 "araç uzayda bağımsız çalışamaz.";
                    break;
                case "power":
                    termTitle = "power — Güç Sistemi (Elektrik Üretim & Dağıtım)";
                    definition = "Güneş panelleri, RTG nükleer jeneratör ve bataryalardan oluşan elektrik " +
                                 "alt sistemi. Üretimin tüketimi aşması (pozitif güç marjı) gölge " +
                                 "geçişlerinde sistemlerin açık kalmasını garanti eder; güç yoksa görev donar.";
                    break;
                case "comms":
                    termTitle = "comms — İletişim Sistemi (Haberleşme)";
                    definition = "Aracı Dünya ile bağlayan antenler ve veri indirme (downlink) alt sistemi. " +
                                 "Sinyal gecikmesi mesafeye göre saniyelerden dakikalara uzanır; downlink " +
                                 "zayıfsa toplanan bilim verileri bellekte birikir ve kaybedilir.";
                    break;
                case "propulsion":
                    termTitle = "propulsion — İtki Sistemi (Motor & Yakıt)";
                    definition = "Motorlar, yakıt ve depolardan oluşan manevra sistemi; yörünme " +
                                 "değişikliklerini ve seyir yakmalarını yapar. Görevin Δv (km/s) " +
                                 "bütçesini bu sistem karşılar.";
                    break;
                case "instrument":
                    termTitle = "instrument — Bilimsel Aletler (Yükler)";
                    definition = "Kamera, spektrometre, yüzey altı radarı gibi bilim verisi toplayan " +
                                 "faydalı yükler. Görevin bilim puanını fiilen üreten bölümdür.";
                    break;
                case "edl":
                    termTitle = "edl — Giriş, Alçalış ve İniş (Entry, Descent, Landing)";
                    definition = "Isı kalkanı, fren paraşütü ve skycrane gibi sistemlerle atmosfere " +
                                 "güvenli giriş ve yüzeye iniş paketi. Yetersizse araç atmosferde yanar " +
                                 "veya yüksek hızla yere çakılır.";
                    break;
                case "sample-container":
                    termTitle = "sample-container — Numune Kabı";
                    definition = "Gezegen yüzeyinden toplanan kaya, toprak ve atmosfer örneklerini " +
                                 "karaya bulmadan Dünya'ya taşıyan steril, sızdırmaz kap.";
                    break;
                case "crew-capsule":
                    termTitle = "crew-capsule — Mürettebat Kapsülü";
                    definition = "Astronotları basınçlı, sıcaklığı kontrol edilmiş yaşanabilir ortamda " +
                                 "taşıyan kabin; insanlı görevlerde fırlatma kaçış sistemiyle birlikte " +
                                 "yüksek güvenilirlik şartı gerektirir.";
                    break;
                case "life-support":
                    termTitle = "life-support — Yaşam Destek (ECLSS)";
                    definition = "Kapalı çevrim yaşam destek sistemi: havayı temizler, suyu ve havayı " +
                                 "geri dönüştürür, karbondioksiti absorbe eder. Mürettebatın görev " +
                                 "süresince hayatta kalmasını sağlar.";
                    break;
                case "thermal":
                    termTitle = "thermal — Isıl Kontrol Sistemi";
                    definition = "Radyatör ve ısı borularıyla aracın ısısını yöneten sistem. Aletler " +
                                 "aşırı ısındığında ısıl kısılma (thermal throttling) devreye girer ve " +
                                 "bilim verisi toplama kalitesi düşer.";
                    break;
                case "adcs":
                    termTitle = "adcs — Yönelim Kontrolü (Attitude Determination & Control)";
                    definition = "Tepki tekerlekleri, iticiler ve yıldız izleyicilerle aracın uzayda " +
                                 "doğru yöne bakmasını sağlayan aviyonik sistem; anten ve güneş " +
                                 "panellerinin verimli çalışmasının ön koşuludur.";
                    break;
                default:
                    termTitle = categoryId ?? string.Empty;
                    definition = $"\"{categoryId}\" kategorisinde bir modül bu görev tipi için zorunludur. " +
                                 "Atölye ekranından bu kategoride bir bileşen seçin.";
                    break;
            }

            return definition;
        }

        // ── Objective Display Helpers ─────────────────────────────────────────────

        /// <summary>Locale'dan okur; anahtar eksikse ham id'yi okunur başlığa çevirir.</summary>
        private static string GetLocalizedOr(string key, string fallback)
        {
            string value = LocalizationManager.Instance.Get(key);
            return string.IsNullOrEmpty(value) || string.Equals(value, key, StringComparison.Ordinal)
                ? fallback
                : value;
        }

        /// <summary>"leo-global-imaging" -> "Leo Global Imaging"</summary>
        private static string PrettifyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            var parts = id.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static string GetDestinationDisplayName(DestinationRecord dest)
        {
            if (dest == null) return string.Empty;
            return GetLocalizedOr(dest.NameKey, PrettifyId(dest.Id));
        }

        private static string GetMissionTypeDisplayName(MissionTypeRecord type)
        {
            if (type == null) return string.Empty;
            return GetLocalizedOr(type.NameKey, PrettifyId(type.Id));
        }

        private static string GetObjectiveDisplayName(ObjectiveRecord obj)
        {
            if (obj == null) return string.Empty;
            return GetLocalizedOr(obj.NameKey, PrettifyId(obj.Id));
        }

        private static string GetObjectiveDescription(ObjectiveRecord obj)
        {
            if (obj == null) return string.Empty;
            return GetLocalizedOr(obj.DescKey, "Bu bilimsel amaç için açıklama tanımlanmamış.");
        }

        /// <summary>Başarı kriterini tooltip "Neden" satırı için cümleye çevirir.</summary>
        private static string GetObjectiveCriterionReason(ObjectiveRecord obj)
        {
            if (obj == null) return string.Empty;
            double t = obj.SuccessCriterionThreshold;
            return (obj.SuccessCriterionType) switch
            {
                "data-delivered-gb" => $"En az {t:0} GB bilimsel veri Dünya'ya iletildiğinde tamamlanır.",
                "coverage-days"     => $"En az {t:0} gün kesintisiz veri kapsaması sağlandığında tamamlanır.",
                "operation-days"    => $"En az {t:0} gün kesintisiz operasyon sürdürüldüğünde tamamlanır.",
                "sample-returned"   => "Toplanan numune kapsülü Dünya'ya güvenle döndürüldüğünde tamamlanır.",
                "sample-prepared"   => "Numuneler iade görevi için hazır hale getirildiğinde tamamlanır.",
                _                   => "Özel bir başarı kriteri tanımlanmıştır.",
            };
        }

        /// <summary>Alet kategorisi kimliğini okunur ada çevirir (camera -> Kamera).</summary>
        private static string GetInstrumentDisplayName(string categoryId) => categoryId switch
        {
            "camera"           => "Kamera",
            "spectrometer"     => "Spektrometre",
            "thermal-infrared" => "Termal Kızılötesi",
            "radar"            => "Radar",
            "surface-science"  => "Yüzey Bilimi Paketi",
            "magnetometer"     => "Manyetometre",
            _                  => PrettifyId(categoryId),
        };

        private void RefreshObjectivesSelection()
        {
            var count = ViewModel.SelectedObjectives.Value?.Count ?? 0;
            if (_objectiveCountLabel != null)
            {
                _objectiveCountLabel.text = $"{count} Amaç Seçildi";
            }
            RefreshCompatibleObjectivesList();
            RefreshPreviewSidebar();
        }

        private void RefreshWindowSelection()
        {
            int selectedIdx = ViewModel.SelectedWindowIndex.Value;
            for (int i = 0; i < _windowCards.Count; i++)
            {
                if (i == selectedIdx)
                {
                    _windowCards[i].AddToClassList("composer-window-option--selected");
                    _windowCards[i].AddToClassList("planning-window-form--selected");
                }
                else
                {
                    _windowCards[i].RemoveFromClassList("composer-window-option--selected");
                    _windowCards[i].RemoveFromClassList("planning-window-form--selected");
                }
            }

            if (_windowDetailLabel != null)
            {
                _windowDetailLabel.text = ViewModel.LaunchWindowText.Value;
            }
        }

        private void RefreshPreviewSidebar()
        {
            var dest = ViewModel.SelectedDestination.Value;
            var type = ViewModel.SelectedMissionType.Value;

            if (_previewTargetLabel != null)
            {
                string d = dest != null ? GetDestinationDisplayName(dest) : "HEDEF YOK";
                string t = type != null ? GetMissionTypeDisplayName(type) : "TİP YOK";
                _previewTargetLabel.text = $"{d} · {t}";
            }

            if (_previewDeltaVLabel != null)
            {
                double dv = ViewModel.EstimatedDeltaV.Value;
                _previewDeltaVLabel.text = $"{dv:N0} m/s";

                if (_previewDeltaVBarFill != null)
                {
                    // Scale against 25,000 m/s max reference
                    float ratio = UnityEngine.Mathf.Clamp01((float)(dv / 25000.0));
                    _previewDeltaVBarFill.style.width = Length.Percent(ratio * 100f);
                }
            }

            if (_previewPadLabel != null)
            {
                _previewPadLabel.text = $"Rampa: Seviye {ViewModel.EstimatedLaunchPadLevel.Value}";
            }

            if (_previewReliabilityLabel != null)
            {
                _previewReliabilityLabel.text = $"Eşik: %{ViewModel.EstimatedReliabilityThreshold.Value * 100:0}";
            }

            if (_previewTransitLabel != null)
            {
                _previewTransitLabel.text = $"Seyir: {ViewModel.EstimatedTransitRange.Value}";
            }

            if (_previewCommDelayLabel != null)
            {
                _previewCommDelayLabel.text = $"Sinyal: {ViewModel.EstimatedCommDelayRange.Value}";
            }

            if (_previewObjectivesLabel != null)
            {
                var selected = ViewModel.SelectedObjectives.Value;
                int count = selected?.Count ?? 0;
                if (count > 0)
                {
                    var names = selected.Select(GetObjectiveDisplayName);
                    _previewObjectivesLabel.text = $"Seçili Amaç ({count}): {string.Join(", ", names)}";
                }
                else
                {
                    _previewObjectivesLabel.text = "Seçili Amaç: 0 adet";
                }
            }

            if (_previewWindowLabel != null)
            {
                _previewWindowLabel.text = ViewModel.LaunchWindowText.Value;
            }
        }
    }
}
