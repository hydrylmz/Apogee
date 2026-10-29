using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates vehicle launcher stage count and tier against launch pad facility capabilities.
    /// GDD §6.1, §7.4 "Rampa uyumsuz", and §21 M1-07.
    /// </summary>
    public sealed class LaunchPadValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.LaunchPad;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var pad = context.LaunchPad;
            if (pad == null) yield break;

            int stageCount = blueprint.Stages.Count;
            if (stageCount > pad.MaxStages)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Rampa Aşama Sınırı Aşıldı",
                    reason: $"Araçta {stageCount} aşama bulunuyor; mevcut fırlatma rampası (Seviye {pad.Level}) en fazla {pad.MaxStages} aşamalı araçları destekler.",
                    hint: "Aşama sayısını azaltmayı veya üssünüzdeki fırlatma rampasını bir üst seviyeye yükseltmeyi değerlendirin.",
                    currentValue: stageCount,
                    targetValue: pad.MaxStages);
            }

            var profile = context.MissionProfile;
            if (profile != null && pad.Level < profile.RequiredLaunchPadLevel)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Yetersiz Rampa Seviyesi",
                    reason: $"Misyon profili Seviye {profile.RequiredLaunchPadLevel} rampa altyapısı gerektirir; mevcut rampa Seviye {pad.Level}.",
                    hint: "Strateji haritasında ilgili üssün fırlatma rampasını yükseltin.",
                    currentValue: pad.Level,
                    targetValue: profile.RequiredLaunchPadLevel);
            }

            if (stageCount <= pad.MaxStages && (profile == null || pad.Level >= profile.RequiredLaunchPadLevel))
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Info,
                    title: "Rampa Altyapısı Uyumlu",
                    reason: $"Araç ({stageCount} aşama) Seviye {pad.Level} rampa tesisi standartlarıyla tam uyumludur.",
                    hint: "Mevcut kule ve yer altyapısı bu roket konfigürasyonunu destekler.",
                    currentValue: pad.Level,
                    targetValue: pad.Level);
            }
        }
    }
}
