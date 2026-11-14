using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Core.Simulation.Scoring
{
    /// <summary>
    /// Configuration weights and thresholds for mission score calculation.
    /// GDD §11.1 (Bileşenler ayar dosyasında tutulur).
    /// </summary>
    public sealed class ScoringConfig
    {
        public double ScienceDeliveryWeight { get; }
        public double ObjectiveSuccessWeight { get; }
        public double ResourceEfficiencyWeight { get; }
        public double DecisionQualityWeight { get; }
        public double PlanningEfficiencyWeight { get; }

        public double IdealResourceMarginMin { get; }
        public double IdealResourceMarginMax { get; }
        public double ExcessiveMarginPenaltyRate { get; }
        public double BaseMaxScore { get; }

        public IReadOnlyList<MedalDefinition> Medals { get; }

        public ScoringConfig(
            double scienceDeliveryWeight = 0.35,
            double objectiveSuccessWeight = 0.20,
            double resourceEfficiencyWeight = 0.15,
            double decisionQualityWeight = 0.15,
            double planningEfficiencyWeight = 0.15,
            double idealResourceMarginMin = 0.10,
            double idealResourceMarginMax = 0.25,
            double excessiveMarginPenaltyRate = 0.5,
            double baseMaxScore = 1000.0,
            IEnumerable<MedalDefinition> medals = null)
        {
            ScienceDeliveryWeight = scienceDeliveryWeight;
            ObjectiveSuccessWeight = objectiveSuccessWeight;
            ResourceEfficiencyWeight = resourceEfficiencyWeight;
            DecisionQualityWeight = decisionQualityWeight;
            PlanningEfficiencyWeight = planningEfficiencyWeight;
            IdealResourceMarginMin = idealResourceMarginMin;
            IdealResourceMarginMax = idealResourceMarginMax;
            ExcessiveMarginPenaltyRate = excessiveMarginPenaltyRate;
            BaseMaxScore = baseMaxScore > 0 ? baseMaxScore : 1000.0;
            Medals = medals != null ? medals.ToList() : GetDefaultMedals();
        }

        public static ScoringConfig CreateDefault()
        {
            return new ScoringConfig();
        }

        public static List<MedalDefinition> GetDefaultMedals()
        {
            return new List<MedalDefinition>
            {
                new MedalDefinition("medal_cheap_effective", "MEDAL_CHEAP_EFFECTIVE_TITLE", "MEDAL_CHEAP_EFFECTIVE_DESC", "medal-piggy-bank", "Efficiency", 150.0),
                new MedalDefinition("medal_zero_data_loss", "MEDAL_ZERO_DATA_LOSS_TITLE", "MEDAL_ZERO_DATA_LOSS_DESC", "medal-satellite-dish", "Science", 120.0),
                new MedalDefinition("medal_weathered_storm", "MEDAL_WEATHERED_STORM_TITLE", "MEDAL_WEATHERED_STORM_DESC", "medal-shield-sun", "Operations", 100.0),
                new MedalDefinition("medal_first_crewed", "MEDAL_FIRST_CREWED_TITLE", "MEDAL_FIRST_CREWED_DESC", "medal-astronaut-helmet", "Prestige", 250.0),
                new MedalDefinition("medal_caught_window", "MEDAL_CAUGHT_WINDOW_TITLE", "MEDAL_CAUGHT_WINDOW_DESC", "medal-calendar-clock", "Planning", 100.0),
                new MedalDefinition("medal_diversified_suppliers", "MEDAL_DIVERSIFIED_SUPPLIERS_TITLE", "MEDAL_DIVERSIFIED_SUPPLIERS_DESC", "medal-handshake", "Procurement", 80.0),
                new MedalDefinition("medal_light_and_fast", "MEDAL_LIGHT_AND_FAST_TITLE", "MEDAL_LIGHT_AND_FAST_DESC", "medal-feather-rocket", "Engineering", 120.0)
            };
        }
    }

    /// <summary>
    /// Metadata definition for a mission medal.
    /// GDD §11.2 (Madalyalar).
    /// </summary>
    public sealed class MedalDefinition
    {
        public string Id { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public string IconKey { get; }
        public string Category { get; }
        public double BonusScore { get; }

        public MedalDefinition(
            string id,
            string titleKey,
            string descriptionKey,
            string iconKey,
            string category,
            double bonusScore)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            TitleKey = titleKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
            Category = category ?? "General";
            BonusScore = bonusScore;
        }

        public override string ToString() => $"[Medal:{Id}] {TitleKey} (+{BonusScore:F0} pts)";
    }

    /// <summary>
    /// An awarded medal with contextual reason.
    /// </summary>
    public sealed class AwardedMedal
    {
        public MedalDefinition Definition { get; }
        public string ReasonSummary { get; }

        public AwardedMedal(MedalDefinition definition, string reasonSummary)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ReasonSummary = reasonSummary ?? string.Empty;
        }

        public override string ToString() => $"{Definition.TitleKey}: {ReasonSummary}";
    }

    /// <summary>
    /// Complete breakdown of the mission score components and final calculated total.
    /// GDD §11.1.
    /// </summary>
    public sealed class MissionScoreBreakdown
    {
        // 5 Core pillars (0.0 to 1.0 each)
        public double ScienceDeliveryScore { get; }
        public double ObjectiveSuccessScore { get; }
        public double ResourceEfficiencyScore { get; }
        public double DecisionQualityScore { get; }
        public double PlanningEfficiencyScore { get; }

        // Raw weighted composite before difficulty multiplier (0 to BaseMaxScore)
        public double BaseWeightedScore { get; }

        // Difficulty multiplier from claim level / difficulty score (e.g. 1.0 to 3.0)
        public double DifficultyMultiplier { get; }

        // Medals bonus points
        public double MedalsBonusScore { get; }

        // Final overall points
        public double FinalScore { get; }

        // Rating tier: "F", "D", "C", "B", "A", "S"
        public string Grade { get; }

        // List of awarded medals
        public IReadOnlyList<AwardedMedal> Medals { get; }

        public MissionScoreBreakdown(
            double scienceDeliveryScore,
            double objectiveSuccessScore,
            double resourceEfficiencyScore,
            double decisionQualityScore,
            double planningEfficiencyScore,
            double baseWeightedScore,
            double difficultyMultiplier,
            double medalsBonusScore,
            double finalScore,
            string grade,
            IEnumerable<AwardedMedal> medals)
        {
            ScienceDeliveryScore = scienceDeliveryScore;
            ObjectiveSuccessScore = objectiveSuccessScore;
            ResourceEfficiencyScore = resourceEfficiencyScore;
            DecisionQualityScore = decisionQualityScore;
            PlanningEfficiencyScore = planningEfficiencyScore;
            BaseWeightedScore = baseWeightedScore;
            DifficultyMultiplier = difficultyMultiplier;
            MedalsBonusScore = medalsBonusScore;
            FinalScore = finalScore;
            Grade = grade ?? "C";
            Medals = medals != null ? medals.ToList() : new List<AwardedMedal>();
        }

        public override string ToString() =>
            $"[Score:{Grade}] Total={FinalScore:F0} (Base={BaseWeightedScore:F0} x {DifficultyMultiplier:F2} + Medals={MedalsBonusScore:F0})";
    }
}
