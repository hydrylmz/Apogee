using System;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure functional evaluator for region data under specific strategic map lenses.
    /// Complies with GDD §6.1.
    /// </summary>
    public static class RegionLensEvaluator
    {
        /// <summary>
        /// Evaluates a normalized score [0.0 - 1.0] for the region under the specified lens.
        /// Higher score generally indicates favorable or prominent attributes for that lens.
        /// </summary>
        public static double EvaluateScore(RegionRecord region, MapLens lens)
        {
            if (region == null) return 0.0;

            switch (lens)
            {
                case MapLens.LaunchEfficiency:
                    // Max Earth rotation assist at equator is ~465 m/s.
                    return Math.Clamp(region.EquatorialBonusDv / 465.0, 0.0, 1.0);

                case MapLens.Logistics:
                    // High logistics accessibility, penalized by high labor costs
                    double costPenalty = Math.Clamp(1.0 - (region.LaborCostMultiplier - 1.0) * 0.5, 0.2, 1.0);
                    return Math.Clamp(region.LogisticsAccessibility * costPenalty, 0.0, 1.0);

                case MapLens.CommsCoverage:
                    return Math.Clamp(region.CommCoverageRating, 0.0, 1.0);

                case MapLens.Risk:
                    // Combined operational risk: weather scrub risk + instability
                    double instability = 1.0 - Math.Clamp(region.Stability, 0.0, 1.0);
                    double combinedRisk = (region.WeatherRisk * 0.6) + (instability * 0.4);
                    return Math.Clamp(combinedRisk, 0.0, 1.0);

                case MapLens.Research:
                    // Regions that allow research centers get high baseline; high stability boosts research
                    bool permitsResearch = region.AllowedBaseTypes != null && region.AllowedBaseTypes.Contains("research_center");
                    double baseScore = permitsResearch ? 0.8 : 0.2;
                    return Math.Clamp(baseScore * region.Stability, 0.0, 1.0);

                case MapLens.Overview:
                default:
                    // General composite rating
                    double stabilityScore = region.Stability;
                    double dvScore = region.EquatorialBonusDv / 465.0;
                    return Math.Clamp((stabilityScore * 0.5) + (dvScore * 0.3) + (region.LogisticsAccessibility * 0.2), 0.0, 1.0);
            }
        }

        /// <summary>
        /// Generates a brief, player-friendly telemetry description for the given region under the active lens.
        /// </summary>
        public static string GetLensDescription(RegionRecord region, MapLens lens)
        {
            if (region == null) return string.Empty;

            switch (lens)
            {
                case MapLens.LaunchEfficiency:
                    return $"+{region.EquatorialBonusDv:0} m/s rotation bonus (Lat {Math.Abs(region.Latitude):0.#}° {(region.Latitude >= 0 ? "N" : "S")})";

                case MapLens.Logistics:
                    return $"Logistics {region.LogisticsAccessibility * 100:0}%, Labor Cost {region.LaborCostMultiplier:0.##}x";

                case MapLens.CommsCoverage:
                    return $"Coverage rating {region.CommCoverageRating * 100:0}%";

                case MapLens.Risk:
                    return $"Weather scrub risk {region.WeatherRisk * 100:0}%, Stability {region.Stability * 100:0}%";

                case MapLens.Research:
                    bool canResearch = region.AllowedBaseTypes != null && region.AllowedBaseTypes.Contains("research_center");
                    return canResearch ? "Research Center permitted" : "No Research Center zoning";

                case MapLens.Overview:
                default:
                    return $"Stability {region.Stability * 100:0}%, +{region.EquatorialBonusDv:0} m/s ?v assist";
            }
        }
    }
}
