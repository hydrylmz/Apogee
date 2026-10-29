using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates vehicle aggregate system reliability against the mission's minimum acceptable threshold.
    /// GDD §7.4, §8.1, and §21 M1-07.
    /// </summary>
    public sealed class ReliabilityValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.Reliability;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            double minReliability = context.MissionProfile != null && context.MissionProfile.MinReliabilityThreshold > 0.0
                ? context.MissionProfile.MinReliabilityThreshold
                : 0.80;

            double current = stats.Reliability;

            if (current < minReliability)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Güvenilirlik Eşiği Altında",
                    reason: $"Araç genel sistem güvenilirliği (%{current * 100.0:F1}), misyonun zorunlu güvenlik eşiğinin (%{minReliability * 100.0:F1}) altındadır.",
                    hint: "Zayıf halka durumundaki düşük güvenilirlikli modülleri sertifikalı tedarikçilerle değiştirin veya üretim kalite testlerini artırın.",
                    currentValue: current,
                    targetValue: minReliability);
            }
            else if (current < minReliability + context.ReliabilityMarginWarningThreshold)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Warning,
                    title: "Kritik Güvenilirlik Marjı",
                    reason: $"Sistem güvenilirliği (%{current * 100.0:F1}), minimum güvenlik sınırına (%{minReliability * 100.0:F1}) çok yakındır.",
                    hint: "Tek nokta arıza risklerini azaltmak için en düşük güvenilirlik çarpanına sahip bileşenleri gözden geçirin.",
                    currentValue: current,
                    targetValue: minReliability);
            }
            else
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Info,
                    title: "Güvenilirlik Standartlara Uygun",
                    reason: $"Sistem güvenilirliği (%{current * 100.0:F1}), misyon gereksinimini güvenli bir toleransla sağlamaktadır.",
                    hint: "Bileşen zinciri görev riski açısından dengelidir.",
                    currentValue: current,
                    targetValue: minReliability);
            }
        }
    }
}
