using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.Operations;

namespace Apogee.Core.Simulation.Report
{
    /// <summary>
    /// Describes the single counterfactual change applied in a What-If analysis.
    /// The player picks one already-resolved operation event and selects the alternative option
    /// they did NOT choose. Everything else (design, launch, cruise, EDL, science) stays identical.
    ///
    /// GDD §10.5 ("Ya şöyle olsaydı?"), §13.2 ("Tek karar değiştirip karşılaştır").
    /// </summary>
    public sealed class WhatIfChange
    {
        /// <summary>Instance ID of the operation event being re-decided.</summary>
        public string EventInstanceId { get; }

        /// <summary>Display title of the event (player-readable).</summary>
        public string EventTitle { get; }

        /// <summary>Option ID originally chosen by the player.</summary>
        public string OriginalOptionId { get; }

        /// <summary>Short label of the original option (player-readable).</summary>
        public string OriginalOptionLabel { get; }

        /// <summary>Alternative option ID chosen for the What-If scenario.</summary>
        public string AlternativeOptionId { get; }

        /// <summary>Short label of the alternative option (player-readable).</summary>
        public string AlternativeOptionLabel { get; }

        public WhatIfChange(
            string eventInstanceId,
            string eventTitle,
            string originalOptionId,
            string originalOptionLabel,
            string alternativeOptionId,
            string alternativeOptionLabel)
        {
            EventInstanceId       = eventInstanceId       ?? throw new ArgumentNullException(nameof(eventInstanceId));
            EventTitle            = eventTitle            ?? string.Empty;
            OriginalOptionId      = originalOptionId      ?? string.Empty;
            OriginalOptionLabel   = originalOptionLabel   ?? string.Empty;
            AlternativeOptionId   = alternativeOptionId   ?? throw new ArgumentNullException(nameof(alternativeOptionId));
            AlternativeOptionLabel = alternativeOptionLabel ?? string.Empty;
        }

        public override string ToString() =>
            $"WhatIf: [{EventTitle}] '{OriginalOptionLabel}' -> '{AlternativeOptionLabel}'";
    }

    /// <summary>
    /// Side-by-side delta between the original mission report and the counterfactual (what-if) report.
    /// Used by the UI to highlight how changing one decision would have changed the outcome.
    ///
    /// GDD §10.5 ("deterministik olduğu için ucuz"), §13.2 ("Tek karar değiştirip karşılaştır").
    /// </summary>
    public sealed class WhatIfComparison
    {
        // ---------------------------------------------------------------- metadata
        /// <summary>The counterfactual change that was applied.</summary>
        public WhatIfChange Change { get; }

        // ---------------------------------------------------------------- reports
        /// <summary>The original mission report (player's actual choices).</summary>
        public MissionReport OriginalReport { get; }

        /// <summary>The counterfactual mission report with the alternative decision applied.</summary>
        public MissionReport WhatIfReport { get; }

        // ---------------------------------------------------------------- derived deltas
        /// <summary>Did the alternative outcome result in an overall mission success where the original failed (or vice-versa)?</summary>
        public bool OutcomeChanged { get; }

        /// <summary>Change in budget reward/penalty (what-if minus original).</summary>
        public double BudgetDelta { get; }

        /// <summary>Change in reputation reward/penalty (what-if minus original).</summary>
        public double ReputationDelta { get; }

        /// <summary>Change in research points granted (what-if minus original).</summary>
        public double ResearchPointsDelta { get; }

        /// <summary>Change in science data delivered in GB (what-if minus original).</summary>
        public double ScienceDataDelta { get; }

        /// <summary>Human-readable plain-language delta summary (sade dil).</summary>
        public string DeltaSummaryText { get; }

        public WhatIfComparison(
            WhatIfChange change,
            MissionReport originalReport,
            MissionReport whatIfReport)
        {
            Change         = change         ?? throw new ArgumentNullException(nameof(change));
            OriginalReport = originalReport ?? throw new ArgumentNullException(nameof(originalReport));
            WhatIfReport   = whatIfReport   ?? throw new ArgumentNullException(nameof(whatIfReport));

            OutcomeChanged = originalReport.IsOverallSuccess != whatIfReport.IsOverallSuccess;

            var origImpact = originalReport.CampaignImpact;
            var altImpact  = whatIfReport.CampaignImpact;

            BudgetDelta         = (altImpact?.BudgetChange        ?? 0.0) - (origImpact?.BudgetChange        ?? 0.0);
            ReputationDelta     = (altImpact?.ReputationChange     ?? 0.0) - (origImpact?.ReputationChange     ?? 0.0);
            ResearchPointsDelta = (altImpact?.ResearchPointsGranted ?? 0.0) - (origImpact?.ResearchPointsGranted ?? 0.0);

            double origScience = originalReport.ScienceSummary?.TotalDownlinkedGb ?? 0.0;
            double altScience  = whatIfReport.ScienceSummary?.TotalDownlinkedGb  ?? 0.0;
            ScienceDataDelta   = altScience - origScience;

            DeltaSummaryText = BuildDeltaSummary();
        }

        private string BuildDeltaSummary()
        {
            var parts = new List<string>();

            if (OutcomeChanged)
            {
                parts.Add(WhatIfReport.IsOverallSuccess
                    ? "Bu kararla görev BAŞARILI olurdu."
                    : "Bu kararla görev BAŞARISIZ olurdu.");
            }
            else
            {
                parts.Add(WhatIfReport.IsOverallSuccess ? "Görev yine başarılı olurdu." : "Görev yine başarısız olurdu.");
            }

            if (Math.Abs(BudgetDelta) > 0.01)
                parts.Add($"Bütçe: {(BudgetDelta >= 0 ? "+" : "")}{BudgetDelta:F1}M$");

            if (Math.Abs(ReputationDelta) > 0.01)
                parts.Add($"İtibar: {(ReputationDelta >= 0 ? "+" : "")}{ReputationDelta:F1}");

            if (Math.Abs(ScienceDataDelta) > 0.05)
                parts.Add($"Bilim Verisi: {(ScienceDataDelta >= 0 ? "+" : "")}{ScienceDataDelta:F1} GB");

            if (Math.Abs(ResearchPointsDelta) > 0.01)
                parts.Add($"Araştırma: {(ResearchPointsDelta >= 0 ? "+" : "")}{ResearchPointsDelta:F0} puan");

            return string.Join(" | ", parts);
        }

        public override string ToString() =>
            $"[WhatIf] {Change} -> {DeltaSummaryText}";
    }
}
