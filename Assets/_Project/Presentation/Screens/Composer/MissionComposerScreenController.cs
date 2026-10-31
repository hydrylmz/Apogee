using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Core.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Mission Composer wizard (GDD §5.1 - §5.5, §14.1 item 5, §21 M3-04).
    /// Implements a 4-step wizard:
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

        // ── Header & Breadcrumbs ──────────────────────────────────────────────────
        private VisualElement _stepTabsContainer;
        private readonly List<Button> _stepTabButtons = new();

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

        // ── Footer Navigation ─────────────────────────────────────────────────────
        private Button _prevButton;
        private Button _nextButton;
        private Button _finalizeButton;
        private Label _stepIndicatorLabel;

        public MissionComposerScreenController(MissionComposerViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.AddToClassList("composer-screen-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // ── 1. Top Header Strip ────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.AddToClassList("composer-header");

            var titleCol = new VisualElement();
            var title = new Label("MİSYON BESTELEYİCİ");
            title.AddToClassList("apogee-panel-title");
            titleCol.Add(title);

            var subtitle = new Label("Kendi Görevini Tasarla — Hedef, Tip, Bilimsel Amaçlar ve Takvim (GDD §5)");
            subtitle.AddToClassList("composer-subtitle");
            titleCol.Add(subtitle);
            header.Add(titleCol);

            var headerRight = new VisualElement();
            headerRight.style.flexDirection = FlexDirection.Row;
            headerRight.style.alignItems = Align.Center;

            var toWorkshopBtn = new Button(() => ViewModel.Navigator?.GoTo(ScreenId.Workshop));
            toWorkshopBtn.text = "🛠️ Tasarım Atölyesi";
            toWorkshopBtn.AddToClassList("apogee-button");
            toWorkshopBtn.style.fontSize = 11;
            headerRight.Add(toWorkshopBtn);

            header.Add(headerRight);

            root.Add(header);

            // ── 2. Step Tabs (Breadcrumbs) ─────────────────────────────────────────
            _stepTabsContainer = new VisualElement();
            _stepTabsContainer.AddToClassList("composer-step-bar");
            BuildStepTabs(_stepTabsContainer);
            root.Add(_stepTabsContainer);

            // ── 3. Main Split Area (Left: Wizard Step Views, Right: Live Preview) ───
            var splitArea = new VisualElement();
            splitArea.AddToClassList("composer-split-area");
            splitArea.style.flexDirection = FlexDirection.Row;
            splitArea.style.flexGrow = 1;

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

                var radioBtn = new Button(() => ViewModel.SetWindowIndex(wIdx));
                radioBtn.text = winTitles[i];
                radioBtn.AddToClassList("composer-window-option-title");
                card.Add(radioBtn);

                var desc = new Label(winDescs[i]);
                desc.AddToClassList("composer-window-option-desc");
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

            var bUnlimited = new Button(() => ViewModel.SetBudgetCeiling(null)) { text = "Standart (Limitsiz)" };
            bUnlimited.AddToClassList("apogee-button");
            bUnlimited.style.marginRight = 6;
            budgetRow.Add(bUnlimited);

            var b50 = new Button(() => ViewModel.SetBudgetCeiling(50.0)) { text = "50 M$ Hedef" };
            b50.AddToClassList("apogee-button");
            b50.style.marginRight = 6;
            budgetRow.Add(b50);

            var b100 = new Button(() => ViewModel.SetBudgetCeiling(100.0)) { text = "100 M$ Hedef" };
            b100.AddToClassList("apogee-button");
            b100.style.marginRight = 6;
            budgetRow.Add(b100);

            var b250 = new Button(() => ViewModel.SetBudgetCeiling(250.0)) { text = "250 M$ Hedef" };
            b250.AddToClassList("apogee-button");
            budgetRow.Add(b250);

            scroll.Add(budgetRow);

            // Section 3: Mission Name
            var nameSectionTitle = new Label("MİSYON ADI VE KODU");
            nameSectionTitle.AddToClassList("composer-section-title");
            scroll.Add(nameSectionTitle);

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            _missionNameField = new TextField();
            _missionNameField.style.flexGrow = 1;
            _missionNameField.style.marginRight = 8;
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

            // 2. Mission Profile Telemetry Preview Box (GDD §5.4)
            var previewBox = new VisualElement();
            previewBox.AddToClassList("apogee-panel");
            previewBox.AddToClassList("composer-preview-box");
            previewBox.style.marginTop = 10;

            var pHeader = new VisualElement();
            pHeader.style.flexDirection = FlexDirection.Row;
            pHeader.style.alignItems = Align.Center;
            pHeader.style.justifyContent = Justify.SpaceBetween;
            pHeader.style.marginBottom = 6;

            var pTitle = new Label("MİSYON GEREKSİNİMLERİ");
            pTitle.AddToClassList("claim-summary-title");
            pHeader.Add(pTitle);

            _previewTargetLabel = new Label("AY · ROVER");
            _previewTargetLabel.AddToClassList("apogee-badge");
            _previewTargetLabel.AddToClassList("apogee-badge--cyan");
            pHeader.Add(_previewTargetLabel);
            previewBox.Add(pHeader);

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
            _previewDeltaVLabel.style.color = new UnityEngine.Color(0.2f, 0.85f, 0.95f);
            dvRow.Add(_previewDeltaVLabel);
            previewBox.Add(dvRow);

            var barBg = new VisualElement();
            barBg.AddToClassList("results-bar-track");
            barBg.style.height = 8;
            barBg.style.marginBottom = 8;

            _previewDeltaVBarFill = new VisualElement();
            _previewDeltaVBarFill.AddToClassList("results-bar-fill");
            _previewDeltaVBarFill.AddToClassList("results-bar-fill--good");
            _previewDeltaVBarFill.style.width = Length.Percent(70);
            barBg.Add(_previewDeltaVBarFill);
            previewBox.Add(barBg);

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
            previewBox.Add(metricsRow);

            _previewObjectivesLabel = new Label("Seçili Amaç: 0 adet");
            _previewObjectivesLabel.AddToClassList("claim-summary-text");
            _previewObjectivesLabel.style.marginBottom = 4;
            previewBox.Add(_previewObjectivesLabel);

            _previewWindowLabel = new Label("Pencere 0: Hemen");
            _previewWindowLabel.AddToClassList("claim-summary-text");
            previewBox.Add(_previewWindowLabel);

            sidebar.Add(previewBox);
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

            _prevButton = new Button(() => ViewModel.PreviousStepCommand.Execute()) { text = "← Önceki Adım" };
            _prevButton.AddToClassList("apogee-button");
            footer.Add(_prevButton);

            _stepIndicatorLabel = new Label("Adım 1 / 4: HEDEF SEÇİMİ");
            _stepIndicatorLabel.AddToClassList("composer-step-indicator-label");
            footer.Add(_stepIndicatorLabel);

            var rightActions = new VisualElement();
            rightActions.style.flexDirection = FlexDirection.Row;
            rightActions.style.alignItems = Align.Center;

            _nextButton = new Button(() => ViewModel.NextStepCommand.Execute()) { text = "Sonraki Adım →" };
            _nextButton.AddToClassList("apogee-button");
            _nextButton.AddToClassList("apogee-button--accent-amber");
            rightActions.Add(_nextButton);

            _finalizeButton = new Button(() => ViewModel.FinalizeMissionCommand.Execute()) { text = "Misyon Dosyasını Oluştur ✓" };
            _finalizeButton.AddToClassList("apogee-button");
            _finalizeButton.AddToClassList("apogee-button--primary");
            _finalizeButton.style.marginLeft = 8;
            rightActions.Add(_finalizeButton);

            footer.Add(rightActions);
        }

        // ── Dynamic Rendering & Refresh ───────────────────────────────────────────

        private void OnStepChanged(ComposerStep step)
        {
            // Toggle step tabs styles
            for (int i = 0; i < _stepTabButtons.Count; i++)
            {
                var tab = _stepTabButtons[i];
                tab.RemoveFromClassList("composer-step-tab--active");
                tab.RemoveFromClassList("composer-step-tab--completed");

                if (i == (int)step)
                {
                    tab.AddToClassList("composer-step-tab--active");
                }
                else if (i < (int)step)
                {
                    tab.AddToClassList("composer-step-tab--completed");
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
                if (isSelected) card.AddToClassList("composer-card--selected");

                // Header row
                var rowTop = new VisualElement();
                rowTop.style.flexDirection = FlexDirection.Row;
                rowTop.style.alignItems = Align.Center;
                rowTop.style.justifyContent = Justify.SpaceBetween;

                var nameLabel = new Label(dest.Id.ToUpperInvariant());
                nameLabel.AddToClassList("composer-card-title");
                rowTop.Add(nameLabel);

                var dvBadge = new Label($"{dest.DvTotal / 1000.0:F1} km/s");
                dvBadge.AddToClassList("apogee-badge");
                dvBadge.AddToClassList("apogee-badge--cyan");
                rowTop.Add(dvBadge);
                card.Add(rowTop);

                // Body specs
                var descLabel = new Label(dest.BodyType == "orbit" ? "Yörünge Görevi" : "Gök Cismi (Yüzey)");
                descLabel.AddToClassList("composer-card-desc");
                card.Add(descLabel);

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

                card.Add(infoRow);

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
                if (isSelected) card.AddToClassList("composer-card--selected");

                var rowTop = new VisualElement();
                rowTop.style.flexDirection = FlexDirection.Row;
                rowTop.style.alignItems = Align.Center;
                rowTop.style.justifyContent = Justify.SpaceBetween;

                var nameLabel = new Label(type.Id.ToUpperInvariant());
                nameLabel.AddToClassList("composer-card-title");
                rowTop.Add(nameLabel);

                var crewPill = new Label(type.IsCrewed ? "İNSANLI" : "ROBOTİK");
                crewPill.AddToClassList("apogee-badge");
                crewPill.AddToClassList(type.IsCrewed ? "apogee-badge--amber" : "apogee-badge--cyan");
                rowTop.Add(crewPill);
                card.Add(rowTop);

                var relLabel = new Label($"Asgari Güvenilirlik Eşiği: %{type.MinReliabilityThreshold * 100:0}");
                relLabel.AddToClassList("composer-card-desc");
                card.Add(relLabel);

                var modulesRow = new VisualElement();
                modulesRow.style.flexDirection = FlexDirection.Row;
                modulesRow.style.flexWrap = Wrap.Wrap;

                foreach (var mod in type.RequiredModuleCategories)
                {
                    var modPill = new Label(mod);
                    modPill.AddToClassList("claim-metric-pill");
                    modPill.AddToClassList("claim-metric-pill--risk");
                    modulesRow.Add(modPill);
                }

                card.Add(modulesRow);

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
                _objectivesListContainer.Add(emptyMsg);
                return;
            }

            foreach (var obj in compatible)
            {
                bool isChecked = selected != null && selected.Any(s => string.Equals(s.Id, obj.Id, StringComparison.OrdinalIgnoreCase));

                var item = new VisualElement();
                item.AddToClassList("composer-objective-item");
                if (isChecked) item.AddToClassList("composer-objective-item--selected");

                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.alignItems = Align.Center;

                var checkIndicator = new Label(isChecked ? "☑" : "☐");
                checkIndicator.style.fontSize = 16;
                checkIndicator.style.marginRight = 8;
                checkIndicator.style.color = isChecked ? new UnityEngine.Color(0.2f, 0.85f, 0.95f) : UnityEngine.Color.gray;
                topRow.Add(checkIndicator);

                var title = new Label(obj.Id);
                title.AddToClassList("composer-card-title");
                topRow.Add(title);

                var pointsPill = new Label($"+{obj.ScienceValue} Bilim / +{obj.DifficultyPoints} Zorluk");
                pointsPill.AddToClassList("claim-metric-pill");
                pointsPill.AddToClassList("claim-metric-pill--reward");
                pointsPill.style.marginLeft = StyleKeyword.Auto;
                topRow.Add(pointsPill);

                item.Add(topRow);

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
                        var instPill = new Label($"Alet: {inst}");
                        instPill.AddToClassList("claim-metric-pill");
                        instPill.AddToClassList("claim-metric-pill--risk");
                        metaRow.Add(instPill);
                    }
                }

                item.Add(metaRow);

                item.RegisterCallback<ClickEvent>(_ => ViewModel.ToggleObjective(obj));
                _objectivesListContainer.Add(item);
            }
        }

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
                    _windowCards[i].AddToClassList("composer-window-option--selected");
                else
                    _windowCards[i].RemoveFromClassList("composer-window-option--selected");
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
                string d = dest != null ? dest.Id.ToUpperInvariant() : "HEDEF YOK";
                string t = type != null ? type.Id.ToUpperInvariant() : "TİP YOK";
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
                int count = ViewModel.SelectedObjectives.Value?.Count ?? 0;
                _previewObjectivesLabel.text = $"Seçili Amaç: {count} adet";
            }

            if (_previewWindowLabel != null)
            {
                _previewWindowLabel.text = ViewModel.LaunchWindowText.Value;
            }
        }
    }
}
