using System;
using Apogee.Core.Simulation.Cruise;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.Navigation;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Operations
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Operations / Cruise screen (ScreenId.Operations).
    /// Displays live mission cruise progress bar, telemetry log, and decision card popups.
    /// GDD §10.1, §10.2, §14.1 #14.
    /// </summary>
    public sealed class OperationsScreenController : ScreenControllerBase<OperationsViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Operations;

        private Label _titleLabel;
        private Label _vehicleLabel;
        private Label _statusBadge;
        private ProgressBar _progressBar;
        private ScrollView _eventLogScroll;
        private VisualElement _decisionBox;
        private Label _decisionTitle;
        private Label _decisionDesc;
        private VisualElement _decisionOptionsContainer;
        private Label _summaryLabel;
        private Label _scienceTelemetryLabel;
        private Button _advanceButton;
        private Button _edlButton;
        private Button _scienceButton;
        private Button _continueButton;
        private Button _backButton;

        public OperationsScreenController(OperationsViewModel viewModel)
            : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.FlexStart;

            // Header (fixed height)
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.style.flexShrink = 0;
            header.style.marginBottom = 8;

            _titleLabel = new Label("OPERASYON VE SEYİR (CRUISE OPERATIONS)");
            _titleLabel.AddToClassList("apogee-panel-title");
            _vehicleLabel = new Label();
            _statusBadge = new Label("SEYİRDE");

            header.Add(_titleLabel);
            header.Add(_vehicleLabel);
            header.Add(_statusBadge);
            root.Add(header);

            // Progress Bar (fixed height)
            var progressPanel = new VisualElement();
            progressPanel.AddToClassList("apogee-panel");
            progressPanel.style.flexShrink = 0;
            progressPanel.style.marginBottom = 8;
            progressPanel.style.paddingTop = 6;
            progressPanel.style.paddingBottom = 6;

            _progressBar = new ProgressBar();
            _progressBar.title = "Seyir İlerlemesi";
            _progressBar.lowValue = 0f;
            _progressBar.highValue = 1f;
            progressPanel.Add(_progressBar);
            root.Add(progressPanel);

            // Decision Card Box (Hidden when no active decision)
            _decisionBox = new VisualElement();
            _decisionBox.AddToClassList("apogee-panel");
            _decisionBox.style.backgroundColor = new UnityEngine.Color(0.2f, 0.15f, 0.05f, 0.9f);
            _decisionBox.style.flexShrink = 0;
            _decisionBox.style.marginBottom = 8;
            _decisionTitle = new Label();
            _decisionTitle.AddToClassList("apogee-panel-title");
            _decisionDesc = new Label();
            _decisionOptionsContainer = new VisualElement();
            _decisionOptionsContainer.style.flexDirection = FlexDirection.Row;
            _decisionOptionsContainer.style.flexWrap = Wrap.Wrap;

            _decisionBox.Add(_decisionTitle);
            _decisionBox.Add(_decisionDesc);
            _decisionBox.Add(_decisionOptionsContainer);
            root.Add(_decisionBox);

            // Telemetry / Event Log (scrollable, flexible)
            _eventLogScroll = new ScrollView(ScrollViewMode.Vertical);
            _eventLogScroll.style.flexGrow = 1;
            _eventLogScroll.style.flexShrink = 1;
            _eventLogScroll.style.minHeight = 120;
            _eventLogScroll.style.marginBottom = 8;
            _eventLogScroll.AddToClassList("apogee-panel");
            root.Add(_eventLogScroll);

            // Summary & Science Telemetry (fixed height info panel)
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("apogee-panel");
            infoContainer.style.flexShrink = 0;
            infoContainer.style.marginBottom = 8;
            infoContainer.style.paddingTop = 6;
            infoContainer.style.paddingBottom = 6;

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-label-body");
            _scienceTelemetryLabel = new Label();
            _scienceTelemetryLabel.AddToClassList("apogee-label-body");
            _scienceTelemetryLabel.style.color = new UnityEngine.Color(0.2f, 0.8f, 0.9f, 1f);

            infoContainer.Add(_summaryLabel);
            infoContainer.Add(_scienceTelemetryLabel);
            root.Add(infoContainer);

            // Footer controls (docked at bottom, never clips)
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.flexShrink = 0;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.FlexStart;
            footer.style.flexWrap = Wrap.Wrap;

            _advanceButton = new Button(() => ViewModel.AdvanceCruiseStepCommand.Execute()) { text = "Seyri İlerlet" };
            _edlButton = new Button(() => ViewModel.ExecuteEdlStepCommand.Execute()) { text = "İniş / EDL Başlat" };
            _scienceButton = new Button(() => ViewModel.ExecuteScienceStepCommand.Execute()) { text = "Bilim Günü İlerlet" };
            _continueButton = new Button(OnContinueClicked) { text = "Görevi Bitir (Atölye)" };
            _backButton = new Button(OnBackClicked) { text = "← Atölyeye Dön" };

            _advanceButton.style.marginRight = 6;
            _edlButton.style.marginRight = 6;
            _scienceButton.style.marginRight = 6;
            _continueButton.style.marginRight = 6;
            _backButton.style.marginLeft = StyleKeyword.Auto; // Sağ köşeye yanaştır

            footer.Add(_advanceButton);
            footer.Add(_edlButton);
            footer.Add(_scienceButton);
            footer.Add(_continueButton);
            footer.Add(_backButton);
            root.Add(footer);

            ViewModel.PropertyChanged += (s, e) => RefreshView();
            ViewModel.OnStateUpdated += RefreshView;

            RefreshView();
            return root;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            var campaign = ViewModel.Campaign ?? new CampaignState(seed: 123456UL);
            if (context is OperationsNavigationArgs args)
            {
                ViewModel.BindMission(campaign, args.MissionId, args);
            }
            else if (string.IsNullOrEmpty(ViewModel.MissionId))
            {
                // Fallback default navigation if accessed directly without args
                var defaultArgs = new OperationsNavigationArgs(
                    missionId: "default_active_mission",
                    missionName: "Test Görevi",
                    destinationId: "moon",
                    blueprint: null,
                    cruiseDurationDays: 30,
                    spacecraftReliability: 0.95,
                    missionSeed: 123456UL
                );
                ViewModel.BindMission(campaign, defaultArgs.MissionId, defaultArgs);
            }

            RefreshView();
        }

        private void RefreshView()
        {
            if (_vehicleLabel != null)
                _vehicleLabel.text = $"{ViewModel.MissionName.Value} ({ViewModel.VehicleName.Value}) -> {ViewModel.DestinationName.Value}";

            if (_statusBadge != null)
            {
                _statusBadge.text = ViewModel.MissionStatusText.Value;
                _statusBadge.RemoveFromClassList("badge--success");
                _statusBadge.RemoveFromClassList("badge--warning");
                _statusBadge.RemoveFromClassList("badge--danger");

                if (ViewModel.MissionStatusText.Value == "ARRIVAL" || ViewModel.MissionStatusText.Value == "COMPLETED")
                    _statusBadge.AddToClassList("badge--success");
                else if (ViewModel.MissionStatusText.Value == "DECISIONPENDING")
                    _statusBadge.AddToClassList("badge--warning");
                else if (ViewModel.MissionStatusText.Value == "FAILED")
                    _statusBadge.AddToClassList("badge--danger");
            }

            if (_progressBar != null)
            {
                _progressBar.value = (float)ViewModel.CruiseProgress.Value;
                _progressBar.title = $"Seyir İlerlemesi: %{(ViewModel.CruiseProgress.Value * 100.0):0}";
            }

            if (_decisionBox != null)
            {
                bool showCruiseDecision = ViewModel.HasActiveDecision.Value;
                bool showOpEventDecision = ViewModel.HasPendingOperationEvent.Value;

                _decisionBox.style.display = (showCruiseDecision || showOpEventDecision) ? DisplayStyle.Flex : DisplayStyle.None;

                if (showOpEventDecision)
                {
                    _decisionTitle.text = ViewModel.OperationEventTitle.Value;
                    _decisionDesc.text = ViewModel.OperationEventNarrative.Value;

                    _decisionOptionsContainer.Clear();
                    foreach (var opt in ViewModel.OperationEventOptions)
                    {
                        var optBtn = new Button(() => ViewModel.ResolveOperationEventOptionCommand.Execute(opt.OptionId))
                        {
                            text = $"{opt.LabelKey}\n({opt.VisibleConsequenceKey})"
                        };
                        optBtn.style.marginRight = 8;
                        _decisionOptionsContainer.Add(optBtn);
                    }
                }
                else if (showCruiseDecision)
                {
                    _decisionTitle.text = ViewModel.DecisionTitle.Value;
                    _decisionDesc.text = ViewModel.DecisionNarrative.Value;

                    _decisionOptionsContainer.Clear();
                    foreach (var opt in ViewModel.CurrentDecisionOptions)
                    {
                        var optBtn = new Button(() => ViewModel.ResolveOptionCommand.Execute(opt.OptionId))
                        {
                            text = $"{opt.LabelKey}\n({opt.VisibleConsequenceKey})"
                        };
                        optBtn.style.marginRight = 8;
                        _decisionOptionsContainer.Add(optBtn);
                    }
                }
            }

            if (_eventLogScroll != null)
            {
                _eventLogScroll.Clear();
                foreach (var ev in ViewModel.EventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.paddingTop = 2;
                    row.style.paddingBottom = 2;
                    row.style.paddingLeft = 6;
                    row.style.paddingRight = 6;
                    row.style.marginBottom = 2;

                    var pLabel = new Label($"[%{(ev.Progress * 100.0):0}]");
                    pLabel.style.width = 60;
                    var tLabel = new Label(ev.TitleKey);
                    tLabel.style.flexGrow = 1;
                    var sLabel = new Label(ev.Status.ToString());
                    sLabel.style.width = 80;
                    sLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                foreach (var edlEv in ViewModel.EdlEventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.backgroundColor = new UnityEngine.Color(0.12f, 0.18f, 0.25f, 0.8f);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.paddingTop = 2;
                    row.style.paddingBottom = 2;
                    row.style.paddingLeft = 6;
                    row.style.paddingRight = 6;
                    row.style.marginBottom = 2;

                    var pLabel = new Label($"[EDL:{edlEv.Phase}]");
                    pLabel.style.width = 90;
                    var tLabel = new Label(edlEv.TitleKey);
                    tLabel.style.flexGrow = 1;
                    var sLabel = new Label(edlEv.Status.ToString());
                    sLabel.style.width = 80;
                    sLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                foreach (var sciEv in ViewModel.ScienceEventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.backgroundColor = new UnityEngine.Color(0.1f, 0.22f, 0.2f, 0.8f);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.paddingTop = 2;
                    row.style.paddingBottom = 2;
                    row.style.paddingLeft = 6;
                    row.style.paddingRight = 6;
                    row.style.marginBottom = 2;

                    var pLabel = new Label($"[BİLİM:GÜN {sciEv.DayNumber}]");
                    pLabel.style.width = 110;
                    var tLabel = new Label(sciEv.TitleKey);
                    tLabel.style.flexGrow = 1;
                    var sLabel = new Label(sciEv.Status.ToString());
                    sLabel.style.width = 80;
                    sLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                foreach (var opEv in ViewModel.OperationEventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.backgroundColor = new UnityEngine.Color(0.25f, 0.18f, 0.08f, 0.8f);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.paddingTop = 2;
                    row.style.paddingBottom = 2;
                    row.style.paddingLeft = 6;
                    row.style.paddingRight = 6;
                    row.style.marginBottom = 2;

                    var pLabel = new Label($"[OLAY:{opEv.Phase}]");
                    pLabel.style.width = 110;
                    var tLabel = new Label(opEv.Record.TitleKey);
                    tLabel.style.flexGrow = 1;
                    var sLabel = new Label(opEv.ChosenOptionId ?? "RESOLVED");
                    sLabel.style.width = 80;
                    sLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                // Otomatik olarak listenin en sonuna odaklan
                if (_eventLogScroll.childCount > 0)
                {
                    _eventLogScroll.ScrollTo(_eventLogScroll[_eventLogScroll.childCount - 1]);
                }
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = ViewModel.NarrativeSummary.Value;
            }

            if (_scienceTelemetryLabel != null)
            {
                if (ViewModel.ScienceDaysCompleted.Value > 0)
                {
                    _scienceTelemetryLabel.text =
                        $"[BİLİM TELEMETRİSİ] Gün: {ViewModel.ScienceDaysCompleted.Value} | İndirilen: {ViewModel.DownlinkDeliveredGb.Value:F1} GB | Tampon: {ViewModel.BufferStorageGb.Value:F1} GB | Kayıp: {ViewModel.DroppedDataGb.Value:F1} GB";
                }
                else
                {
                    _scienceTelemetryLabel.text = string.Empty;
                }
            }

            if (_advanceButton != null)
            {
                _advanceButton.SetEnabled(ViewModel.CanAdvance.Value);
            }

            if (_edlButton != null)
            {
                _edlButton.SetEnabled(ViewModel.CanExecuteEdl.Value);
            }

            if (_scienceButton != null)
            {
                _scienceButton.SetEnabled(ViewModel.CanExecuteScience.Value);
            }

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(ViewModel.IsComplete.Value);
            }
        }

        private void OnContinueClicked()
        {
            // Görev tamamlandığında veya sonlandırıldığında Sonuç Raporu ekranına yönlendir (GDD §10.5, §14.1 #16)
            if (!string.IsNullOrEmpty(ViewModel.MissionId))
            {
                ViewModel.Navigator?.GoTo(ScreenId.MissionReport, ViewModel.MissionId);
            }
            else
            {
                ViewModel.Navigator?.GoTo(ScreenId.Workshop);
            }
        }

        private void OnBackClicked()
        {
            ViewModel.Navigator?.GoTo(ScreenId.Workshop);
        }
    }
}
