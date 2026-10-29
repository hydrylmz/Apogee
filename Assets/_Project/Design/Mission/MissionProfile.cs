using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents the operational and engineering requirement profile for a space mission.
    /// Derived from destination, mission type, and selected objectives.
    /// Acts as the target requirement specification against which a <see cref="Blueprint"/> is validated.
    /// Complies with GDD §5.4, §7.4, §17.3, and §19.2/§19.3.
    /// </summary>
    public sealed class MissionProfile
    {
        private readonly List<string> _requiredModuleCategories = new List<string>();
        private readonly List<string> _requiredResearchIds = new List<string>();
        private readonly List<string> _selectedObjectiveIds = new List<string>();
        private readonly List<string> _requiredInstrumentCategories = new List<string>();
        private readonly List<MissionSuccessCriterion> _successCriteria = new List<MissionSuccessCriterion>();

        public string Id { get; }
        public string Name { get; }
        public string TargetDestinationId { get; }
        public string TargetMissionTypeId { get; }

        /// <summary>
        /// Total required mission Delta-V in m/s (launch losses + transfer + insertion + landing/return).
        /// GDD §5.4 "menzil ihtiyacı".
        /// </summary>
        public double RequiredDeltaV { get; }

        /// <summary>
        /// Minimum required overall system reliability [0.0 - 1.0].
        /// GDD §5.4 "güvenilirlik eşiği".
        /// </summary>
        public double MinReliabilityThreshold { get; }

        /// <summary>
        /// Minimum launch pad tier level required for this mission (1-5).
        /// GDD §6.1 / §16.7.
        /// </summary>
        public int RequiredLaunchPadLevel { get; }

        /// <summary>
        /// Minimum downlink rate required in GB/day for telemetry and science.
        /// </summary>
        public double MinDownlinkRate { get; }

        /// <summary>
        /// Minimum on-board storage capacity in GB.
        /// </summary>
        public double MinDataStorage { get; }

        /// <summary>
        /// Whether an Entry, Descent & Landing (EDL) module is mandatory (e.g. lander/rover).
        /// GDD §7.7.
        /// </summary>
        public bool RequiresEdl { get; }

        /// <summary>
        /// Whether life support and crew cabin systems are mandatory (crewed missions).
        /// GDD §7.6.
        /// </summary>
        public bool RequiresLifeSupport { get; }

        /// <summary>
        /// Semantic category tags for modules that must be present in the design.
        /// GDD §5.4 "zorunlu modüller".
        /// </summary>
        public IReadOnlyList<string> RequiredModuleCategories => _requiredModuleCategories;

        /// <summary>
        /// Specific research node IDs required for this mission.
        /// </summary>
        public IReadOnlyList<string> RequiredResearchIds => _requiredResearchIds;

        /// <summary>
        /// Selected objective IDs associated with this mission profile.
        /// </summary>
        public IReadOnlyList<string> SelectedObjectiveIds => _selectedObjectiveIds;

        /// <summary>
        /// Specific scientific instrument categories required by the selected objectives.
        /// </summary>
        public IReadOnlyList<string> RequiredInstrumentCategories => _requiredInstrumentCategories;

        /// <summary>
        /// Measurable success criteria generated from the selected objectives.
        /// GDD §5.5.
        /// </summary>
        public IReadOnlyList<MissionSuccessCriterion> SuccessCriteria => _successCriteria;

        // ── Timeline & Windows (GDD §5.4) ──────────────────────────────────────────

        public int TransitDaysMin { get; }
        public int TransitDaysMax { get; }
        public int MinDurationDays { get; }
        public double CommDelaySecondsMin { get; }
        public double CommDelaySecondsMax { get; }
        public int WindowPeriodDays { get; }
        public int WindowFirstOffsetDays { get; }
        public int WindowDurationDays { get; }
        public int SelectedWindowIndex { get; }

        // ── Evaluation & Estimates (GDD §5.3, §5.4) ───────────────────────────────

        public double DifficultyScore { get; }
        public string ClaimLevel { get; }
        public MissionClaimLevel ClaimLevelType { get; }
        public double RewardMultiplier { get; }
        public double RiskMultiplier { get; }
        public string RewardSummary { get; }
        public string RiskSummary { get; }
        public double TotalScienceValue { get; }
        public double EstimatedCostMin { get; }
        public double EstimatedCostMax { get; }
        public int EstimatedBuildTimeDaysMin { get; }
        public int EstimatedBuildTimeDaysMax { get; }
        public double? BudgetCeiling { get; }

        public MissionProfile(
            string id,
            string name = null,
            string targetDestinationId = null,
            string targetMissionTypeId = null,
            double requiredDeltaV = 0.0,
            double minReliabilityThreshold = 0.80,
            int requiredLaunchPadLevel = 1,
            double minDownlinkRate = 0.0,
            double minDataStorage = 0.0,
            bool requiresEdl = false,
            bool requiresLifeSupport = false,
            IEnumerable<string> requiredModuleCategories = null,
            IEnumerable<string> requiredResearchIds = null,
            IEnumerable<string> selectedObjectiveIds = null,
            IEnumerable<string> requiredInstrumentCategories = null,
            IEnumerable<MissionSuccessCriterion> successCriteria = null,
            int transitDaysMin = 0,
            int transitDaysMax = 0,
            int minDurationDays = 0,
            double commDelaySecondsMin = 0.0,
            double commDelaySecondsMax = 0.0,
            int windowPeriodDays = 0,
            int windowFirstOffsetDays = 0,
            int windowDurationDays = 0,
            int selectedWindowIndex = 0,
            double difficultyScore = 0.0,
            string claimLevel = null,
            double totalScienceValue = 0.0,
            double estimatedCostMin = 0.0,
            double estimatedCostMax = 0.0,
            int estimatedBuildTimeDaysMin = 0,
            int estimatedBuildTimeDaysMax = 0,
            double? budgetCeiling = null,
            MissionClaimLevel? claimLevelType = null,
            double rewardMultiplier = 1.0,
            double riskMultiplier = 1.0,
            string rewardSummary = null,
            string riskSummary = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissionProfile ID cannot be empty.", nameof(id));

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : id;
            TargetDestinationId = targetDestinationId ?? "leo";
            TargetMissionTypeId = targetMissionTypeId ?? "orbiter";

            RequiredDeltaV = Math.Max(0.0, requiredDeltaV);
            MinReliabilityThreshold = Math.Max(0.0, Math.Min(1.0, minReliabilityThreshold));
            RequiredLaunchPadLevel = Math.Max(1, requiredLaunchPadLevel);
            MinDownlinkRate = Math.Max(0.0, minDownlinkRate);
            MinDataStorage = Math.Max(0.0, minDataStorage);

            RequiresEdl = requiresEdl;
            RequiresLifeSupport = requiresLifeSupport;

            TransitDaysMin = Math.Max(0, transitDaysMin);
            TransitDaysMax = Math.Max(TransitDaysMin, transitDaysMax);
            MinDurationDays = Math.Max(0, minDurationDays);
            CommDelaySecondsMin = Math.Max(0.0, commDelaySecondsMin);
            CommDelaySecondsMax = Math.Max(CommDelaySecondsMin, commDelaySecondsMax);
            WindowPeriodDays = Math.Max(0, windowPeriodDays);
            WindowFirstOffsetDays = Math.Max(0, windowFirstOffsetDays);
            WindowDurationDays = Math.Max(0, windowDurationDays);
            SelectedWindowIndex = Math.Max(0, selectedWindowIndex);

            DifficultyScore = Math.Max(0.0, difficultyScore);
            ClaimLevel = !string.IsNullOrWhiteSpace(claimLevel) ? claimLevel : "Orta";
            ClaimLevelType = claimLevelType ?? (DifficultyScore < 5.0 ? MissionClaimLevel.Low : (DifficultyScore < 13.0 ? MissionClaimLevel.Medium : (DifficultyScore < 25.0 ? MissionClaimLevel.High : MissionClaimLevel.Extreme)));
            RewardMultiplier = Math.Max(1.0, rewardMultiplier);
            RiskMultiplier = Math.Max(1.0, riskMultiplier);
            RewardSummary = !string.IsNullOrWhiteSpace(rewardSummary) ? rewardSummary : $"+%{Math.Round((RewardMultiplier - 1.0) * 100):0} Bilim ve İtibar Bonusu";
            RiskSummary = !string.IsNullOrWhiteSpace(riskSummary) ? riskSummary : $"+%{Math.Round((RiskMultiplier - 1.0) * 100):0} Operasyonel Risk";
            TotalScienceValue = Math.Max(0.0, totalScienceValue);
            EstimatedCostMin = Math.Max(0.0, estimatedCostMin);
            EstimatedCostMax = Math.Max(EstimatedCostMin, estimatedCostMax);
            EstimatedBuildTimeDaysMin = Math.Max(0, estimatedBuildTimeDaysMin);
            EstimatedBuildTimeDaysMax = Math.Max(EstimatedBuildTimeDaysMin, estimatedBuildTimeDaysMax);
            BudgetCeiling = budgetCeiling;

            if (requiredModuleCategories != null)
            {
                foreach (var cat in requiredModuleCategories)
                {
                    if (!string.IsNullOrWhiteSpace(cat) && !_requiredModuleCategories.Contains(cat))
                        _requiredModuleCategories.Add(cat);
                }
            }

            if (requiredResearchIds != null)
            {
                foreach (var res in requiredResearchIds)
                {
                    if (!string.IsNullOrWhiteSpace(res) && !_requiredResearchIds.Contains(res))
                        _requiredResearchIds.Add(res);
                }
            }

            if (selectedObjectiveIds != null)
            {
                foreach (var obj in selectedObjectiveIds)
                {
                    if (!string.IsNullOrWhiteSpace(obj) && !_selectedObjectiveIds.Contains(obj))
                        _selectedObjectiveIds.Add(obj);
                }
            }

            if (requiredInstrumentCategories != null)
            {
                foreach (var inst in requiredInstrumentCategories)
                {
                    if (!string.IsNullOrWhiteSpace(inst) && !_requiredInstrumentCategories.Contains(inst))
                        _requiredInstrumentCategories.Add(inst);
                }
            }

            if (successCriteria != null)
            {
                _successCriteria.AddRange(successCriteria);
            }
        }

        public override string ToString() =>
            $"[MissionProfile '{Name}' Dest: {TargetDestinationId} Type: {TargetMissionTypeId} Δv Req: {RequiredDeltaV:0} m/s Rel Req: {MinReliabilityThreshold:P0} Claim: {ClaimLevel}]";
    }
}
