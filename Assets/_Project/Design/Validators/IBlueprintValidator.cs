using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Pure function validator interface for inspecting a blueprint against engineering/mission constraints.
    /// Complies with GDD §7.4, §17.3 item 6, and §20.1.
    /// </summary>
    public interface IBlueprintValidator
    {
        /// <summary>Unique rule identifier handled by this validator (see <see cref="ValidationRuleId"/>).</summary>
        string RuleId { get; }

        /// <summary>
        /// Validates a vehicle blueprint and its calculated stats against the supplied context.
        /// </summary>
        /// <param name="blueprint">The vehicle blueprint under test.</param>
        /// <param name="stats">The derived statistics calculated for this blueprint.</param>
        /// <param name="context">Requirements, infrastructure limits, and technology unlocks.</param>
        /// <returns>A collection of validation issues found, or empty/info if valid.</returns>
        IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context);
    }
}
