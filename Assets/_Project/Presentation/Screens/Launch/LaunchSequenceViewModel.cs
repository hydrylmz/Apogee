using System;
using System.Collections.Generic;
using Apogee.Core.Simulation.Launch;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Item view model representing a single step/event in the ascent sequence.
    /// </summary>
    public sealed class LaunchEventItemViewModel : ViewModelBase
    {
        public LaunchPhaseType Phase { get; }
        public double TimestampSeconds { get; }
        public LaunchPhaseStatus Status { get; }
        public string TitleKey { get; }
        public string NarrativeKey { get; }
        public double AltitudeKm { get; }
        public double VelocityMps { get; }
        public string TechnicalDetails { get; }

        public string TimeLabel => $"T+{(int)TimestampSeconds}s";
        public string TelemetryLabel => $"Alt: {AltitudeKm:0.#} km | Hız: {VelocityMps:0} m/s";

        public LaunchEventItemViewModel(LaunchPhaseEvent phaseEvent)
        {
            if (phaseEvent == null) throw new ArgumentNullException(nameof(phaseEvent));

            Phase = phaseEvent.Phase;
            TimestampSeconds = phaseEvent.TimestampSeconds;
            Status = phaseEvent.Status;
            TitleKey = phaseEvent.TitleKey;
            NarrativeKey = phaseEvent.NarrativeKey;
            AltitudeKm = phaseEvent.AltitudeKm;
            VelocityMps = phaseEvent.VelocityMps;
            TechnicalDetails = phaseEvent.TechnicalDetails;
        }
    }

    /// <summary>
    /// ViewModel driving the animated Launch Sequence screen (GDD §10.1, §14.1 #13).
    /// Presents chronological mission events, altitude/velocity telemetry, Go/No-Go status,
    /// cause-and-effect summary, and next action navigation.
    /// </summary>
    public sealed class LaunchSequenceViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        private string _vehicleName;
        private string _missionName;
        private LaunchOutcome _outcome;
        private bool _isCrewSafe;
        private string _weatherSummary;
        private string _causeAndEffectSummary;
        private double _currentAltitudeKm;
        private double _currentVelocityMps;
        private int _currentPhaseIndex;
        private bool _isSequenceFinished;

        private readonly List<LaunchEventItemViewModel> _events = new List<LaunchEventItemViewModel>();

        public IScreenNavigator Navigator => _navigator;

        public string VehicleName
        {
            get => _vehicleName;
            set => SetProperty(ref _vehicleName, value);
        }

        public string MissionName
        {
            get => _missionName;
            set => SetProperty(ref _missionName, value);
        }

        public LaunchOutcome Outcome
        {
            get => _outcome;
            set => SetProperty(ref _outcome, value);
        }

        public bool IsCrewSafe
        {
            get => _isCrewSafe;
            set => SetProperty(ref _isCrewSafe, value);
        }

        public string WeatherSummary
        {
            get => _weatherSummary;
            set => SetProperty(ref _weatherSummary, value);
        }

        public string CauseAndEffectSummary
        {
            get => _causeAndEffectSummary;
            set => SetProperty(ref _causeAndEffectSummary, value);
        }

        public double CurrentAltitudeKm
        {
            get => _currentAltitudeKm;
            set => SetProperty(ref _currentAltitudeKm, value);
        }

        public double CurrentVelocityMps
        {
            get => _currentVelocityMps;
            set => SetProperty(ref _currentVelocityMps, value);
        }

        public int CurrentPhaseIndex
        {
            get => _currentPhaseIndex;
            set => SetProperty(ref _currentPhaseIndex, value);
        }

        public bool IsSequenceFinished
        {
            get => _isSequenceFinished;
            set => SetProperty(ref _isSequenceFinished, value);
        }

        public IReadOnlyList<LaunchEventItemViewModel> Events => _events;

        public ViewActionCommand StepNextPhaseCommand { get; }
        public ViewActionCommand SkipToEndCommand { get; }

        public event Action OnSequenceCompleted;

        public LaunchSequenceViewModel(IScreenNavigator navigator = null)
        {
            _navigator = navigator;
            StepNextPhaseCommand = new ViewActionCommand(ExecuteStepNextPhase, () => !IsSequenceFinished && _events.Count > 0);
            SkipToEndCommand = new ViewActionCommand(ExecuteSkipToEnd, () => !IsSequenceFinished && _events.Count > 0);
        }

        public void BindResult(string missionName, string vehicleName, LaunchSimulationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            MissionName = missionName;
            VehicleName = vehicleName;
            Outcome = result.Outcome;
            IsCrewSafe = result.IsCrewSafe;
            WeatherSummary = $"Hava Durumu: {result.WeatherCondition} (Risk: %{result.WeatherRisk * 100:0})";
            CauseAndEffectSummary = result.PlainLanguageSummary;

            _events.Clear();
            foreach (var ev in result.PhaseEvents)
            {
                _events.Add(new LaunchEventItemViewModel(ev));
            }

            CurrentPhaseIndex = 0;
            IsSequenceFinished = false;

            if (_events.Count > 0)
            {
                UpdateTelemetryToPhase(0);
            }
            else
            {
                IsSequenceFinished = true;
            }

            StepNextPhaseCommand.RaiseCanExecuteChanged();
            SkipToEndCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteStepNextPhase()
        {
            if (IsSequenceFinished || _events.Count == 0) return;

            if (CurrentPhaseIndex < _events.Count - 1)
            {
                CurrentPhaseIndex++;
                UpdateTelemetryToPhase(CurrentPhaseIndex);
            }
            else
            {
                IsSequenceFinished = true;
                StepNextPhaseCommand.RaiseCanExecuteChanged();
                SkipToEndCommand.RaiseCanExecuteChanged();
                OnSequenceCompleted?.Invoke();
            }
        }

        private void ExecuteSkipToEnd()
        {
            if (IsSequenceFinished || _events.Count == 0) return;

            CurrentPhaseIndex = _events.Count - 1;
            UpdateTelemetryToPhase(CurrentPhaseIndex);
            IsSequenceFinished = true;

            StepNextPhaseCommand.RaiseCanExecuteChanged();
            SkipToEndCommand.RaiseCanExecuteChanged();
            OnSequenceCompleted?.Invoke();
        }

        private void UpdateTelemetryToPhase(int index)
        {
            if (index >= 0 && index < _events.Count)
            {
                var ev = _events[index];
                CurrentAltitudeKm = ev.AltitudeKm;
                CurrentVelocityMps = ev.VelocityMps;
            }
        }
    }
}
