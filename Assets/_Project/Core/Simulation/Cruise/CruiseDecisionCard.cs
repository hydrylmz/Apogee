using System.Collections.Generic;

namespace Apogee.Core.Simulation.Cruise
{
    /// <summary>
    /// A decision card presented to the player during the cruise phase (GDD §10.2).
    /// Each option has a visible consequence summary and a set of hidden outcome modifiers.
    /// Only the visible consequence text is stored here; actual effects are resolved by the command layer.
    /// </summary>
    public sealed class CruiseDecisionCard
    {
        /// <summary>Unique identifier for this decision within the cruise simulation.</summary>
        public string DecisionId { get; }

        /// <summary>Localisation key for the event title shown to the player.</summary>
        public string TitleKey { get; }

        /// <summary>Localisation key for the narrative description of the situation.</summary>
        public string NarrativeKey { get; }

        /// <summary>Available options for the player to choose from.</summary>
        public IReadOnlyList<CruiseDecisionOption> Options { get; }

        /// <summary>Index of the segment event that spawned this decision card.</summary>
        public int SegmentIndex { get; }

        public CruiseDecisionCard(
            string decisionId,
            string titleKey,
            string narrativeKey,
            int segmentIndex,
            IEnumerable<CruiseDecisionOption> options)
        {
            DecisionId    = decisionId ?? string.Empty;
            TitleKey      = titleKey ?? string.Empty;
            NarrativeKey  = narrativeKey ?? string.Empty;
            SegmentIndex  = segmentIndex;

            var list = new List<CruiseDecisionOption>();
            if (options != null)
                list.AddRange(options);
            Options = list;
        }

        public override string ToString() =>
            $"[DecisionCard:{DecisionId}] Seg={SegmentIndex}, Options={Options.Count}";
    }

    /// <summary>
    /// One selectable option in a cruise decision card (GDD §10.2).
    /// </summary>
    public sealed class CruiseDecisionOption
    {
        /// <summary>Identifier for this option (used by ResolveCruiseDecisionCommand).</summary>
        public string OptionId { get; }

        /// <summary>Localisation key for the option label shown to the player.</summary>
        public string LabelKey { get; }

        /// <summary>Visible consequence summary (sade dil, GDD §10.2 — "görünür artı/eksi etki").</summary>
        public string VisibleConsequenceKey { get; }

        /// <summary>
        /// Hidden technical tag used by the resolver to apply campaign effects.
        /// Examples: "SAFE_MODE", "RISK_HARDWARE", "EXTRA_COST", "SCIENCE_LOSS".
        /// </summary>
        public string EffectTag { get; }

        public CruiseDecisionOption(
            string optionId,
            string labelKey,
            string visibleConsequenceKey,
            string effectTag)
        {
            OptionId               = optionId ?? string.Empty;
            LabelKey               = labelKey ?? string.Empty;
            VisibleConsequenceKey  = visibleConsequenceKey ?? string.Empty;
            EffectTag              = effectTag ?? string.Empty;
        }

        public override string ToString() => $"[Option:{OptionId}] Tag={EffectTag}";
    }
}
