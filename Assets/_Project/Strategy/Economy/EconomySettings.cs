using System;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure data model configuring campaign economics, annual budget allocations,
    /// reputation scaling, maintenance cost modifiers, and insolvency thresholds.
    /// Fully data-driven; complies with GDD §4.3, §6.4, §8.1, §16.8, §19.13 and §21 M4-08.
    /// Uncalibrated default figures are documented as TODO-VERİ.
    /// </summary>
    public sealed class EconomySettings
    {
        // ---------------------------------------------------------------- Budget Allocation
        /// <summary>
        /// Base annual government/consortium budget allocation in million USD (M$) before reputation multipliers.
        /// (TODO-VERİ: default 250.0 M$ per game year).
        /// </summary>
        public double BaseAnnualBudget { get; set; } = 250.0;

        /// <summary>
        /// Minimum annual budget floor in M$, guaranteed regardless of how low reputation drops.
        /// Prevents immediate soft-lock while penalizing poor performance.
        /// </summary>
        public double MinimumAnnualBudget { get; set; } = 50.0;

        /// <summary>
        /// Maximum annual budget ceiling in M$, reached at peak reputation (100).
        /// </summary>
        public double MaximumAnnualBudget { get; set; } = 800.0;

        /// <summary>
        /// Reputation exponent curve. Formula:
        /// Allocation = BaseAnnualBudget * (0.2 + 0.8 * (Reputation / 100)^ReputationCurveExponent) * Multiplier.
        /// Linear (1.0) by default.
        /// </summary>
        public double ReputationCurveExponent { get; set; } = 1.0;

        // ---------------------------------------------------------------- Reputation Scale
        public const double MinReputation = 0.0;
        public const double MaxReputation = 100.0;

        /// <summary>
        /// Monthly passive reputation decay if no active missions or achievements occur.
        /// Default 0.0 (no passive decay in standard difficulty).
        /// </summary>
        public double PassiveReputationDecayPerMonth { get; set; } = 0.0;

        // ---------------------------------------------------------------- Maintenance
        /// <summary>
        /// Global multiplier applied to all base facility maintenance costs (from BaseLevelSpec).
        /// Allows difficulty scaling (e.g. 0.8 for Easy, 1.2 for Hard).
        /// </summary>
        public double MaintenanceCostMultiplier { get; set; } = 1.0;

        // ---------------------------------------------------------------- Insolvency & Failure Condition
        /// <summary>
        /// If budget drops below this threshold AND reputation reaches MinReputation (0),
        /// the campaign enters insolvency crisis (GDD §4.5).
        /// </summary>
        public double BankruptcyDebtLimit { get; set; } = -100.0;

        // ---------------------------------------------------------------- Factory Method
        /// <summary>Returns the standard default economy configuration for normal difficulty.</summary>
        public static EconomySettings CreateDefault() => new EconomySettings();
    }
}
