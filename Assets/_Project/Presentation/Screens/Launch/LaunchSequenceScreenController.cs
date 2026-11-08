using System;
using Apogee.Core.Simulation.Launch;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Operations;
using UnityEngine;
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
            root.style.paddingLeft = 24;
            root.style.paddingRight = 24;
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // Header
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var titleCol = new VisualElement();
            _titleLabel = new Label("FIRLATMA OPERASYONU (LAUNCH SEQUENCE)");
            _titleLabel.AddToClassList("apogee-panel-title");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            titleCol.Add(_titleLabel);

            _vehicleLabel = new Label("Araç & Görev Yükleniyor...");
            _vehicleLabel.style.fontSize = 12;
            _vehicleLabel.style.color = new Color(0.4f, 0.75f, 0.95f);
            titleCol.Add(_vehicleLabel);
            header.Add(titleCol);

            var badgeCol = new VisualElement();
            badgeCol.style.flexDirection = FlexDirection.Row;
            badgeCol.style.alignItems = Align.Center;

            _weatherLabel = new Label("Hava: Nominal");
            _weatherLabel.AddToClassList("apogee-badge");
            _weatherLabel.style.marginRight = 10;
            badgeCol.Add(_weatherLabel);

            _statusBadge = new Label("HAZIR");
            _statusBadge.AddToClassList("apogee-badge");
            badgeCol.Add(_statusBadge);

            header.Add(badgeCol);
            root.Add(header);

            // Telemetry metrics HUD
            var telemPanel = new VisualElement();
            telemPanel.AddToClassList("apogee-panel");
            telemPanel.style.flexDirection = FlexDirection.Row;
            telemPanel.style.justifyContent = Justify.SpaceAround;
            telemPanel.style.alignItems = Align.Center;
            telemPanel.style.paddingTop = 12;
            telemPanel.style.paddingBottom = 12;
            telemPanel.style.marginTop = 8;
            telemPanel.style.marginBottom = 8;
            telemPanel.style.backgroundColor = new Color(0.06f, 0.09f, 0.16f, 0.95f);

            var altBox = new VisualElement { style = { alignItems = Align.Center } };
            var altTitle = new Label("İRTİFA (ALTITUDE)") { style = { fontSize = 10, color = new Color(0.45f, 0.6f, 0.75f) } };
            _altitudeLabel = new Label("0.0 km") { style = { fontSize = 24, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.35f, 0.9f, 1f) } };
            altBox.Add(altTitle);
            altBox.Add(_altitudeLabel);
            telemPanel.Add(altBox);

            var divider = new VisualElement { style = { width = 1, height = 36, backgroundColor = new Color(0.18f, 0.25f, 0.38f, 0.8f) } };
            telemPanel.Add(divider);

            var velBox = new VisualElement { style = { alignItems = Align.Center } };
            var velTitle = new Label("HIZ (VELOCITY)") { style = { fontSize = 10, color = new Color(0.45f, 0.6f, 0.75f) } };
            _velocityLabel = new Label("0 m/s") { style = { fontSize = 24, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.35f, 0.95f, 0.65f) } };
            velBox.Add(velTitle);
            velBox.Add(_velocityLabel);
            telemPanel.Add(velBox);

            root.Add(telemPanel);

            // Event log (Timeline cards)
            _eventListScroll = new ScrollView(ScrollViewMode.Vertical);
            _eventListScroll.style.flexGrow = 1;
            _eventListScroll.style.minHeight = 180;
            _eventListScroll.style.paddingTop = 8;
            _eventListScroll.style.paddingBottom = 8;
            _eventListScroll.AddToClassList("apogee-panel");
            root.Add(_eventListScroll);

            // Summary
            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-panel");
            _summaryLabel.style.fontSize = 12;
            _summaryLabel.style.color = new Color(0.85f, 0.9f, 0.95f);
            _summaryLabel.style.marginTop = 8;
            _summaryLabel.style.marginBottom = 8;
            _summaryLabel.style.paddingTop = 8;
            _summaryLabel.style.paddingBottom = 8;
            _summaryLabel.style.paddingLeft = 12;
            _summaryLabel.style.paddingRight = 12;
            root.Add(_summaryLabel);

            // Controls
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;

            var leftBtns = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            _nextStepButton = new Button(() => ViewModel.StepNextPhaseCommand.Execute()) { text = "▶ İleri (Sonraki Aşama)" };
            _nextStepButton.AddToClassList("apogee-button");
            _nextStepButton.AddToClassList("apogee-button--primary");
            _nextStepButton.style.marginRight = 8;
            leftBtns.Add(_nextStepButton);

            _skipButton = new Button(() => ViewModel.SkipToEndCommand.Execute()) { text = "⏭ Sekansı Atla" };
            _skipButton.AddToClassList("apogee-button");
            leftBtns.Add(_skipButton);

            footer.Add(leftBtns);

            _continueButton = new Button(OnContinueClicked) { text = "Seyre Devam Et (Operations) ▶" };
            _continueButton.AddToClassList("apogee-button");
            _continueButton.AddToClassList("apogee-button--accent-amber");
            _continueButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _continueButton.style.fontSize = 13;
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
            if (_vehicleLabel != null) _vehicleLabel.text = $"{ViewModel.MissionName} · {ViewModel.VehicleName}";
            if (_weatherLabel != null) _weatherLabel.text = $"Hava: {ViewModel.WeatherSummary}";
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
                _summaryLabel.text = !string.IsNullOrEmpty(ViewModel.CauseAndEffectSummary)
                    ? $"[UÇUŞ ÖZETİ] {ViewModel.CauseAndEffectSummary}"
                    : "Fırlatma sekansı geri sayımda...";
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
                    itemElem.style.flexDirection = FlexDirection.Row;
                    itemElem.style.justifyContent = Justify.SpaceBetween;
                    itemElem.style.alignItems = Align.Center;
                    itemElem.style.paddingTop = 6;
                    itemElem.style.paddingBottom = 6;
                    itemElem.style.paddingLeft = 12;
                    itemElem.style.paddingRight = 12;
                    itemElem.style.marginBottom = 4;
                    itemElem.style.backgroundColor = new Color(0.08f, 0.12f, 0.19f, 0.9f);
                    itemElem.style.borderTopLeftRadius = 4;
                    itemElem.style.borderTopRightRadius = 4;
                    itemElem.style.borderBottomLeftRadius = 4;
                    itemElem.style.borderBottomRightRadius = 4;
                    itemElem.style.borderLeftWidth = 3;
                    itemElem.style.borderLeftColor = ev.Status == LaunchPhaseStatus.Nominal
                        ? new Color(0.2f, 0.8f, 0.6f)
                        : new Color(0.95f, 0.4f, 0.3f);

                    var leftBlock = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

                    var timeLbl = new Label(ev.TimeLabel);
                    timeLbl.style.width = 65;
                    timeLbl.style.fontSize = 11;
                    timeLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    timeLbl.style.color = new Color(0.35f, 0.85f, 1f);
                    leftBlock.Add(timeLbl);

                    var titleLbl = new Label(FormatLaunchTitle(ev.TitleKey));
                    titleLbl.style.fontSize = 12;
                    titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    titleLbl.style.color = Color.white;
                    leftBlock.Add(titleLbl);

                    itemElem.Add(leftBlock);

                    var telemLbl = new Label(ev.TelemetryLabel);
                    telemLbl.style.fontSize = 11;
                    telemLbl.style.color = new Color(0.55f, 0.75f, 0.9f);
                    itemElem.Add(telemLbl);

                    _eventListScroll.Add(itemElem);
                }

                if (_eventListScroll.childCount > 0)
                {
                    _eventListScroll.schedule.Execute(() =>
                    {
                        if (_eventListScroll.childCount > 0)
                        {
                            _eventListScroll.ScrollTo(_eventListScroll[_eventListScroll.childCount - 1]);
                        }
                    });
                }
            }

            if (_continueButton != null)
            {
                _continueButton.SetEnabled(ViewModel.IsSequenceFinished);
            }
        }

        private static string FormatLaunchTitle(string rawKey)
        {
            if (string.IsNullOrEmpty(rawKey)) return "Nominal Aşama";

            return rawKey switch
            {
                "LAUNCH_EVENT_IGNITION_TITLE"           => "Motor Ateşleme & Kalkış",
                "LAUNCH_EVENT_MAXQ_TITLE"               => "Maksimum Dinamik Basınç (Max-Q)",
                "LAUNCH_EVENT_MECO_TITLE"               => "1. Aşama Motor Kapanışı (MECO)",
                "LAUNCH_EVENT_STAGE1_SEP_TITLE"         => "1. Aşama Ayrılması (Staging)",
                "LAUNCH_EVENT_STAGE2_IGNITION_TITLE"    => "2. Aşama Motor Ateşlemesi",
                "LAUNCH_EVENT_FAIRING_JETTISON_TITLE"   => "Aerodinamik Kaporta (Fairing) Ayrılması",
                "LAUNCH_EVENT_SECO1_TITLE"              => "2. Aşama Motor Kapanışı (SECO-1)",
                "LAUNCH_EVENT_INSERTION_ANOMALY_TITLE"  => "Yörüngeye Yerleşme Sapması / Anomali",
                "LAUNCH_EVENT_ORBIT_SUCCESS_TITLE"      => "Hedef Yörüngeye Başarılı Giriş",
                "LAUNCH_EVENT_SCRUB_WEATHER_TITLE"      => "Hava Muhalefeti Nedeniyle Geri Sayım İptali",
                "LAUNCH_EVENT_PAD_ABORT_TITLE"          => "Rampa Kaçış Sistemi Devrede (Pad Abort)",
                "LAUNCH_EVENT_STAGE_FAILURE_TITLE"      => "Aşama İtme / Ayrılma Arızası",
                _ => rawKey.Replace("LAUNCH_EVENT_", "").Replace("_TITLE", "").Replace("_", " ")
            };
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
