using System;
using Apogee.Core.Simulation.Launch;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Operations;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Launch Sequence screen (ScreenId.LaunchSequence).
    /// Renders real-time telemetry meters, visual countdown/events, status badges,
    /// and plain language cause-and-effect narrative summary (GDD §10.1, §14.1 #13).
    /// </summary>
    public sealed class LaunchSequenceScreenController : ScreenControllerBase<LaunchSequenceViewModel>
    {
        public override ScreenId ScreenId => ScreenId.LaunchSequence;

        private Label _titleLabel;
        private Label _vehicleLabel;
        private Label _weatherLabel;
        private Label _altitudeLabel;
        private Label _velocityLabel;
        private Label _statusBadge;
        private Label _summaryLabel;
        private ScrollView _eventListScroll;
        private Button _nextStepButton;
        private Button _skipButton;
        private Button _continueButton;

        private LaunchNavigationArgs _lastLaunchArgs;

        public LaunchSequenceScreenController(LaunchSequenceViewModel viewModel)
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

            _titleLabel = new Label("FIRLATMA OPERASYONU (LAUNCH SEQUENCE)");
            _titleLabel.AddToClassList("apogee-panel-title");
            _vehicleLabel = new Label();
            _weatherLabel = new Label();
            _statusBadge = new Label("HAZIR");

            header.Add(_titleLabel);
            header.Add(_vehicleLabel);
            header.Add(_weatherLabel);
            header.Add(_statusBadge);
            root.Add(header);

            // Telemetry metrics
            var telemPanel = new VisualElement();
            telemPanel.AddToClassList("apogee-panel");
            _altitudeLabel = new Label("0.0 km");
            _velocityLabel = new Label("0 m/s");
            telemPanel.Add(_altitudeLabel);
            telemPanel.Add(_velocityLabel);
            root.Add(telemPanel);

            // Event log
            _eventListScroll = new ScrollView();
            _eventListScroll.style.flexGrow = 1;
            root.Add(_eventListScroll);

            // Summary
            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-label-body");
            root.Add(_summaryLabel);

            // Controls
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.flexDirection = FlexDirection.Row;

            _nextStepButton = new Button(() => ViewModel.StepNextPhaseCommand.Execute()) { text = "İleri (Sonraki Aşama)" };
            _skipButton = new Button(() => ViewModel.SkipToEndCommand.Execute()) { text = "Sekansı Atla" };
            _continueButton = new Button(OnContinueClicked) { text = "Devam Et" };

            footer.Add(_nextStepButton);
            footer.Add(_skipButton);
            footer.Add(_continueButton);
            root.Add(footer);

            ViewModel.PropertyChanged += (s, e) => RefreshView();
            ViewModel.OnSequenceCompleted += RefreshView;

            RefreshView();
            return root;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            if (context is LaunchNavigationArgs args)
            {
                _lastLaunchArgs = args;
                var launchParams = args.Blueprint != null
                    ? args.Blueprint.ToLaunchParameters(
                        stats: args.Stats,
                        padWeatherRisk: args.WeatherRisk,
                        equatorialBonusDv: args.EquatorialBonusDv,
                        isCrewed: args.IsCrewed,
                        seed: args.LaunchSeed != 0 ? args.LaunchSeed : 1234567UL)
                    : new LaunchSimulationParameters(
                        missionId: "mission_preview",
                        vehicleName: args.MissionName,
                        stageCount: 2,
                        stageReliabilities: new[] { 0.95, 0.95 },
                        overallReliability: 0.95,
                        launchPadWeatherRisk: args.WeatherRisk,
                        seed: args.LaunchSeed != 0 ? args.LaunchSeed : 1234567UL);

                var simResult = LaunchSimulator.Simulate(launchParams);
                ViewModel.BindResult(args.MissionName, args.Blueprint?.Name ?? "Araç", simResult);
            }

            RefreshView();
        }

        private void RefreshView()
        {
            if (_vehicleLabel != null) _vehicleLabel.text = $"{ViewModel.MissionName} - {ViewModel.VehicleName}";
            if (_weatherLabel != null) _weatherLabel.text = ViewModel.WeatherSummary;
            if (_altitudeLabel != null) _altitudeLabel.text = $"{ViewModel.CurrentAltitudeKm:0.#} km";
            if (_velocityLabel != null) _velocityLabel.text = $"{ViewModel.CurrentVelocityMps:0} m/s";

            if (_statusBadge != null)
            {
                _statusBadge.text = ViewModel.Outcome.ToString().ToUpperInvariant();
                _statusBadge.RemoveFromClassList("badge--success");
                _statusBadge.RemoveFromClassList("badge--warning");
                _statusBadge.RemoveFromClassList("badge--danger");

                if (ViewModel.Outcome == LaunchOutcome.Success)
                    _statusBadge.AddToClassList("badge--success");
                else if (ViewModel.Outcome == LaunchOutcome.PartialOrbit || ViewModel.Outcome == LaunchOutcome.WeatherScrub)
                    _statusBadge.AddToClassList("badge--warning");
                else
                    _statusBadge.AddToClassList("badge--danger");
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = ViewModel.CauseAndEffectSummary;
            }

            if (_eventListScroll != null && ViewModel.Events != null)
            {
                _eventListScroll.Clear();
                int maxIdx = Math.Min(ViewModel.CurrentPhaseIndex, ViewModel.Events.Count - 1);
                for (int i = 0; i <= maxIdx; i++)
                {
                    var ev = ViewModel.Events[i];
                    var itemElem = new VisualElement();
                    itemElem.AddToClassList("launch-event-row");

                    var timeLbl = new Label(ev.TimeLabel);
                    var titleLbl = new Label(ev.TitleKey);
                    var telemLbl = new Label(ev.TelemetryLabel);

                    itemElem.Add(timeLbl);
                    itemElem.Add(titleLbl);
                    itemElem.Add(telemLbl);
                    _eventListScroll.Add(itemElem);
                }
            }

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(ViewModel.IsSequenceFinished);
            }
        }

        private void OnContinueClicked()
        {
            if (ViewModel.Outcome == LaunchOutcome.Success || ViewModel.Outcome == LaunchOutcome.PartialOrbit)
            {
                var opsArgs = new OperationsNavigationArgs(
                    missionId: "mission_" + (_lastLaunchArgs?.Blueprint?.Id ?? "active"),
                    missionName: ViewModel.MissionName,
                    destinationId: _lastLaunchArgs?.MissionName?.ToLowerInvariant() ?? "moon",
                    blueprint: _lastLaunchArgs?.Blueprint,
                    cruiseDurationDays: 30,
                    spacecraftReliability: _lastLaunchArgs?.Stats?.Reliability ?? 0.95,
                    missionSeed: _lastLaunchArgs?.LaunchSeed != 0 ? _lastLaunchArgs.LaunchSeed : 1234567UL
                );
                ViewModel.Navigator?.GoTo(ScreenId.Operations, opsArgs);
            }
            else
            {
                ViewModel.Navigator?.GoTo(ScreenId.Workshop);
            }
        }
    }
}
