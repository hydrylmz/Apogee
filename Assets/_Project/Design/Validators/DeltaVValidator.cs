using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates vehicle net Delta-V capability against mission requirements.
    /// GDD §7.4 "Menzil açığı", §8.1, and §21 M1-07.
    /// </summary>
    public sealed class DeltaVValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.DeltaV;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var profile = context.MissionProfile;
            if (profile == null || profile.RequiredDeltaV <= 0.0)
            {
                yield break;
            }

            double required = profile.RequiredDeltaV;
            double current = stats.NetDeltaV;

            if (current < required)
            {
                double deficit = required - current;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Menzil Açığı (Yetersiz Δv)",
                    reason: $"Aracın net menzili ({current:F0} m/s), hedeflenen misyon için gereken menzilden ({required:F0} m/s) {deficit:F0} m/s daha az.",
                    hint: "Daha yüksek verimli (Isp) bir motor seçmeyi, aşamaların yakıt miktarını artırmayı veya üst aşama ve faydalı yük kütlesini hafifletmeyi değerlendirin.",
                    currentValue: current,
                    targetValue: required);
            }
            else
            {
                double margin = (current - required) / required;
                if (margin < context.DeltaVMarginWarningThreshold)
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Warning,
                        title: "Düşük Menzil Marjı",
                        reason: $"Net menzil ({current:F0} m/s) gereksinimi karşılıyor ancak güvenlik marjı (%{margin * 100.0:F1}) dar (önerilen: en az %{context.DeltaVMarginWarningThreshold * 100.0:F0}).",
                        hint: "Fırlatma ve seyir sırasındaki yörünge düzeltmeleri veya yerçekimi kayıpları yakıtın erken tükenmesine yol açabilir.",
                        currentValue: current,
                        targetValue: required);
                }
                else
                {
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Info,
                        title: "Menzil Yeterli",
                        reason: $"Aracın menzili ({current:F0} m/s) misyon hedefini %{margin * 100.0:F1} güvenlik marjıyla karşılıyor.",
                        hint: "İtki konfigürasyonu hedef için uygun menzile sahip.",
                        currentValue: current,
                        targetValue: required);
                }
            }
        }
    }
}
