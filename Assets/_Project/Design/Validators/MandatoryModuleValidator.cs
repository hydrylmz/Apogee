using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Design
{
    /// <summary>
    /// Validates presence of essential mission modules: bus, propulsion, EDL, life support,
    /// and mission-specific required category tags.
    /// GDD §5.4, §7.4 "Zorunlu modül", §7.6, §7.7, and §21 M1-07.
    /// </summary>
    public sealed class MandatoryModuleValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.MandatoryModules;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            bool hasError = false;

            // 1. Spacecraft Bus check
            if (blueprint.Payload == null || blueprint.Payload.Bus == null)
            {
                hasError = true;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Uzay Aracı Bus Modülü Eksik",
                    reason: "Uzay aracında temel gövde/aviyonik platformu (Bus) bulunmuyor. Sistemler bus olmadan entegre edilemez.",
                    hint: "Faydalı yük sekmesinden göreve uygun bir uydu veya araç Bus'ı seçin.");
            }

            // 2. Rocket Stages & Engines check
            if (blueprint.Stages.Count == 0)
            {
                hasError = true;
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Error,
                    title: "Roket Aşaması Yok",
                    reason: "Araçta hiç itki aşaması bulunmuyor. Yörüngeye ulaşmak için en az bir roket aşaması gereklidir.",
                    hint: "Tasarım Atölyesi'nde araca roket aşamaları ekleyin.");
            }
            else
            {
                for (int i = 0; i < blueprint.Stages.Count; i++)
                {
                    var stage = blueprint.Stages[i];
                    if (stage.Engine == null)
                    {
                        hasError = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: "Aşama Motoru Eksik",
                            reason: $"'{stage.Name}' ({i + 1}. Aşama) için herhangi bir roket motoru atanmamış.",
                            hint: "Aşama yapılandırmasından uygun itkiye sahip bir motor seçin.",
                            sourceComponentId: stage.Id);
                    }
                }
            }

            // 3. EDL requirement (GDD §7.7)
            bool requiresEdl = (context.MissionProfile != null && context.MissionProfile.RequiresEdl) ||
                               string.Equals(blueprint.TargetMissionTypeId, "lander", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(blueprint.TargetMissionTypeId, "rover", StringComparison.OrdinalIgnoreCase);

            if (requiresEdl)
            {
                if (blueprint.Payload == null || blueprint.Payload.Edl == null)
                {
                    hasError = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Zorunlu EDL Paketi Eksik",
                        reason: "Hedef yüzeye iniş gerektiren bu görevde Giriş, Alçalış ve İniş (EDL) paketi seçilmemiş.",
                        hint: "Faydalı yük sekmesinde hedef atmosferine uygun ısı kalkanı, paraşüt veya retro itki modülü seçin.");
                }
            }

            // 4. Crew Life Support requirement (GDD §7.6)
            bool requiresLifeSupport = (context.MissionProfile != null && context.MissionProfile.RequiresLifeSupport) ||
                                       string.Equals(blueprint.TargetMissionTypeId, "crewed", StringComparison.OrdinalIgnoreCase);

            if (requiresLifeSupport)
            {
                if (blueprint.Payload == null || blueprint.Payload.LifeSupport == null)
                {
                    hasError = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Zorunlu Yaşam Destek Sistemi Eksik",
                        reason: "İnsanlı görev sınıfı için kabin, radyasyon kalkanı ve yaşam destek (ECLSS) sistemi zorunludur.",
                        hint: "Faydalı yük sekmesine yaşam destek ünitesi ve mürettebat kabin modülü ekleyin.");
                }
            }

            // 5. Explicit required categories from MissionProfile (GDD §5.4)
            if (context.MissionProfile != null && context.MissionProfile.RequiredModuleCategories.Count > 0)
            {
                var presentCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (blueprint.Payload != null)
                {
                    foreach (var comp in blueprint.Payload.AllComponents)
                    {
                        if (comp != null && !string.IsNullOrWhiteSpace(comp.Category))
                            presentCategories.Add(comp.Category);
                    }
                }

                foreach (var stage in blueprint.Stages)
                {
                    if (stage.Engine != null) presentCategories.Add(stage.Engine.Category);
                    if (stage.Tank != null) presentCategories.Add(stage.Tank.Category);
                    foreach (var extra in stage.ExtraComponents)
                    {
                        if (extra != null && !string.IsNullOrWhiteSpace(extra.Category))
                            presentCategories.Add(extra.Category);
                    }
                }

                foreach (var reqCat in context.MissionProfile.RequiredModuleCategories)
                {
                    if (!presentCategories.Contains(reqCat))
                    {
                        hasError = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: $"Zorunlu Modül Kategorisi Eksik: {reqCat}",
                            reason: $"Misyon profili '{reqCat}' kategorisinde en az bir aktif bileşen bulunmasını şart koşmaktadır.",
                            hint: $"Araca '{reqCat}' tipinde bir donanım modülü ekleyin.");
                    }
                }
            }

            if (!hasError)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Info,
                    title: "Zorunlu Modüller Tamam",
                    reason: "Tasarım, misyonun ve fırlatıcı mimarisinin talep ettiği tüm zorunlu modülleri barındırmaktadır.",
                    hint: "Eksik kritik sistem bulunmuyor.");
            }
        }
    }
}
