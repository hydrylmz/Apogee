using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Comparison
{
    /// <summary>
    /// Navigation arguments passed when opening the Comparison screen (GDD §7.5 & §14.1 #17).
    /// </summary>
    public sealed class ComparisonNavigationArgs
    {
        public Blueprint BlueprintA { get; }
        public Blueprint BlueprintB { get; }
        public string LabelA { get; }
        public string LabelB { get; }
        public double RequiredDeltaV { get; }

        public ComparisonNavigationArgs(
            Blueprint blueprintA,
            Blueprint blueprintB,
            string labelA = "Tasarım A",
            string labelB = "Tasarım B",
            double requiredDeltaV = 13500.0)
        {
            BlueprintA = blueprintA ?? throw new ArgumentNullException(nameof(blueprintA));
            BlueprintB = blueprintB ?? throw new ArgumentNullException(nameof(blueprintB));
            LabelA = labelA;
            LabelB = labelB;
            RequiredDeltaV = requiredDeltaV;
        }
    }

    /// <summary>
    /// ViewModel for the Blueprint Comparison screen (GDD §7.5 & §14.1 #17).
    /// Computes and exposes BlueprintDifference including per-stat and per-slot comparisons.
    /// </summary>
    public sealed class ComparisonViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        public ObservableValue<string> LabelA { get; } = new("Tasarım A");
        public ObservableValue<string> LabelB { get; } = new("Tasarım B");
        public ObservableValue<string> SummaryText { get; } = new(string.Empty);

        public ObservableValue<IReadOnlyList<StatComparisonItem>> StatComparisons { get; } =
            new(Array.Empty<StatComparisonItem>());

        public ObservableValue<IReadOnlyList<SlotComparisonItem>> SlotComparisons { get; } =
            new(Array.Empty<SlotComparisonItem>());

        public ObservableValue<int> ChangedSlotsCount { get; } = new(0);
        public ObservableValue<int> BetterCount { get; } = new(0);
        public ObservableValue<int> WorseCount { get; } = new(0);

        public ObservableValue<IReadOnlyList<SlotComparisonItem>> ChangedSlotsOnly { get; } =
            new(Array.Empty<SlotComparisonItem>());

        public ObservableValue<bool> ShowAllSlots { get; } = new(false);

        public ViewActionCommand BackCommand { get; }
        public ViewActionCommand ToggleSlotFilterCommand { get; }

        public ComparisonViewModel(IScreenNavigator navigator = null)
        {
            _navigator = navigator;

            BackCommand = new ViewActionCommand(() => _navigator?.GoBack());
            ToggleSlotFilterCommand = new ViewActionCommand(() =>
            {
                ShowAllSlots.Value = !ShowAllSlots.Value;
            });
        }

        public void ApplyComparison(ComparisonNavigationArgs args)
        {
            if (args == null) return;

            LabelA.Value = args.LabelA;
            LabelB.Value = args.LabelB;

            var diff = BlueprintComparator.Compare(args.BlueprintA, args.BlueprintB, args.RequiredDeltaV);

            StatComparisons.Value = diff.StatComparisons;
            SlotComparisons.Value = diff.SlotComparisons;
            ChangedSlotsCount.Value = diff.ChangedSlotsCount;
            BetterCount.Value = diff.BetterStatsCount;
            WorseCount.Value = diff.WorseStatsCount;

            ChangedSlotsOnly.Value = diff.SlotComparisons
                .Where(s => s.Status != SlotComparisonStatus.BothEmpty && s.Status != SlotComparisonStatus.Identical)
                .ToList();

            SummaryText.Value = BuildSummary(diff);
        }

        private static string BuildSummary(BlueprintDifference diff)
        {
            int changed = diff.ChangedSlotsCount;
            int better = diff.BetterStatsCount;
            int worse = diff.WorseStatsCount;

            if (changed == 0 && better == 0 && worse == 0)
                return "İki tasarım özdeş.";

            var parts = new List<string>();
            if (changed > 0) parts.Add($"{changed} slot değişti");
            if (better > 0) parts.Add($"{better} istatistik iyileşti");
            if (worse > 0) parts.Add($"{worse} istatistik kötüleşti");

            return string.Join(" · ", parts) + ".";
        }
    }
}
