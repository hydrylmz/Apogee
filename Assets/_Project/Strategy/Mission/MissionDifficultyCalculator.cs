using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure functional engine for calculating mission difficulty, claim levels, reward bonuses, and operational risk.
    /// Complies with GDD §5.1, §5.3 ("Zorluk Katsayısı"), §8.3, and §11.1.
    /// </summary>
    public static class MissionDifficultyCalculator
    {
        public const double ThresholdLow = 5.0;
        public const double ThresholdMedium = 13.0;
        public const double ThresholdHigh = 25.0;

        /// <summary>
        /// Evaluates the complete difficulty, claim level, reward multipliers, and risk summaries for a set of mission choices.
        /// Formula (GDD §5.3): Zorluk = Σ(amaç zorlukları) × hedef çarpanı × tip çarpanı.
        /// </summary>
        public static MissionDifficultyEvaluation Evaluate(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives)
        {
            double sumObjectiveDifficulty = 0.0;
            if (objectives != null)
            {
                foreach (var obj in objectives)
                {
                    if (obj != null)
                        sumObjectiveDifficulty += Math.Max(0.0, obj.DifficultyPoints);
                }
            }

            // Baseline difficulty for an exploration flight without specific payload instruments
            if (sumObjectiveDifficulty <= 0.0)
                sumObjectiveDifficulty = 1.0;

            double destMultiplier = GetDestinationMultiplier(destination);
            double typeMultiplier = missionType != null && missionType.RiskMultiplier > 0.0
                ? missionType.RiskMultiplier
                : 1.0;

            double score = Math.Round(sumObjectiveDifficulty * destMultiplier * typeMultiplier, 1);

            var (claimLevel, label) = ClassifyClaimLevel(score);

            double baseReward = missionType != null && missionType.RewardMultiplier > 0.0
                ? missionType.RewardMultiplier
                : 1.0;

            double baseRisk = typeMultiplier;

            // Reward scales with difficulty: +4% per difficulty point on top of mission type reward
            double rewardMultiplier = Math.Round(baseReward * (1.0 + (score * 0.04)), 2);

            // Operational risk scales with difficulty: +3% per difficulty point on top of type risk
            double riskMultiplier = Math.Round(baseRisk * (1.0 + (score * 0.03)), 2);

            string rewardSummary = GenerateRewardSummary(claimLevel, rewardMultiplier, missionType);
            string riskSummary = GenerateRiskSummary(claimLevel, riskMultiplier, destination, missionType);

            return new MissionDifficultyEvaluation(
                difficultyScore: score,
                claimLevel: claimLevel,
                claimLevelLabel: label,
                rewardMultiplier: rewardMultiplier,
                riskMultiplier: riskMultiplier,
                rewardSummary: rewardSummary,
                riskSummary: riskSummary,
                sumObjectiveDifficulty: sumObjectiveDifficulty,
                destinationMultiplier: destMultiplier,
                missionTypeMultiplier: typeMultiplier);
        }

        /// <summary>
        /// Classifies difficulty score into player-facing claim level categories and localized labels.
        /// </summary>
        public static (MissionClaimLevel level, string label) ClassifyClaimLevel(double score)
        {
            if (score < ThresholdLow)
                return (MissionClaimLevel.Low, "Düşük");
            if (score < ThresholdMedium)
                return (MissionClaimLevel.Medium, "Orta");
            if (score < ThresholdHigh)
                return (MissionClaimLevel.High, "Yüksek");

            return (MissionClaimLevel.Extreme, "Çok Yüksek");
        }

        /// <summary>
        /// Returns the target destination's environmental and navigational difficulty multiplier.
        /// </summary>
        public static double GetDestinationMultiplier(DestinationRecord destination)
        {
            if (destination == null) return 1.0;

            switch (destination.Id?.ToLowerInvariant())
            {
                case "leo": return 1.0;
                case "moon": return 1.3;
                case "venus": return 1.8;
                case "mars": return 2.0;
                case "europa": return 2.5;
                default:
                    double hazardBonus = destination.Hazards != null ? destination.Hazards.Values.Sum() * 0.4 : 0.0;
                    return Math.Round(1.0 + hazardBonus, 2);
            }
        }

        private static string GenerateRewardSummary(
            MissionClaimLevel level,
            double rewardMultiplier,
            MissionTypeRecord missionType)
        {
            int rewardBonusPercent = (int)Math.Round((rewardMultiplier - 1.0) * 100);
            string bonusPrefix = rewardBonusPercent >= 0 ? $"+%{rewardBonusPercent}" : $"%{rewardBonusPercent}";

            string extra = string.Empty;
            if (missionType != null && missionType.IsCrewed)
            {
                extra = ", İnsanlı Keşif Prestij Bonusu";
            }
            else if (level == MissionClaimLevel.Extreme)
            {
                extra = ", Tarihi Misyon Bonusu";
            }
            else if (level == MissionClaimLevel.High)
            {
                extra = ", Yüksek Ajans İtibarı";
            }

            return $"{bonusPrefix} Bilim & İtibar Çarpanı{extra}";
        }

        private static string GenerateRiskSummary(
            MissionClaimLevel level,
            double riskMultiplier,
            DestinationRecord destination,
            MissionTypeRecord missionType)
        {
            int riskBonusPercent = (int)Math.Round((riskMultiplier - 1.0) * 100);
            string riskPrefix = riskBonusPercent >= 0 ? $"+%{riskBonusPercent}" : $"%{riskBonusPercent}";

            double minRel = missionType?.MinReliabilityThreshold ?? 0.80;
            if (missionType != null && missionType.IsCrewed)
                minRel = Math.Max(minRel, 0.98);

            string condition = $"%{Math.Round(minRel * 100):0}+ Güvenilirlik";

            string hazardNote = string.Empty;
            if (destination != null)
            {
                if (string.Equals(destination.Id, "europa", StringComparison.OrdinalIgnoreCase) ||
                    (destination.Hazards != null && destination.Hazards.TryGetValue("radiation", out double r) && r >= 0.5))
                {
                    hazardNote = ", Radyasyon Kalkanı Şartı";
                }
                else if (missionType != null && missionType.RequiresEdl)
                {
                    hazardNote = ", EDL İniş Hassasiyeti";
                }
            }

            return $"{riskPrefix} Operasyonel Risk, {condition}{hazardNote}";
        }
    }
}
