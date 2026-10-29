using System;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Immutable evaluation result containing difficulty score, claim level classification,
    /// reward multipliers, risk multipliers, and player-facing summaries.
    /// Complies with GDD §5.1, §5.3 ("Zorluk Katsayısı ve İddia Seviyesi"), §8.3, and §11.1.
    /// </summary>
    public sealed class MissionDifficultyEvaluation
    {
        /// <summary>Total calculated numerical difficulty score: Σ(amaç) × hedef × tip.</summary>
        public double DifficultyScore { get; }

        /// <summary>Categorical claim classification (Low, Medium, High, Extreme).</summary>
        public MissionClaimLevel ClaimLevel { get; }

        /// <summary>Player-facing localized claim level label (e.g. "Düşük", "Orta", "Yüksek", "Çok Yüksek").</summary>
        public string ClaimLevelLabel { get; }

        /// <summary>Expected reward multiplier (science, reputation, budget prestige bonus).</summary>
        public double RewardMultiplier { get; }

        /// <summary>Expected operational risk multiplier (failure sensitivity, event volatility).</summary>
        public double RiskMultiplier { get; }

        /// <summary>Player-facing concise summary of expected rewards (GDD §5.3, §8.3).</summary>
        public string RewardSummary { get; }

        /// <summary>Player-facing concise summary of operational risks and constraints (GDD §5.3, §8.3).</summary>
        public string RiskSummary { get; }

        /// <summary>Combined concise reward/risk summary line.</summary>
        public string CombinedSummary { get; }

        /// <summary>Sum of individual objective difficulty points.</summary>
        public double SumObjectiveDifficulty { get; }

        /// <summary>Target destination environment/distance difficulty multiplier.</summary>
        public double DestinationMultiplier { get; }

        /// <summary>Mission profile risk multiplier.</summary>
        public double MissionTypeMultiplier { get; }

        public MissionDifficultyEvaluation(
            double difficultyScore,
            MissionClaimLevel claimLevel,
            string claimLevelLabel,
            double rewardMultiplier,
            double riskMultiplier,
            string rewardSummary,
            string riskSummary,
            double sumObjectiveDifficulty,
            double destinationMultiplier,
            double missionTypeMultiplier)
        {
            DifficultyScore = Math.Round(Math.Max(0.0, difficultyScore), 1);
            ClaimLevel = claimLevel;
            ClaimLevelLabel = !string.IsNullOrWhiteSpace(claimLevelLabel) ? claimLevelLabel : "Orta";
            RewardMultiplier = Math.Round(Math.Max(1.0, rewardMultiplier), 2);
            RiskMultiplier = Math.Round(Math.Max(1.0, riskMultiplier), 2);
            RewardSummary = rewardSummary ?? string.Empty;
            RiskSummary = riskSummary ?? string.Empty;
            CombinedSummary = $"Ödül: {RewardSummary} | Risk: {RiskSummary}";
            SumObjectiveDifficulty = Math.Round(Math.Max(0.0, sumObjectiveDifficulty), 1);
            DestinationMultiplier = Math.Round(Math.Max(1.0, destinationMultiplier), 2);
            MissionTypeMultiplier = Math.Round(Math.Max(1.0, missionTypeMultiplier), 2);
        }

        public override string ToString() =>
            $"[Difficulty {DifficultyScore:0.0} ({ClaimLevelLabel}) Reward={RewardMultiplier:0.00}x Risk={RiskMultiplier:0.00}x]";
    }
}
