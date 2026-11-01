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
        private Button _advanceButton;
        private Button _continueButton;

        public OperationsScreenController(OperationsViewModel viewModel)
            : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // Header
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");

            _titleLabel = new Label("OPERASYON VE SEYİR (CRUISE OPERATIONS)");
            _titleLabel.AddToClassList("apogee-panel-title");
            _vehicleLabel = new Label();
            _statusBadge = new Label("SEYİRDE");

            header.Add(_titleLabel);
            header.Add(_vehicleLabel);
            header.Add(_statusBadge);
            root.Add(header);

            // Progress Bar
            var progressPanel = new VisualElement();
            progressPanel.AddToClassList("apogee-panel");
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
            _decisionTitle = new Label();
            _decisionTitle.AddToClassList("apogee-panel-title");
            _decisionDesc = new Label();
            _decisionOptionsContainer = new VisualElement();
            _decisionOptionsContainer.style.flexDirection = FlexDirection.Row;

            _decisionBox.Add(_decisionTitle);
            _decisionBox.Add(_decisionDesc);
            _decisionBox.Add(_decisionOptionsContainer);
            root.Add(_decisionBox);

            // Telemetry / Event Log
            _eventLogScroll = new ScrollView();
            _eventLogScroll.style.flexGrow = 1;
            root.Add(_eventLogScroll);

            // Summary
            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-label-body");
            root.Add(_summaryLabel);

            // Footer controls
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.flexDirection = FlexDirection.Row;

            _advanceButton = new Button(() => ViewModel.AdvanceCruiseStepCommand.Execute()) { text = "Seyri İlerlet" };
            _continueButton = new Button(OnContinueClicked) { text = "Devam Et (Haritaya Dön)" };

            footer.Add(_advanceButton);
            footer.Add(_continueButton);
            root.Add(footer);

            ViewModel.PropertyChanged += (s, e) => RefreshView();
            ViewModel.OnStateUpdated += RefreshView;

            RefreshView();
            return root;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            if (context is OperationsNavigationArgs args)
            {
                var campaign = ViewModel.Campaign ?? new CampaignState(seed: 123456UL);
                ViewModel.BindMission(campaign, args.MissionId, args);
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
                _decisionBox.style.display = ViewModel.HasActiveDecision.Value ? DisplayStyle.Flex : DisplayStyle.None;
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

            if (_eventLogScroll != null)
            {
                _eventLogScroll.Clear();
                foreach (var ev in ViewModel.EventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    var pLabel = new Label($"[%{(ev.Progress * 100.0):0}]");
                    var tLabel = new Label(ev.TitleKey);
                    var sLabel = new Label(ev.Status.ToString());

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = ViewModel.NarrativeSummary.Value;
            }

            if (_advanceButton != null)
            {
                _advanceButton.SetEnabled(ViewModel.CanAdvance.Value);
            }

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(ViewModel.IsComplete.Value);
            }
        }

        private void OnContinueClicked()
        {
            ViewModel.Navigator?.GoTo(ScreenId.CampaignMap);
        }
    }
}
