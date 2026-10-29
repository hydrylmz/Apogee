using System;
using System.Collections.Generic;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Screen controller for the Design Workshop (GDD §7 & §14.3).
    /// Implements slot list view, module selection drawer, and live hayalet delta indicators (M2-02).
    /// </summary>
    public class WorkshopScreenController : ScreenControllerBase<WorkshopViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Workshop;

        private Label _targetLabel;
        private Label _blueprintNameLabel;
        private Button _undoButton;
        private Button _redoButton;

        // Slot list & Module drawer
        private VisualElement _slotListContainer;
        private VisualElement _moduleDrawerContainer;
        private Label _drawerTitleLabel;
        private VisualElement _drawerOptionsList;
        private VisualElement _resizerHandle;
        private Button _toggleExpandButton;
        private float _drawerHeight = 350f;
        private bool _isMaximized = false;
        private const float MinDrawerHeight = 160f;

        // Telemetry values & Ghost delta indicators
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

        private VisualElement _ghostSummaryBox;
        private Label _ghostSummaryTextLabel;

        private Button _reviewButton;

        public WorkshopScreenController(WorkshopViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // ── Header strip (GDD §14.3) ────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");

            _blueprintNameLabel = new Label("APOGEE Atölye");
            _blueprintNameLabel.AddToClassList("apogee-panel-title");
            header.Add(_blueprintNameLabel);

            var headerRight = new VisualElement();
            headerRight.style.flexDirection = FlexDirection.Row;
            headerRight.style.alignItems = Align.Center;

            _targetLabel = new Label("Hedef: Ay");
            _targetLabel.AddToClassList("apogee-badge");
            _targetLabel.AddToClassList("apogee-badge--cyan");
            headerRight.Add(_targetLabel);

            // Undo & Redo buttons (GDD §14.4)
            _undoButton = new Button(() => ViewModel.UndoCommand.Execute());
            _undoButton.text = "↺ Geri Al";
            _undoButton.AddToClassList("apogee-button");
            _undoButton.style.marginLeft = 12;
            _undoButton.style.fontSize = 11;
            headerRight.Add(_undoButton);

            _redoButton = new Button(() => ViewModel.RedoCommand.Execute());
            _redoButton.text = "↻ Yinele";
            _redoButton.AddToClassList("apogee-button");
            _redoButton.style.marginLeft = 6;
            _redoButton.style.fontSize = 11;
            headerRight.Add(_redoButton);

            var saveTemplateBtn = new Button(() => ViewModel.Templates.SaveCurrentCommand.Execute());
            saveTemplateBtn.text = "💾 Şablon Kaydet";
            saveTemplateBtn.AddToClassList("apogee-button");
            saveTemplateBtn.style.marginLeft = 8;
            saveTemplateBtn.style.fontSize = 11;
            headerRight.Add(saveTemplateBtn);

            var compareBtn = new Button(() => ViewModel.OpenComparisonCommand.Execute());
            compareBtn.text = "⚖️ Karşılaştır";
            compareBtn.AddToClassList("apogee-button");
            compareBtn.AddToClassList("apogee-button--primary");
            compareBtn.style.marginLeft = 6;
            compareBtn.style.fontSize = 11;
            headerRight.Add(compareBtn);

            header.Add(headerRight);
            root.Add(header);

            // ── Main body area (3 columns: Slots & Drawer | 2D Rocket | Telemetry) ─
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.marginTop = 12;

            // ── Column 1: Slot List & Selection Drawer (Left, 32%) ─────────────────
            var slotColumn = new VisualElement();
            slotColumn.AddToClassList("apogee-panel");
            slotColumn.style.width = Length.Percent(32);
            slotColumn.style.marginRight = 8;

            var slotTitleRow = new VisualElement();
            slotTitleRow.style.flexDirection = FlexDirection.Row;
            slotTitleRow.style.justifyContent = Justify.SpaceBetween;
            slotTitleRow.style.alignItems = Align.Center;

            var slotTitle = new Label("Bileşen Slotları");
            slotTitle.AddToClassList("apogee-panel-title");
            slotTitle.style.fontSize = 14;
            slotTitleRow.Add(slotTitle);

            var slotHintBadge = new Label("Seçim Command ile Gider");
            slotHintBadge.AddToClassList("apogee-badge");
            slotTitleRow.Add(slotHintBadge);
            slotColumn.Add(slotTitleRow);

            var slotScroll = new ScrollView();
            slotScroll.style.flexGrow = 1;
            slotScroll.style.flexShrink = 1;
            slotScroll.style.marginTop = 8;
            _slotListContainer = new VisualElement();
            slotScroll.Add(_slotListContainer);
            slotColumn.Add(slotScroll);

            // ── Resizer Splitter Handle (M2 UI İyileştirme) ──────────────────────────
            _resizerHandle = new VisualElement();
            _resizerHandle.AddToClassList("apogee-splitter-handle");
            _resizerHandle.style.display = DisplayStyle.None;

            var gripLine = new VisualElement();
            gripLine.AddToClassList("apogee-splitter-grip");
            _resizerHandle.Add(gripLine);

            SetupResizerDrag(_resizerHandle, slotColumn);
            slotColumn.Add(_resizerHandle);

            // ── Selection Drawer (GDD §7.3) ──────────────────────────────────────────
            _moduleDrawerContainer = new VisualElement();
            _moduleDrawerContainer.AddToClassList("apogee-module-drawer");
            _moduleDrawerContainer.style.display = DisplayStyle.None;
            _moduleDrawerContainer.style.height = _drawerHeight;
            _moduleDrawerContainer.style.flexShrink = 0;
            _moduleDrawerContainer.style.minHeight = MinDrawerHeight;

            var drawerHeader = new VisualElement();
            drawerHeader.style.flexDirection = FlexDirection.Row;
            drawerHeader.style.justifyContent = Justify.SpaceBetween;
            drawerHeader.style.alignItems = Align.Center;

            _drawerTitleLabel = new Label("Uyumlu Modüller");
            _drawerTitleLabel.AddToClassList("apogee-panel-title");
            _drawerTitleLabel.style.fontSize = 12;
            _drawerTitleLabel.style.color = new Color(0.22f, 0.74f, 0.97f);
            drawerHeader.Add(_drawerTitleLabel);

            var drawerControls = new VisualElement();
            drawerControls.style.flexDirection = FlexDirection.Row;
            drawerControls.style.alignItems = Align.Center;

            _toggleExpandButton = new Button(ToggleDrawerMaximize);
            _toggleExpandButton.text = "⤢ Genişlet";
            _toggleExpandButton.AddToClassList("apogee-button");
            _toggleExpandButton.style.paddingLeft = 6;
            _toggleExpandButton.style.paddingRight = 6;
            _toggleExpandButton.style.paddingTop = 1;
            _toggleExpandButton.style.paddingBottom = 1;
            _toggleExpandButton.style.fontSize = 10;
            _toggleExpandButton.style.marginRight = 4;
            drawerControls.Add(_toggleExpandButton);

            var closeDrawerButton = new Button(() =>
            {
                _moduleDrawerContainer.style.display = DisplayStyle.None;
                _resizerHandle.style.display = DisplayStyle.None;
                if (ViewModel.SelectedSlot.Value != null)
                {
                    ViewModel.SelectedSlot.Value.IsSelected.Value = false;
                }
            });
            closeDrawerButton.text = "✕";
            closeDrawerButton.AddToClassList("apogee-button");
            closeDrawerButton.style.paddingLeft = 6;
            closeDrawerButton.style.paddingRight = 6;
            closeDrawerButton.style.paddingTop = 1;
            closeDrawerButton.style.paddingBottom = 1;
            drawerControls.Add(closeDrawerButton);
            drawerHeader.Add(drawerControls);
            _moduleDrawerContainer.Add(drawerHeader);

            var drawerScroll = new ScrollView();
            drawerScroll.style.flexGrow = 1;
            drawerScroll.style.marginTop = 6;
            _drawerOptionsList = new VisualElement();
            drawerScroll.Add(_drawerOptionsList);
            _moduleDrawerContainer.Add(drawerScroll);

            slotColumn.Add(_moduleDrawerContainer);
            mainContent.Add(slotColumn);

            // ── Column 2: 2D Rocket / Craft cross-section (Center) ─────────────────
            var centerColumn = new VisualElement();
            centerColumn.AddToClassList("apogee-panel");
            centerColumn.AddToClassList("apogee-panel--surface");
            centerColumn.style.flexGrow = 1;
            centerColumn.style.marginRight = 8;
            centerColumn.style.paddingTop = 10;
            centerColumn.style.paddingBottom = 10;
            centerColumn.style.paddingLeft = 12;
            centerColumn.style.paddingRight = 12;

            var crossSectionView = new RocketCrossSectionView(ViewModel.CrossSection);
            centerColumn.Add(crossSectionView);
            mainContent.Add(centerColumn);

            // ── Column 3: Telemetry & Results Panel (Right, 30%) ───────────────────
            var telemetryColumn = new VisualElement();
            telemetryColumn.AddToClassList("apogee-panel");
            telemetryColumn.style.width = Length.Percent(30);

            var telemetryTitle = new Label("Canlı İstatistikler & Hayalet Delta");
            telemetryTitle.AddToClassList("apogee-panel-title");
            telemetryTitle.style.fontSize = 14;
            telemetryColumn.Add(telemetryTitle);

            // Delta-V Row + Ghost Delta
            var deltaVRow = new VisualElement();
            deltaVRow.AddToClassList("apogee-stat-row");
            deltaVRow.style.marginTop = 12;

            var deltaVLeft = new VisualElement();
            deltaVLeft.style.flexDirection = FlexDirection.Row;
            var deltaVLabel = new Label("Menzil (Δv)");
            deltaVLabel.AddToClassList("apogee-stat-label");
            deltaVLeft.Add(deltaVLabel);

            var deltaVRight = new VisualElement();
            deltaVRight.style.flexDirection = FlexDirection.Row;
            _deltaVValueLabel = new Label("0 / 0 m/s");
            _deltaVValueLabel.AddToClassList("apogee-stat-value");
            _deltaVGhostLabel = new Label(string.Empty);
            _deltaVGhostLabel.AddToClassList("apogee-ghost-delta");
            deltaVRight.Add(_deltaVValueLabel);
            deltaVRight.Add(_deltaVGhostLabel);

            deltaVRow.Add(deltaVLeft);
            deltaVRow.Add(deltaVRight);
            telemetryColumn.Add(deltaVRow);

            var barContainer = new VisualElement();
            barContainer.AddToClassList("apogee-bar-container");
            _deltaVBarFill = new VisualElement();
            _deltaVBarFill.AddToClassList("apogee-bar-fill");
            _deltaVBarFill.AddToClassList("apogee-bar-fill--green");
            barContainer.Add(_deltaVBarFill);

            _deltaVGhostBarFill = new VisualElement();
            _deltaVGhostBarFill.style.position = Position.Absolute;
            _deltaVGhostBarFill.style.height = Length.Percent(100);
            _deltaVGhostBarFill.style.opacity = 0.5f;
            _deltaVGhostBarFill.style.backgroundColor = new Color(0.22f, 0.74f, 0.97f);
            barContainer.Add(_deltaVGhostBarFill);

            telemetryColumn.Add(barContainer);

            // Mass Row + Ghost Delta
            var massRow = new VisualElement();
            massRow.AddToClassList("apogee-stat-row");
            var massLabel = new Label("Kütle (Islak)");
            massLabel.AddToClassList("apogee-stat-label");

            var massRight = new VisualElement();
            massRight.style.flexDirection = FlexDirection.Row;
            _massValueLabel = new Label("0 t");
            _massValueLabel.AddToClassList("apogee-stat-value");
            _massGhostLabel = new Label(string.Empty);
            _massGhostLabel.AddToClassList("apogee-ghost-delta");
            massRight.Add(_massValueLabel);
            massRight.Add(_massGhostLabel);

            massRow.Add(massLabel);
            massRow.Add(massRight);
            telemetryColumn.Add(massRow);

            // Cost Row + Ghost Delta
            var costRow = new VisualElement();
            costRow.AddToClassList("apogee-stat-row");
            var costLabel = new Label("Maliyet");
            costLabel.AddToClassList("apogee-stat-label");

            var costRight = new VisualElement();
            costRight.style.flexDirection = FlexDirection.Row;
            _costValueLabel = new Label("$0 M");
            _costValueLabel.AddToClassList("apogee-stat-value");
            _costGhostLabel = new Label(string.Empty);
            _costGhostLabel.AddToClassList("apogee-ghost-delta");
            costRight.Add(_costValueLabel);
            costRight.Add(_costGhostLabel);

            costRow.Add(costLabel);
            costRow.Add(costRight);
            telemetryColumn.Add(costRow);

            // Reliability Row + Ghost Delta
            var relRow = new VisualElement();
            relRow.AddToClassList("apogee-stat-row");
            var relLabel = new Label("Güvenilirlik");
            relLabel.AddToClassList("apogee-stat-label");

            var relRight = new VisualElement();
            relRight.style.flexDirection = FlexDirection.Row;
            _reliabilityValueLabel = new Label("%0");
            _reliabilityValueLabel.AddToClassList("apogee-stat-value");
            _reliabilityGhostLabel = new Label(string.Empty);
            _reliabilityGhostLabel.AddToClassList("apogee-ghost-delta");
            relRight.Add(_reliabilityValueLabel);
            relRight.Add(_reliabilityGhostLabel);

            relRow.Add(relLabel);
            relRow.Add(relRight);
            telemetryColumn.Add(relRow);

            // Power Margin Row + Ghost Delta
            var powerRow = new VisualElement();
            powerRow.AddToClassList("apogee-stat-row");
            var powerLabel = new Label("Güç Dengesi");
            powerLabel.AddToClassList("apogee-stat-label");

            var powerRight = new VisualElement();
            powerRight.style.flexDirection = FlexDirection.Row;
            _powerMarginValueLabel = new Label("0.0 kW");
            _powerMarginValueLabel.AddToClassList("apogee-stat-value");
            _powerMarginGhostLabel = new Label(string.Empty);
            _powerMarginGhostLabel.AddToClassList("apogee-ghost-delta");
            powerRight.Add(_powerMarginValueLabel);
            powerRight.Add(_powerMarginGhostLabel);

            powerRow.Add(powerLabel);
            powerRow.Add(powerRight);
            telemetryColumn.Add(powerRow);

            // Ghost Summary Banner Box (appears during hover)
            _ghostSummaryBox = new VisualElement();
            _ghostSummaryBox.AddToClassList("apogee-ghost-summary-box");
            _ghostSummaryBox.style.display = DisplayStyle.None;

            var ghostSummaryTitle = new Label("ÖNİZLEME (HAYALET DELTA)");
            ghostSummaryTitle.AddToClassList("apogee-ghost-summary-title");
            _ghostSummaryBox.Add(ghostSummaryTitle);

            _ghostSummaryTextLabel = new Label(string.Empty);
            _ghostSummaryTextLabel.AddToClassList("apogee-ghost-summary-text");
            _ghostSummaryBox.Add(_ghostSummaryTextLabel);

            telemetryColumn.Add(_ghostSummaryBox);

            // Review button at bottom
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            telemetryColumn.Add(spacer);

            _reviewButton = new Button(() => ViewModel.GoToReviewCommand.Execute());
            _reviewButton.text = "Tasarım İncelemesine Git (Review) →";
            _reviewButton.AddToClassList("apogee-button");
            _reviewButton.AddToClassList("apogee-button--primary");
            _reviewButton.style.marginTop = 16;
            telemetryColumn.Add(_reviewButton);

            mainContent.Add(telemetryColumn);
            root.Add(mainContent);

            BuildSlotsList();

            return root;
        }

        private void BuildSlotsList()
        {
            _slotListContainer.Clear();

            string currentGroup = null;

            foreach (var slot in ViewModel.Slots)
            {
                var s = slot;

                // Group header if group changed
                if (s.GroupName != currentGroup)
                {
                    currentGroup = s.GroupName;
                    var groupLabel = new Label(currentGroup);
                    groupLabel.AddToClassList("apogee-panel-title");
                    groupLabel.style.fontSize = 11;
                    groupLabel.style.color = new Color(0.58f, 0.64f, 0.72f);
                    groupLabel.style.marginTop = 8;
                    groupLabel.style.marginBottom = 4;
                    _slotListContainer.Add(groupLabel);
                }

                var item = new VisualElement();
                item.AddToClassList("apogee-slot-item");

                var headerRow = new VisualElement();
                headerRow.AddToClassList("apogee-slot-header");

                var title = new Label(s.DisplayName);
                title.AddToClassList("apogee-slot-title");
                headerRow.Add(title);

                if (s.IsRequired)
                {
                    var req = new Label("Zorunlu");
                    req.AddToClassList("apogee-badge");
                    req.AddToClassList("apogee-badge--amber");
                    headerRow.Add(req);
                }

                item.Add(headerRow);

                var equippedLabel = new Label();
                equippedLabel.AddToClassList("apogee-slot-equipped");
                s.EquippedName.Subscribe(val =>
                {
                    equippedLabel.text = val;
                    if (s.EquippedComponent.Value != null)
                    {
                        equippedLabel.RemoveFromClassList("apogee-slot-equipped--empty");
                    }
                    else
                    {
                        equippedLabel.AddToClassList("apogee-slot-equipped--empty");
                    }
                });
                item.Add(equippedLabel);

                var specsLabel = new Label();
                specsLabel.AddToClassList("apogee-slot-specs");
                s.SpecsSummary.Subscribe(val => specsLabel.text = val);
                item.Add(specsLabel);

                // Selection style binding
                s.IsSelected.Subscribe(isSelected =>
                {
                    if (isSelected)
                    {
                        item.AddToClassList("apogee-slot-item--selected");
                    }
                    else
                    {
                        item.RemoveFromClassList("apogee-slot-item--selected");
                    }
                });

                // Click to select slot
                item.RegisterCallback<ClickEvent>(evt =>
                {
                    s.SelectSlotCommand.Execute();
                });

                _slotListContainer.Add(item);
            }
        }

        private void RenderDrawerOptions(IReadOnlyList<ModuleOptionViewModel> options)
        {
            _drawerOptionsList.Clear();

            if (options == null || options.Count == 0)
            {
                var emptyLabel = new Label("Bu slot için uyumlu modül bulunamadı.");
                emptyLabel.AddToClassList("apogee-slot-equipped--empty");
                emptyLabel.style.marginTop = 8;
                _drawerOptionsList.Add(emptyLabel);
                return;
            }

            foreach (var opt in options)
            {
                var option = opt;

                var card = new VisualElement();
                card.AddToClassList("apogee-module-card");

                if (option.IsCurrentlyEquipped.Value)
                {
                    card.AddToClassList("apogee-module-card--equipped");
                }

                var titleRow = new VisualElement();
                titleRow.style.flexDirection = FlexDirection.Row;
                titleRow.style.justifyContent = Justify.SpaceBetween;

                var nameLabel = new Label(option.Name);
                nameLabel.AddToClassList("apogee-module-card-title");
                titleRow.Add(nameLabel);

                if (option.IsCurrentlyEquipped.Value)
                {
                    var eqBadge = new Label("Takılı");
                    eqBadge.AddToClassList("apogee-badge");
                    eqBadge.AddToClassList("apogee-badge--green");
                    titleRow.Add(eqBadge);
                }

                card.Add(titleRow);

                // Badges row
                var badges = new VisualElement();
                badges.AddToClassList("apogee-module-card-badges");

                var massBadge = new Label($"{option.DryMass:F2}t");
                massBadge.AddToClassList("apogee-badge");
                massBadge.AddToClassList("apogee-badge--mini");
                badges.Add(massBadge);

                var costBadge = new Label($"${option.Cost:F1}M");
                costBadge.AddToClassList("apogee-badge");
                costBadge.AddToClassList("apogee-badge--mini");
                badges.Add(costBadge);

                var relBadge = new Label($"%{option.ReliabilityPct:F0}");
                relBadge.AddToClassList("apogee-badge");
                relBadge.AddToClassList("apogee-badge--mini");
                badges.Add(relBadge);

                if (option.PowerGeneration > 0)
                {
                    var pwrBadge = new Label($"+{option.PowerGeneration:F1}kW");
                    pwrBadge.AddToClassList("apogee-badge");
                    pwrBadge.AddToClassList("apogee-badge--cyan");
                    pwrBadge.AddToClassList("apogee-badge--mini");
                    badges.Add(pwrBadge);
                }
                else if (option.PowerConsumption > 0)
                {
                    var pwrBadge = new Label($"-{option.PowerConsumption:F1}kW");
                    pwrBadge.AddToClassList("apogee-badge");
                    pwrBadge.AddToClassList("apogee-badge--mini");
                    badges.Add(pwrBadge);
                }

                if (option.Isp > 0)
                {
                    var ispBadge = new Label($"{option.Isp:F0}s Isp");
                    ispBadge.AddToClassList("apogee-badge");
                    ispBadge.AddToClassList("apogee-badge--mini");
                    badges.Add(ispBadge);
                }

                card.Add(badges);

                // Pointer Hover -> Hayalet Delta
                card.RegisterCallback<PointerEnterEvent>(evt => option.OnHover?.Invoke());
                card.RegisterCallback<PointerLeaveEvent>(evt => option.OnUnhover?.Invoke());

                // Click -> Equip Command
                card.RegisterCallback<ClickEvent>(evt =>
                {
                    if (option.SelectCommand.CanExecute())
                    {
                        option.SelectCommand.Execute();
                    }
                });

                _drawerOptionsList.Add(card);
            }
        }

        protected override void OnInitialized()
        {
            ViewModel.BlueprintName.Subscribe(val => _blueprintNameLabel.text = val);
            ViewModel.TargetDestination.Subscribe(val => _targetLabel.text = $"Hedef: {val}");

            // Undo / Redo states
            ViewModel.CanUndo.Subscribe(canUndo => _undoButton.SetEnabled(canUndo));
            ViewModel.CanRedo.Subscribe(canRedo => _redoButton.SetEnabled(canRedo));

            // Selected Slot & Drawer
            ViewModel.SelectedSlot.Subscribe(slot =>
            {
                if (slot != null)
                {
                    _drawerTitleLabel.text = $"{slot.DisplayName} Seçenekleri";
                    _moduleDrawerContainer.style.display = DisplayStyle.Flex;
                    _resizerHandle.style.display = DisplayStyle.Flex;
                    if (_isMaximized)
                    {
                        _moduleDrawerContainer.style.height = Length.Percent(75);
                    }
                    else
                    {
                        _moduleDrawerContainer.style.height = _drawerHeight;
                    }
                }
                else
                {
                    _moduleDrawerContainer.style.display = DisplayStyle.None;
                    _resizerHandle.style.display = DisplayStyle.None;
                }
            });

            ViewModel.AvailableOptions.Subscribe(RenderDrawerOptions);

            // Telemetry Subscriptions
            ViewModel.TotalDeltaV.Subscribe(val =>
            {
                _deltaVValueLabel.text = $"{val:N0} / {ViewModel.TargetDeltaV.Value:N0} m/s";
                float ratio = (float)(val / Math.Max(1.0, ViewModel.TargetDeltaV.Value));
                _deltaVBarFill.style.width = Length.Percent(Math.Min(100f, ratio * 100f));
            });

            ViewModel.TotalMassTons.Subscribe(val => _massValueLabel.text = $"{val:F1} t");
            ViewModel.TotalCostMillions.Subscribe(val => _costValueLabel.text = $"${val:F1} M");
            ViewModel.ReliabilityPercent.Subscribe(val => _reliabilityValueLabel.text = $"%{val:F1}");
            ViewModel.PowerMarginKw.Subscribe(val =>
            {
                _powerMarginValueLabel.text = val >= 0 ? $"+{val:F1} kW" : $"{val:F1} kW";
                _powerMarginValueLabel.style.color = val >= 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });

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

                _deltaVGhostLabel.text = diff > 0 ? $"(+{diff:N0})" : $"({diff:N0})";
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

                _massGhostLabel.text = diff > 0 ? $"(+{diff:F2}t)" : $"({diff:F2}t)";
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

                _costGhostLabel.text = diff > 0 ? $"(+${diff:F1}M)" : $"(-${Math.Abs(diff):F1}M)";
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

                _reliabilityGhostLabel.text = diff > 0 ? $"(+%{diff:F1})" : $"(-%{Math.Abs(diff):F1})";
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

                _powerMarginGhostLabel.text = diff > 0 ? $"(+{diff:F1}kW)" : $"({diff:F1}kW)";
                _powerMarginGhostLabel.style.color = diff > 0
                    ? new Color(0.2f, 0.83f, 0.6f)
                    : new Color(0.97f, 0.44f, 0.44f);
            });
        }

        private void SetupResizerDrag(VisualElement handle, VisualElement parentColumn)
        {
            bool isDragging = false;
            float startY = 0f;
            float startHeight = _drawerHeight;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                isDragging = true;
                startY = evt.position.y;
                startHeight = _drawerHeight;
                handle.CapturePointer(evt.pointerId);
                handle.AddToClassList("apogee-splitter-handle--dragging");
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!isDragging) return;
                float deltaY = startY - evt.position.y; // Yukarı çektikçe çekmece yüksekliği artar
                float columnHeight = parentColumn.resolvedStyle.height > 0 ? parentColumn.resolvedStyle.height : 700f;
                float maxHeight = columnHeight * 0.85f;
                float newHeight = Mathf.Clamp(startHeight + deltaY, MinDrawerHeight, maxHeight);
                SetDrawerHeight(newHeight);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    handle.ReleasePointer(evt.pointerId);
                    handle.RemoveFromClassList("apogee-splitter-handle--dragging");
                    evt.StopPropagation();
                }
            });

            handle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    handle.ReleasePointer(evt.pointerId);
                    handle.RemoveFromClassList("apogee-splitter-handle--dragging");
                    evt.StopPropagation();
                }
            });
        }

        private void SetDrawerHeight(float height)
        {
            _drawerHeight = height;
            _moduleDrawerContainer.style.height = height;
            _isMaximized = false;
            if (_toggleExpandButton != null) _toggleExpandButton.text = "⤢ Genişlet";
        }

        private void ToggleDrawerMaximize()
        {
            _isMaximized = !_isMaximized;
            if (_isMaximized)
            {
                _moduleDrawerContainer.style.height = Length.Percent(75);
                if (_toggleExpandButton != null) _toggleExpandButton.text = "⤡ Normale Dön";
            }
            else
            {
                _moduleDrawerContainer.style.height = _drawerHeight;
                if (_toggleExpandButton != null) _toggleExpandButton.text = "⤢ Genişlet";
            }
        }
    }
}
