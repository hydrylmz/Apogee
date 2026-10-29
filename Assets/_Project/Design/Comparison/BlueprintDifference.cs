using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    public enum SlotComparisonStatus
    {
        BothEmpty = 0,
        Identical = 1,
        Changed = 2,
        AddedInB = 3,
        RemovedInB = 4
    }

    /// <summary>
    /// Holds comparison data for a single telemetry statistic between two blueprints (GDD §7.5 & §14.1).
    /// </summary>
    public sealed class StatComparisonItem
    {
        public string Key { get; }
        public string Label { get; }
        public double ValueA { get; }
        public double ValueB { get; }
        public double Delta => ValueB - ValueA;
        public string Unit { get; }
        public bool? IsBetter { get; }

        public string FormattedValueA { get; }
        public string FormattedValueB { get; }
        public string FormattedDelta { get; }

        public StatComparisonItem(
            string key,
            string label,
            double valueA,
            double valueB,
            string unit,
            bool higherIsBetter,
            string format = "F1")
        {
            Key = key;
            Label = label;
            ValueA = valueA;
            ValueB = valueB;
            Unit = unit;

            FormattedValueA = $"{valueA.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {unit}".Trim();
            FormattedValueB = $"{valueB.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {unit}".Trim();

            double diff = ValueB - ValueA;
            if (Math.Abs(diff) < 0.0001)
            {
                IsBetter = null;
                FormattedDelta = "0";
            }
            else
            {
                IsBetter = higherIsBetter ? diff > 0 : diff < 0;
                string sign = diff > 0 ? "+" : "";
                FormattedDelta = $"{sign}{diff.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {unit}".Trim();
            }
        }
    }

    /// <summary>
    /// Holds comparison data for a single slot module between two blueprints.
    /// </summary>
    public sealed class SlotComparisonItem
    {
        public VehicleSlotId SlotId { get; }
        public string SlotName { get; }
        public DesignComponent ComponentA { get; }
        public DesignComponent ComponentB { get; }
        public SlotComparisonStatus Status { get; }

        public string NameA => ComponentA != null ? ComponentA.Name : "(Boş)";
        public string NameB => ComponentB != null ? ComponentB.Name : "(Boş)";

        public SlotComparisonItem(
            VehicleSlotId slotId,
            string slotName,
            DesignComponent componentA,
            DesignComponent componentB)
        {
            SlotId = slotId;
            SlotName = slotName;
            ComponentA = componentA;
            ComponentB = componentB;

            if (componentA == null && componentB == null)
            {
                Status = SlotComparisonStatus.BothEmpty;
            }
            else if (componentA == null)
            {
                Status = SlotComparisonStatus.AddedInB;
            }
            else if (componentB == null)
            {
                Status = SlotComparisonStatus.RemovedInB;
            }
            else if (string.Equals(componentA.Id, componentB.Id, StringComparison.OrdinalIgnoreCase))
            {
                Status = SlotComparisonStatus.Identical;
            }
            else
            {
                Status = SlotComparisonStatus.Changed;
            }
        }
    }

    /// <summary>
    /// Comprehensive differential analysis between two blueprints (Design A vs Design B).
    /// Used by Comparison Screen and Review (GDD §7.5 & §14.1 #17).
    /// </summary>
    public sealed class BlueprintDifference
    {
        public Blueprint BlueprintA { get; }
        public Blueprint BlueprintB { get; }
        public BlueprintStats StatsA { get; }
        public BlueprintStats StatsB { get; }

        public IReadOnlyList<StatComparisonItem> StatComparisons { get; }
        public IReadOnlyList<SlotComparisonItem> SlotComparisons { get; }

        public int ChangedSlotsCount { get; }
        public int BetterStatsCount { get; }
        public int WorseStatsCount { get; }

        public BlueprintDifference(
            Blueprint blueprintA,
            Blueprint blueprintB,
            BlueprintStats statsA,
            BlueprintStats statsB,
            IReadOnlyList<StatComparisonItem> statComparisons,
            IReadOnlyList<SlotComparisonItem> slotComparisons)
        {
            BlueprintA = blueprintA;
            BlueprintB = blueprintB;
            StatsA = statsA;
            StatsB = statsB;
            StatComparisons = statComparisons ?? Array.Empty<StatComparisonItem>();
            SlotComparisons = slotComparisons ?? Array.Empty<SlotComparisonItem>();

            int changed = 0;
            foreach (var sc in SlotComparisons)
            {
                if (sc.Status != SlotComparisonStatus.BothEmpty && sc.Status != SlotComparisonStatus.Identical)
                {
                    changed++;
                }
            }
            ChangedSlotsCount = changed;

            int better = 0;
            int worse = 0;
            foreach (var st in StatComparisons)
            {
                if (st.IsBetter == true) better++;
                else if (st.IsBetter == false) worse++;
            }
            BetterStatsCount = better;
            WorseStatsCount = worse;
        }
    }
}
