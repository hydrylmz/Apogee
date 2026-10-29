using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates vehicle mass against launch pad structural limit and verifies vehicle non-emptiness.
    /// GDD §7.4 "Kütle > kapasite", §8.1, and §21 M1-07.
    /// </summary>
    public sealed class MassCapacityValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.MassCapacity;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (stats.DryMass <= 0.0)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Boş Araç Tasarımı",
                    reason: "Araçta herhangi bir yapısal bileşen veya faydalı yük bulunmuyor.",
                    hint: "Faydalı yük ve roket aşama slotlarına bileşenler yerleştirin.",
                    currentValue: 0.0,
                    targetValue: 0.1);
                yield break;
            }

            var pad = context.LaunchPad;
            if (pad == null) yield break;

            double wetMass = stats.WetMass;
            double maxPadMass = pad.MaxWetMass;

            if (wetMass > maxPadMass)
            {
                double excess = wetMass - maxPadMass;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Fırlatma Kütlesi Kapasiteyi Aşıyor",
                    reason: $"Aracın toplam fırlatma kütlesi ({wetMass:F1} t), fırlatma rampasının maksimum yapısal kapasitesini ({maxPadMass:F1} t) {excess:F1} t aşıyor.",
                    hint: "Yakıt miktarını azaltmayı, kompozit gibi hafif yapı malzemeleri seçmeyi veya fırlatma rampası tesisini üst seviyeye yükseltmeyi değerlendirin.",
                    currentValue: wetMass,
                    targetValue: maxPadMass);
            }
            else if (wetMass > maxPadMass * context.MassMarginWarningThreshold)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Warning,
                    title: "Kritik Rampa Kütle Yükü",
                    reason: $"Aracın toplam kütlesi ({wetMass:F1} t), rampa sınırının ({maxPadMass:F1} t) %{wetMass / maxPadMass * 100.0:F0}'ine ulaştı.",
                    hint: "Herhangi bir ekleme rampayı aşacaktır; marjı artırmak için yapı hafifletmelerini değerlendirin.",
                    currentValue: wetMass,
                    targetValue: maxPadMass);
            }
            else
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Info,
                    title: "Kütle Sınırlar Dahilinde",
                    reason: $"Toplam ıslak kütle ({wetMass:F1} t), rampa kapasitesi ({maxPadMass:F1} t) sınırları içindedir.",
                    hint: "Fırlatma rampası bu araç ölçeğini desteklemektedir.",
                    currentValue: wetMass,
                    targetValue: maxPadMass);
            }
        }
    }
}
