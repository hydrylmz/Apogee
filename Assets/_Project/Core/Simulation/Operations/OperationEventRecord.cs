using System.Collections.Generic;

namespace Apogee.Core.Simulation.Operations
{
    /// <summary>
    /// Enumerates the mission phases in which an operation event may fire.
    /// GDD §10.2, §10.3.
    /// </summary>
    public enum OperationEventPhase
    {
        Any     = 0,
        Cruise  = 1,
        Edl     = 2,
        Science = 3,
    }

    /// <summary>
    /// Visible consequence information for a single decision option in an operation event card.
    /// GDD §10.2 — "görünür artı/eksi etki" (visible benefit/cost).
    /// </summary>
    public sealed class OperationEventOption
    {
        /// <summary>Unique identifier within this event's options.</summary>
        public string OptionId { get; }

        /// <summary>Short player-visible label (localisation key).</summary>
        public string LabelKey { get; }

        /// <summary>Sade dil consequence shown before selection (GDD §13.2).</summary>
        public string VisibleConsequenceKey { get; }

        /// <summary>
        /// Effect tag consumed by ResolveOperationEventCommand.
        /// Examples: "SAFE_MODE", "RISK_HARDWARE", "EXTRA_COST", "SCIENCE_LOSS",
        ///           "BONUS_SCIENCE", "RELIABILITY_HIT", "BUDGET_SPEND", "REPAIR".
        /// </summary>
        public string EffectTag { get; }

        /// <summary>Budget delta applied when this option is chosen (negative = spend).</summary>
        public double BudgetDelta { get; }

        /// <summary>Reliability penalty applied to the mission [0.0 – 1.0].</summary>
        public double ReliabilityPenalty { get; }

        /// <summary>Extra science data gained or lost in GB (negative = loss).</summary>
        public double ScienceDataDeltaGb { get; }

        /// <summary>Research points delta awarded to the campaign.</summary>
        public double ResearchPointsDelta { get; }

        /// <summary>Reputation delta applied to the campaign.</summary>
        public double ReputationDelta { get; }

        public OperationEventOption(
            string optionId,
            string labelKey,
            string visibleConsequenceKey,
            string effectTag,
            double budgetDelta         = 0.0,
            double reliabilityPenalty  = 0.0,
            double scienceDataDeltaGb  = 0.0,
            double researchPointsDelta = 0.0,
            double reputationDelta     = 0.0)
        {
            OptionId              = optionId              ?? string.Empty;
            LabelKey              = labelKey              ?? string.Empty;
            VisibleConsequenceKey = visibleConsequenceKey ?? string.Empty;
            EffectTag             = effectTag             ?? string.Empty;
            BudgetDelta           = budgetDelta;
            ReliabilityPenalty    = reliabilityPenalty    < 0.0 ? 0.0 : reliabilityPenalty;
            ScienceDataDeltaGb    = scienceDataDeltaGb;
            ResearchPointsDelta   = researchPointsDelta;
            ReputationDelta       = reputationDelta;
        }

        public override string ToString() =>
            $"[OpOption:{OptionId}] Tag={EffectTag} Budget={BudgetDelta:+#.##;-#.##;0}";
    }

    /// <summary>
    /// A single operation event card definition.
    /// Instances are built by OperationEventManager and stored in
    /// MissionInstance.OperationEventLog once resolved.
    /// GDD §10.2, §10.3, §19.12.
    /// </summary>
    public sealed class OperationEventRecord
    {
        /// <summary>Unique stable identifier for this event type.</summary>
        public string EventId { get; }

        /// <summary>Player-visible title (localisation key).</summary>
        public string TitleKey { get; }

        /// <summary>Narrative description (sade dil — localisation key).</summary>
        public string NarrativeKey { get; }

        /// <summary>Phase(s) in which this event may fire.</summary>
        public OperationEventPhase Phase { get; }

        /// <summary>
        /// Base weight for event selection.
        /// Higher values mean more likely relative to other events in the same pool.
        /// GDD §10.3: event probability weights derived from spacecraft design weaknesses.
        /// </summary>
        public double BaseWeight { get; }

        /// <summary>
        /// Design-weakness weighting fields: multiplier applied when the spacecraft's
        /// power/thermal/comms margin is below the given threshold [0.0 – 1.0].
        /// </summary>
        public double PowerWeaknessTriggerBelow   { get; }
        public double ThermalWeaknessTriggerBelow { get; }
        public double CommsWeaknessTriggerBelow   { get; }

        /// <summary>Weight multiplier when the matching weakness exists.</summary>
        public double WeaknessWeightMultiplier { get; }

        /// <summary>Available player choices.</summary>
        public IReadOnlyList<OperationEventOption> Options { get; }

        /// <summary>Whether this event may recur (cooldown is always applied).</summary>
        public bool IsRepeatable { get; }

        public OperationEventRecord(
            string eventId,
            string titleKey,
            string narrativeKey,
            OperationEventPhase phase,
            double baseWeight,
            IEnumerable<OperationEventOption> options,
            bool isRepeatable                     = true,
            double powerWeaknessTriggerBelow       = 1.1,  // 1.1 = effectively disabled
            double thermalWeaknessTriggerBelow     = 1.1,
            double commsWeaknessTriggerBelow       = 1.1,
            double weaknessWeightMultiplier        = 2.0,
            double dustWeaknessTriggerBelow        = 1.1)  // dust hazard threshold (OperationEventManager computes separately)
        {
            EventId                   = eventId    ?? string.Empty;
            TitleKey                  = titleKey   ?? string.Empty;
            NarrativeKey              = narrativeKey ?? string.Empty;
            Phase                     = phase;
            BaseWeight                = baseWeight  < 0.1 ? 0.1 : baseWeight;
            IsRepeatable              = isRepeatable;
            PowerWeaknessTriggerBelow   = powerWeaknessTriggerBelow;
            ThermalWeaknessTriggerBelow = thermalWeaknessTriggerBelow;
            CommsWeaknessTriggerBelow   = commsWeaknessTriggerBelow;
            WeaknessWeightMultiplier    = weaknessWeightMultiplier < 1.0 ? 1.0 : weaknessWeightMultiplier;

            var list = new List<OperationEventOption>();
            if (options != null) list.AddRange(options);
            Options = list;
        }


        public override string ToString() =>
            $"[OpEvent:{EventId}] Phase={Phase} Weight={BaseWeight}";
    }
}
