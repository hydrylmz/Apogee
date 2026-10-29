using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Pure functional coordinator for design validation and vehicle Design Review (GDD §7.4).
    /// Executes the 8 core design validators and aggregates findings into a <see cref="ValidationReport"/>.
    /// Satisfies GDD §7.4, §8.1, §17.3 item 6, and §21 M1-07.
    /// </summary>
    public static class BlueprintValidator
    {
        private static readonly IBlueprintValidator[] StandardValidators = new IBlueprintValidator[]
        {
            new DeltaVValidator(),
            new MassCapacityValidator(),
            new LaunchPadValidator(),
            new ResearchLockValidator(),
            new PowerValidator(),
            new ThermalValidator(),
            new MandatoryModuleValidator(),
            new ReliabilityValidator()
        };

        /// <summary>
        /// Gets the immutable standard set of 8 core design validators.
        /// </summary>
        public static IReadOnlyList<IBlueprintValidator> DefaultValidators => StandardValidators;

        /// <summary>
        /// Validates a vehicle blueprint and its calculated statistics against the given validation context.
        /// Pure function: (Blueprint, BlueprintStats, ValidationContext) -> ValidationReport.
        /// </summary>
        /// <param name="blueprint">Vehicle design blueprint to evaluate.</param>
        /// <param name="stats">Derived engineering statistics for the vehicle.</param>
        /// <param name="context">Validation context (Mission profile, launch pad, technology unlocks).</param>
        /// <param name="customValidators">Optional custom or supplemental validators.</param>
        /// <returns>A comprehensive <see cref="ValidationReport"/>.</returns>
        public static ValidationReport Validate(
            Blueprint blueprint,
            BlueprintStats stats,
            ValidationContext context,
            IEnumerable<IBlueprintValidator> customValidators = null)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));

            context ??= new ValidationContext();

            var issues = new List<ValidationIssue>();

            // Run standard 8 validators
            for (int i = 0; i < StandardValidators.Length; i++)
            {
                var issuesFromRule = StandardValidators[i].Validate(blueprint, stats, context);
                if (issuesFromRule != null)
                {
                    issues.AddRange(issuesFromRule);
                }
            }

            // Run any additional validators
            if (customValidators != null)
            {
                foreach (var validator in customValidators)
                {
                    if (validator == null) continue;
                    var issuesFromRule = validator.Validate(blueprint, stats, context);
                    if (issuesFromRule != null)
                    {
                        issues.AddRange(issuesFromRule);
                    }
                }
            }

            return new ValidationReport(blueprint, stats, context, issues);
        }

        /// <summary>
        /// Convenience overload that automatically computes <see cref="BlueprintStats"/> via <see cref="BlueprintEvaluator"/>
        /// before executing all validators.
        /// </summary>
        public static ValidationReport Validate(
            Blueprint blueprint,
            ValidationContext context,
            IEnumerable<IBlueprintValidator> customValidators = null)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            return Validate(blueprint, stats, context, customValidators);
        }
    }
}
