using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Validates that the spacecraft's actual payload mass does not exceed the rocket's
    /// computed maximum deliverable payload for the target mission trajectory.
    ///
    /// GDD §8.1 — "Fırlatıcı kapasitesi: Roketin hedef yörüngeye taşıyabileceği kütle (tasarımdan türer)"
    /// GDD §7.4 — Design Review validation (red / yellow / green feedback).
    /// GDD §21 M1-08.
    ///
    /// The <see cref="LaunchCapacityResult"/> must already be present in
    /// <see cref="BlueprintStats.LaunchCapacity"/>; this validator only interprets it.
    /// If no capacity result is available (e.g. no required ΔV in the mission profile),
    /// the validator skips silently.
    /// </summary>
    public sealed class PayloadCapacityValidator : IBlueprintValidator
    {
        public string RuleId => ValidationRuleId.PayloadCapacity;

        public IEnumerable<ValidationIssue> Validate(
            Blueprint blueprint,
            BlueprintStats stats,
            ValidationContext context)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (stats == null)     throw new ArgumentNullException(nameof(stats));
            if (context == null)   throw new ArgumentNullException(nameof(context));

            var capacity = stats.LaunchCapacity;

            // Skip when no capacity analysis was performed
            // (no required ΔV in mission profile → BlueprintEvaluator did not call LaunchCapacityCalculator)
            if (capacity == null)
                yield break;

            // Skip when required ΔV is zero or mission profile absent — nothing to check
            if (context.MissionProfile == null || context.MissionProfile.RequiredDeltaV <= 0.0)
                yield break;

            double maxPayload    = capacity.MaxPayloadMass;
            double actualPayload = capacity.ActualPayloadMass;
            double margin        = capacity.CapacityMargin; // (max - actual) / max

            // ── Case 1: Rocket cannot meet the ΔV requirement at all (even at zero payload) ──
            if (maxPayload <= 0.0 && actualPayload > 0.0)
            {
                yield return new ValidationIssue(
                    ruleId:       RuleId,
                    severity:     ValidationSeverity.Error,
                    title:        "Yetersiz Fırlatıcı Kapasitesi (Sıfır Kapasite)",
                    reason:       $"Bu roket konfigürasyonu, hedef menzili ({capacity.RequiredDeltaV:F0} m/s) " +
                                  $"sıfır yük taşıyarak bile karşılayamıyor. Roket yapısının ΔV kapasitesi misyon gereksiniminin altında.",
                    hint:         "Roket aşamalarına daha fazla yakıt ekleyin, daha yüksek verimli (Isp) motorlar seçin " +
                                  "ya da üst aşama ekleyerek menzili artırın.",
                    currentValue: 0.0,
                    targetValue:  actualPayload);
                yield break;
            }

            // ── Case 2: Payload exceeds computed capacity (overloaded) ───────────────
            if (!capacity.IsWithinCapacity)
            {
                double excess = actualPayload - maxPayload;
                yield return new ValidationIssue(
                    ruleId:       RuleId,
                    severity:     ValidationSeverity.Error,
                    title:        "Faydalı Yük Kapasitesi Aşıldı",
                    reason:       $"Uzay aracı ({actualPayload:0.###} t), bu roketin hedef menzil için " +
                                  $"taşıyabileceği maksimum yük miktarından ({maxPayload:0.###} t) " +
                                  $"{excess:0.###} t daha ağır.",
                    hint:         "Uzay aracından bileşen çıkararak kütleyi azaltın, daha hafif malzeme seçin, " +
                                  "bilim aletlerini azaltın veya daha güçlü / çok aşamalı bir roket tasarlayın.",
                    currentValue: actualPayload,
                    targetValue:  maxPayload);
                yield break;
            }

            // ── Case 3: Payload fits but margin is uncomfortably low (warning) ────────
            // Warning threshold: capacity margin < 10% (context does not expose a dedicated
            // payload-capacity margin; we use 10% as the design-balance default from GDD §8.3).
            const double LowMarginWarningThreshold = 0.10;
            if (margin < LowMarginWarningThreshold)
            {
                yield return new ValidationIssue(
                    ruleId:       RuleId,
                    severity:     ValidationSeverity.Warning,
                    title:        "Düşük Yük Kapasitesi Marjı",
                    reason:       $"Uzay aracı ({actualPayload:0.###} t) maksimum kapasiteye " +
                                  $"(%{margin * 100.0:F1} marjla) yakın. Tasarım değişiklikleri veya " +
                                  $"beklenmedik kütle artışları kapasiteyi aşabilir.",
                    hint:         "Kapasiteyi artırmak için yakıt miktarını artırın ya da uzay aracı kütlesini azaltın. " +
                                  "İdeal marj %10-20 aralığındadır.",
                    currentValue: actualPayload,
                    targetValue:  maxPayload);
                yield break;
            }

            // ── Case 4: Payload well within capacity (info) ──────────────────────────
            yield return new ValidationIssue(
                ruleId:       RuleId,
                severity:     ValidationSeverity.Info,
                title:        "Yük Kapasitesi Yeterli",
                reason:       $"Uzay aracı ({actualPayload:0.###} t), roketin hedef menzil için " +
                              $"taşıyabileceği maksimum yükün (%{margin * 100.0:F1} kapasitesi kullanımında) içinde.",
                hint:         "Fırlatıcı konfigürasyonu hedef menzil için yeterli yük kapasitesine sahip.",
                currentValue: actualPayload,
                targetValue:  maxPayload);
        }
    }
}
