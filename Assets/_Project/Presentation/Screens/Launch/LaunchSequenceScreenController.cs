using System;
using System.Collections.Generic;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.Core.Simulation.Launch;
using Apogee.Design;
using Apogee.UI.Audio;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Operations;
using Apogee.UI.Screens.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// UI Toolkit Screen Controller for the redesigned Launch Sequence screen (Flight Theatre).
    /// Orchestrates <see cref="FlightStageView"/>, <see cref="PlaybackClock"/>, <see cref="PhaseRailView"/>,
    /// <see cref="FlightHudView"/>, <see cref="FlightCaptionView"/>, and audio triggers.
    /// (GDD §3.4, §3.5, §3.6, §10.1, §14.1 #13)
    /// </summary>
    public sealed class LaunchSequenceScreenController : ScreenControllerBase<LaunchSequenceViewModel>
    {
        public override ScreenId ScreenId => ScreenId.LaunchSequence;

        private FlightStageView _stageView;
        private PhaseRailView _phaseRail;
        private FlightHudView _hudView;
        private FlightCaptionView _captionView;

        // Playback controls in footer
        private Button _playPauseBtn;
        private Button _speedBtn;
        private Button _stepBtn;
        private Button _skipBtn;
        private Button _rewatchBtn;
        private Button _captionToggleBtn;
        private Button _continueBtn;

        private readonly PlaybackClock _clock = new PlaybackClock();
        private FlightBeat[] _timelineBeats;
        private LaunchNavigationArgs _lastLaunchArgs;

        private IVisualElementScheduledItem _tickSchedule;
        private UnityAudioService _audioService;

        // ── Flight Theatre Audio Director & Performance Budget (ADR-061) ──────
        private FlightAudioDirector _audioDirector;
        private readonly FlightPerformanceBudget _budget = new FlightPerformanceBudget();
        private Label _perfLabel;
        private int _perfLabelCooldown;

        private float _currentSpeed = PlaybackClock.Speed1x;

        public LaunchSequenceScreenController(LaunchSequenceViewModel viewModel)
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
            _phaseRail.OnBeatSelected += OnPhaseRailBeatSelected;
            overlayRow.Add(_phaseRail);

            // Right content area (HUD on top, Captions in middle/lower, Controls at bottom)
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
            rightArea.Add(_hudView);

            // Middle / bottom Captions
            _captionView = new FlightCaptionView();
            _captionView.OnBeatSelected += OnPhaseRailBeatSelected;
            rightArea.Add(_captionView);

            // Bottom Playback Controls footer
            var footer = CreateFooter();
            rightArea.Add(footer);

            overlayRow.Add(rightArea);
            root.Add(overlayRow);

            // Keyboard shortcut navigation
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Wire clock events
            _clock.BeatAdvanced += OnBeatAdvanced;
            _clock.SequenceFinished += OnClockSequenceFinished;

            // Wire ViewModel
            ViewModel.PropertyChanged += (s, e) => SyncViewModelState();

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
                    backgroundColor = new Color(0.04f, 0.06f, 0.10f, 0.92f),
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.12f, 0.18f, 0.28f, 0.8f)
                }
            };

            var leftControls = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            // Play / Pause
            _playPauseBtn = new Button(OnPlayPauseClicked) { text = "⏸ Duraklat" };
            _playPauseBtn.AddToClassList("apogee-button");
            _playPauseBtn.style.marginRight = 6;
            leftControls.Add(_playPauseBtn);

            // Speed cycle (1x -> 2x -> 4x)
            _speedBtn = new Button(OnSpeedCycleClicked) { text = "1×" };
            _speedBtn.AddToClassList("apogee-button");
            _speedBtn.style.width = 44;
            _speedBtn.style.marginRight = 6;
            leftControls.Add(_speedBtn);

            // Step
            _stepBtn = new Button(OnStepClicked) { text = "⏭ Adım" };
            _stepBtn.AddToClassList("apogee-button");
            _stepBtn.style.marginRight = 6;
            leftControls.Add(_stepBtn);

            // Skip
            _skipBtn = new Button(OnSkipClicked) { text = "⏩ Sona Atla" };
            _skipBtn.AddToClassList("apogee-button");
            _skipBtn.style.marginRight = 6;
            leftControls.Add(_skipBtn);

            // Rewatch
            _rewatchBtn = new Button(OnRewatchClicked) { text = "🔄 Yeniden İzle" };
            _rewatchBtn.AddToClassList("apogee-button");
            _rewatchBtn.style.marginRight = 12;
            leftControls.Add(_rewatchBtn);

            // Caption Mode toggle
            _captionToggleBtn = new Button(OnCaptionToggleClicked) { text = "💬 Altyazı: Açık" };
            _captionToggleBtn.AddToClassList("apogee-button");
            leftControls.Add(_captionToggleBtn);

            footer.Add(leftControls);

            // WebGL performance budget overlay (ADR-061, dev audit)
            _perfLabel = new Label { text = string.Empty };
            _perfLabel.style.fontSize = 11;
            _perfLabel.style.opacity = 0.65f;
            _perfLabel.style.marginRight = 12;
            _perfLabel.style.color = new Color(0.55f, 0.75f, 0.85f);
            footer.Add(_perfLabel);

            // Continue action
            _continueBtn = new Button(OnContinueClicked) { text = "Operasyona Geç (Operations) ▶" };
            _continueBtn.AddToClassList("apogee-button");
            _continueBtn.AddToClassList("apogee-button--accent-amber");
            _continueBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _continueBtn.style.fontSize = 13;
            _continueBtn.SetEnabled(false);
            footer.Add(_continueBtn);

            return footer;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            LoadSettingsPreferences();

            if (context is LaunchNavigationArgs args)
            {
                _lastLaunchArgs = args;

                var launchParams = args.Blueprint != null
                    ? args.Blueprint.ToLaunchParameters(
                        stats: args.Stats,
                        padWeatherRisk: args.WeatherRisk,
                        equatorialBonusDv: args.EquatorialBonusDv,
                        isCrewed: args.IsCrewed,
                        seed: args.LaunchSeed != 0 ? args.LaunchSeed : LaunchNavigationArgs.DefaultFallbackSeed)
                    : new LaunchSimulationParameters(
                        missionId: "mission_preview",
                        vehicleName: args.MissionName,
                        stageCount: 2,
                        stageReliabilities: new[] { 0.95, 0.95 },
                        overallReliability: 0.95,
                        launchPadWeatherRisk: args.WeatherRisk,
                        seed: args.LaunchSeed != 0 ? args.LaunchSeed : LaunchNavigationArgs.DefaultFallbackSeed);

                var simResult = LaunchSimulator.Simulate(launchParams);
                ViewModel.BindResult(args.MissionName, args.Blueprint?.Name ?? "Araç", simResult);

                // Build FlightTimeline beats
                _timelineBeats = FlightTimeline.FromLaunch(simResult);

                // Apply data-driven overrides if choreography is available
                for (int i = 0; i < _timelineBeats.Length; i++)
                {
                    _timelineBeats[i] = FlightChoreography.Instance.ApplyOverride(_timelineBeats[i]);
                }

                // Bind Destination profile to stage view
                string destId = !string.IsNullOrWhiteSpace(args.DestinationId)
                    ? args.DestinationId
                    : (args.MissionName?.ToLowerInvariant() ?? "leo");
                ApplyDestinationProfile(destId);

                // Setup Phase Rail and HUD
                _phaseRail.BindBeats(_timelineBeats);
                LocalizeRailTitles();

                _hudView.SetMissionInfo(args.MissionName, args.Blueprint?.Name ?? "Taşıyıcı Araç", ViewModel.WeatherSummary);

                _captionView.ClearLog();

                // Load Clock and begin auto-playback
                _clock.Load(_timelineBeats);
                _clock.SetSpeed(_currentSpeed);
                _clock.Play();

                _continueBtn.SetEnabled(false);
                UpdatePlayPauseButtonText();

                // Apply initial beat (Beat 0)
                if (_timelineBeats.Length > 0)
                {
                    ApplyBeatToViews(_timelineBeats[0], 0);
                }
            }

            // Start animation tick
            _stageView.StartAnimation();
            StartClockTicker();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            _stageView.StopAnimation();
            StopClockTicker();
            _audioDirector?.StopMusic();
        }

        private void StartClockTicker()
        {
            StopClockTicker();
            _tickSchedule = RootElement.schedule.Execute(() =>
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
            }).Every(16); // ~60 Hz tick for smooth clock timing
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

        private void StopClockTicker()
        {
            _tickSchedule?.Pause();
            _tickSchedule = null;
        }

        private void OnBeatAdvanced(int newIndex, int oldIndex)
        {
            if (_timelineBeats == null || newIndex < 0 || newIndex >= _timelineBeats.Length) return;

            var beat = _timelineBeats[newIndex];
            ApplyBeatToViews(beat, newIndex);

            // Sync with ViewModel so existing properties remain updated
            ViewModel.CurrentPhaseIndex = newIndex;
            ViewModel.CurrentAltitudeKm = beat.AltitudeKm;
            ViewModel.CurrentVelocityMps = beat.VelocityMps;

            // Audio dispatch through the Flight Theatre director (SFX + phase music)
            _audioDirector?.OnBeatStarted(beat);
        }

        private void ApplyBeatToViews(FlightBeat beat, int index)
        {
            _stageView.ApplyBeat(beat);
            _phaseRail.SetCurrentIndex(index);
            _hudView.UpdateTelemetry(beat);

            string title = LocalizeKey(beat.TitleKey);
            string desc = LocalizeKey(beat.NarrativeKey);
            _captionView.DisplayBeat(beat, title, desc, index);
        }

        private void OnClockSequenceFinished()
        {
            ViewModel.IsSequenceFinished = true;
            _continueBtn.SetEnabled(true);
            _hudView.SetOutcomeBadge(ViewModel.Outcome);
            UpdatePlayPauseButtonText();

            bool success = ViewModel.Outcome == LaunchOutcome.Success ||
                           ViewModel.Outcome == LaunchOutcome.PartialOrbit;
            _audioDirector?.OnSequenceFinished(success);
        }

        private void OnPhaseRailBeatSelected(int index)
        {
            _clock.SeekTo(index);
            if (index >= 0 && index < _timelineBeats.Length)
            {
                ApplyBeatToViews(_timelineBeats[index], index);
            }
        }

        private void OnPlayPauseClicked()
        {
            if (_clock.IsPlaying)
            {
                _clock.Pause();
            }
            else
            {
                if (_clock.IsFinished)
                {
                    _clock.SeekTo(0);
                }
                _clock.Play();
            }
            UpdatePlayPauseButtonText();
        }

        private void OnSpeedCycleClicked()
        {
            if (Mathf.Approximately(_currentSpeed, PlaybackClock.Speed1x))
            {
                _currentSpeed = PlaybackClock.Speed2x;
                _speedBtn.text = "2×";
            }
            else if (Mathf.Approximately(_currentSpeed, PlaybackClock.Speed2x))
            {
                _currentSpeed = PlaybackClock.Speed4x;
                _speedBtn.text = "4×";
            }
            else
            {
                _currentSpeed = PlaybackClock.Speed1x;
                _speedBtn.text = "1×";
            }

            _clock.SetSpeed(_currentSpeed);
        }

        private void OnStepClicked()
        {
            _clock.StepNext();
            ViewModel.StepNextPhaseCommand.Execute();
        }

        private void OnSkipClicked()
        {
            _clock.SkipToEnd();
            ViewModel.SkipToEndCommand.Execute();
        }

        private void OnRewatchClicked()
        {
            TriggerAudio(AudioEvent.ButtonClick);
            if (_timelineBeats != null && _timelineBeats.Length > 0)
            {
                _clock.Load(_timelineBeats);
                _clock.SetSpeed(_currentSpeed);
                _clock.SeekTo(0);
                _clock.Play();
                ApplyBeatToViews(_timelineBeats[0], 0);
                UpdatePlayPauseButtonText();
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
                    _currentSpeed = PlaybackClock.Speed1x;
                    _speedBtn.text = "1×";
                    _clock.SetSpeed(_currentSpeed);
                    evt.StopPropagation();
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    _currentSpeed = PlaybackClock.Speed2x;
                    _speedBtn.text = "2×";
                    _clock.SetSpeed(_currentSpeed);
                    evt.StopPropagation();
                    break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    _currentSpeed = PlaybackClock.Speed4x;
                    _speedBtn.text = "4×";
                    _clock.SetSpeed(_currentSpeed);
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
            }
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
            _captionToggleBtn.text = nextMode switch
            {
                FlightCaptionMode.Off => "💬 Altyazı: Kapalı",
                FlightCaptionMode.FullLog => "💬 Altyazı: Günlük (Log)",
                _ => "💬 Altyazı: Altyazı"
            };
        }

        private void UpdatePlayPauseButtonText()
        {
            if (_clock.IsPlaying)
            {
                _playPauseBtn.text = "⏸ Duraklat";
            }
            else
            {
                _playPauseBtn.text = _clock.IsFinished ? "↺ Tekrar Oynat" : "▶ Oynat";
            }
        }

        private void LocalizeRailTitles()
        {
            if (_timelineBeats == null) return;
            for (int i = 0; i < _timelineBeats.Length; i++)
            {
                var localized = LocalizeKey(_timelineBeats[i].TitleKey);
                if (!string.IsNullOrEmpty(localized))
                {
                    _phaseRail.SetBeatTitle(i, localized);
                }
            }
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
                // Fallback gracefully to raw key formatting
            }

            return key switch
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
                _ => key.Replace("LAUNCH_EVENT_", "").Replace("_TITLE", "").Replace("_", " ")
            };
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

        private void LoadSettingsPreferences()
        {
            try
            {
                var storage = new PlayerPrefsSettingsStorage();
                var settings = storage.LoadSettings();

                _stageView.SetReduceMotion(settings.ReduceMotion);
                _captionView.SetMode(settings.CaptionMode);
                _captionView.SetFontScale(settings.FontScale);

                // Flight Theatre audio director (created or ReduceMotion-synced) — ADR-061
                EnsureAudioDirector(settings.ReduceMotion);

                _captionToggleBtn.text = settings.CaptionMode switch
                {
                    FlightCaptionMode.Off => "💬 Altyazı: Kapalı",
                    FlightCaptionMode.FullLog => "💬 Altyazı: Günlük (Log)",
                    _ => "💬 Altyazı: Altyazı"
                };
            }
            catch
            {
                // Defaults if settings could not be loaded
                EnsureAudioDirector(reduceMotion: false);
            }
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

            string id = destinationId?.ToLowerInvariant() ?? "leo";
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

        private void SyncViewModelState()
        {
            _hudView.SetOutcomeBadge(ViewModel.Outcome);
            _continueBtn.SetEnabled(ViewModel.IsSequenceFinished);
        }

        private void OnContinueClicked()
        {
            if (ViewModel.Outcome == LaunchOutcome.Success || ViewModel.Outcome == LaunchOutcome.PartialOrbit)
            {
                string destId = !string.IsNullOrWhiteSpace(_lastLaunchArgs?.DestinationId)
                    ? _lastLaunchArgs.DestinationId
                    : (_lastLaunchArgs?.MissionName?.ToLowerInvariant() ?? "moon");

                var opsArgs = new OperationsNavigationArgs(
                    missionId: "mission_" + (_lastLaunchArgs?.Blueprint?.Id ?? "active"),
                    missionName: ViewModel.MissionName,
                    destinationId: destId,
                    blueprint: _lastLaunchArgs?.Blueprint,
                    cruiseDurationDays: 30,
                    spacecraftReliability: _lastLaunchArgs?.Stats?.Reliability ?? 0.95,
                    missionSeed: _lastLaunchArgs?.LaunchSeed != 0 ? _lastLaunchArgs.LaunchSeed : LaunchNavigationArgs.DefaultFallbackSeed,
                    fromLaunchHandover: true
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
