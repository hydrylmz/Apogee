using Apogee.Core.Simulation.Launch;
using System;
using System.Collections.Generic;
using Apogee.Core.Simulation.Cruise;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Report;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Operations
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Operations / Cruise screen (ScreenId.Operations).
    /// Displays live mission cruise progress bar, telemetry log, decision card popups,
    /// EDL touchdown, science downlinks, and completes missions with a full report.
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
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.FlexStart;

            // Header (fixed height)
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.style.flexShrink = 0;
            header.style.marginBottom = 8;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var titleCol = new VisualElement();
            _titleLabel = new Label("OPERASYON VE SEYİR (CRUISE & SURFACE OPS)");
            _titleLabel.AddToClassList("apogee-panel-title");
            _titleLabel.style.fontSize = 15;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            titleCol.Add(_titleLabel);

            _vehicleLabel = new Label("Aktif Misyon...");
            _vehicleLabel.style.fontSize = 12;
            _vehicleLabel.style.color = new Color(0.4f, 0.75f, 0.95f);
            titleCol.Add(_vehicleLabel);
            header.Add(titleCol);

            _statusBadge = new Label("SEYİRDE");
            _statusBadge.AddToClassList("apogee-badge");
            header.Add(_statusBadge);
            root.Add(header);

            // Progress Bar (fixed height)
            var progressPanel = new VisualElement();
            progressPanel.AddToClassList("apogee-panel");
            progressPanel.style.flexShrink = 0;
            progressPanel.style.marginBottom = 8;
            progressPanel.style.paddingTop = 8;
            progressPanel.style.paddingBottom = 8;

            _progressBar = new ProgressBar();
            _progressBar.title = "Seyir İlerlemesi";
            _progressBar.lowValue = 0f;
            _progressBar.highValue = 1f;
            progressPanel.Add(_progressBar);
            root.Add(progressPanel);

            // Decision Card Box (Hidden when no active decision)
            _decisionBox = new VisualElement();
            _decisionBox.AddToClassList("apogee-panel");
            _decisionBox.style.backgroundColor = new Color(0.18f, 0.12f, 0.04f, 0.95f);
            _decisionBox.style.borderLeftWidth = 4;
            _decisionBox.style.borderLeftColor = new Color(0.95f, 0.7f, 0.2f);
            _decisionBox.style.flexShrink = 0;
            _decisionBox.style.marginBottom = 8;
            _decisionBox.style.paddingTop = 10;
            _decisionBox.style.paddingBottom = 10;
            _decisionBox.style.paddingLeft = 14;
            _decisionBox.style.paddingRight = 14;

            _decisionTitle = new Label();
            _decisionTitle.AddToClassList("apogee-panel-title");
            _decisionTitle.style.fontSize = 14;
            _decisionTitle.style.color = new Color(1f, 0.8f, 0.3f);
            _decisionDesc = new Label();
            _decisionDesc.style.fontSize = 12;
            _decisionDesc.style.color = new Color(0.9f, 0.9f, 0.95f);
            _decisionDesc.style.marginBottom = 8;
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
            _eventLogScroll.style.minHeight = 140;
            _eventLogScroll.style.marginBottom = 8;
            _eventLogScroll.AddToClassList("apogee-panel");
            root.Add(_eventLogScroll);

            // Summary & Science Telemetry (fixed height info panel)
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("apogee-panel");
            infoContainer.style.flexShrink = 0;
            infoContainer.style.marginBottom = 8;
            infoContainer.style.paddingTop = 8;
            infoContainer.style.paddingBottom = 8;

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-label-body");
            _summaryLabel.style.fontSize = 12;
            _summaryLabel.style.color = new Color(0.85f, 0.9f, 0.95f);

            _scienceTelemetryLabel = new Label();
            _scienceTelemetryLabel.AddToClassList("apogee-label-body");
            _scienceTelemetryLabel.style.fontSize = 12;
            _scienceTelemetryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scienceTelemetryLabel.style.color = new Color(0.3f, 0.9f, 0.95f, 1f);
            _scienceTelemetryLabel.style.marginTop = 4;

            infoContainer.Add(_summaryLabel);
            infoContainer.Add(_scienceTelemetryLabel);
            root.Add(infoContainer);

            // Footer controls (docked at bottom, colored buttons)
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.flexShrink = 0;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.FlexStart;

            _advanceButton = new Button(() => ViewModel.AdvanceCruiseStepCommand.Execute()) { text = "⏩ Seyri İlerlet" };
            _advanceButton.AddToClassList("apogee-button");
            _advanceButton.AddToClassList("apogee-button--primary");
            _advanceButton.style.marginRight = 8;

            _edlButton = new Button(() => ViewModel.ExecuteEdlStepCommand.Execute()) { text = "🛬 İniş / EDL Başlat" };
            _edlButton.AddToClassList("apogee-button");
            _edlButton.AddToClassList("apogee-button--accent-amber");
            _edlButton.style.marginRight = 8;

            _scienceButton = new Button(() => ViewModel.ExecuteScienceStepCommand.Execute()) { text = "🔬 Bilim Günü İlerlet" };
            _scienceButton.AddToClassList("apogee-button");
            _scienceButton.style.backgroundColor = new Color(0.15f, 0.45f, 0.55f, 1f);
            _scienceButton.style.color = Color.white;
            _scienceButton.style.marginRight = 8;

            _continueButton = new Button(OnContinueClicked) { text = "🏁 Görevi Tamamla & Raporu Gör ▶" };
            _continueButton.AddToClassList("apogee-button");
            _continueButton.style.backgroundColor = new Color(0.18f, 0.55f, 0.3f, 1f);
            _continueButton.style.color = Color.white;
            _continueButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _continueButton.style.marginRight = 8;

            _backButton = new Button(OnBackClicked) { text = "🗺️ Komuta Masası" };
            _backButton.AddToClassList("apogee-button");
            _backButton.style.marginLeft = StyleKeyword.Auto;

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
                _vehicleLabel.text = $"{ViewModel.MissionName.Value} · {ViewModel.VehicleName.Value} → Hedef: {ViewModel.DestinationName.Value}";

            if (_statusBadge != null)
            {
                _statusBadge.text = ViewModel.MissionStatusText.Value;
                _statusBadge.RemoveFromClassList("badge--success");
                _statusBadge.RemoveFromClassList("badge--warning");
                _statusBadge.RemoveFromClassList("badge--danger");

                if (ViewModel.MissionStatusText.Value == "ARRIVAL" || ViewModel.MissionStatusText.Value == "OPERATING" || ViewModel.MissionStatusText.Value == "COMPLETED")
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
                    _decisionTitle.text = $"⚠️ {ViewModel.OperationEventTitle.Value}";
                    _decisionDesc.text = ViewModel.OperationEventNarrative.Value;

                    _decisionOptionsContainer.Clear();
                    foreach (var opt in ViewModel.OperationEventOptions)
                    {
                        var optBtn = new Button(() => ViewModel.ResolveOperationEventOptionCommand.Execute(opt.OptionId))
                        {
                            text = $"{opt.LabelKey}\n({opt.VisibleConsequenceKey})"
                        };
                        optBtn.AddToClassList("apogee-button");
                        optBtn.style.marginRight = 8;
                        optBtn.style.marginBottom = 4;
                        _decisionOptionsContainer.Add(optBtn);
                    }
                }
                else if (showCruiseDecision)
                {
                    _decisionTitle.text = $"⚠️ {ViewModel.DecisionTitle.Value}";
                    _decisionDesc.text = ViewModel.DecisionNarrative.Value;

                    _decisionOptionsContainer.Clear();
                    foreach (var opt in ViewModel.CurrentDecisionOptions)
                    {
                        var optBtn = new Button(() => ViewModel.ResolveOptionCommand.Execute(opt.OptionId))
                        {
                            text = $"{opt.LabelKey}\n({opt.VisibleConsequenceKey})"
                        };
                        optBtn.AddToClassList("apogee-button");
                        optBtn.style.marginRight = 8;
                        optBtn.style.marginBottom = 4;
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
                    row.style.alignItems = Align.Center;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.marginBottom = 3;
                    row.style.backgroundColor = new Color(0.06f, 0.09f, 0.15f, 0.9f);
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = new Color(0.35f, 0.75f, 0.95f);

                    var pLabel = new Label($"[%{(ev.Progress * 100.0):0}]");
                    pLabel.style.minWidth = 55;
                    pLabel.style.fontSize = 11;
                    pLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    pLabel.style.color = new Color(0.4f, 0.85f, 1f);

                    var tLabel = new Label(FormatOperationTitle(ev.TitleKey));
                    tLabel.style.flexGrow = 1;
                    tLabel.style.fontSize = 12;
                    tLabel.style.color = Color.white;

                    var sLabel = new Label(ev.Status.ToString());
                    sLabel.style.minWidth = 70;
                    sLabel.style.fontSize = 11;
                    sLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                    sLabel.style.color = new Color(0.35f, 0.85f, 0.55f);

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                foreach (var edlEv in ViewModel.EdlEventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.backgroundColor = new Color(0.09f, 0.14f, 0.22f, 0.95f);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.alignItems = Align.Center;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.marginBottom = 3;
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = new Color(0.95f, 0.6f, 0.2f);

                    var pLabel = new Label($"[EDL: {edlEv.Phase}]");
                    pLabel.style.minWidth = 140;
                    pLabel.style.marginRight = 10;
                    pLabel.style.fontSize = 11;
                    pLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    pLabel.style.color = new Color(0.95f, 0.75f, 0.35f);

                    var tLabel = new Label(FormatOperationTitle(edlEv.TitleKey));
                    tLabel.style.flexGrow = 1;
                    tLabel.style.fontSize = 12;
                    tLabel.style.color = Color.white;

                    var sLabel = new Label(edlEv.Status.ToString());
                    sLabel.style.minWidth = 70;
                    sLabel.style.fontSize = 11;
                    sLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                    sLabel.style.color = new Color(0.4f, 0.9f, 0.6f);

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                foreach (var sciEv in ViewModel.ScienceEventLog)
                {
                    var row = new VisualElement();
                    row.AddToClassList("launch-event-row");
                    row.style.backgroundColor = new Color(0.06f, 0.14f, 0.18f, 0.95f);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.justifyContent = Justify.SpaceBetween;
                    row.style.alignItems = Align.Center;
                    row.style.paddingTop = 4;
                    row.style.paddingBottom = 4;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.marginBottom = 3;
                    row.style.borderLeftWidth = 3;
                    row.style.borderLeftColor = new Color(0.2f, 0.85f, 0.75f);

                    var pLabel = new Label($"[BİLİM Gün {sciEv.DayNumber}]");
                    pLabel.style.minWidth = 120;
                    pLabel.style.marginRight = 10;
                    pLabel.style.fontSize = 11;
                    pLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    pLabel.style.color = new Color(0.35f, 0.9f, 0.85f);

                    var tLabel = new Label(FormatOperationTitle(sciEv.TitleKey));
                    tLabel.style.flexGrow = 1;
                    tLabel.style.fontSize = 12;
                    tLabel.style.color = Color.white;

                    var sLabel = new Label(sciEv.Status.ToString());
                    sLabel.style.minWidth = 70;
                    sLabel.style.fontSize = 11;
                    sLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                    sLabel.style.color = new Color(0.4f, 0.9f, 0.6f);

                    row.Add(pLabel);
                    row.Add(tLabel);
                    row.Add(sLabel);
                    _eventLogScroll.Add(row);
                }

                // Otomatik olarak listenin en sonuna odaklan
                if (_eventLogScroll.childCount > 0)
                {
                    _eventLogScroll.schedule.Execute(() =>
                    {
                        if (_eventLogScroll.childCount > 0)
                        {
                            _eventLogScroll.ScrollTo(_eventLogScroll[_eventLogScroll.childCount - 1]);
                        }
                    });
                }
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = !string.IsNullOrEmpty(ViewModel.NarrativeSummary.Value)
                    ? $"[DURUM] {ViewModel.NarrativeSummary.Value}"
                    : "Uzay aracı operasyonları nominal seyirde devam ediyor.";
            }

            if (_scienceTelemetryLabel != null)
            {
                if (ViewModel.ScienceDaysCompleted.Value > 0)
                {
                    _scienceTelemetryLabel.text =
                        $"[BİLİM TELEMETRİSİ] Tamamlanan Gün: {ViewModel.ScienceDaysCompleted.Value} | Toplam İndirilen: {ViewModel.DownlinkDeliveredGb.Value:F1} GB | Tampon Bellek: {ViewModel.BufferStorageGb.Value:F1} GB | Kayıp: {ViewModel.DroppedDataGb.Value:F1} GB";
                }
                else
                {
                    _scienceTelemetryLabel.text = string.Empty;
                }
            }

            if (_advanceButton != null) _advanceButton.SetEnabled(ViewModel.CanAdvance.Value);
            if (_edlButton != null) _edlButton.SetEnabled(ViewModel.CanExecuteEdl.Value);
            if (_scienceButton != null) _scienceButton.SetEnabled(ViewModel.CanExecuteScience.Value);
            if (_continueButton != null) _continueButton.SetEnabled(ViewModel.IsComplete.Value);
        }

        private static string FormatOperationTitle(string rawKey)
        {
            if (string.IsNullOrEmpty(rawKey)) return "Nominal Operasyon";

            return rawKey switch
            {
                "CRUISE_SEG_NOMINAL_TITLE"                  => "Derin Uzay Seyri (Nominal)",
                "CRUISE_SEG_ANOMALY_TITLE"                  => "Alt Sistem Uyarısı & Anomali",
                "EDL_EVENT_DEORBIT_NOMINAL_TITLE"           => "Yörüngeden Çıkış Ateşlemesi (Deorbit)",
                "EDL_EVENT_ENTRY_INTERFACE_TITLE"          => "Atmosfere Giriş Arayüzü",
                "EDL_EVENT_HEATSHIELD_PEAK_TITLE"           => "Pik Isı & Plazma Karartması",
                "EDL_EVENT_PARACHUTE_DEPLOY_TITLE"          => "Sesüstü Paraşüt Açılması",
                "EDL_EVENT_TERMINAL_DESCENT_NOMINAL_TITLE"  => "Son Yaklaşma & İtkili Alçalma",
                "EDL_EVENT_TOUCHDOWN_SUCCESS_TITLE"         => "Yüzeye Başarılı Yumuşak İniş",
                "EDL_EVENT_TOUCHDOWN_HARD_TITLE"            => "Sert İniş & Hasar",
                "EDL_EVENT_BURNUP_TITLE"                    => "Atmosferik Yanma & Parçalanma",
                _ => rawKey.Replace("CRUISE_SEG_", "").Replace("EDL_EVENT_", "").Replace("SCI_EVENT_", "").Replace("_TITLE", "").Replace("_", " ")
            };
        }

        private void OnContinueClicked()
        {
            var campaign = ViewModel.Campaign;
            string missionId = ViewModel.MissionId;

            if (campaign != null && !string.IsNullOrEmpty(missionId) && campaign.Missions.TryGetValue(missionId, out var mission))
            {
                // Generate comprehensive report if not present
                if (mission.Report == null)
                {
                    var launchResult = new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.05, 9500, "nominal", "Nominal Fırlatma");
                    var edlResult = mission.EdlResult ?? new Apogee.Core.Simulation.EDL.EdlSimulationResult(
                        outcome: Apogee.Core.Simulation.EDL.EdlOutcome.Success,
                        isCrewSafe: true,
                        remainingHeatShieldIntegrity: 1.0,
                        touchdownVelocityMps: 1.5,
                        primaryCauseKey: "nominal",
                        plainLanguageSummary: "Yumuşak İniş Başarılı");

                    var report = MissionReportEvaluator.Evaluate(
                        missionId: mission.MissionId,
                        missionName: mission.MissionName,
                        destinationId: mission.DestinationId,
                        profile: mission.Profile,
                        blueprint: mission.Blueprint,
                        launchResult: launchResult,
                        edlResult: edlResult,
                        downlinkedGb: mission.AccumulatedDownlinkDataGb,
                        rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                        droppedGb: mission.AccumulatedDroppedDataGb,
                        scienceDaysCompleted: mission.ScienceDaysCompleted,
                        accumulatedReliabilityPenalty: 0.0,
                        accumulatedCorrectionFuel: 0.0,
                        operationEvents: mission.OperationEventLog);

                    mission.Report = report;
                }

                campaign.Execute(new CompleteMissionCommand(campaign, missionId));

                var reportArgs = new MissionReportNavigationArgs(missionId, mission.Report);
                ViewModel.Navigator?.GoTo(ScreenId.MissionReport, reportArgs);
            }
            else
            {
                ViewModel.Navigator?.GoTo(ScreenId.CampaignMap);
            }
        }

        private void OnBackClicked()
        {
            ViewModel.Navigator?.GoTo(ScreenId.CampaignMap);
        }
    }
}
