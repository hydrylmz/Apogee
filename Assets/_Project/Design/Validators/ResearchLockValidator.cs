using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates that all components, materials, and mission technologies in the blueprint have been researched.
    /// GDD §7.3, §7.4 "Araştırma kilitli", §9.2, and §21 M1-07.
    /// </summary>
    public sealed class ResearchLockValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.ResearchLock;

        public IEnumerable<ValidationIssue> Validate(Blueprint blueprint, BlueprintStats stats, ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (context == null) throw new ArgumentNullException(nameof(context));

            bool anyLocked = false;

            // 1. Check Payload components
            if (blueprint.Payload != null)
            {
                foreach (var comp in blueprint.Payload.AllComponents)
                {
                    if (comp != null && !context.IsResearchUnlocked(comp.RequiredResearchId))
                    {
                        anyLocked = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: "Kilitli Teknoloji Kullanımı (Modül)",
                            reason: $"'{comp.Name}' modülü henüz açılmamış '{comp.RequiredResearchId}' teknolojisini gerektirir.",
                            hint: "Araştırma ağacında ilgili teknolojiyi tamamlayın veya temel teknoloji seviyesindeki bir modülle değiştirin.",
                            sourceComponentId: comp.Id);
                    }

                    if (comp?.Material != null && !context.IsResearchUnlocked(comp.Material.RequiredResearchId))
                    {
                        anyLocked = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: "Kilitli Malzeme Teknolojisi",
                            reason: $"'{comp.Name}' üzerinde seçili '{comp.Material.Name}' malzemesi açılmamış '{comp.Material.RequiredResearchId}' araştırmasını gerektirir.",
                            hint: "Araştırma ağacında gelişmiş malzeme dalını tamamlayın veya Al-Alaşım gibi temel bir malzeme seçin.",
                            sourceComponentId: comp.Id);
                    }
                }
            }

            // 2. Check Fairing / Adapter
            if (blueprint.FairingAdapter != null)
            {
                var fa = blueprint.FairingAdapter;
                if (!context.IsResearchUnlocked(fa.RequiredResearchId))
                {
                    anyLocked = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Kilitli Fairing/Adaptör Teknolojisi",
                        reason: $"Fairing/adaptör '{fa.Name}' açılmamış '{fa.RequiredResearchId}' araştırmasını gerektirir.",
                        hint: "Temel fairing modülüne dönün veya gerekli araştırmayı başlatın.",
                        sourceComponentId: fa.Id);
                }

                if (fa.Material != null && !context.IsResearchUnlocked(fa.Material.RequiredResearchId))
                {
                    anyLocked = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Kilitli Fairing Malzemesi",
                        reason: $"Fairing malzemesi '{fa.Material.Name}' açılmamış '{fa.Material.RequiredResearchId}' araştırmasını gerektirir.",
                        hint: "Temel malzeme seçeneğine dönün.",
                        sourceComponentId: fa.Id);
                }
            }

            // 3. Check Stages (Engines, Tanks, Materials, ExtraComponents)
            foreach (var stage in blueprint.Stages)
            {
                if (stage == null) continue;

                if (stage.Engine != null && !context.IsResearchUnlocked(stage.Engine.RequiredResearchId))
                {
                    anyLocked = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Kilitli Motor Teknolojisi",
                        reason: $"'{stage.Name}' aşamasındaki '{stage.Engine.Name}' motoru açılmamış '{stage.Engine.RequiredResearchId}' araştırmasını gerektirir.",
                        hint: "İtki dalındaki motor araştırmasını tamamlayın veya temel kerosen/katı yakıtlı motor seçin.",
                        sourceComponentId: stage.Engine.Id);
                }

                if (stage.Tank != null && !context.IsResearchUnlocked(stage.Tank.RequiredResearchId))
                {
                    anyLocked = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Kilitli Tank Teknolojisi",
                        reason: $"'{stage.Name}' aşamasındaki tank '{stage.Tank.Name}' açılmamış '{stage.Tank.RequiredResearchId}' araştırmasını gerektirir.",
                        hint: "Standart tank yapısını seçin veya tank araştırmasını yapın.",
                        sourceComponentId: stage.Tank.Id);
                }

                if (stage.StructureMaterial != null && !context.IsResearchUnlocked(stage.StructureMaterial.RequiredResearchId))
                {
                    anyLocked = true;
                    yield return new ValidationIssue(
                        ruleId: RuleId,
                        severity: ValidationSeverity.Error,
                        title: "Kilitli Aşama Malzemesi",
                        reason: $"'{stage.Name}' aşama gövde malzemesi '{stage.StructureMaterial.Name}' açılmamış '{stage.StructureMaterial.RequiredResearchId}' araştırmasını gerektirir.",
                        hint: "Alüminyum alaşım gibi başlangıç malzemelerine dönün veya malzeme dalını araştırın.",
                        sourceComponentId: stage.Id);
                }

                foreach (var extra in stage.ExtraComponents)
                {
                    if (extra != null && !context.IsResearchUnlocked(extra.RequiredResearchId))
                    {
                        anyLocked = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: "Kilitli Aşama Bileşeni",
                            reason: $"'{stage.Name}' aşamasındaki '{extra.Name}' bileşeni açılmamış '{extra.RequiredResearchId}' araştırmasını gerektirir.",
                            hint: "Bileşeni kaldırın veya gerekli araştırmayı tamamlayın.",
                            sourceComponentId: extra.Id);
                    }
                }
            }

            // 4. Check MissionProfile RequiredResearchIds
            if (context.MissionProfile != null)
            {
                foreach (var reqRes in context.MissionProfile.RequiredResearchIds)
                {
                    if (!context.IsResearchUnlocked(reqRes))
                    {
                        anyLocked = true;
                        yield return new ValidationIssue(
                            ruleId: RuleId,
                            severity: ValidationSeverity.Error,
                            title: "Misyon Önkoşul Araştırması Eksik",
                            reason: $"Bu misyonun gerektirdiği '{reqRes}' araştırması henüz ajans tarafından tamamlanmamıştır.",
                            hint: "Misyonu başlatmadan önce araştırma ağacından ilgili düğümü tamamlayın.");
                    }
                }
            }

            if (!anyLocked)
            {
                yield return new ValidationIssue(
                    ruleId: RuleId,
                    severity: ValidationSeverity.Info,
                    title: "Tüm Teknolojiler Açık",
                    reason: "Tasarımdaki tüm modüller ve malzemeler ajansın onaylanmış ve açık teknolojileriyle uyumludur.",
                    hint: "Kilitli araştırma kısıtı yoktur.");
            }
        }
    }
}
