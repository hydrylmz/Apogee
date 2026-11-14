using System;
using System.Collections.Generic;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.Core.Simulation.Cruise;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Science;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Audio;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Vehicle;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.Report;
using Apogee.UI.Screens.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Operations
{
    /// <summary>
    /// UI Toolkit Screen Controller for Flight Theatre Phase 4: Operations, Cruise, EDL & Science.
    /// Orchestrates full-screen cinematic stage, dynamic telemetry HUD, narrative captions,
    /// interactive phase rail, decision cards, and deterministic simulation playback.
    /// GDD §3.4, §3.5, §3.6, §10.1, §10.2, §14.1 #14.
    /// </summary>
    public sealed class OperationsScreenController : ScreenControllerBase<OperationsViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Operations;

        // Stage & Overlays
        private FlightStageView _stageView;
        private PhaseRailView _phaseRail;
        private FlightHudView _hudView;
        private FlightCaptionView _captionView;

        // Decision Card Modal Overlay
        private VisualElement _decisionModalOverlay;
        private Label _decisionCardBadge;
        private Label _decisionCardTitle;
        private Label _decisionCardNarrative;
        private VisualElement _decisionOptionsContainer;

        // Footer Playback Controls
        private Button _playPauseBtn;
        private Button _speedBtn;
        private Button _stepBtn;
        private Button _skipBtn;
        private Button _rewatchBtn;
        private Button _captionToggleBtn;

        // Footer Mission Actions
        private Button _advanceCruiseBtn;
        private Button _autoCruiseBtn;
        private bool _isAutoCruiseActive;
        private Button _edlBtn;
        private Button _scienceBtn;
        private Button _continueBtn;
        private Button _backBtn;

        // Playback Engine & Timelines
        private readonly PlaybackClock _clock = new PlaybackClock();
        private FlightBeat[] _currentBeats;
        private float _currentSpeed = 1.0f;
        private IVisualElementScheduledItem _tickSchedule;
        private UnityAudioService _audioService;

        // ── Flight Theatre Audio Director & Performance Budget (ADR-061) ──────
        private FlightAudioDirector _audioDirector;
        private readonly FlightPerformanceBudget _budget = new FlightPerformanceBudget();
        private Label _perfLabel;
        private int _perfLabelCooldown;

        // Navigation & State
        private OperationsNavigationArgs _lastArgs;
        private string _destinationId = "moon";
        private string _missionId;
        private GameSettingsData _settings;

        public OperationsScreenController(OperationsViewModel viewModel)
            : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.overflow = Overflow.Hidden;

            // 1. Stage view (Painter2D canvas background)
            _stageView = new FlightStageView(reduceMotion: false);
            _stageView.SetPerformanceBudget(_budget);
            root.Add(_stageView);

            // 2. Main overlay container (Row: Left PhaseRail, Right Theater Content)
            var overlayRow = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    flexDirection = FlexDirection.Row
                }
            };

            // Left Phase Rail
            _phaseRail = new PhaseRailView();
            _phaseRail.SetHeaderTitle("SEYİR VE OPERASYON AŞAMALARI");
            _phaseRail.OnBeatSelected += OnPhaseRailBeatSelected;
            overlayRow.Add(_phaseRail);

            // Right content area (HUD on top, Decision Card in middle, Captions lower, Controls at bottom)
            var rightArea = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    position = Position.Relative,
                    justifyContent = Justify.SpaceBetween
                }
            };

            // Top HUD
            _hudView = new FlightHudView();
            _hudView.SetWeatherVisible(false);
            rightArea.Add(_hudView);

            // Middle: Floating Decision Card Modal Overlay
            _decisionModalOverlay = CreateDecisionModal();
            rightArea.Add(_decisionModalOverlay);

            // Middle / Lower Captions
            _captionView = new FlightCaptionView();
            _captionView.OnBeatSelected += OnPhaseRailBeatSelected;
            rightArea.Add(_captionView);

            // Bottom Playback & Operations Footer
            var footer = CreateFooter();
            rightArea.Add(footer);

            overlayRow.Add(rightArea);
            root.Add(overlayRow);

            // Keyboard shortcut support
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.RegisterCallback<WheelEvent>(OnStageWheel);

            // Wire clock events
            _clock.BeatAdvanced += OnBeatAdvanced;
            _clock.SequenceFinished += OnClockSequenceFinished;
            _clock.DecisionRequired += OnClockDecisionRequired;

            // Wire ViewModel
            ViewModel.PropertyChanged += (s, e) => SyncUIFromMissionState();
            ViewModel.OnStateUpdated += SyncUIFromMissionState;

            return root;
        }

        private VisualElement CreateFooter()
        {
            var footer = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    bottom = 0,
                    left = 0,
                    right = 0,
                    height = 56,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center,
                    paddingLeft = 16,
                    paddingRight = 16,
                    backgroundColor = new Color(0.04f, 0.06f, 0.10f, 0.94f),
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.12f, 0.18f, 0.28f, 0.8f)
                }
            };

            // Left: Playback controls group
            var controlsGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            _playPauseBtn = new Button(OnPlayPauseClicked) { text = "Oynat" };
            _playPauseBtn.AddToClassList("apogee-button");
            _playPauseBtn.style.minWidth = 90;
            _playPauseBtn.style.marginRight = 6;
            controlsGroup.Add(_playPauseBtn);

            _speedBtn = new Button(OnSpeedClicked) { text = "1×" };
            _speedBtn.AddToClassList("apogee-button");
            _speedBtn.style.minWidth = 46;
            _speedBtn.style.marginRight = 6;
            controlsGroup.Add(_speedBtn);

            _stepBtn = new Button(OnStepClicked) { text = "Adım" };
            _stepBtn.AddToClassList("apogee-button");
            _stepBtn.style.minWidth = 70;
            _stepBtn.style.marginRight = 6;
            controlsGroup.Add(_stepBtn);

            _skipBtn = new Button(OnSkipClicked) { text = "Atla" };
            _skipBtn.AddToClassList("apogee-button");
            _skipBtn.style.minWidth = 70;
            _skipBtn.style.marginRight = 6;
            controlsGroup.Add(_skipBtn);

            _rewatchBtn = new Button(OnRewatchClicked) { text = "Yeniden İzle" };
            _rewatchBtn.AddToClassList("apogee-button");
            _rewatchBtn.style.minWidth = 100;
            _rewatchBtn.style.marginRight = 10;
            controlsGroup.Add(_rewatchBtn);

            _captionToggleBtn = new Button(OnCaptionToggleClicked) { text = "Altyazı" };
            _captionToggleBtn.AddToClassList("apogee-button");
            _captionToggleBtn.style.minWidth = 110;
            controlsGroup.Add(_captionToggleBtn);

            footer.Add(controlsGroup);

            // WebGL performance budget overlay (ADR-061, dev audit)
            _perfLabel = new Label { text = string.Empty };
            _perfLabel.style.fontSize = 11;
            _perfLabel.style.opacity = 0.65f;
            _perfLabel.style.marginRight = 12;
            _perfLabel.style.color = new Color(0.55f, 0.75f, 0.85f);
            footer.Add(_perfLabel);

            // Right: Mission actions group
            var actionGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            _advanceCruiseBtn = new Button(OnAdvanceCruiseClicked) { text = "Seyri İlerlet" };
            _advanceCruiseBtn.AddToClassList("apogee-button");
            _advanceCruiseBtn.AddToClassList("apogee-button--primary");
            _advanceCruiseBtn.style.marginRight = 6;
            actionGroup.Add(_advanceCruiseBtn);

            _autoCruiseBtn = new Button(OnAutoCruiseToggleClicked) { text = "⚡ Otomatik Seyir: Kapalı" };
            _autoCruiseBtn.AddToClassList("apogee-button");
            _autoCruiseBtn.style.marginRight = 8;
            actionGroup.Add(_autoCruiseBtn);

            _edlBtn = new Button(OnEdlClicked) { text = "🛬 İniş / EDL Başlat" };
            _edlBtn.AddToClassList("apogee-button");
            _edlBtn.AddToClassList("apogee-button--accent-amber");
            _edlBtn.style.marginRight = 8;
            actionGroup.Add(_edlBtn);

            _scienceBtn = new Button(OnScienceClicked) { text = "🔬 Bilim Günü İlerlet" };
            _scienceBtn.AddToClassList("apogee-button");
            _scienceBtn.style.backgroundColor = new Color(0.15f, 0.45f, 0.55f, 1f);
            _scienceBtn.style.color = Color.white;
            _scienceBtn.style.marginRight = 8;
            actionGroup.Add(_scienceBtn);

            _continueBtn = new Button(OnContinueClicked) { text = "Görevi Tamamla & Raporu Gör ▶" };
            _continueBtn.AddToClassList("apogee-button");
            _continueBtn.style.backgroundColor = new Color(0.18f, 0.55f, 0.3f, 1f);
            _continueBtn.style.color = Color.white;
            _continueBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _continueBtn.style.marginRight = 8;
            actionGroup.Add(_continueBtn);

            _backBtn = new Button(OnBackClicked) { text = "🗺️ Komuta Masası" };
            _backBtn.AddToClassList("apogee-button");
            actionGroup.Add(_backBtn);

            footer.Add(actionGroup);

            return footer;
        }

        private VisualElement CreateDecisionModal()
        {
            var overlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 60,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    backgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.75f),
                    display = DisplayStyle.None
                }
            };

            var card = new VisualElement
            {
                style =
                {
                    width = 540,
                    maxWidth = Length.Percent(90),
                    backgroundColor = new Color(0.08f, 0.11f, 0.18f, 0.98f),
                    borderLeftWidth = 4,
                    borderLeftColor = new Color(0.961f, 0.620f, 0.043f),
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.20f, 0.30f, 0.45f, 0.6f),
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.20f, 0.30f, 0.45f, 0.6f),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.20f, 0.30f, 0.45f, 0.6f),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 16,
                    paddingBottom = 16,
                    paddingLeft = 20,
                    paddingRight = 20
                }
            };

            _decisionCardBadge = new Label("▲ KRİTİK SEYİR KARARI (OPERATIONS DECISION)")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.961f, 0.620f, 0.043f),
                    marginBottom = 4
                }
            };
            card.Add(_decisionCardBadge);

            _decisionCardTitle = new Label("Anomali Tespit Edildi")
            {
                style =
                {
                    fontSize = 16,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    marginBottom = 6
                }
            };
            card.Add(_decisionCardTitle);

            _decisionCardNarrative = new Label("Açıklama yükleniyor...")
            {
                style =
                {
                    fontSize = 12,
                    color = new Color(0.80f, 0.88f, 0.96f),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 16
                }
            };
            card.Add(_decisionCardNarrative);

            _decisionOptionsContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };
            card.Add(_decisionOptionsContainer);

            overlay.Add(card);
            return overlay;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            var campaign = ViewModel.Campaign ?? new CampaignState(seed: 123456UL);

            if (context is OperationsNavigationArgs args)
            {
                _lastArgs = args;
                _destinationId = args.DestinationId ?? "moon";
                _missionId = args.MissionId;
                ViewModel.BindMission(campaign, args.MissionId, args);
            }
            else if (context is MissionInstance mi)
            {
                _lastArgs = new OperationsNavigationArgs(
                    missionId: mi.MissionId,
                    missionName: mi.MissionName,
                    destinationId: mi.DestinationId,
                    blueprint: mi.Blueprint,
                    cruiseDurationDays: mi.CruiseDurationDays,
                    spacecraftReliability: 0.95,
                    missionSeed: mi.MissionSeed);
                _destinationId = mi.DestinationId ?? "moon";
                _missionId = mi.MissionId;
                ViewModel.BindMission(campaign, mi.MissionId, _lastArgs);
            }
            else if (string.IsNullOrEmpty(ViewModel.MissionId))
            {
                _lastArgs = new OperationsNavigationArgs(
                    missionId: "default_active_mission",
                    missionName: "Artemis Görevi",
                    destinationId: "moon",
                    blueprint: null,
                    cruiseDurationDays: 30,
                    spacecraftReliability: 0.95,
                    missionSeed: 123456UL);
                _destinationId = "moon";
                _missionId = _lastArgs.MissionId;
                ViewModel.BindMission(campaign, _lastArgs.MissionId, _lastArgs);
            }

            // Load settings
            try
            {
                var storage = new PlayerPrefsSettingsStorage();
                _settings = storage.LoadSettings();
            }
            catch
            {
                _settings = new GameSettingsData();
            }

            // Apply settings
            _stageView.SetReduceMotion(_settings.ReduceMotion);
            _captionView.SetMode(_settings.CaptionMode);
            _captionView.SetFontScale(_settings.FontScale);
            UpdateCaptionToggleButtonText();

            // Flight Theatre audio director (created or ReduceMotion-synced) — ADR-061
            EnsureAudioDirector(_settings.ReduceMotion);

            // Setup Destination Visuals & Payload silhouette
            ApplyDestinationProfile(_destinationId);
            ConfigurePayloadSilhouette();

            // Start animation & 60Hz tick
            _stageView.StartAnimation();
            _tickSchedule = RootElement.schedule.Execute(OnUpdateTick).Every(16);

            // Initial timeline setup
            SetupInitialTimeline();
            SyncUIFromMissionState();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            _clock.Pause();
            _stageView.StopAnimation();
            _tickSchedule?.Pause();
            _tickSchedule = null;
            _audioDirector?.StopMusic();
        }

        private void OnUpdateTick()
        {
            float dt = Time.deltaTime;
            _clock.Tick(dt);
            _budget.Tick(dt);
            _perfLabelCooldown++;
            if (_perfLabelCooldown >= 60)
            {
                _perfLabelCooldown = 0;
                UpdatePerformanceLabel();
            }
            UpdatePlayPauseButtonText();
        }

        /// <summary>Resolve the audio service and build the Flight Theatre audio director (ADR-061).</summary>
        private void EnsureAudioDirector(bool reduceMotion)
        {
            if (_audioDirector != null)
            {
                _audioDirector.SetReduceMotion(reduceMotion);
                return;
            }

            if (_audioService == null)
            {
                _audioService = UnityEngine.Object.FindObjectOfType<UnityAudioService>();
            }

            _audioDirector = new FlightAudioDirector(
                _audioService != null ? (IAudioService)_audioService : new NullAudioService(),
                _budget,
                reduceMotion);
        }

        private void UpdatePerformanceLabel()
        {
            if (_perfLabel == null) return;
            _perfLabel.text = _budget.GetSummary();
        }

        private void TriggerAudio(AudioEvent evt)
        {
            if (evt == AudioEvent.None) return;
            if (_audioService == null)
            {
                _audioService = UnityEngine.Object.FindObjectOfType<UnityAudioService>();
            }
            _audioService?.PlaySfx(evt);
        }

        private void OnBeatAdvanced(int newIndex, int oldIndex)
        {
            var beat = _clock.CurrentBeat;
            if (beat == null) return;

            // Apply data-driven choreography overrides before visual stage and UI consumption
            FlightChoreography.Instance.ApplyOverride(beat);

            // Apply beat to visual stage and camera
            _stageView.ApplyBeat(beat);

            // Update UI elements
            _phaseRail.SetCurrentIndex(newIndex);
            _hudView.UpdateTelemetry(beat);

            string locTitle = LocalizeKey(beat.TitleKey);
            string locNarrative = LocalizeKey(beat.NarrativeKey);
            _captionView.DisplayBeat(beat, locTitle, locNarrative, newIndex);

            // Audio dispatch through the Flight Theatre director (SFX + phase music)
            if (beat.AudioEventId != AudioEvent.None)
            {
                _audioDirector?.OnBeatStarted(beat);
            }

            // Check if beat requires player decision
            if (beat.RequiresDecision)
            {
                SetAutoCruise(false);
                _clock.Pause();
                RefreshDecisionModal();
            }
        }

        private void OnClockSequenceFinished()
        {
            UpdatePlayPauseButtonText();
            SyncUIFromMissionState();

            // Terminal beat outcome → success / failure musical sting (ADR-061)
            bool success = true;
            if (_currentBeats != null && _currentBeats.Length > 0)
            {
                string tag = _currentBeats[_currentBeats.Length - 1].OutcomeTag;
                success = string.IsNullOrEmpty(tag) ||
                          !(tag.Contains("Fail") || tag.Contains("Crash") ||
                            tag.Contains("Loss") || tag.Contains("Abort") ||
                            tag.Contains("Burn"));
            }
            _audioDirector?.OnSequenceFinished(success);

            if (_isAutoCruiseActive)
            {
                if (ViewModel.CanAdvance.Value && !ViewModel.HasActiveDecision.Value && !ViewModel.HasPendingOperationEvent.Value)
                {
                    OnAdvanceCruiseClicked();
                }
                else
                {
                    SetAutoCruise(false);
                }
            }
        }

        private void OnClockDecisionRequired(FlightBeat beat)
        {
            SetAutoCruise(false);
            TriggerAudio(AudioEvent.AbortAlarm);
            RefreshDecisionModal();
        }

        private void SetupInitialTimeline()
        {
            if (ViewModel.Campaign != null && !string.IsNullOrEmpty(ViewModel.MissionId) &&
                ViewModel.Campaign.Missions.TryGetValue(ViewModel.MissionId, out var mission))
            {
                if (mission.EdlResult != null)
                {
                    bool hasAtmo = IsAtmosphericDestination(_destinationId);
                    _currentBeats = FlightTimeline.FromEdl(mission.EdlResult, hasAtmo);
                    _phaseRail.SetHeaderTitle("İNİŞ AŞAMALARI (EDL)");
                }
                else if (mission.CruiseEventLog != null && mission.CruiseEventLog.Count > 0)
                {
                    _currentBeats = FlightTimeline.FromCruise(mission.CruiseEventLog);
                    _phaseRail.SetHeaderTitle("SEYİR AŞAMALARI (CRUISE)");
                }
                else
                {
                    // Auto-advance first segment if starting from scratch
                    ViewModel.AdvanceCruiseStepCommand.Execute();
                    _currentBeats = FlightTimeline.FromCruise(ViewModel.EventLog);
                    _phaseRail.SetHeaderTitle("SEYİR AŞAMALARI (CRUISE)");
                }
            }
            else if (ViewModel.EventLog.Count > 0)
            {
                _currentBeats = FlightTimeline.FromCruise(ViewModel.EventLog);
                _phaseRail.SetHeaderTitle("SEYİR AŞAMALARI (CRUISE)");
            }
            else
            {
                ViewModel.AdvanceCruiseStepCommand.Execute();
                _currentBeats = FlightTimeline.FromCruise(ViewModel.EventLog);
                _phaseRail.SetHeaderTitle("SEYİR AŞAMALARI (CRUISE)");
            }

            // Visual handover from launch (Trans-Injection burn beat)
            if (_lastArgs?.FromLaunchHandover == true && _currentBeats != null && _currentBeats.Length > 0)
            {
                var transferBeat = new FlightBeat(
                    phaseId: "Cruise.TransferInjection",
                    statusId: "TransferBurn",
                    titleKey: "CRUISE_TRANSFER_INJECTION_TITLE",
                    narrativeKey: "CRUISE_TRANSFER_INJECTION_DESC",
                    technicalDetails: "TMI Trans-Injection Δv: +3,150 m/s. Interplanetary trajectory insertion confirmed.",
                    timestampSeconds: 0,
                    altitudeKm: 400.0,
                    velocityMps: 11200.0,
                    heatShieldIntegrity: 1.0,
                    cruiseProgress: 0.0,
                    cameraMotionKey: "cam_transfer_injection",
                    visualEffectKey: "fx_transfer_burn",
                    audioEventId: AudioEvent.RocketIgnition,
                    durationSeconds: 4.0f,
                    requiresDecision: false,
                    isTerminal: false,
                    outcomeTag: "TransferInjection"
                );

                var list = new List<FlightBeat>(_currentBeats.Length + 1);
                list.Add(transferBeat);
                list.AddRange(_currentBeats);
                _currentBeats = list.ToArray();

                _lastArgs = new OperationsNavigationArgs(
                    _lastArgs.MissionId,
                    _lastArgs.MissionName,
                    _lastArgs.DestinationId,
                    _lastArgs.Blueprint,
                    _lastArgs.CruiseDurationDays,
                    _lastArgs.SpacecraftReliability,
                    _lastArgs.MissionSeed,
                    fromLaunchHandover: false
                );
            }

            if (_currentBeats != null && _currentBeats.Length > 0)
            {
                _phaseRail.BindBeats(_currentBeats);
                LocalizeRailTitles();
                _clock.Load(_currentBeats);
                _clock.SetSpeed(_currentSpeed);
                _clock.Play();
            }
        }

        private void SyncUIFromMissionState()
        {
            string missionName = ViewModel.MissionName.Value;
            string vehicleName = ViewModel.VehicleName.Value;
            string destName = ViewModel.DestinationName.Value;
            _hudView.SetMissionInfo(missionName, $"{vehicleName} → {destName}", null);

            // Status badge color and text
            string statusText = ViewModel.MissionStatusText.Value;
            Color badgeColor = statusText switch
            {
                "INCRUISE" or "SEYİRDE" => new Color(0.12f, 0.35f, 0.55f),
                "ARRIVAL" or "VARIŞ" => new Color(0.06f, 0.65f, 0.75f),
                "DECISIONPENDING" or "KARAR BEKLENİYOR" => new Color(0.90f, 0.55f, 0.10f),
                "OPERATING" or "OPERASYONEL" => new Color(0.10f, 0.65f, 0.40f),
                "COMPLETED" or "TAMAMLANDI" => new Color(0.10f, 0.70f, 0.35f),
                "FAILED" or "BAŞARISIZ" => new Color(0.85f, 0.20f, 0.20f),
                _ => new Color(0.15f, 0.25f, 0.35f)
            };
            _hudView.SetStatusBadge(statusText, badgeColor);

            // Action button visibilities & enabled states
            _advanceCruiseBtn.SetEnabled(ViewModel.CanAdvance.Value && !ViewModel.HasActiveDecision.Value);
            _advanceCruiseBtn.style.display = ViewModel.CanAdvance.Value ? DisplayStyle.Flex : DisplayStyle.None;

            _autoCruiseBtn.SetEnabled(ViewModel.CanAdvance.Value && !ViewModel.HasActiveDecision.Value);
            _autoCruiseBtn.style.display = ViewModel.CanAdvance.Value ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ViewModel.CanAdvance.Value || ViewModel.HasActiveDecision.Value)
            {
                SetAutoCruise(false);
            }

            _edlBtn.SetEnabled(ViewModel.CanExecuteEdl.Value);
            _edlBtn.style.display = ViewModel.CanExecuteEdl.Value ? DisplayStyle.Flex : DisplayStyle.None;

            _scienceBtn.SetEnabled(ViewModel.CanExecuteScience.Value);
            _scienceBtn.style.display = ViewModel.CanExecuteScience.Value ? DisplayStyle.Flex : DisplayStyle.None;

            _continueBtn.SetEnabled(ViewModel.IsComplete.Value);
            _continueBtn.style.display = ViewModel.IsComplete.Value ? DisplayStyle.Flex : DisplayStyle.None;

            // Check if Decision Modal should show
            RefreshDecisionModal();
        }

        private void RefreshDecisionModal()
        {
            if (ViewModel.HasActiveDecision.Value)
            {
                _decisionModalOverlay.style.display = DisplayStyle.Flex;
                _decisionCardBadge.text = "▲ KRİTİK SEYİR KARARI (CRUISE DECISION)";
                _decisionCardTitle.text = LocalizeKey(ViewModel.DecisionTitle.Value);
                _decisionCardNarrative.text = LocalizeKey(ViewModel.DecisionNarrative.Value);

                _decisionOptionsContainer.Clear();
                foreach (var opt in ViewModel.CurrentDecisionOptions)
                {
                    string label = LocalizeKey(opt.LabelKey);
                    string consequence = LocalizeKey(opt.VisibleConsequenceKey);
                    var optBtn = new Button(() => OnDecisionOptionSelected(opt.OptionId))
                    {
                        text = string.IsNullOrEmpty(consequence) ? label : $"{label}\n({consequence})"
                    };
                    optBtn.AddToClassList("apogee-button");
                    optBtn.style.marginBottom = 6;
                    optBtn.style.paddingTop = 8;
                    optBtn.style.paddingBottom = 8;
                    optBtn.style.fontSize = 11;
                    _decisionOptionsContainer.Add(optBtn);
                }
            }
            else if (ViewModel.HasPendingOperationEvent.Value)
            {
                _decisionModalOverlay.style.display = DisplayStyle.Flex;
                _decisionCardBadge.text = "▲ OPERASYON OLAYI (OPERATION EVENT)";
                _decisionCardTitle.text = LocalizeKey(ViewModel.OperationEventTitle.Value);
                _decisionCardNarrative.text = LocalizeKey(ViewModel.OperationEventNarrative.Value);

                _decisionOptionsContainer.Clear();
                foreach (var opt in ViewModel.OperationEventOptions)
                {
                    string label = LocalizeKey(opt.LabelKey);
                    string consequence = LocalizeKey(opt.VisibleConsequenceKey);
                    var optBtn = new Button(() => OnOperationEventOptionSelected(opt.OptionId))
                    {
                        text = string.IsNullOrEmpty(consequence) ? label : $"{label}\n({consequence})"
                    };
                    optBtn.AddToClassList("apogee-button");
                    optBtn.style.marginBottom = 6;
                    optBtn.style.paddingTop = 8;
                    optBtn.style.paddingBottom = 8;
                    optBtn.style.fontSize = 11;
                    _decisionOptionsContainer.Add(optBtn);
                }
            }
            else
            {
                _decisionModalOverlay.style.display = DisplayStyle.None;
            }
        }

        private void OnDecisionOptionSelected(string optionId)
        {
            TriggerAudio(AudioEvent.ButtonClick);
            ViewModel.ResolveOptionCommand.Execute(optionId);
            _decisionModalOverlay.style.display = DisplayStyle.None;

            // Re-sync timeline
            if (ViewModel.EventLog.Count > 0)
            {
                _currentBeats = FlightTimeline.FromCruise(ViewModel.EventLog);
                _phaseRail.BindBeats(_currentBeats);
                LocalizeRailTitles();
                _clock.Load(_currentBeats);
                _clock.Play();
            }
        }

        private void OnOperationEventOptionSelected(string optionId)
        {
            TriggerAudio(AudioEvent.ButtonClick);
            ViewModel.ResolveOperationEventOptionCommand.Execute(optionId);
            _decisionModalOverlay.style.display = DisplayStyle.None;
        }

        private void OnAdvanceCruiseClicked()
        {
            TriggerAudio(AudioEvent.RetroThrust);
            ViewModel.AdvanceCruiseStepCommand.Execute();

            _currentBeats = FlightTimeline.FromCruise(ViewModel.EventLog);
            _phaseRail.SetHeaderTitle("SEYİR AŞAMALARI (CRUISE)");
            _phaseRail.BindBeats(_currentBeats);
            LocalizeRailTitles();
            _clock.Load(_currentBeats);
            _clock.Play();
        }

        private void OnEdlClicked()
        {
            TriggerAudio(AudioEvent.AtmosphericEntryRumble);
            ViewModel.ExecuteEdlStepCommand.Execute();

            if (ViewModel.Campaign != null && !string.IsNullOrEmpty(ViewModel.MissionId) &&
                ViewModel.Campaign.Missions.TryGetValue(ViewModel.MissionId, out var mission) &&
                mission.EdlResult != null)
            {
                bool hasAtmo = IsAtmosphericDestination(_destinationId);
                _currentBeats = FlightTimeline.FromEdl(mission.EdlResult, hasAtmo);
                _phaseRail.SetHeaderTitle("İNİŞ AŞAMALARI (EDL)");
                _phaseRail.BindBeats(_currentBeats);
                LocalizeRailTitles();
                _clock.Load(_currentBeats);
                _clock.Play();
            }
        }

        private void OnScienceClicked()
        {
            TriggerAudio(AudioEvent.TelemetryPing);
            ViewModel.ExecuteScienceStepCommand.Execute();

            if (ViewModel.Campaign != null && !string.IsNullOrEmpty(ViewModel.MissionId) &&
                ViewModel.Campaign.Missions.TryGetValue(ViewModel.MissionId, out var mission) &&
                mission.ScienceLog.Count > 0)
            {
                var lastSci = mission.ScienceLog[^1];
                _currentBeats = FlightTimeline.FromScience(lastSci);
                _phaseRail.SetHeaderTitle($"BİLİM GÜNÜ #{mission.ScienceDaysCompleted}");
                _phaseRail.BindBeats(_currentBeats);
                LocalizeRailTitles();
                _clock.Load(_currentBeats);
                _clock.Play();
            }
        }

        private void OnContinueClicked()
        {
            TriggerAudio(AudioEvent.LandingSuccess);
            string missionId = ViewModel.MissionId;
            var campaign = ViewModel.Campaign;

            if (campaign != null && !string.IsNullOrEmpty(missionId) &&
                campaign.Missions.TryGetValue(missionId, out var mission))
            {
                if (mission.Report == null)
                {
                    var launchResult = new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.05, 9500, "nominal", "Nominal Fırlatma");
                    var edlResult = mission.EdlResult ?? new EdlSimulationResult(
                        outcome: EdlOutcome.Success,
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

        private void OnPlayPauseClicked()
        {
            if (_clock.IsPlaying)
            {
                _clock.Pause();
            }
            else
            {
                if (_clock.IsFinished && _currentBeats != null && _currentBeats.Length > 0)
                {
                    _clock.Load(_currentBeats);
                }
                _clock.Play();
            }
            UpdatePlayPauseButtonText();
        }

        private void OnSpeedClicked()
        {
            _currentSpeed = _currentSpeed switch
            {
                1.0f => 2.0f,
                2.0f => 4.0f,
                _ => 1.0f
            };
            _clock.SetSpeed(_currentSpeed);
            _speedBtn.text = $"{_currentSpeed:0}×";
        }

        private void OnStepClicked()
        {
            _clock.StepNext();
        }

        private void OnSkipClicked()
        {
            _clock.SkipToEnd();
        }

        private void OnRewatchClicked()
        {
            TriggerAudio(AudioEvent.ButtonClick);
            if (_currentBeats != null && _currentBeats.Length > 0)
            {
                _clock.Load(_currentBeats);
                _clock.SetSpeed(_currentSpeed);
                _clock.SeekTo(0);
                _clock.Play();
                _stageView.ApplyBeat(_currentBeats[0]);
                _hudView.UpdateTelemetry(_currentBeats[0]);
                UpdatePlayPauseButtonText();
            }
        }

        private void OnAutoCruiseToggleClicked()
        {
            TriggerAudio(AudioEvent.ButtonClick);
            SetAutoCruise(!_isAutoCruiseActive);
            if (_isAutoCruiseActive)
            {
                if (!_clock.IsPlaying)
                {
                    _clock.Play();
                }
                else if (_clock.IsFinished && ViewModel.CanAdvance.Value)
                {
                    OnAdvanceCruiseClicked();
                }
            }
        }

        private void SetAutoCruise(bool active)
        {
            _isAutoCruiseActive = active;
            if (_autoCruiseBtn != null)
            {
                _autoCruiseBtn.text = _isAutoCruiseActive ? "⚡ Otomatik Seyir: Açık" : "⚡ Otomatik Seyir: Kapalı";
                if (_isAutoCruiseActive)
                {
                    _autoCruiseBtn.AddToClassList("apogee-button--accent-cyan");
                }
                else
                {
                    _autoCruiseBtn.RemoveFromClassList("apogee-button--accent-cyan");
                }
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    OnPlayPauseClicked();
                    evt.StopPropagation();
                    break;
                case KeyCode.T:
                    OnCaptionToggleClicked();
                    evt.StopPropagation();
                    break;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    _currentSpeed = 1.0f;
                    _clock.SetSpeed(_currentSpeed);
                    _speedBtn.text = "1×";
                    evt.StopPropagation();
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    _currentSpeed = 2.0f;
                    _clock.SetSpeed(_currentSpeed);
                    _speedBtn.text = "2×";
                    evt.StopPropagation();
                    break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    _currentSpeed = 4.0f;
                    _clock.SetSpeed(_currentSpeed);
                    _speedBtn.text = "4×";
                    evt.StopPropagation();
                    break;
                case KeyCode.RightArrow:
                case KeyCode.N:
                    OnStepClicked();
                    evt.StopPropagation();
                    break;
                case KeyCode.End:
                    OnSkipClicked();
                    evt.StopPropagation();
                    break;
                case KeyCode.R:
                    OnRewatchClicked();
                    evt.StopPropagation();
                    break;

                // ── Camera zoom (wheel / +/- / Z reset) ──
                case KeyCode.Equals:
                case KeyCode.KeypadPlus:
                    _stageView.AdjustCameraZoom(+FlightCamera.ZoomStep);
                    evt.StopPropagation();
                    break;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    _stageView.AdjustCameraZoom(-FlightCamera.ZoomStep);
                    evt.StopPropagation();
                    break;
                case KeyCode.Z:
                    _stageView.ResetCameraZoom();
                    evt.StopPropagation();
                    break;
            }
        }

        /// <summary>
        /// Mouse wheel zoom over the flight stage (ADR-061 player camera control).
        /// Scroll up = zoom in, scroll down = zoom out.
        /// </summary>
        private void OnStageWheel(WheelEvent evt)
        {
            float step = -Mathf.Sign(evt.delta.y) * FlightCamera.ZoomStep;
            _stageView.AdjustCameraZoom(step);
            evt.StopPropagation();
        }

        private void OnCaptionToggleClicked()
        {
            var nextMode = _captionView.Mode switch
            {
                FlightCaptionMode.Subtitle => FlightCaptionMode.FullLog,
                FlightCaptionMode.FullLog => FlightCaptionMode.Off,
                _ => FlightCaptionMode.Subtitle
            };

            _captionView.SetMode(nextMode);
            UpdateCaptionToggleButtonText();
        }

        private void UpdateCaptionToggleButtonText()
        {
            _captionToggleBtn.text = _captionView.Mode switch
            {
                FlightCaptionMode.Off => "Altyazı: Kapalı",
                FlightCaptionMode.FullLog => "Altyazı: Günlük",
                _ => "Altyazı: Altyazı"
            };
        }

        private void UpdatePlayPauseButtonText()
        {
            if (_clock.IsPlaying)
            {
                _playPauseBtn.text = "Duraklat";
            }
            else
            {
                _playPauseBtn.text = _clock.IsFinished ? "Tekrar Oynat" : "Oynat";
            }
        }

        private void OnPhaseRailBeatSelected(int index)
        {
            _clock.SeekTo(index);
        }

        private void LocalizeRailTitles()
        {
            if (_currentBeats == null) return;
            for (int i = 0; i < _currentBeats.Length; i++)
            {
                var localized = LocalizeKey(_currentBeats[i].TitleKey);
                if (!string.IsNullOrEmpty(localized))
                {
                    _phaseRail.SetBeatTitle(i, localized);
                }
            }
        }

        private void ConfigurePayloadSilhouette()
        {
            var bp = _lastArgs?.Blueprint;
            var payloadType = VehicleSilhouetteView.PayloadType.Orbiter;

            if (bp != null)
            {
                bool isRover = bp.Name?.IndexOf("rover", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isLander = bp.Name?.IndexOf("lander", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isRover) payloadType = VehicleSilhouetteView.PayloadType.Rover;
                else if (isLander) payloadType = VehicleSilhouetteView.PayloadType.Lander;
            }
            else if (_destinationId.Contains("moon") || _destinationId.Contains("mars"))
            {
                payloadType = VehicleSilhouetteView.PayloadType.Rover;
            }

            _stageView.SetPayloadType(payloadType);
        }

        private void ApplyDestinationProfile(string destinationId)
        {
            var profile = new FlightStageView.DestinationProfile
            {
                BodyScale = 1.0f,
                GlowIntensity = 0.3f,
                HasAtmosphere = true,
                HasRings = false,
                BodyColor = new Color(0.10f, 0.35f, 0.60f),
                AtmosphereColor = new Color(0.063f, 0.361f, 0.573f, 0.5f),
                OrbitRingColor = new Color(0.024f, 0.714f, 0.831f)
            };

            string id = destinationId?.ToLowerInvariant() ?? "moon";
            if (id.Contains("moon"))
            {
                profile.BodyColor = new Color(0.55f, 0.55f, 0.55f);
                profile.AtmosphereColor = Color.clear;
                profile.HasAtmosphere = false;
                profile.BodyScale = 0.27f;
                profile.OrbitRingColor = new Color(0.80f, 0.84f, 0.88f);
            }
            else if (id.Contains("mars"))
            {
                profile.BodyColor = new Color(0.76f, 0.27f, 0.05f);
                profile.AtmosphereColor = new Color(0.83f, 0.51f, 0.42f, 0.3f);
                profile.HasAtmosphere = true;
                profile.BodyScale = 0.53f;
                profile.OrbitRingColor = new Color(0.94f, 0.27f, 0.27f);
            }
            else if (id.Contains("venus"))
            {
                profile.BodyColor = new Color(0.78f, 0.66f, 0.29f);
                profile.AtmosphereColor = new Color(0.91f, 0.78f, 0.44f, 0.5f);
                profile.HasAtmosphere = true;
                profile.BodyScale = 0.95f;
                profile.OrbitRingColor = new Color(0.96f, 0.62f, 0.04f);
            }
            else if (id.Contains("europa"))
            {
                profile.BodyColor = new Color(0.78f, 0.85f, 0.91f);
                profile.AtmosphereColor = Color.clear;
                profile.HasAtmosphere = false;
                profile.BodyScale = 0.25f;
                profile.OrbitRingColor = new Color(0.40f, 0.91f, 0.98f);
            }

            _stageView.SetDestinationProfile(profile);
        }

        private static bool IsAtmosphericDestination(string destinationId)
        {
            string id = destinationId?.ToLowerInvariant() ?? string.Empty;
            return id.Contains("mars") || id.Contains("venus") || id.Contains("earth") || id.Contains("leo");
        }

        private static string LocalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                var loc = LocalizationManager.Instance;
                var res = loc?.Get(key);
                if (!string.IsNullOrEmpty(res) && res != key)
                {
                    return res;
                }
            }
            catch
            {
                // Fallback gracefully
            }

            return key switch
            {
                // Cruise segments
                "CRUISE_TRANSFER_INJECTION_TITLE"   => "Transfer Ateşlemesi (Trans-Injection Burn)",
                "CRUISE_TRANSFER_INJECTION_DESC"    => "Dünya yörüngesinden ayrılmak ve hedef rotasına girmek için üst aşama ateşlendi.",
                "CRUISE_ARRIVAL_TITLE"              => "Hedefe Varış ve Yörüngeye Giriş",
                "CRUISE_ARRIVAL_DESC"               => "Hedef gövdenin çekim alanına girildi. İniş ve operasyon hazırlığı tamamlandı.",
                "CRUISE_SEG_NOMINAL_TITLE"          => "Nominal Seyir Rotası",
                "CRUISE_SEG_NOMINAL_DESC"           => "Tüm alt sistemler yeşil. Planlanan transfer yörüngesi korunuyor.",
                "CRUISE_SEG_COMM_BLACKOUT_TITLE"    => "Haberleşme Karartması",
                "CRUISE_SEG_COMM_BLACKOUT_DESC"     => "Güneş arkası geçişi nedeniyle telemetri bağlantısı kesildi.",
                "CRUISE_SEG_SOLAR_FLARE_TITLE"      => "Güneş Radyasyon Dalgası",
                "CRUISE_SEG_SOLAR_FLARE_DESC"       => "Güneş patlaması algılandı, aviyonik kalkanlar devrede.",
                "CRUISE_SEG_DUST_IMPACT_TITLE"      => "Mikrometeorit / Toz Çarpması",
                "CRUISE_SEG_DUST_IMPACT_DESC"       => "Mikrometeorit çarpması gövde sensörleri tarafından kaydedildi.",
                "CRUISE_SEG_DECISION_TITLE"         => "Kritik Seyir Kararı",
                "CRUISE_SEG_DECISION_DESC"          => "Görevin devamı için Komuta Masasından karar bekleniyor.",
                // EDL events
                "EDL_EVENT_DEORBIT_BURN_TITLE"      => "Yörüngeden Çıkış Ateşlemesi (Deorbit)",
                "EDL_EVENT_DEORBIT_BURN_DESC"       => "İniş transferi için frenleme motorları ateşlendi.",
                "EDL_EVENT_ATMOSPHERIC_ENTRY_TITLE" => "Atmosfere Giriş (Interface)",
                "EDL_EVENT_ATMOSPHERIC_ENTRY_DESC"  => "Atmosferik sürtünme başladı, termal yük artıyor.",
                "EDL_EVENT_PEAK_HEATING_TITLE"      => "Zirve Isınma ve Plazma",
                "EDL_EVENT_PEAK_HEATING_DESC"       => "Isı kalkanı maksimum plazma akısına ulaştı.",
                "EDL_EVENT_PARACHUTE_DEPLOY_TITLE"  => "Süpersonik Paraşüt Açılması",
                "EDL_EVENT_PARACHUTE_DEPLOY_DESC"   => "Ana paraşüt başarıyla açıldı, hız hızla düşüyor.",
                "EDL_EVENT_POWERED_DESCENT_TITLE"   => "İtkili İniş (Skycrane / Retro)",
                "EDL_EVENT_POWERED_DESCENT_DESC"    => "Yumuşak iniş roketleri ateşlendi, yüzey tarayıcı kilitlendi.",
                "EDL_EVENT_TOUCHDOWN_TITLE"         => "Yüzeye Temas (Touchdown)",
                "EDL_EVENT_TOUCHDOWN_DESC"          => "Uzay aracı hedef yüzeye güvenle temas etti.",
                // Science events
                "SCI_EVENT_GENERIC_TITLE"           => "Bilim Operasyonu",
                "SCI_EVENT_GENERIC_DESC"            => "Yüzey ve yörünge bilimsel gözlem telemetrisi alınıyor.",
                "SCI_EVENT_DATA_COLLECTION_TITLE"   => "Veri Toplama Döngüsü",
                "SCI_EVENT_DATA_COLLECTION_DESC"    => "Enstrümanlar planlanan bilimsel veriyi kaydetti.",
                "SCI_EVENT_DOWNLINK_BURST_TITLE"    => "Dünya Veri Aktarımı (Downlink)",
                "SCI_EVENT_DOWNLINK_BURST_DESC"     => "Depolanan bilim paketleri yer istasyonlarına iletildi.",
                _                                   => key
            };
        }
    }
}
