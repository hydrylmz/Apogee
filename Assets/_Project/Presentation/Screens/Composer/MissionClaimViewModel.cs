using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// Reactive ViewModel for Mission Composer's Claim Level, Difficulty, Reward, and Risk display (GDD §5.1, §5.3, §14.5).
    /// Tracks active destination, mission type, and selected objectives.
    /// Automatically updates observable properties whenever player selection changes.
    /// </summary>
    public sealed class MissionClaimViewModel : ViewModelBase
    {
        private DestinationRecord _destination;
        private MissionTypeRecord _missionType;
        private readonly List<ObjectiveRecord> _objectives = new();

        public ObservableValue<DestinationRecord> Destination { get; }
        public ObservableValue<MissionTypeRecord> MissionType { get; }
        public ObservableValue<IReadOnlyList<ObjectiveRecord>> SelectedObjectives { get; }

        public ObservableValue<double> DifficultyScore { get; }
        public ObservableValue<string> ClaimLevel { get; }
        public ObservableValue<MissionClaimLevel> ClaimLevelEnum { get; }
        public ObservableValue<double> RewardMultiplier { get; }
        public ObservableValue<double> RiskMultiplier { get; }
        public ObservableValue<string> RewardSummary { get; }
        public ObservableValue<string> RiskSummary { get; }
        public ObservableValue<string> BadgeCssClass { get; }
        public ObservableValue<MissionDifficultyEvaluation> CurrentEvaluation { get; }

        public MissionClaimViewModel(
            DestinationRecord initialDestination = null,
            MissionTypeRecord initialMissionType = null,
            IEnumerable<ObjectiveRecord> initialObjectives = null)
        {
            _destination = initialDestination;
            _missionType = initialMissionType;
            if (initialObjectives != null)
                _objectives.AddRange(initialObjectives.Where(o => o != null));

            Destination = new ObservableValue<DestinationRecord>(_destination);
            MissionType = new ObservableValue<MissionTypeRecord>(_missionType);
            SelectedObjectives = new ObservableValue<IReadOnlyList<ObjectiveRecord>>(_objectives.ToList());

            DifficultyScore = new ObservableValue<double>(1.0);
            ClaimLevel = new ObservableValue<string>("Düşük");
            ClaimLevelEnum = new ObservableValue<MissionClaimLevel>(MissionClaimLevel.Low);
            RewardMultiplier = new ObservableValue<double>(1.0);
            RiskMultiplier = new ObservableValue<double>(1.0);
            RewardSummary = new ObservableValue<string>(string.Empty);
            RiskSummary = new ObservableValue<string>(string.Empty);
            BadgeCssClass = new ObservableValue<string>("claim-badge--low");
            CurrentEvaluation = new ObservableValue<MissionDifficultyEvaluation>(null);

            RegisterDisposable(Destination);
            RegisterDisposable(MissionType);
            RegisterDisposable(SelectedObjectives);
            RegisterDisposable(DifficultyScore);
            RegisterDisposable(ClaimLevel);
            RegisterDisposable(ClaimLevelEnum);
            RegisterDisposable(RewardMultiplier);
            RegisterDisposable(RiskMultiplier);
            RegisterDisposable(RewardSummary);
            RegisterDisposable(RiskSummary);
            RegisterDisposable(BadgeCssClass);
            RegisterDisposable(CurrentEvaluation);

            Recalculate();
        }

        /// <summary>
        /// Updates the target destination and triggers reactive recalculation.
        /// </summary>
        public void SetDestination(DestinationRecord destination)
        {
            _destination = destination;
            Destination.Value = destination;
            Recalculate();
        }

        /// <summary>
        /// Updates the mission type profile and triggers reactive recalculation.
        /// </summary>
        public void SetMissionType(MissionTypeRecord missionType)
        {
            _missionType = missionType;
            MissionType.Value = missionType;
            Recalculate();
        }

        /// <summary>
        /// Replaces the entire active objectives list and triggers reactive recalculation.
        /// </summary>
        public void SetObjectives(IEnumerable<ObjectiveRecord> objectives)
        {
            _objectives.Clear();
            if (objectives != null)
                _objectives.AddRange(objectives.Where(o => o != null));

            SelectedObjectives.Value = _objectives.ToList();
            Recalculate();
        }

        /// <summary>
        /// Toggles an objective in the current selection (adds if absent, removes if present).
        /// </summary>
        public void ToggleObjective(ObjectiveRecord objective)
        {
            if (objective == null) return;

            int index = _objectives.FindIndex(o => string.Equals(o.Id, objective.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _objectives.RemoveAt(index);
            else
                _objectives.Add(objective);

            SelectedObjectives.Value = _objectives.ToList();
            Recalculate();
        }

        /// <summary>
        /// Adds an objective to the selection if not already present.
        /// </summary>
        public void AddObjective(ObjectiveRecord objective)
        {
            if (objective == null) return;
            if (_objectives.Any(o => string.Equals(o.Id, objective.Id, StringComparison.OrdinalIgnoreCase))) return;

            _objectives.Add(objective);
            SelectedObjectives.Value = _objectives.ToList();
            Recalculate();
        }

        /// <summary>
        /// Removes an objective from the selection by ID.
        /// </summary>
        public void RemoveObjective(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId)) return;

            int removed = _objectives.RemoveAll(o => string.Equals(o.Id, objectiveId, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SelectedObjectives.Value = _objectives.ToList();
                Recalculate();
            }
        }

        /// <summary>
        /// Empties selected scientific objectives.
        /// </summary>
        public void ClearObjectives()
        {
            if (_objectives.Count > 0)
            {
                _objectives.Clear();
                SelectedObjectives.Value = _objectives.ToList();
                Recalculate();
            }
        }

        private void Recalculate()
        {
            var eval = MissionDifficultyCalculator.Evaluate(_destination, _missionType, _objectives);
            CurrentEvaluation.Value = eval;

            DifficultyScore.Value = eval.DifficultyScore;
            ClaimLevel.Value = eval.ClaimLevelLabel;
            ClaimLevelEnum.Value = eval.ClaimLevel;
            RewardMultiplier.Value = eval.RewardMultiplier;
            RiskMultiplier.Value = eval.RiskMultiplier;
            RewardSummary.Value = eval.RewardSummary;
            RiskSummary.Value = eval.RiskSummary;

            BadgeCssClass.Value = eval.ClaimLevel switch
            {
                MissionClaimLevel.Low => "claim-badge--low",
                MissionClaimLevel.Medium => "claim-badge--medium",
                MissionClaimLevel.High => "claim-badge--high",
                MissionClaimLevel.Extreme => "claim-badge--extreme",
                _ => "claim-badge--low"
            };

            OnPropertyChanged(nameof(CurrentEvaluation));
            OnPropertyChanged(nameof(DifficultyScore));
            OnPropertyChanged(nameof(ClaimLevel));
            OnPropertyChanged(nameof(RewardMultiplier));
            OnPropertyChanged(nameof(RiskMultiplier));
        }
    }
}
