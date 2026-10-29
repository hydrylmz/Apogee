using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents an individual finding produced by a design validator.
    /// In accordance with GDD §7.4 and §8.3:
    /// Every issue produces a clear explanation of "why" (Reason) and guidance on "what to inspect" (Hint)
    /// without dictating the exact answer.
    /// </summary>
    public sealed class ValidationIssue
    {
        public string RuleId { get; }
        public ValidationSeverity Severity { get; }
        public string Title { get; }
        public string Reason { get; }
        public string Hint { get; }

        public double? CurrentValue { get; }
        public double? TargetValue { get; }
        public string SourceComponentId { get; }

        public bool IsError => Severity == ValidationSeverity.Error;
        public bool IsWarning => Severity == ValidationSeverity.Warning;
        public bool IsInfo => Severity == ValidationSeverity.Info;

        public ValidationIssue(
            string ruleId,
            ValidationSeverity severity,
            string title,
            string reason,
            string hint,
            double? currentValue = null,
            double? targetValue = null,
            string sourceComponentId = null)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
                throw new ArgumentException("RuleId cannot be empty.", nameof(ruleId));

            RuleId = ruleId;
            Severity = severity;
            Title = title ?? ruleId;
            Reason = reason ?? string.Empty;
            Hint = hint ?? string.Empty;
            CurrentValue = currentValue;
            TargetValue = targetValue;
            SourceComponentId = sourceComponentId;
        }

        public override string ToString() => $"[{Severity}] ({RuleId}) {Title} - {Reason} (Hint: {Hint})";
    }
}
