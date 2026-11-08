using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Design;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Report
{
    /// <summary>
    /// ViewModel for the What-If analysis panel embedded in the Mission Report screen.
    ///
    /// Exposes the list of re-decidable operation events, allows the player to pick an event
    /// and an alternative option, then runs <see cref="WhatIfSimulator"/> and presents
    /// the resulting <see cref="WhatIfComparison"/> side-by-side with the original report.
    ///
    /// Design: no mutation of CampaignState or MissionInstance — purely read + re-evaluate.
    /// Complies with GDD §10.5 ("deterministik olduğu için ucuz"), §13.2, §21 M5-07.
    /// </summary>
    public sealed class WhatIfViewModel : ViewModelBase
    {
        // ---------------------------------------------------------------- Observable state
        /// <summary>True when at least one eligible event exists for what-if re-analysis.</summary>
        public ObservableValue<bool> HasEligibleEvents { get; } = new ObservableValue<bool>(false);

        /// <summary>Summary of the currently selected alternative and its impact delta.</summary>
        public ObservableValue<string> WhatIfDeltaSummary { get; } = new ObservableValue<string>(string.Empty);

        /// <summary>Badge: "Bu kararla BAŞARILI olurdu" / "Bu kararla BAŞARISIZ olurdu" / "Sonuç değişmezdi".</summary>
        public ObservableValue<string> WhatIfOutcomeBadge { get; } = new ObservableValue<string>(string.Empty);

        /// <summary>True if the what-if outcome differs from the original outcome.</summary>
        public ObservableValue<bool> WhatIfOutcomeChanged { get; } = new ObservableValue<bool>(false);

        /// <summary>True when a what-if result is currently displayed.</summary>
        public ObservableValue<bool> IsWhatIfActive { get; } = new ObservableValue<bool>(false);

        // ---------------------------------------------------------------- Lists (UI-bindable snapshots)
        /// <summary>Eligible resolved operation events that can be re-decided.</summary>
        public List<ActiveOperationEvent> EligibleEvents { get; } = new List<ActiveOperationEvent>();

        /// <summary>Alternative options for the currently selected event.</summary>
        public List<OperationEventOption> AlternativeOptions { get; } = new List<OperationEventOption>();

        // ---------------------------------------------------------------- Current selection
        public ActiveOperationEvent SelectedEvent { get; private set; }
        public string SelectedAlternativeOptionId { get; private set; }

        // ---------------------------------------------------------------- Latest result
        public WhatIfComparison LastComparison { get; private set; }

        // ---------------------------------------------------------------- Commands
        public ViewActionCommand ClearWhatIfCommand { get; }

        // ---------------------------------------------------------------- Events
        public event Action OnWhatIfUpdated;

        // ---------------------------------------------------------------- Internal mission data snapshot
        // These are set once by BindMission() and never mutated.
        private string _missionId;
        private string _missionName;
        private string _destinationId;
        private MissionProfile _profile;
        private Apogee.Design.Blueprint _blueprint;
        private Apogee.Core.Simulation.Launch.LaunchSimulationResult _launchResult;
        private Apogee.Core.Simulation.EDL.EdlSimulationResult _edlResult;
        private double _downlinkedGb;
        private double _rawGeneratedGb;
        private double _droppedGb;
        private int _scienceDays;
        private double _reliabilityPenalty;
        private double _correctionFuel;
        private IReadOnlyList<ActiveOperationEvent> _operationEventLog;
        private bool _isMissionFailedEarly;
        private string _earlyFailureReason;
        private MissionReport _originalReport;

        public WhatIfViewModel()
        {
            ClearWhatIfCommand = new ViewActionCommand(
                ClearWhatIf,
                () => IsWhatIfActive.Value
            );
        }

        // ---------------------------------------------------------------- Public API

        /// <summary>
        /// Binds this ViewModel to a completed mission instance's data snapshot.
        /// Call once when the Mission Report screen loads.
        /// </summary>
        public void BindMission(MissionInstance mission, MissionReport originalReport)
        {
            if (mission == null) return;

            _missionId              = mission.MissionId;
            _missionName            = mission.MissionName;
            _destinationId          = mission.DestinationId;
            _profile                = mission.Profile;
            _blueprint              = mission.Blueprint;
            _launchResult           = mission.LaunchResult;
            _edlResult              = mission.EdlResult;
            _downlinkedGb           = mission.AccumulatedDownlinkDataGb;
            _rawGeneratedGb         = mission.AccumulatedRawDataGeneratedGb;
            _droppedGb              = mission.AccumulatedDroppedDataGb;
            _scienceDays            = mission.ScienceDaysCompleted;
            _reliabilityPenalty     = mission.AccumulatedReliabilityPenalty;
            _correctionFuel         = mission.AccumulatedCorrectionFuel;
            _operationEventLog      = mission.OperationEventLog;
            _isMissionFailedEarly   = mission.Status == MissionStatus.Failed;
            _earlyFailureReason     = null; // reconstructed from report if needed
            _originalReport         = originalReport;

            // Populate eligible events list
            EligibleEvents.Clear();
            var eligible = WhatIfSimulator.GetEligibleEvents(_operationEventLog);
            EligibleEvents.AddRange(eligible);
            HasEligibleEvents.Value = EligibleEvents.Count > 0;

            // Reset selection state
            ClearWhatIf();

            OnWhatIfUpdated?.Invoke();
        }

        /// <summary>
        /// Called when the player selects an event to re-decide.
        /// Populates <see cref="AlternativeOptions"/> for that event.
        /// </summary>
        public void SelectEvent(ActiveOperationEvent ev)
        {
            SelectedEvent = ev;
            SelectedAlternativeOptionId = null;

            AlternativeOptions.Clear();
            if (ev != null)
            {
                AlternativeOptions.AddRange(WhatIfSimulator.GetAlternativeOptions(ev));
            }

            // Clear any active what-if result
            if (IsWhatIfActive.Value)
                ClearWhatIf();

            OnWhatIfUpdated?.Invoke();
        }

        /// <summary>
        /// Runs the What-If simulation for the currently selected event and alternative option.
        /// Results are stored in <see cref="LastComparison"/> and observable properties are updated.
        /// </summary>
        public void RunWhatIf(string alternativeOptionId)
        {
            if (SelectedEvent == null)
            {
                SetError("Önce bir karar seçin.");
                return;
            }
            if (string.IsNullOrEmpty(alternativeOptionId))
            {
                SetError("Lütfen alternatif bir seçenek belirtin.");
                return;
            }
            if (_originalReport == null)
            {
                SetError("Karşılaştırmak için orijinal rapor bulunamadı.");
                return;
            }

            try
            {
                var comparison = WhatIfSimulator.Simulate(
                    missionId:                   _missionId,
                    missionName:                 _missionName,
                    destinationId:               _destinationId,
                    profile:                     _profile,
                    blueprint:                   _blueprint,
                    launchResult:                _launchResult,
                    edlResult:                   _edlResult,
                    downlinkedGb:                _downlinkedGb,
                    rawGeneratedGb:              _rawGeneratedGb,
                    droppedGb:                   _droppedGb,
                    scienceDaysCompleted:        _scienceDays,
                    accumulatedReliabilityPenalty: _reliabilityPenalty,
                    accumulatedCorrectionFuel:   _correctionFuel,
                    operationEventLog:           _operationEventLog,
                    targetEvent:                 SelectedEvent,
                    alternativeOptionId:         alternativeOptionId,
                    originalReport:              _originalReport,
                    isMissionFailedEarly:        _isMissionFailedEarly,
                    earlyFailureReason:          _earlyFailureReason
                );

                LastComparison = comparison;
                SelectedAlternativeOptionId = alternativeOptionId;
                IsWhatIfActive.Value = true;

                // Update observables
                WhatIfDeltaSummary.Value = comparison.DeltaSummaryText;
                WhatIfOutcomeChanged.Value = comparison.OutcomeChanged;

                if (comparison.OutcomeChanged)
                {
                    WhatIfOutcomeBadge.Value = comparison.WhatIfReport.IsOverallSuccess
                        ? "Bu kararla görev BAŞARILI olurdu"
                        : "Bu kararla görev BAŞARISIZ olurdu";
                }
                else
                {
                    WhatIfOutcomeBadge.Value = comparison.WhatIfReport.IsOverallSuccess
                        ? "Yine de başarılı olunurdu"
                        : "Yine de başarısız olunurdu";
                }

                ClearWhatIfCommand.RaiseCanExecuteChanged();
                OnWhatIfUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                SetError($"What-If hesaplama hatası: {ex.Message}");
            }
        }

        /// <summary>Clears the active what-if result and resets to initial state.</summary>
        public void ClearWhatIf()
        {
            LastComparison = null;
            SelectedAlternativeOptionId = null;
            IsWhatIfActive.Value = false;
            WhatIfDeltaSummary.Value = string.Empty;
            WhatIfOutcomeBadge.Value = string.Empty;
            WhatIfOutcomeChanged.Value = false;
            ClearWhatIfCommand.RaiseCanExecuteChanged();
            OnWhatIfUpdated?.Invoke();
        }

        // ---------------------------------------------------------------- helpers
        private void SetError(string message)
        {
            WhatIfDeltaSummary.Value = message;
            WhatIfOutcomeBadge.Value = string.Empty;
            WhatIfOutcomeChanged.Value = false;
            IsWhatIfActive.Value = false;
            OnWhatIfUpdated?.Invoke();
        }
    }
}
