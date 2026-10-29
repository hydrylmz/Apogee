using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure functional domain engine for composing space missions.
    /// Derives an engineering <see cref="MissionProfile"/> from player selections (Destination, Mission Type, Objectives).
    /// Complies with GDD §5.1 - §5.5, §7.6, §7.7, §8.1, §17.2, and §17.3 item 8.
    /// </summary>
    public static class MissionComposer
    {
        /// <summary>
        /// Composes a complete <see cref="MissionProfile"/> from the provided composition request.
        /// </summary>
        public static MissionProfile Compose(MissionCompositionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return Compose(
                destination: request.Destination,
                missionType: request.MissionType,
                objectives: request.Objectives,
                windowIndex: request.WindowIndex,
                budgetCeiling: request.BudgetCeiling,
                missionName: request.MissionName,
                missionId: request.MissionId);
        }

        /// <summary>
        /// Composes a complete <see cref="MissionProfile"/> from explicit player selections.
        /// </summary>
        public static MissionProfile Compose(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives,
            int windowIndex = 0,
            double? budgetCeiling = null,
            string missionName = null,
            string missionId = null)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Target destination is mandatory for mission composition.");
            if (missionType == null)
                throw new ArgumentNullException(nameof(missionType), "Mission type is mandatory for mission composition.");

            var objectiveList = objectives != null
                ? objectives.Where(o => o != null).ToList()
                : new List<ObjectiveRecord>();

            string id = !string.IsNullOrWhiteSpace(missionId)
                ? missionId
                : $"MIS-{destination.Id.ToUpperInvariant()}-{missionType.Id.ToUpperInvariant()}-{DateTime.UtcNow.Ticks % 1000000:000000}";

            string name = !string.IsNullOrWhiteSpace(missionName)
                ? missionName
                : $"{destination.Id.ToUpperInvariant()} {missionType.Id.ToUpperInvariant()}";

            // 1. Calculate Required Delta-V (GDD §5.4, §8.1, §16.1)
            double requiredDeltaV = CalculateRequiredDeltaV(destination, missionType);

            // 2. Derive Required Module Categories (GDD §5.4, §7.6, §7.7)
            var requiredModuleCategories = DetermineRequiredModuleCategories(destination, missionType, objectiveList);

            // 3. Derive Scientific Instruments from Objectives
            var requiredInstrumentCategories = DetermineRequiredInstrumentCategories(objectiveList);

            // 4. Derive Required Research IDs (GDD §5.4, §19.1)
            var requiredResearchIds = DetermineRequiredResearch(destination, missionType, objectiveList);

            // 5. Reliability Threshold (GDD §5.4, §7.6)
            double minReliabilityThreshold = CalculateReliabilityThreshold(destination, missionType);

            // 6. Launch Pad Level Requirement (GDD §6.1, §16.7)
            int launchPadLevel = DetermineRequiredLaunchPadLevel(requiredDeltaV, missionType.IsCrewed);

            // 7. Telemetry & Data Requirements (GDD §5.4, §8.1)
            double totalDataGb = objectiveList.Sum(o => Math.Max(0.0, o.MinimumDataGb));
            if (totalDataGb <= 0.0) totalDataGb = 2.0; // Minimal baseline telemetry
            double minDataStorage = Math.Ceiling(totalDataGb * 1.25); // 25% safety margin

            int maxObjectiveDuration = objectiveList.Count > 0
                ? objectiveList.Max(o => Math.Max(1, o.MinimumDurationDays))
                : 14;

            double minDownlinkRate = Math.Max(0.5, Math.Round((totalDataGb / Math.Max(1, maxObjectiveDuration)) * 1.5, 2));

            // 8. Timelines & Durations (GDD §5.4, §16.1)
            bool isRoundTrip = missionType.Id == "sample-return" || missionType.IsCrewed;
            int transitMultiplier = isRoundTrip && destination.Id != "leo" ? 2 : 1;
            int transitDaysMin = destination.TransitDaysMin * transitMultiplier;
            int transitDaysMax = destination.TransitDaysMax * transitMultiplier;
            int minDurationDays = transitDaysMax + maxObjectiveDuration;

            // 9. Difficulty Score & Claim Level (GDD §5.3)
            var difficultyEvaluation = MissionDifficultyCalculator.Evaluate(destination, missionType, objectiveList);
            double difficultyScore = difficultyEvaluation.DifficultyScore;
            string claimLevel = difficultyEvaluation.ClaimLevelLabel;


            // 10. Total Science Value
            double totalScienceValue = objectiveList.Sum(o => Math.Max(0.0, o.ScienceValue));

            // 11. Cost & Build Time Estimates (GDD §5.4)
            var (costMin, costMax) = EstimateCostRange(requiredDeltaV, missionType, objectiveList);
            var (buildMin, buildMax) = EstimateBuildTimeRange(requiredDeltaV, missionType);

            // 12. Measurable Success Criteria (GDD §5.5)
            var successCriteria = GenerateSuccessCriteria(objectiveList);

            var selectedObjectiveIds = objectiveList.Select(o => o.Id).ToList();

            return new MissionProfile(
                id: id,
                name: name,
                targetDestinationId: destination.Id,
                targetMissionTypeId: missionType.Id,
                requiredDeltaV: requiredDeltaV,
                minReliabilityThreshold: minReliabilityThreshold,
                requiredLaunchPadLevel: launchPadLevel,
                minDownlinkRate: minDownlinkRate,
                minDataStorage: minDataStorage,
                requiresEdl: missionType.RequiresEdl,
                requiresLifeSupport: missionType.IsCrewed,
                requiredModuleCategories: requiredModuleCategories,
                requiredResearchIds: requiredResearchIds,
                selectedObjectiveIds: selectedObjectiveIds,
                requiredInstrumentCategories: requiredInstrumentCategories,
                successCriteria: successCriteria,
                transitDaysMin: transitDaysMin,
                transitDaysMax: transitDaysMax,
                minDurationDays: minDurationDays,
                commDelaySecondsMin: destination.CommDelaySecondsMin,
                commDelaySecondsMax: destination.CommDelaySecondsMax,
                windowPeriodDays: destination.WindowPeriodDays,
                windowFirstOffsetDays: destination.WindowFirstOffsetDays,
                windowDurationDays: destination.WindowDurationDays,
                selectedWindowIndex: windowIndex,
                difficultyScore: difficultyScore,
                claimLevel: claimLevel,
                totalScienceValue: totalScienceValue,
                estimatedCostMin: costMin,
                estimatedCostMax: costMax,
                estimatedBuildTimeDaysMin: buildMin,
                estimatedBuildTimeDaysMax: buildMax,
                budgetCeiling: budgetCeiling,
                claimLevelType: difficultyEvaluation.ClaimLevel,
                rewardMultiplier: difficultyEvaluation.RewardMultiplier,
                riskMultiplier: difficultyEvaluation.RiskMultiplier,
                rewardSummary: difficultyEvaluation.RewardSummary,
                riskSummary: difficultyEvaluation.RiskSummary);
        }

        /// <summary>
        /// Computes the total required mission Δv according to orbital destination budget and mission profile.
        /// Complies with GDD §5.4 and §8.1.
        /// </summary>
        public static double CalculateRequiredDeltaV(DestinationRecord destination, MissionTypeRecord missionType)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (missionType == null) throw new ArgumentNullException(nameof(missionType));

            // Baseline: Ascent + Transfer + Arrival
            double dv = destination.DvAscent + destination.DvTransfer + destination.DvArrival;

            // Surface landing (Lander, Rover, Sample-Return, Crewed)
            if (missionType.RequiresEdl ||
                string.Equals(missionType.Id, "lander", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(missionType.Id, "rover", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(missionType.Id, "sample-return", StringComparison.OrdinalIgnoreCase) ||
                missionType.IsCrewed)
            {
                dv += destination.DvLanding;
            }

            // Return leg (Sample-Return, Crewed) for non-LEO missions
            if (!string.Equals(destination.Id, "leo", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(missionType.Id, "sample-return", StringComparison.OrdinalIgnoreCase) || missionType.IsCrewed))
            {
                dv += destination.DvReturn;
            }

            return Math.Round(dv, 0);
        }

        /// <summary>
        /// Computes mission difficulty score according to GDD §5.3:
        /// Zorluk = Σ(amaç zorlukları) × hedef çarpanı × tip çarpanı.
        /// </summary>
        public static double CalculateDifficultyScore(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives)
        {
            return MissionDifficultyCalculator.Evaluate(destination, missionType, objectives).DifficultyScore;
        }

        /// <summary>
        /// Maps numerical difficulty score to user-facing Claim Level (GDD §5.3 "İddia Seviyesi").
        /// </summary>
        public static string DetermineClaimLevel(double score)
        {
            return MissionDifficultyCalculator.ClassifyClaimLevel(score).label;
        }

        /// <summary>
        /// Derives all mandatory module categories required for vehicle assembly.
        /// Complies with GDD §5.4, §7.6 (Crewed), §7.7 (Rover/EDL).
        /// </summary>
        public static List<string> DetermineRequiredModuleCategories(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives)
        {
            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From mission type
            if (missionType?.RequiredModuleCategories != null)
            {
                foreach (var cat in missionType.RequiredModuleCategories)
                {
                    if (!string.IsNullOrWhiteSpace(cat))
                        categories.Add(cat);
                }
            }

            // EDL mandatory rule
            if (missionType != null && missionType.RequiresEdl)
            {
                categories.Add("edl");
            }

            // Crewed mandatory rules (GDD §7.6)
            if (missionType != null && missionType.IsCrewed)
            {
                categories.Add("cabin");
                categories.Add("life-support");
                categories.Add("abort-system");
            }

            // Environmental hazard rules (Radiation shielding)
            if (destination != null)
            {
                bool hasSevereRadiation = destination.Hazards != null &&
                    destination.Hazards.TryGetValue("radiation", out double rad) && rad >= 0.5;

                if (hasSevereRadiation ||
                    string.Equals(destination.Id, "europa", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(destination.Id, "mars", StringComparison.OrdinalIgnoreCase) && missionType != null && missionType.IsCrewed))
                {
                    categories.Add("radiation-shielding");
                }
            }

            // From selected objectives
            if (objectives != null)
            {
                foreach (var obj in objectives)
                {
                    if (obj == null) continue;

                    if (obj.RequiredModuleCategories != null)
                    {
                        foreach (var cat in obj.RequiredModuleCategories)
                        {
                            if (!string.IsNullOrWhiteSpace(cat))
                                categories.Add(cat);
                        }
                    }

                    if (obj.RequiredInstrumentCategories != null && obj.RequiredInstrumentCategories.Count > 0)
                    {
                        categories.Add("instrument");
                    }
                }
            }

            return categories.ToList();
        }

        /// <summary>
        /// Extracts all unique instrument categories required by the selected objectives.
        /// </summary>
        public static List<string> DetermineRequiredInstrumentCategories(IEnumerable<ObjectiveRecord> objectives)
        {
            var instruments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (objectives != null)
            {
                foreach (var obj in objectives)
                {
                    if (obj?.RequiredInstrumentCategories != null)
                    {
                        foreach (var inst in obj.RequiredInstrumentCategories)
                        {
                            if (!string.IsNullOrWhiteSpace(inst))
                                instruments.Add(inst);
                        }
                    }
                }
            }
            return instruments.ToList();
        }

        /// <summary>
        /// Identifies all prerequisite technology and research node IDs required for this mission.
        /// </summary>
        public static List<string> DetermineRequiredResearch(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives)
        {
            var research = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (destination?.UnlockResearchIds != null)
            {
                foreach (var resId in destination.UnlockResearchIds)
                {
                    if (!string.IsNullOrWhiteSpace(resId))
                        research.Add(resId);
                }
            }

            if (missionType != null && missionType.IsCrewed)
            {
                research.Add("crewed-systems");
            }

            return research.ToList();
        }

        /// <summary>
        /// Computes system reliability threshold required for this mission profile.
        /// </summary>
        public static double CalculateReliabilityThreshold(DestinationRecord destination, MissionTypeRecord missionType)
        {
            double threshold = missionType != null && missionType.MinReliabilityThreshold > 0.0
                ? missionType.MinReliabilityThreshold
                : 0.80;

            if (missionType != null && missionType.IsCrewed)
            {
                threshold = Math.Max(threshold, 0.98); // Crewed missions require near-zero tolerance (GDD §7.6)
            }

            return Math.Round(threshold, 2);
        }

        /// <summary>
        /// Determines the minimum launch pad infrastructure level required (1-5) based on mission Δv and human rating.
        /// </summary>
        public static int DetermineRequiredLaunchPadLevel(double requiredDeltaV, bool isCrewed)
        {
            if (isCrewed && requiredDeltaV > 14000)
                return 5; // Super heavy crew-rated launch pad

            if (requiredDeltaV > 18000) return 5;
            if (requiredDeltaV > 15500) return 4;
            if (requiredDeltaV > 13500) return 3;
            if (requiredDeltaV > 9500) return 2;
            return 1;
        }

        /// <summary>
        /// Generates measurable, verifiable success criteria from selected scientific objectives.
        /// </summary>
        public static List<MissionSuccessCriterion> GenerateSuccessCriteria(IEnumerable<ObjectiveRecord> objectives)
        {
            var list = new List<MissionSuccessCriterion>();
            if (objectives == null) return list;

            foreach (var obj in objectives)
            {
                if (obj == null) continue;
                list.Add(new MissionSuccessCriterion(
                    objectiveId: obj.Id,
                    criterionType: obj.SuccessCriterionType,
                    threshold: obj.SuccessCriterionThreshold));
            }

            return list;
        }

        private static double GetDestinationMultiplier(DestinationRecord destination)
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
                    return Math.Round(1.0 + hazardBonus, 1);
            }
        }

        private static (double minCost, double maxCost) EstimateCostRange(
            double deltaV,
            MissionTypeRecord missionType,
            IReadOnlyList<ObjectiveRecord> objectives)
        {
            double baseCost = (deltaV / 100.0) * (missionType?.RewardMultiplier ?? 1.0);
            if (missionType != null && missionType.IsCrewed)
                baseCost *= 1.8;

            double objectiveCostBonus = (objectives?.Count ?? 0) * 8.0;
            baseCost += objectiveCostBonus;

            double min = Math.Round(baseCost * 0.85, 1);
            double max = Math.Round(baseCost * 1.35, 1);
            return (min, max);
        }

        private static (int minDays, int maxDays) EstimateBuildTimeRange(double deltaV, MissionTypeRecord missionType)
        {
            double baseDays = (deltaV / 80.0) * (missionType?.RiskMultiplier ?? 1.0);
            if (missionType != null && missionType.IsCrewed)
                baseDays *= 1.5;

            int min = (int)Math.Max(30, Math.Round(baseDays * 0.85));
            int max = (int)Math.Max(min + 15, Math.Round(baseDays * 1.35));
            return (min, max);
        }
    }
}
