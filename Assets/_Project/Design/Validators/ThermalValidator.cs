using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates vehicle thermal load, radiation dissipation, and thermal balance margins.
    /// GDD §7.4, §8.1, and §21 M1-07.
    /// </summary>
    public sealed class ThermalValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.Thermal;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            double load = stats.ThermalLoad;
            double diss = stats.ThermalDissipation;

            if (load > 0.0 && diss < load)
            {
                double excess = load - diss;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Isı Aşırılığı (Yetersiz Dağıtım)",
                    reason: $"Aracın ısıl yükü ({load:F2} kW), soğutma/dağıtım kapasitesinden ({diss:F2} kW) {excess:F2} kW fazladır.",
                    hint: "Radyatör modülü ekleyin, ısı dağıtım alanını büyütün veya aşırı ısınan cihazları pasif konuma almayı değerlendirin.",
                    currentValue: diss,
                    targetValue: load);
            }
            else if (diss > 0.0)
            {
                double margin = (diss - load) / diss;
                if (margin < context.ThermalMarginWarningThreshold)
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Warning,
                        title: "Düşük Isıl Marj",
                        reason: $"Isı dağıtım güvenlik marjı (%{margin * 100.0:F1}), önerilen eşiğin (%{context.ThermalMarginWarningThreshold * 100.0:F0}) altındadır.",
                        hint: "Doğrudan güneş akısı veya yüksek alet çalışma oranlarında sıcaklık sınır değerlere yaklaşabilir.",
                        currentValue: margin,
                        targetValue: context.ThermalMarginWarningThreshold);
                }
                else
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Info,
                        title: "Isıl Denge Sağlandı",
                        reason: $"Isı dağıtım kapasitesi ({diss:F2} kW), ısıl yükü ({load:F2} kW) %{margin * 100.0:F1} güvenlik marjıyla tahliye etmektedir.",
                        hint: "Uzay aracı ısıl kontrol mimarisi kararlıdır.",
                        currentValue: diss,
                        targetValue: load);
                }
            }
        }
    }
}
