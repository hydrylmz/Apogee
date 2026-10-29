using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Pure C# differential analysis engine for comparing two vehicle blueprints (GDD §7.5 & §14.1 #17).
    /// </summary>
    public static class BlueprintComparator
    {
        public static BlueprintDifference Compare(
            Blueprint blueprintA,
            Blueprint blueprintB,
            double requiredDeltaV = 13500.0)
        {
            if (blueprintA == null) throw new ArgumentNullException(nameof(blueprintA));
            if (blueprintB == null) throw new ArgumentNullException(nameof(blueprintB));

            var statsA = BlueprintEvaluator.Evaluate(blueprintA, requiredDeltaV: requiredDeltaV);
            var statsB = BlueprintEvaluator.Evaluate(blueprintB, requiredDeltaV: requiredDeltaV);

            // 1. Stat comparisons
            var statItems = new List<StatComparisonItem>
            {
                new StatComparisonItem(
                    "DeltaV",
                    "Menzil (Δv)",
                    statsA.NetDeltaV,
                    statsB.NetDeltaV,
                    "m/s",
                    higherIsBetter: true,
                    format: "F0"),

                new StatComparisonItem(
                    "WetMass",
                    "Islak Kütle",
                    statsA.WetMass,
                    statsB.WetMass,
                    "t",
                    higherIsBetter: false,
                    format: "F2"),

                new StatComparisonItem(
                    "DryMass",
                    "Kuru Kütle",
                    statsA.DryMass,
                    statsB.DryMass,
                    "t",
                    higherIsBetter: false,
                    format: "F2"),

                new StatComparisonItem(
                    "Cost",
                    "Tahmini Maliyet",
                    statsA.Cost,
                    statsB.Cost,
                    "M$",
                    higherIsBetter: false,
                    format: "F1"),

                new StatComparisonItem(
                    "Reliability",
                    "Güvenilirlik",
                    statsA.Reliability * 100.0,
                    statsB.Reliability * 100.0,
                    "%",
                    higherIsBetter: true,
                    format: "F1"),

                new StatComparisonItem(
                    "PowerMargin",
                    "Güç Dengesi",
                    statsA.PowerMargin,
                    statsB.PowerMargin,
                    "kW",
                    higherIsBetter: true,
                    format: "F2"),

                new StatComparisonItem(
                    "ThermalCapacity",
                    "Isıl Kapasite",
                    statsA.ThermalDissipation,
                    statsB.ThermalDissipation,
                    "kW",
                    higherIsBetter: true,
                    format: "F2"),

                new StatComparisonItem(
                    "BuildTime",
                    "Üretim Süresi",
                    statsA.BuildTimeDays,
                    statsB.BuildTimeDays,
                    "gün",
                    higherIsBetter: false,
                    format: "F0")
            };

            // 2. Slot comparisons
            var slotItems = new List<SlotComparisonItem>();
            foreach (VehicleSlotId slotId in Enum.GetValues(typeof(VehicleSlotId)))
            {
                var def = VehicleSlotDefinition.Get(slotId);
                string slotName = def?.DisplayName ?? slotId.ToString();
                var compA = blueprintA.GetComponentInSlot(slotId);
                var compB = blueprintB.GetComponentInSlot(slotId);

                slotItems.Add(new SlotComparisonItem(slotId, slotName, compA, compB));
            }

            return new BlueprintDifference(blueprintA, blueprintB, statsA, statsB, statItems, slotItems);
        }
    }
}
