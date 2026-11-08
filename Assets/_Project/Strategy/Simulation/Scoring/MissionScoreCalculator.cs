using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Design;

namespace Apogee.Core.Simulation.Scoring
{
    /// <summary>
    /// Pure functional engine that evaluates mission performance across the 5 pillars of GDD §11.1
    /// and awards medals based on criteria defined in GDD §11.2.
    ///
    /// Pure C# model with no UnityEngine dependencies.
    /// Complies with GDD §11.1, §11.2, and §21 M5-08.
    /// </summary>
    public static class MissionScoreCalculator
    {
        /// <summary>
        /// Calculates the complete score breakdown and awards medals for a finished mission.
        /// </summary>
        public static MissionScoreBreakdown Calculate(
            MissionProfile profile,
            Blueprint blueprint,
            MissionReport report,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double downlinkedGb,
            double rawGeneratedGb,
            double droppedGb,
            int scienceDaysCompleted,
            double accumulatedReliabilityPenalty,
            IReadOnlyList<ActiveOperationEvent> operationEvents,
            bool caughtLaunchWindow = true,
            ScoringConfig config = null)
        {
            config = config ?? ScoringConfig.CreateDefault();

            // 1. Bilim Teslimi (%35): Yere inen veri / hedef veya üretilen veri
            double scienceDeliveryRatio = 0.0;
            if (rawGeneratedGb > 0.001)
            {
                scienceDeliveryRatio = Math.Min(1.0, downlinkedGb / rawGeneratedGb);
            }
            else if (downlinkedGb > 0.0)
            {
                scienceDeliveryRatio = 1.0;
            }

            // 2. Amaç Başarısı (%20): Kriterlerin sağlanma oranı
            double objectiveSuccessRatio = 0.0;
            if (report != null && report.CriteriaResults != null && report.CriteriaResults.Count > 0)
            {
                int metCount = report.CriteriaResults.Count(c => c.IsMet);
                objectiveSuccessRatio = (double)metCount / report.CriteriaResults.Count;
            }
            else if (report != null && report.IsOverallSuccess)
            {
                objectiveSuccessRatio = 1.0;
            }

            // 3. Kaynak Verimliliği (%15): Kütle, güç ve bütçe marjları (aşırı marj cezalandırılır, ideal %10-25)
            double resourceEfficiency = EvaluateResourceEfficiency(blueprint, profile, config);

            // 4. Karar Kalitesi (%15): Arıza cezaları, olay kararlarında zarar verici seçeneklerin oranı
            double decisionQuality = EvaluateDecisionQuality(
                launchResult,
                edlResult,
                accumulatedReliabilityPenalty,
                operationEvents
            );

            // 5. Planlama / Koordinasyon Verimliliği (%15): Pencere yakalama, erken kayıp olmama, süre hedefine uyum
            double planningEfficiency = EvaluatePlanningEfficiency(
                report,
                launchResult,
                edlResult,
                caughtLaunchWindow
            );

            // Composite Base Score (0 to BaseMaxScore)
            double compositeRatio =
                (scienceDeliveryRatio * config.ScienceDeliveryWeight) +
                (objectiveSuccessRatio * config.ObjectiveSuccessWeight) +
                (resourceEfficiency * config.ResourceEfficiencyWeight) +
                (decisionQuality * config.DecisionQualityWeight) +
                (planningEfficiency * config.PlanningEfficiencyWeight);

            compositeRatio = Math.Max(0.0, Math.Min(1.0, compositeRatio));
            double baseWeightedScore = Math.Round(compositeRatio * config.BaseMaxScore, 1);

            // Difficulty multiplier
            double difficultyMultiplier = 1.0;
            if (profile != null)
            {
                // Derived from claim level or risk/reward multipliers (GDD §5.3)
                difficultyMultiplier = Math.Max(1.0, Math.Min(3.5, profile.RewardMultiplier));
            }

            // Evaluate Medals (GDD §11.2)
            var awardedMedals = EvaluateMedals(
                profile,
                blueprint,
                report,
                launchResult,
                edlResult,
                downlinkedGb,
                droppedGb,
                operationEvents,
                caughtLaunchWindow,
                config
            );

            double medalsBonus = awardedMedals.Sum(m => m.Definition.BonusScore);
            double finalScore = Math.Round((baseWeightedScore * difficultyMultiplier) + medalsBonus, 1);

            string grade = DetermineGrade(compositeRatio, report != null && report.IsOverallSuccess);

            return new MissionScoreBreakdown(
                scienceDeliveryScore: Math.Round(scienceDeliveryRatio, 2),
                objectiveSuccessScore: Math.Round(objectiveSuccessRatio, 2),
                resourceEfficiencyScore: Math.Round(resourceEfficiency, 2),
                decisionQualityScore: Math.Round(decisionQuality, 2),
                planningEfficiencyScore: Math.Round(planningEfficiency, 2),
                baseWeightedScore: baseWeightedScore,
                difficultyMultiplier: Math.Round(difficultyMultiplier, 2),
                medalsBonusScore: medalsBonus,
                finalScore: finalScore,
                grade: grade,
                medals: awardedMedals
            );
        }

        private static double EvaluateResourceEfficiency(Blueprint blueprint, MissionProfile profile, ScoringConfig config)
        {
            if (blueprint == null) return 0.7; // baseline fallback

            var stats = BlueprintEvaluator.Evaluate(blueprint);
            if (stats == null) return 0.7;

            double score = 1.0;

            // Power margin check
            if (stats.PowerConsumption > 0.01)
            {
                double powerMargin = (stats.PowerGeneration - stats.PowerConsumption) / stats.PowerConsumption;
                if (powerMargin < config.IdealResourceMarginMin)
                {
                    // Too tight margin penalty
                    score -= 0.15;
                }
                else if (powerMargin > 0.80)
                {
                    // Over-engineered waste penalty
                    double excess = powerMargin - 0.80;
                    score -= Math.Min(0.20, excess * config.ExcessiveMarginPenaltyRate);
                }
            }

            // DeltaV margin check if profile exists
            if (profile != null && profile.RequiredDeltaV > 0 && stats.NetDeltaV > 0)
            {
                double deltaVMargin = (stats.NetDeltaV - profile.RequiredDeltaV) / profile.RequiredDeltaV;
                if (deltaVMargin < 0.0)
                {
                    score -= 0.40; // deficient
                }
                else if (deltaVMargin > 0.50)
                {
                    // Over-engineered rocket penalty
                    double excess = deltaVMargin - 0.50;
                    score -= Math.Min(0.20, excess * config.ExcessiveMarginPenaltyRate);
                }
            }

            return Math.Max(0.1, Math.Min(1.0, score));
        }

        private static double EvaluateDecisionQuality(
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double reliabilityPenalty,
            IReadOnlyList<ActiveOperationEvent> operationEvents)
        {
            double score = 1.0;

            if (launchResult != null && !launchResult.IsSuccessful)
            {
                score -= 0.50;
            }

            if (edlResult != null && !edlResult.IsSuccessful)
            {
                score -= 0.40;
            }

            // Degradation penalty from cruise/environment
            score -= Math.Min(0.30, reliabilityPenalty * 1.5);

            // Operation events decisions
            if (operationEvents != null && operationEvents.Count > 0)
            {
                int harmfulCount = 0;
                foreach (var ev in operationEvents)
                {
                    var opt = ev.Record?.Options?.FirstOrDefault(o => o.OptionId == ev.ChosenOptionId);
                    if (opt != null && (opt.ReliabilityPenalty > 0 || opt.ReputationDelta < 0 || opt.BudgetDelta < 0))
                    {
                        harmfulCount++;
                    }
                }

                double harmfulRatio = (double)harmfulCount / operationEvents.Count;
                score -= (harmfulRatio * 0.20);
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        private static double EvaluatePlanningEfficiency(
            MissionReport report,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            bool caughtLaunchWindow)
        {
            double score = 0.5;

            if (caughtLaunchWindow)
            {
                score += 0.25;
            }

            if (launchResult != null && launchResult.IsSuccessful)
            {
                score += 0.15;
            }

            if (report != null && report.IsOverallSuccess)
            {
                score += 0.10;
            }
            else if (launchResult != null && !launchResult.IsSuccessful)
            {
                score -= 0.35;
            }
            else if (edlResult != null && !edlResult.IsSuccessful)
            {
                score -= 0.25;
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        private static List<AwardedMedal> EvaluateMedals(
            MissionProfile profile,
            Blueprint blueprint,
            MissionReport report,
            LaunchSimulationResult launchResult,
            EdlSimulationResult edlResult,
            double downlinkedGb,
            double droppedGb,
            IReadOnlyList<ActiveOperationEvent> operationEvents,
            bool caughtLaunchWindow,
            ScoringConfig config)
        {
            var list = new List<AwardedMedal>();
            var medalDefs = config.Medals.ToDictionary(m => m.Id, m => m);

            bool isOverallSuccess = report != null && report.IsOverallSuccess;

            // 1. "Sıfır kayıp veri" (Zero Data Loss)
            if (medalDefs.TryGetValue("medal_zero_data_loss", out var mZeroData))
            {
                if (isOverallSuccess && downlinkedGb > 0.5 && droppedGb <= 0.001)
                {
                    list.Add(new AwardedMedal(mZeroData, "Üretilen tüm bilimsel veriler %100 başarıyla ve sıfır taşma kaybıyla indirildi."));
                }
            }

            // 2. "Pencereyi yakaladı" (Caught Window)
            if (medalDefs.TryGetValue("medal_caught_window", out var mWindow))
            {
                if (caughtLaunchWindow && (launchResult == null || launchResult.IsSuccessful))
                {
                    list.Add(new AwardedMedal(mWindow, "Fırlatma transfer penceresi zamanında ve erteleme olmadan yakalandı."));
                }
            }

            // 3. "Fırtınayı atlattı" (Weathered Storm)
            if (medalDefs.TryGetValue("medal_weathered_storm", out var mStorm))
            {
                if (operationEvents != null && operationEvents.Any(e => e.Record?.EventId == "op_solar_storm" && e.ChosenOptionId == "safe_mode"))
                {
                    list.Add(new AwardedMedal(mStorm, "Güneş fırtınası olayında güvenli mod seçilerek donanım hasarı önlendi."));
                }
            }

            // 4. "İlk insanlı" (First Crewed)
            if (medalDefs.TryGetValue("medal_first_crewed", out var mCrewed))
            {
                if (profile != null && profile.IsCrewed && isOverallSuccess)
                {
                    list.Add(new AwardedMedal(mCrewed, "İnsanlı görev tüm mürettebat güvenliği sağlanarak başarıyla tamamlandı."));
                }
            }

            // 5. "Ucuz ama etkili" (Cheap and Effective)
            if (medalDefs.TryGetValue("medal_cheap_effective", out var mCheap))
            {
                if (blueprint != null && isOverallSuccess)
                {
                    var stats = BlueprintEvaluator.Evaluate(blueprint);
                    if (stats != null && stats.Cost < 45.0) // Low budget vehicle
                    {
                        list.Add(new AwardedMedal(mCheap, "Düşük bütçeli araç mimarisiyle tam bilimsel başarı elde edildi."));
                    }
                }
            }

            // 6. "Tek kaynak yok" (Diversified Suppliers)
            if (medalDefs.TryGetValue("medal_diversified_suppliers", out var mDivSuppliers))
            {
                if (blueprint != null && isOverallSuccess)
                {
                    var suppliers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (blueprint.Payload?.Bus?.Supplier != null && !string.IsNullOrEmpty(blueprint.Payload.Bus.Supplier.Id))
                        suppliers.Add(blueprint.Payload.Bus.Supplier.Id);

                    if (blueprint.Stages != null)
                    {
                        foreach (var stg in blueprint.Stages)
                        {
                            if (stg?.StageSupplier != null && !string.IsNullOrEmpty(stg.StageSupplier.Id))
                                suppliers.Add(stg.StageSupplier.Id);
                            else if (stg?.Engine?.Supplier != null && !string.IsNullOrEmpty(stg.Engine.Supplier.Id))
                                suppliers.Add(stg.Engine.Supplier.Id);
                        }
                    }

                    if (suppliers.Count >= 2)
                    {
                        list.Add(new AwardedMedal(mDivSuppliers, "Tasarımda çoklu tedarikçi kullanılarak tek kaynak yoğunlaşma riski dağıtıldı."));
                    }
                }
            }

            // 7. "Hafif ve hızlı" (Light and Fast)
            if (medalDefs.TryGetValue("medal_light_and_fast", out var mLightFast))
            {
                if (blueprint != null && isOverallSuccess)
                {
                    var stats = BlueprintEvaluator.Evaluate(blueprint);
                    if (stats != null && stats.WetMass < 120.0 && stats.NetDeltaV >= 12000.0)
                    {
                        list.Add(new AwardedMedal(mLightFast, "Düşük ıslak kütleye rağmen yüksek hız değişimi (Delta-V) sağlandı."));
                    }
                }
            }

            return list;
        }

        private static string DetermineGrade(double compositeRatio, bool isSuccess)
        {
            if (!isSuccess)
            {
                return compositeRatio > 0.4 ? "D" : "F";
            }

            if (compositeRatio >= 0.90) return "S";
            if (compositeRatio >= 0.80) return "A";
            if (compositeRatio >= 0.65) return "B";
            if (compositeRatio >= 0.50) return "C";
            return "D";
        }
    }
}
