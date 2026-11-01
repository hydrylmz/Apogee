using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Itemized breakdown of monthly infrastructure and operational expenses.
    /// Provides clear "why" explanations without exposing raw formulas to the UI (GDD §8, §13).
    /// </summary>
    public sealed class MonthlyExpenseBreakdown
    {
        public double TotalMonthlyCost { get; }
        public IReadOnlyList<FacilityExpenseItem> FacilityExpenses { get; }

        public MonthlyExpenseBreakdown(double totalMonthlyCost, IReadOnlyList<FacilityExpenseItem> facilityExpenses)
        {
            TotalMonthlyCost = totalMonthlyCost;
            FacilityExpenses = facilityExpenses ?? Array.Empty<FacilityExpenseItem>();
        }
    }

    public sealed class FacilityExpenseItem
    {
        public string FacilityId { get; }
        public string RegionId { get; }
        public BaseType FacilityType { get; }
        public int Level { get; }
        public double MonthlyCost { get; }

        public FacilityExpenseItem(string facilityId, string regionId, BaseType facilityType, int level, double monthlyCost)
        {
            FacilityId = facilityId;
            RegionId = regionId;
            FacilityType = facilityType;
            Level = level;
            MonthlyCost = monthlyCost;
        }

        public override string ToString() =>
            $"{FacilityType} (Lvl {Level}, {RegionId}): {MonthlyCost:F2} M$/mo";
    }

    /// <summary>
    /// Pure mathematical evaluator for campaign annual budget allocations,
    /// base maintenance expenditures, and economic solvency checks.
    /// Complies with GDD §4.3, §6.1, §6.4, §8.1, §16.7, §16.8, §19.13 and §21 M4-08.
    /// </summary>
    public static class EconomyEvaluator
    {
        /// <summary>
        /// Calculates the annual budget allocation granted to the agency on January 1st
        /// based on its current reputation score [0 - 100].
        /// GDD §4.3: "Bütçe: Yıllık tahsis (itibara bağlı) + bonuslar".
        /// </summary>
        public static double CalculateAnnualBudgetAllocation(double reputation, EconomySettings settings = null)
        {
            settings ??= EconomySettings.CreateDefault();
            double repClamped = Math.Clamp(reputation, EconomySettings.MinReputation, EconomySettings.MaxReputation);

            // Normalized ratio [0.0 - 1.0]
            double normalizedRep = repClamped / EconomySettings.MaxReputation;
            if (settings.ReputationCurveExponent != 1.0)
            {
                normalizedRep = Math.Pow(normalizedRep, settings.ReputationCurveExponent);
            }

            // Allocation curve: Minimum guaranteed floor + scaled allocation
            // At Rep 0: floor ~50M$, At Rep 30 (Start): ~275M$, At Rep 100: ~800M$
            double spread = settings.MaximumAnnualBudget - settings.MinimumAnnualBudget;
            double allocation = settings.MinimumAnnualBudget + spread * normalizedRep;

            return Math.Round(Math.Clamp(allocation, settings.MinimumAnnualBudget, settings.MaximumAnnualBudget), 1);
        }

        /// <summary>
        /// Computes the detailed breakdown and sum of monthly maintenance costs
        /// across all active agency base facilities (GDD §6.1, §8.1, §16.7).
        /// </summary>
        public static MonthlyExpenseBreakdown ComputeMonthlyExpenses(
            IEnumerable<BaseFacility> bases,
            EconomySettings settings = null)
        {
            settings ??= EconomySettings.CreateDefault();
            var items = new List<FacilityExpenseItem>();
            double total = 0.0;

            if (bases != null)
            {
                foreach (var facility in bases)
                {
                    double baseCost = BaseLevelSpec.GetMonthlyMaintenanceCost(facility.Type, facility.Level);
                    double effectiveCost = baseCost * settings.MaintenanceCostMultiplier;
                    effectiveCost = Math.Round(effectiveCost, 2);

                    items.Add(new FacilityExpenseItem(
                        facility.Id,
                        facility.RegionId,
                        facility.Type,
                        facility.Level,
                        effectiveCost));

                    total += effectiveCost;
                }
            }

            return new MonthlyExpenseBreakdown(Math.Round(total, 2), items);
        }

        /// <summary>
        /// Checks whether the agency is currently facing bankruptcy/insolvency (GDD §4.5).
        /// Failure condition: Budget &lt; BankruptcyDebtLimit AND Reputation &lt;= 0.
        /// </summary>
        public static bool IsBankrupt(double budget, double reputation, EconomySettings settings = null)
        {
            settings ??= EconomySettings.CreateDefault();
            return budget < settings.BankruptcyDebtLimit && reputation <= EconomySettings.MinReputation;
        }
    }
}
