using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// Step-by-step wizard ViewModel for Mission Composer (GDD §5.1 - §5.5, §14.1 item 5, §21 M3-04).
    /// Manages player selections across four sequential wizard steps:
    ///   1. Destination selection (LEO, Moon, Venus, Mars, Europa).
    ///   2. Mission type selection (Orbiter, Lander, Rover, Sample Return, Crewed).
    ///   3. Scientific and operational objectives (multi-select, dynamically filtered).
    ///   4. Launch window schedule, budget ceiling, and mission naming.
    /// Derives live claim levels, telemetry estimates, and constructs the final <see cref="MissionProfile"/>.
    /// </summary>
    public class MissionComposerViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private readonly List<ObjectiveRecord> _selectedObjectives = new();
        private bool _isCustomNameEntered = false;

        public IScreenNavigator Navigator => _navigator;

        // ── Catalogs ─────────────────────────────────────────────────────────────
        public IReadOnlyList<DestinationRecord> Destinations { get; }
        public IReadOnlyList<MissionTypeRecord> MissionTypes { get; }
        public IReadOnlyList<ObjectiveRecord> AllObjectives { get; }

        // ── Wizard Navigation Observables ─────────────────────────────────────────
        public ObservableValue<ComposerStep> CurrentStep { get; } = new(ComposerStep.Destination);
        public ObservableValue<int> CurrentStepIndex { get; } = new(0);
        public ObservableValue<string> StepTitle { get; } = new("1. HEDEF SEÇİMİ");
        public ObservableValue<bool> CanGoNext { get; } = new(false);
        public ObservableValue<bool> CanGoPrevious { get; } = new(false);
        public ObservableValue<bool> CanFinalize { get; } = new(false);

        // ── Selections Observables ────────────────────────────────────────────────
        public ObservableValue<DestinationRecord> SelectedDestination { get; } = new(null);
        public ObservableValue<MissionTypeRecord> SelectedMissionType { get; } = new(null);
        public ObservableValue<IReadOnlyList<ObjectiveRecord>> SelectedObjectives { get; } = new(Array.Empty<ObjectiveRecord>());
        public ObservableValue<IReadOnlyList<ObjectiveRecord>> CompatibleObjectives { get; } = new(Array.Empty<ObjectiveRecord>());
        public ObservableValue<int> SelectedWindowIndex { get; } = new(0);
        public ObservableValue<double?> BudgetCeiling { get; } = new(null);
        public ObservableValue<string> MissionName { get; } = new(string.Empty);

        // ── Live Claims & Telemetry Estimations ────────────────────────────────────
        public MissionClaimViewModel ClaimViewModel { get; }

        public ObservableValue<double> EstimatedDeltaV { get; } = new(0.0);
        public ObservableValue<int> EstimatedLaunchPadLevel { get; } = new(1);
        public ObservableValue<double> EstimatedReliabilityThreshold { get; } = new(0.80);
        public ObservableValue<string> EstimatedTransitRange { get; } = new("-");
        public ObservableValue<string> EstimatedCommDelayRange { get; } = new("-");
        public ObservableValue<string> LaunchWindowText { get; } = new("-");

        // ── Output ────────────────────────────────────────────────────────────────
        public ObservableValue<MissionProfile> ComposedProfile { get; } = new(null);
        public event Action<MissionProfile> OnMissionComposed;

        // ── Commands ──────────────────────────────────────────────────────────────
        public ViewActionCommand NextStepCommand { get; }
        public ViewActionCommand PreviousStepCommand { get; }
        public ViewActionCommand FinalizeMissionCommand { get; }

        public MissionComposerViewModel(
            IScreenNavigator navigator = null,
            IEnumerable<DestinationRecord> destinations = null,
            IEnumerable<MissionTypeRecord> missionTypes = null,
            IEnumerable<ObjectiveRecord> objectives = null,
            DestinationRecord initialDestination = null,
            MissionTypeRecord initialMissionType = null)
        {
            _navigator = navigator;

            Destinations = destinations != null
                ? destinations.ToList()
                : LoadDefaultDestinations();

            MissionTypes = missionTypes != null
                ? missionTypes.ToList()
                : LoadDefaultMissionTypes();

            AllObjectives = objectives != null
                ? objectives.ToList()
                : LoadDefaultObjectives();

            ClaimViewModel = new MissionClaimViewModel();
            RegisterDisposable(ClaimViewModel);

            RegisterDisposable(CurrentStep);
            RegisterDisposable(CurrentStepIndex);
            RegisterDisposable(StepTitle);
            RegisterDisposable(CanGoNext);
            RegisterDisposable(CanGoPrevious);
            RegisterDisposable(CanFinalize);
            RegisterDisposable(SelectedDestination);
            RegisterDisposable(SelectedMissionType);
            RegisterDisposable(SelectedObjectives);
            RegisterDisposable(CompatibleObjectives);
            RegisterDisposable(SelectedWindowIndex);
            RegisterDisposable(BudgetCeiling);
            RegisterDisposable(MissionName);
            RegisterDisposable(EstimatedDeltaV);
            RegisterDisposable(EstimatedLaunchPadLevel);
            RegisterDisposable(EstimatedReliabilityThreshold);
            RegisterDisposable(EstimatedTransitRange);
            RegisterDisposable(EstimatedCommDelayRange);
            RegisterDisposable(LaunchWindowText);
            RegisterDisposable(ComposedProfile);

            NextStepCommand = new ViewActionCommand(NextStep, () => CanGoNext.Value);
            PreviousStepCommand = new ViewActionCommand(PreviousStep, () => CanGoPrevious.Value);
            FinalizeMissionCommand = new ViewActionCommand(() => ComposeMissionProfile(), () => CanFinalize.Value);

            // Apply initial selection if provided, or default to first unlocked destination (e.g., Moon)
            var dest = initialDestination ?? Destinations.FirstOrDefault(d => d.Id == "moon") ?? Destinations.FirstOrDefault();
            if (dest != null)
            {
                SelectDestination(dest);
            }

            var type = initialMissionType ?? MissionTypes.FirstOrDefault(m => m.Id == "rover") ?? MissionTypes.FirstOrDefault();
            if (type != null)
            {
                SelectMissionType(type);
            }

            UpdateStepState();
        }

        // ── Step Navigation ───────────────────────────────────────────────────────

        public void GoToStep(ComposerStep step)
        {
            CurrentStep.Value = step;
            CurrentStepIndex.Value = (int)step;

            StepTitle.Value = step switch
            {
                ComposerStep.Destination => "1. HEDEF SEÇİMİ",
                ComposerStep.MissionType => "2. MİSYON TİPİ",
                ComposerStep.Objectives => "3. BİLİMSEL VE OPERASYONEL AMAÇLAR",
                ComposerStep.ScheduleAndBudget => "4. TAKVİM, BÜTÇE VE İSİM",
                _ => "MİSYON BESTELEYİCİ"
            };

            UpdateStepState();
        }

        public void NextStep()
        {
            if (CurrentStep.Value < ComposerStep.ScheduleAndBudget && CanGoNext.Value)
            {
                GoToStep((ComposerStep)((int)CurrentStep.Value + 1));
            }
        }

        public void PreviousStep()
        {
            if (CurrentStep.Value > ComposerStep.Destination)
            {
                GoToStep((ComposerStep)((int)CurrentStep.Value - 1));
            }
        }

        // ── Selection Operations ──────────────────────────────────────────────────

        public void SelectDestination(DestinationRecord destination)
        {
            if (destination == null) return;

            SelectedDestination.Value = destination;
            ClaimViewModel.SetDestination(destination);

            RefreshCompatibleObjectives();
            UpdateTelemetryEstimates();
            UpdateDefaultMissionName();
            UpdateStepState();
        }

        public void SelectMissionType(MissionTypeRecord missionType)
        {
            if (missionType == null) return;

            SelectedMissionType.Value = missionType;
            ClaimViewModel.SetMissionType(missionType);

            RefreshCompatibleObjectives();
            UpdateTelemetryEstimates();
            UpdateDefaultMissionName();
            UpdateStepState();
        }

        public void ToggleObjective(ObjectiveRecord objective)
        {
            if (objective == null) return;

            int index = _selectedObjectives.FindIndex(o => string.Equals(o.Id, objective.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _selectedObjectives.RemoveAt(index);
            }
            else
            {
                _selectedObjectives.Add(objective);
            }

            SelectedObjectives.Value = _selectedObjectives.ToList();
            ClaimViewModel.SetObjectives(_selectedObjectives);

            UpdateTelemetryEstimates();
            UpdateStepState();
        }

        public void SelectAllCompatibleObjectives()
        {
            var compatible = CompatibleObjectives.Value;
            if (compatible == null || compatible.Count == 0) return;

            _selectedObjectives.Clear();
            _selectedObjectives.AddRange(compatible);

            SelectedObjectives.Value = _selectedObjectives.ToList();
            ClaimViewModel.SetObjectives(_selectedObjectives);

            UpdateTelemetryEstimates();
            UpdateStepState();
        }

        public void ClearSelectedObjectives()
        {
            if (_selectedObjectives.Count > 0)
            {
                _selectedObjectives.Clear();
                SelectedObjectives.Value = _selectedObjectives.ToList();
                ClaimViewModel.ClearObjectives();

                UpdateTelemetryEstimates();
                UpdateStepState();
            }
        }

        public void SetWindowIndex(int windowIndex)
        {
            SelectedWindowIndex.Value = Math.Max(0, windowIndex);
            UpdateTelemetryEstimates();
        }

        public void SetBudgetCeiling(double? budget)
        {
            BudgetCeiling.Value = budget;
        }

        public void SetMissionName(string name)
        {
            _isCustomNameEntered = !string.IsNullOrWhiteSpace(name);
            MissionName.Value = name ?? string.Empty;
        }

        // ── Finalization & Profile Composition ────────────────────────────────────

        /// <summary>
        /// Composes the complete <see cref="MissionProfile"/> from active wizard selections (GDD §5.4, §21 M3-04).
        /// </summary>
        public MissionProfile ComposeMissionProfile()
        {
            if (SelectedDestination.Value == null)
                throw new InvalidOperationException("Cannot compose mission profile without selecting a destination.");
            if (SelectedMissionType.Value == null)
                throw new InvalidOperationException("Cannot compose mission profile without selecting a mission type.");

            string effectiveName = !string.IsNullOrWhiteSpace(MissionName.Value)
                ? MissionName.Value
                : GenerateDefaultMissionName(SelectedDestination.Value, SelectedMissionType.Value);

            var request = new MissionCompositionRequest(
                destination: SelectedDestination.Value,
                missionType: SelectedMissionType.Value,
                objectives: _selectedObjectives,
                windowIndex: SelectedWindowIndex.Value,
                budgetCeiling: BudgetCeiling.Value,
                missionName: effectiveName
            );

            var profile = MissionComposer.Compose(request);
            ComposedProfile.Value = profile;
            OnMissionComposed?.Invoke(profile);

            // Navigate to next screen with result arguments
            var navArgs = new MissionCompositionResultArgs(
                profile: profile,
                destination: SelectedDestination.Value,
                missionType: SelectedMissionType.Value,
                objectives: _selectedObjectives,
                windowIndex: SelectedWindowIndex.Value,
                budgetCeiling: BudgetCeiling.Value,
                missionName: effectiveName
            );

            if (_navigator != null)
            {
                bool navigated = _navigator.GoTo(ScreenId.MissionBrief, navArgs);
                if (!navigated)
                {
                    // Fallback to Workshop if MissionBrief is not yet registered
                    _navigator.GoTo(ScreenId.Workshop, navArgs);
                }
            }

            return profile;
        }

        // ── Internal Helpers ──────────────────────────────────────────────────────

        private void RefreshCompatibleObjectives()
        {
            var dest = SelectedDestination.Value;
            var type = SelectedMissionType.Value;

            if (dest == null)
            {
                CompatibleObjectives.Value = Array.Empty<ObjectiveRecord>();
                _selectedObjectives.Clear();
                SelectedObjectives.Value = Array.Empty<ObjectiveRecord>();
                ClaimViewModel.ClearObjectives();
                return;
            }

            var list = AllObjectives.Where(obj =>
            {
                if (obj.DestinationIds != null && obj.DestinationIds.Count > 0 && !obj.DestinationIds.Contains(dest.Id))
                    return false;

                if (type != null && obj.MissionTypeIds != null && obj.MissionTypeIds.Count > 0 && !obj.MissionTypeIds.Contains(type.Id))
                    return false;

                return true;
            }).ToList();

            CompatibleObjectives.Value = list;

            // Prune any selected objectives that are no longer compatible
            int beforeCount = _selectedObjectives.Count;
            _selectedObjectives.RemoveAll(s => !list.Any(c => string.Equals(c.Id, s.Id, StringComparison.OrdinalIgnoreCase)));

            if (_selectedObjectives.Count != beforeCount)
            {
                SelectedObjectives.Value = _selectedObjectives.ToList();
                ClaimViewModel.SetObjectives(_selectedObjectives);
            }
        }

        private void UpdateTelemetryEstimates()
        {
            var dest = SelectedDestination.Value;
            var type = SelectedMissionType.Value;

            if (dest == null)
            {
                EstimatedDeltaV.Value = 0.0;
                EstimatedLaunchPadLevel.Value = 1;
                EstimatedReliabilityThreshold.Value = 0.80;
                EstimatedTransitRange.Value = "-";
                EstimatedCommDelayRange.Value = "-";
                LaunchWindowText.Value = "-";
                return;
            }

            double dv = type != null
                ? MissionComposer.CalculateRequiredDeltaV(dest, type)
                : dest.DvAscent + dest.DvTransfer + dest.DvArrival;
            EstimatedDeltaV.Value = dv;

            int pad = type != null
                ? MissionComposer.DetermineRequiredLaunchPadLevel(dv, type.IsCrewed)
                : 1;
            EstimatedLaunchPadLevel.Value = pad;

            double rel = type != null
                ? MissionComposer.CalculateReliabilityThreshold(dest, type)
                : 0.80;
            EstimatedReliabilityThreshold.Value = rel;

            bool isRoundTrip = type != null && (type.Id == "sample-return" || type.IsCrewed) && dest.Id != "leo";
            int transitMultiplier = isRoundTrip ? 2 : 1;
            EstimatedTransitRange.Value = $"{dest.TransitDaysMin * transitMultiplier} - {dest.TransitDaysMax * transitMultiplier} gün";

            EstimatedCommDelayRange.Value = dest.CommDelaySecondsMax < 60
                ? $"{dest.CommDelaySecondsMin:F1}s - {dest.CommDelaySecondsMax:F1}s"
                : $"{dest.CommDelaySecondsMin / 60:F1}dk - {dest.CommDelaySecondsMax / 60:F1}dk";

            int wIndex = SelectedWindowIndex.Value;
            if (dest.WindowPeriodDays <= 0)
            {
                LaunchWindowText.Value = "Sürekli Fırlatma Penceresi (LEO)";
            }
            else
            {
                int offsetDays = dest.WindowFirstOffsetDays + (wIndex * dest.WindowPeriodDays);
                LaunchWindowText.Value = $"Pencere {wIndex}: T+{offsetDays} gün (Süre: {dest.WindowDurationDays} gün)";
            }
        }

        private void UpdateDefaultMissionName()
        {
            if (_isCustomNameEntered) return;

            var dest = SelectedDestination.Value;
            var type = SelectedMissionType.Value;
            if (dest != null && type != null)
            {
                MissionName.Value = GenerateDefaultMissionName(dest, type);
            }
        }

        private static string GenerateDefaultMissionName(DestinationRecord dest, MissionTypeRecord type)
        {
            string dName = dest?.Id?.ToUpperInvariant() switch
            {
                "LEO" => "LEO",
                "MOON" => "AY",
                "MARS" => "MARS",
                "VENUS" => "VENÜS",
                "EUROPA" => "EUROPA",
                _ => dest?.Id?.ToUpperInvariant() ?? "GÖREV"
            };

            string tName = type?.Id?.ToUpperInvariant() switch
            {
                "ORBITER" => "YÖRÜNGE",
                "LANDER" => "İNİŞ",
                "ROVER" => "GEZGİN",
                "SAMPLE-RETURN" => "NUMUNE",
                "CREWED" => "İNSANLI",
                _ => type?.Id?.ToUpperInvariant() ?? "MİSYON"
            };

            return $"{dName} {tName}-1";
        }

        private void UpdateStepState()
        {
            bool hasValidDest = SelectedDestination.Value != null;
            bool hasValidType = SelectedMissionType.Value != null;

            CanGoPrevious.Value = CurrentStep.Value > ComposerStep.Destination;

            CanGoNext.Value = CurrentStep.Value switch
            {
                ComposerStep.Destination => hasValidDest,
                ComposerStep.MissionType => hasValidType,
                ComposerStep.Objectives => true,
                ComposerStep.ScheduleAndBudget => false,
                _ => false
            };

            CanFinalize.Value = hasValidDest && hasValidType;

            NextStepCommand?.RaiseCanExecuteChanged();
            PreviousStepCommand?.RaiseCanExecuteChanged();
            FinalizeMissionCommand?.RaiseCanExecuteChanged();
        }

        // ── Catalog Loaders & Fallbacks ───────────────────────────────────────────

        private static List<DestinationRecord> LoadDefaultDestinations()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "destinations.json");
            if (File.Exists(path))
            {
                try
                {
                    return CatalogLoader.Load<DestinationRecord>(File.ReadAllText(path)).All.ToList();
                }
                catch { }
            }

            return new List<DestinationRecord>();
        }

        private static List<MissionTypeRecord> LoadDefaultMissionTypes()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "mission_types.json");
            if (File.Exists(path))
            {
                try
                {
                    return CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(path)).All.ToList();
                }
                catch { }
            }

            return new List<MissionTypeRecord>();
        }

        private static List<ObjectiveRecord> LoadDefaultObjectives()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "objectives.json");
            if (File.Exists(path))
            {
                try
                {
                    return CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(path)).All.ToList();
                }
                catch { }
            }

            return new List<ObjectiveRecord>();
        }
    }
}
