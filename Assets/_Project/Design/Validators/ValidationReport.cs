using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Design
{
    /// <summary>
    /// Comprehensive validation review report produced by <see cref="BlueprintValidator"/>.
    /// Contains evaluated issues, traffic light status, and helper filters for UI and game logic.
    /// Complies with GDD §7.4 (Design Review) and §17.3 item 6.
    /// </summary>
    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues;

        public Blueprint Blueprint { get; }
        public BlueprintStats Stats { get; }
        public ValidationContext Context { get; }

        public IReadOnlyList<ValidationIssue> Issues => _issues;

        /// <summary>
        /// True if there are zero blocking errors (Severity == Error).
        /// Warnings do not prevent validity.
        /// </summary>
        public bool IsValid => !_issues.Any(i => i.IsError);

        /// <summary>
        /// True if there is at least one warning (Severity == Warning).
        /// </summary>
        public bool HasWarnings => _issues.Any(i => i.IsWarning);

        public IEnumerable<ValidationIssue> Errors => _issues.Where(i => i.IsError);
        public IEnumerable<ValidationIssue> Warnings => _issues.Where(i => i.IsWarning);
        public IEnumerable<ValidationIssue> Infos => _issues.Where(i => i.IsInfo);

        public int ErrorCount => _issues.Count(i => i.IsError);
        public int WarningCount => _issues.Count(i => i.IsWarning);
        public int InfoCount => _issues.Count(i => i.IsInfo);

        public ValidationReport(
            Blueprint blueprint,
            BlueprintStats stats,
            ValidationContext context,
            IEnumerable<ValidationIssue> issues)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Context = context ?? throw new ArgumentNullException(nameof(context));

            _issues = issues != null ? issues.ToList() : new List<ValidationIssue>();
        }

        public IEnumerable<ValidationIssue> GetIssuesForRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return Enumerable.Empty<ValidationIssue>();
            return _issues.Where(i => string.Equals(i.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasError(string ruleId) =>
            _issues.Any(i => i.IsError && string.Equals(i.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

        public bool HasWarning(string ruleId) =>
            _issues.Any(i => i.IsWarning && string.Equals(i.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));

        public override string ToString() =>
            $"[ValidationReport: {(IsValid ? "PASS" : "FAIL")}] Errors={ErrorCount}, Warnings={WarningCount}, Infos={InfoCount}";
    }
}
