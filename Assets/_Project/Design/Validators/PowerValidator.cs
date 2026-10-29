using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates electrical power generation, consumption, and safety margin.
    /// GDD §7.4 "Güç açığı", §8.1, and §21 M1-07.
    /// </summary>
    public sealed class PowerValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.Power;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            double gen = stats.PowerGeneration;
            double con = stats.PowerConsumption;

            if (con > 0.0 && gen < con)
            {
                double deficit = con - gen;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Güç Açığı (Yetersiz Enerji)",
                    reason: $"Araç güç tüketimi ({con:F2} kW), güç üretiminden ({gen:F2} kW) {deficit:F2} kW fazladır.",
                    hint: "Güneş paneli alanını büyütün, RTG ekleyin veya enerji tüketen ikincil alt sistemleri hafifletin.",
                    currentValue: gen,
                    targetValue: con);
            }
            else if (gen > 0.0)
            {
                double margin = (gen - con) / gen;
                if (margin < context.PowerMarginWarningThreshold)
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Warning,
                        title: "Düşük Güç Marjı",
                        reason: $"Güç üretim fazlalığı (%{margin * 100.0:F1}), önerilen güvenlik marjının (%{context.PowerMarginWarningThreshold * 100.0:F0}) altındadır.",
                        hint: "Tutulma (gölge) geçişlerinde veya güneş panel verimi düşüşünde batarya rezervi yetersiz kalabilir.",
                        currentValue: margin,
                        targetValue: context.PowerMarginWarningThreshold);
                }
                else
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Info,
                        title: "Güç Dengesi Kararlı",
                        reason: $"Güç üretimi ({gen:F2} kW), tüketimi ({con:F2} kW) %{margin * 100.0:F1} güvenlik marjıyla desteklemektedir.",
                        hint: "Enerji üretim ve dağıtım mimarisi yeterlidir.",
                        currentValue: gen,
                        targetValue: con);
                }
            }
            else if (blueprint.Payload != null && blueprint.Payload.Instruments.Count > 0 && gen <= 0.0)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Güç Kaynağı Yok",
                    reason: "Faydalı yükte aktif bilimsel aletler yer alıyor fakat araçta hiçbir güç üretim modülü (panel veya RTG) bulunmuyor.",
                    hint: "Uzay aracına güneş paneli veya RTG güç ünitesi ekleyin.",
                    currentValue: 0.0,
                    targetValue: 0.1);
            }
        }
    }
}
