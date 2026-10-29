using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Apogee.Design
{
    /// <summary>
    /// Single row in a statistic's source breakdown list.
    /// Explains how a specific source contributed to the final value.
    /// </summary>
    public sealed class StatBreakdownEntry
    {
        public string SourceName { get; }
        public StatModifierType ModifierType { get; }
        public double ModifierValue { get; }

        /// <summary>
        /// The computed delta this entry contributed to the final stat value in physical units.
        /// </summary>
        public double NetContribution { get; }

        public string DescriptionKey { get; }

        public StatBreakdownEntry(
            string sourceName,
            StatModifierType modifierType,
            double modifierValue,
            double netContribution,
            string descriptionKey = "")
        {
            SourceName = sourceName ?? "Unknown";
            ModifierType = modifierType;
            ModifierValue = modifierValue;
            NetContribution = netContribution;
            DescriptionKey = descriptionKey ?? string.Empty;
        }

        public override string ToString()
        {
            var inv = CultureInfo.InvariantCulture;
            string sign = NetContribution >= 0 ? "+" : "";
            string modDisplay = ModifierType == StatModifierType.Flat
                ? string.Format(inv, "{0}{1:0.###}", sign, ModifierValue)
                : string.Format(inv, "{0}{1:0.#}% ({2}{3:0.###})", (ModifierValue >= 0 ? "+" : ""), ModifierValue * 100, sign, NetContribution);

            return $"{SourceName}: {modDisplay}";
        }
    }

    /// <summary>
    /// Complete explanation breakdown of a computed statistic.
    /// Provides base value, modifiers, net contributions, and player-friendly summary text.
    /// Complies with GDD §8.2, §8.3 and §13.2.
    /// </summary>
    public sealed class StatBreakdown
    {
        public StatType TargetStat { get; }
        public StatType Type => TargetStat;
        public double BaseValue { get; }
        public double FinalValue { get; }
        public IReadOnlyList<StatBreakdownEntry> Entries { get; }

        public StatBreakdown(StatType targetStat, double baseValue, double finalValue, List<StatBreakdownEntry> entries)
        {
            TargetStat = targetStat;
            BaseValue = baseValue;
            FinalValue = finalValue;
            Entries = entries ?? new List<StatBreakdownEntry>();
        }

        /// <summary>
        /// Generates a human-readable multi-line explanation of the calculation.
        /// </summary>
        public string ToFormattedSummary(string unit = "")
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"--- {TargetStat} Breakdown ---");
            sb.AppendLine(string.Format(inv, "Base: {0:0.###} {1}", BaseValue, unit).Trim());

            foreach (var entry in Entries)
            {
                sb.AppendLine($"  {entry}");
            }

            sb.AppendLine(string.Format(inv, "Total: {0:0.###} {1}", FinalValue, unit).Trim());
            return sb.ToString();
        }

        public override string ToString() => ToFormattedSummary();
    }
}
