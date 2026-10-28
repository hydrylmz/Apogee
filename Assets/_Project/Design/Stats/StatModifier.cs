using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents a discrete modification applied to a statistic.
    /// Tracks source, value, modifier type, and explanation key for player transparency.
    /// </summary>
    public sealed class StatModifier
    {
        public string Id { get; }
        public StatType TargetStat { get; }
        public StatModifierType Type { get; }
        public double Value { get; }
        public int Order { get; }

        /// <summary>
        /// Source object or token that introduced this modifier (e.g. Supplier, Material, TechNode).
        /// </summary>
        public object Source { get; }

        /// <summary>
        /// Human-readable name or label of the source (e.g. "Nordvik Propulsion", "Titanium Alloy").
        /// </summary>
        public string SourceName { get; }

        /// <summary>
        /// Localization key or description token explaining this modifier.
        /// </summary>
        public string DescriptionKey { get; }

        public StatModifier(
            StatType targetStat,
            StatModifierType type,
            double value,
            object source = null,
            string sourceName = "",
            string descriptionKey = "",
            string id = null,
            int? customOrder = null)
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
            TargetStat = targetStat;
            Type = type;
            Value = value;
            Source = source;
            SourceName = string.IsNullOrEmpty(sourceName) ? (source?.ToString() ?? "Unknown") : sourceName;
            DescriptionKey = descriptionKey ?? string.Empty;
            Order = customOrder ?? (int)type;
        }

        public static StatModifier CreateFlat(StatType stat, double value, object source = null, string sourceName = "", string descriptionKey = "") =>
            new StatModifier(stat, StatModifierType.Flat, value, source, sourceName, descriptionKey);

        public static StatModifier CreatePercentAdd(StatType stat, double value, object source = null, string sourceName = "", string descriptionKey = "") =>
            new StatModifier(stat, StatModifierType.PercentAdd, value, source, sourceName, descriptionKey);

        public static StatModifier CreatePercentMult(StatType stat, double value, object source = null, string sourceName = "", string descriptionKey = "") =>
            new StatModifier(stat, StatModifierType.PercentMult, value, source, sourceName, descriptionKey);

        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            string valStr = Type == StatModifierType.Flat ? $"{sign}{Value:0.###}" : $"{sign}{Value * 100:0.##}%";
            return $"[{TargetStat}] {SourceName}: {valStr} ({Type})";
        }
    }
}
