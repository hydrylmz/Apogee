using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Scoring;
using Apogee.Design;

namespace Apogee.Core.Simulation.Report
{
    /// <summary>
    /// Functional generator that evaluates completed mission execution records and produces
    /// an explainable, deterministic <see cref="MissionReport"/> with success criteria,
    /// science data delivery, cause-and-effect narrative, and campaign impacts.
    ///
    /// Pure C# model with no UnityEngine dependencies.
    /// Complies with GDD §10.5, §13.2, and §21 M5-06.
    /// </summary>
    public static class MissionReportEvaluator
    {
        /// <summary>
        /// Generates a comprehensive MissionReport from mission instance data.
        /// </summary>
        public static MissionReport Evaluate(
            string missionId,
            string missionName,
            string destinationId,
            MissionProfile profile,
            Blueprint blueprint,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double downlinkedGb,
            double rawGeneratedGb,
            double droppedGb,
            int scienceDaysCompleted,
            double accumulatedReliabilityPenalty,
            double accumulatedCorrectionFuel,
            IReadOnlyList<ActiveOperationEvent> operationEvents,
            bool isMissionFailedEarly = false,
            string earlyFailureReason = null)
        {
            // 1. Evaluate Success Criteria
            var criteriaResults = new List<CriterionEvaluationResult>();
            bool allCriteriaMet = true;

            if (profile != null && profile.SuccessCriteria != null && profile.SuccessCriteria.Count > 0)
            {
                foreach (var criterion in profile.SuccessCriteria)
                {
                    var eval = EvaluateCriterion(
                        criterion,
                        launchResult,
                        edlResult,
                        downlinkedGb,
                        scienceDaysCompleted,
                        isMissionFailedEarly
                    );
                    criteriaResults.Add(eval);
                    if (!eval.IsMet)
                    {
                        allCriteriaMet = false;
                    }
                }
            }
            else
            {
                // Fallback baseline criterion when no profile attached
                bool defaultMet = !isMissionFailedEarly &&
                                  (launchResult == null || launchResult.IsSuccessful) &&
                                  (edlResult == null || edlResult.IsSuccessful);
                var defaultCrit = new MissionSuccessCriterion(
                    "obj_default",
                    "operation-days",
                    1.0,
                    "Görevin güvenle icra edilmesi ve veri toplanması."
                );
                criteriaResults.Add(new CriterionEvaluationResult(
                    defaultCrit,
                    defaultMet,
                    scienceDaysCompleted > 0 ? scienceDaysCompleted : (defaultMet ? 1.0 : 0.0),
                    defaultMet ? "Görev operasyonu tamamlandı." : "Görev hedefe ulaşamadan sonlandı."
                ));
                allCriteriaMet = defaultMet;
            }

            // Overall mission success
            bool isLaunchSuccess = launchResult == null || launchResult.IsSuccessful;
            bool isEdlSuccess = edlResult == null || edlResult.IsSuccessful;
            bool isOverallSuccess = !isMissionFailedEarly && isLaunchSuccess && isEdlSuccess && allCriteriaMet;

            // 2. Science Delivery Summary
            var scienceSummary = new ScienceDeliverySummary(rawGeneratedGb, downlinkedGb, droppedGb);

            // 3. Cause and Effect Chain
            var causeChain = BuildCauseAndEffectChain(
                blueprint,
                profile,
                launchResult,
                edlResult,
                accumulatedReliabilityPenalty,
                accumulatedCorrectionFuel,
                scienceSummary,
                operationEvents,
                isMissionFailedEarly,
                earlyFailureReason
            );

            // 4. Lessons Learned (GDD §4.3 "Bilgi Puanı ve Dersler", §10.5, §13.2)
            var lessons = DeriveLessonsLearned(
                blueprint,
                launchResult,
                edlResult,
                scienceSummary,
                accumulatedReliabilityPenalty,
                operationEvents
            );

            // 5. Campaign Impact Calculation (GDD §4.3, §10.5)
            var campaignImpact = CalculateCampaignImpact(
                isOverallSuccess,
                allCriteriaMet,
                launchResult,
                edlResult,
                profile,
                scienceSummary,
                lessons.Count
            );

            // Final status summary text
            string finalStatusSummary;
            if (isOverallSuccess)
            {
                finalStatusSummary = "Misyon tüm bilimsel ve operasyonel hedefleri başarıyla tamamladı.";
            }
            else if (launchResult != null && !launchResult.IsSuccessful)
            {
                finalStatusSummary = $"Fırlatma safhasında görev kaybı: {launchResult.PlainLanguageSummary}";
            }
            else if (edlResult != null && !edlResult.IsSuccessful)
            {
                finalStatusSummary = $"Hedefe iniş (EDL) safhasında kaza/arıza: {edlResult.PlainLanguageSummary}";
            }
            else if (isMissionFailedEarly)
            {
                finalStatusSummary = !string.IsNullOrWhiteSpace(earlyFailureReason)
                    ? earlyFailureReason
                    : "Seyir veya operasyon sırasında yaşanan arıza nedeniyle görev tamamlanamadı.";
            }
            else
            {
                finalStatusSummary = "Misyon icra edildi fakat belirlenen bilimsel kriterlerin bir kısmı karşılanamadı.";
            }

            var tempReportForScoring = new MissionReport(
                missionId,
                missionName,
                destinationId,
                isOverallSuccess,
                finalStatusSummary,
                criteriaResults,
                scienceSummary,
                causeChain,
                campaignImpact,
                lessons,
                null
            );

            // 6. Calculate Mission Score Breakdown & Medals (GDD §11, §21 M5-08)
            var scoreBreakdown = MissionScoreCalculator.Calculate(
                profile: profile,
                blueprint: blueprint,
                report: tempReportForScoring,
                launchResult: launchResult,
                edlResult: edlResult,
                downlinkedGb: downlinkedGb,
                rawGeneratedGb: rawGeneratedGb,
                droppedGb: droppedGb,
                scienceDaysCompleted: scienceDaysCompleted,
                accumulatedReliabilityPenalty: accumulatedReliabilityPenalty,
                operationEvents: operationEvents,
                caughtLaunchWindow: true
            );

            return new MissionReport(
                missionId,
                missionName,
                destinationId,
                isOverallSuccess,
                finalStatusSummary,
                criteriaResults,
                scienceSummary,
                causeChain,
                campaignImpact,
                lessons,
                scoreBreakdown
            );
        }

        private static CriterionEvaluationResult EvaluateCriterion(
            MissionSuccessCriterion criterion,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double downlinkedGb,
            int scienceDays,
            bool isFailedEarly)
        {
            if (criterion == null)
            {
                return new CriterionEvaluationResult(
                    new MissionSuccessCriterion("none", "none", 0),
                    false,
                    0,
                    "Geçersiz kriter."
                );
            }

            if (isFailedEarly || (launchResult != null && !launchResult.IsSuccessful))
            {
                return new CriterionEvaluationResult(
                    criterion,
                    false,
                    0.0,
                    "Görev hedefe varamadan başarısızlıkla sonuçlandığı için kriter sağlanamadı."
                );
            }

            string type = criterion.CriterionType?.ToLowerInvariant();
            double threshold = criterion.Threshold;

            switch (type)
            {
                case "data-delivered-gb":
                {
                    bool isMet = downlinkedGb >= (threshold - 0.05);
                    string summary = isMet
                        ? $"Hedeflenen veri teslim edildi ({downlinkedGb:F1} GB / {threshold:F1} GB)."
                        : $"Yetersiz veri teslimi ({downlinkedGb:F1} GB / {threshold:F1} GB).";
                    return new CriterionEvaluationResult(criterion, isMet, downlinkedGb, summary);
                }

                case "operation-days":
                case "coverage-days":
                {
                    bool isMet = scienceDays >= (int)threshold;
                    string summary = isMet
                        ? $"Operasyonel çalışma süresi karşılandı ({scienceDays} gün / {threshold:0} gün)."
                        : $"Çalışma süresi yetersiz kaldı ({scienceDays} gün / {threshold:0} gün).";
                    return new CriterionEvaluationResult(criterion, isMet, scienceDays, summary);
                }

                case "sample-returned":
                case "sample-prepared":
                {
                    // Landing success and science days >= 3 required
                    bool isLandingSuccess = edlResult != null && edlResult.IsSuccessful;
                    bool isMet = isLandingSuccess && scienceDays >= 1;
                    string summary = isMet
                        ? "Yüzey numune operasyonu başarıyla tamamlandı."
                        : (isLandingSuccess ? "Yüzeyde numune toplama işlemi gerçekleştirilemedi." : "Yüzeye iniş yapılamadı.");
                    return new CriterionEvaluationResult(criterion, isMet, isMet ? 1.0 : 0.0, summary);
                }

                default:
                {
                    // Generic fallback
                    bool isMet = scienceDays >= 1 && downlinkedGb > 0.0;
                    string summary = isMet
                        ? "Hedef başarıyla tamamlandı."
                        : "Hedef gereksinimleri karşılanamadı.";
                    return new CriterionEvaluationResult(criterion, isMet, isMet ? 1.0 : 0.0, summary);
                }
            }
        }

        private static List<CauseEffectNode> BuildCauseAndEffectChain(
            Blueprint blueprint,
            MissionProfile profile,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double reliabilityPenalty,
            double correctionFuel,
            ScienceDeliverySummary scienceSummary,
            IReadOnlyList<ActiveOperationEvent> operationEvents,
            bool isFailedEarly,
            string earlyFailureReason)
        {
            var nodes = new List<CauseEffectNode>();

            // 1. Design phase decisions
            if (blueprint != null)
            {
                var stats = BlueprintEvaluator.Evaluate(blueprint);
                if (stats != null)
                {
                    if (stats.Reliability < 0.85)
                    {
                        nodes.Add(new CauseEffectNode(
                            "Tasarım",
                            "Düşük Taban Güvenilirlik",
                            $"Uzay aracı taban sistem güvenilirliği %{stats.Reliability * 100:F0} olarak tasarlandı; seyir ve inişte arıza olasılığı yükseldi.",
                            isNegativeOrFault: true
                        ));
                    }
                    else
                    {
                        nodes.Add(new CauseEffectNode(
                            "Tasarım",
                            "Yüksek Sistem Güvenilirliği",
                            $"Uzay aracı güvenilir bileşenlerle (%{stats.Reliability * 100:F0}) donatıldı.",
                            isNegativeOrFault: false
                        ));
                    }

                    if (stats.PowerGeneration < stats.PowerConsumption * 1.1)
                    {
                        nodes.Add(new CauseEffectNode(
                            "Tasarım",
                            "Dar Güç Marjı",
                            "Güç üretimi ile tüketim başa baş dengede bırakıldı; gölge veya arıza durumlarında batarya riski oluştu.",
                            isNegativeOrFault: true
                        ));
                    }

                    if (stats.DataRate > stats.DownlinkRate * 1.5)
                    {
                        nodes.Add(new CauseEffectNode(
                            "Tasarım",
                            "Veri - Anten Darboğazı",
                            $"Alet veri üretim hızı ({stats.DataRate:F1} GB/gün), anten downlink hızından ({stats.DownlinkRate:F1} GB/gün) belirgin şekilde yüksek seçildi.",
                            isNegativeOrFault: true
                        ));
                    }
                }
            }

            // 2. Launch phase
            if (launchResult != null)
            {
                if (launchResult.IsSuccessful)
                {
                    nodes.Add(new CauseEffectNode(
                        "Fırlatma",
                        "Yörüngeye Başarılı Yerleşme",
                        $"Roket aşamaları planlanan şekilde ayrıldı, Δv sağlandı ({launchResult.DeliveredDeltaV:F0} m/s) ve transfer yörüngesine girildi."
                    ));
                }
                else
                {
                    nodes.Add(new CauseEffectNode(
                        "Fırlatma",
                        "Fırlatma Başarısızlığı",
                        $"{launchResult.PlainLanguageSummary} (Aşama arızası: {launchResult.PrimaryCauseKey})",
                        isNegativeOrFault: true
                    ));
                }
            }

            // 3. Cruise phase events & degradation
            if (reliabilityPenalty > 0.05)
            {
                nodes.Add(new CauseEffectNode(
                    "Seyir",
                    "Alt Sistem Yıpranması",
                    $"Uzun gezegenler arası seyir sırasında donanım yıpranması meydana geldi (-%{reliabilityPenalty * 100:F1} güvenilirlik kaybı).",
                    isNegativeOrFault: true
                ));
            }

            if (correctionFuel > 0.10)
            {
                nodes.Add(new CauseEffectNode(
                    "Seyir",
                    "Rota Düzeltme Yakıtı Harcandı",
                    $"Yörünge sapmaları nedeniyle planlanandan fazla itki yakıtı (%{correctionFuel * 100:F0}) harcandı.",
                    isNegativeOrFault: true
                ));
            }

            // 4. Operation events during mission
            if (operationEvents != null)
            {
                foreach (var opEvent in operationEvents)
                {
                    if (opEvent?.Record == null) continue;

                    var chosenOpt = opEvent.Record.Options?.FirstOrDefault(o => o.OptionId == opEvent.ChosenOptionId);
                    bool isHarmful = chosenOpt != null &&
                        (chosenOpt.ReliabilityPenalty > 0 ||
                         chosenOpt.ReputationDelta < 0 ||
                         chosenOpt.BudgetDelta < 0);

                    nodes.Add(new CauseEffectNode(
                        "Operasyon Olayı",
                        opEvent.Record.TitleKey,
                        $"Olay: {opEvent.Record.NarrativeKey} → Seçilen Karar: '{chosenOpt?.LabelKey ?? "Standart"}'.",
                        isNegativeOrFault: isHarmful
                    ));
                }
            }

            // 5. EDL / Arrival phase
            if (edlResult != null)
            {
                if (edlResult.IsSuccessful)
                {
                    nodes.Add(new CauseEffectNode(
                        "Varış ve İniş",
                        "Güvenli İniş Gerçekleşti",
                        $"{edlResult.PlainLanguageSummary} (Kalan Isı Kalkanı: %{edlResult.RemainingHeatShieldIntegrity * 100:F0}, Temas Hızı: {edlResult.TouchdownVelocityMps:F1} m/s)."
                    ));
                }
                else
                {
                    nodes.Add(new CauseEffectNode(
                        "Varış ve İniş",
                        "İniş Kazası",
                        $"{edlResult.PlainLanguageSummary} ({edlResult.PrimaryCauseKey})",
                        isNegativeOrFault: true
                    ));
                }
            }

            // 6. Science operations bottleneck
            if (scienceSummary != null)
            {
                if (scienceSummary.TotalDroppedGb > 0.1)
                {
                    nodes.Add(new CauseEffectNode(
                        "Bilim",
                        "Depolama Taşması ve Veri Kaybı",
                        scienceSummary.BottleneckAnalysis,
                        isNegativeOrFault: true
                    ));
                }
                else if (scienceSummary.TotalDownlinkedGb > 0.0)
                {
                    nodes.Add(new CauseEffectNode(
                        "Bilim",
                        "Düzenli Bilim İndirimi",
                        $"Toplam {scienceSummary.TotalDownlinkedGb:F1} GB bilim verisi istasyonlara başarıyla indirildi."
                    ));
                }
            }

            // 7. Early termination if any
            if (isFailedEarly && !string.IsNullOrWhiteSpace(earlyFailureReason))
            {
                nodes.Add(new CauseEffectNode(
                    "Sonuç",
                    "Erken Görev Sonu",
                    earlyFailureReason,
                    isNegativeOrFault: true
                ));
            }

            return nodes;
        }

        private static List<string> DeriveLessonsLearned(
            Blueprint blueprint,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            ScienceDeliverySummary scienceSummary,
            double reliabilityPenalty,
            IReadOnlyList<ActiveOperationEvent> operationEvents)
        {
            var lessons = new List<string>();

            if (launchResult != null && !launchResult.IsSuccessful)
            {
                lessons.Add("Fırlatıcı Güvenilirlik Testi Dersi: Fırlatıcı aşamalarının kalitesini artırmak için fabrikada tam sistem ateşleme testi uygulanmalı.");
            }

            if (edlResult != null && !edlResult.IsSuccessful)
            {
                lessons.Add("Giriş ve İniş (EDL) Dersi: Hedef atmosferin yoğunluğu ve aerotermal yükü hafife alınmamalı; ısı kalkanı marjı en az %25 tutulmalı.");
            }

            if (scienceSummary != null && scienceSummary.HasDataBottleneck)
            {
                lessons.Add("Haberleşme & Depolama Dengesi: Alet eklerken downlink kapasitesi hesaba katılmalı; yüksek kazançlı anten veya ek yer istasyonu planlanmalı.");
            }

            if (reliabilityPenalty > 0.15)
            {
                lessons.Add("Uzun Menzil Dayanıklılık Dersi: Uzak hedeflere seyahat ederken radyasyon kalkanı ve yedekli aviyonik üniteleri kullanılmalı.");
            }

            if (blueprint != null)
            {
                var stats = BlueprintEvaluator.Evaluate(blueprint);
                if (stats != null && stats.PowerGeneration < stats.PowerConsumption * 1.05)
                {
                    lessons.Add("Güç Yönetimi Marjı: Kritik bilim aletlerinin devre dışı kalmaması için tutulma/gece sürelerini karşılayacak batarya kapasitesi ayrılmalı.");
                }
            }

            if (lessons.Count == 0)
            {
                lessons.Add("Nominal Uçuş Mimarisi: Seçilen güç, veri ve itki dengesi hedef profili için optimum başarı sağladı.");
            }

            return lessons;
        }

        private static MissionCampaignImpact CalculateCampaignImpact(
            bool isOverallSuccess,
            bool allCriteriaMet,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            MissionProfile profile,
            ScienceDeliverySummary scienceSummary,
            int lessonsCount)
        {
            double budgetChange = 0.0;
            double reputationChange = 0.0;
            double researchPoints = 0.0;
            double knowledgePoints = lessonsCount * 5.0; // GDD §4.3 "Bilgi Puanı"

            double rewardMultiplier = profile != null ? profile.RewardMultiplier : 1.0;
            double scienceValue = profile != null ? profile.TotalScienceValue : 25.0;

            if (isOverallSuccess)
            {
                // Full mission victory bonus
                budgetChange = Math.Round(25.0 * rewardMultiplier, 1);
                reputationChange = Math.Round(12.0 * rewardMultiplier, 1);
                researchPoints = Math.Round(scienceValue * 1.5, 1);
            }
            else if (launchResult != null && !launchResult.IsSuccessful)
            {
                // Launch catastrophe penalty
                budgetChange = -5.0;
                reputationChange = launchResult.IsCrewSafe ? -8.0 : -25.0;
                researchPoints = 0.0;
            }
            else if (edlResult != null && !edlResult.IsSuccessful)
            {
                // EDL loss penalty
                budgetChange = 0.0;
                reputationChange = -6.0;
                researchPoints = Math.Round((scienceSummary?.TotalDownlinkedGb ?? 0.0) * 2.0, 1);
            }
            else
            {
                // Partial completion
                budgetChange = Math.Round(10.0 * rewardMultiplier, 1);
                reputationChange = Math.Round(4.0 * rewardMultiplier, 1);
                researchPoints = Math.Round((scienceSummary?.TotalDownlinkedGb ?? 0.0) * 3.0, 1);
            }

            string summaryText = isOverallSuccess
                ? $"+{reputationChange:F0} İtibar | +{budgetChange:F1}M$ Bütçe Ödülü | +{researchPoints:F0} Araştırma Puanı | +{knowledgePoints:F0} Bilgi Puanı"
                : $"{(reputationChange >= 0 ? "+" : "")}{reputationChange:F0} İtibar | +{researchPoints:F0} Araştırma Puanı | +{knowledgePoints:F0} Bilgi Puanı";

            return new MissionCampaignImpact(
                budgetChange,
                reputationChange,
                researchPoints,
                knowledgePoints,
                summaryText
            );
        }
    }
}
