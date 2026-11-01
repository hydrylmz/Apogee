using System;
using System.Collections.Generic;

namespace Apogee.Strategy
{
    /// <summary>
    /// Comprehensive evaluation report for single-source supplier concentration and volume discounts.
    /// Complies with GDD §9.1, §8.3 (sade sunum), and §13.2 ("Neden?" kartları).
    /// </summary>
    public sealed class SingleSourceRiskReport
    {
        /// <summary>Total evaluated components with assigned suppliers.</summary>
        public int TotalComponents { get; }

        /// <summary>Number of components supplied by each supplier ID.</summary>
        public IReadOnlyDictionary<string, int> ComponentCountBySupplier { get; }

        /// <summary>Proportional share of each supplier [0.0 - 1.0].</summary>
        public IReadOnlyDictionary<string, double> SupplierShareRatio { get; }

        /// <summary>ID of the dominant supplier company with the highest component share.</summary>
        public string DominantSupplierId { get; }

        /// <summary>Dominant supplier's share of total components [0.0 - 1.0].</summary>
        public double DominantSupplierShare { get; }

        /// <summary>True if single source concentration threshold is met (DominantSupplierShare >= 0.50).</summary>
        public bool HasSingleSourceConcentration { get; }

        /// <summary>Applied volume discount percentage [0.0 - 0.15] (e.g. 0.05 for 5%, 0.10 for 10%).</summary>
        public double VolumeDiscountPercent { get; }

        /// <summary>Calculated risk exposure level.</summary>
        public SingleSourceRiskLevel RiskLevel { get; }

        /// <summary>Short summary in plain Turkish/English for UI badges and callouts.</summary>
        public string SummaryText { get; }

        /// <summary>Detailed plain-language explanation of why this risk score was given (GDD §13.2).</summary>
        public string RiskExplanation { get; }

        public SingleSourceRiskReport(
            int totalComponents,
            IReadOnlyDictionary<string, int> countBySupplier,
            IReadOnlyDictionary<string, double> shareBySupplier,
            string dominantSupplierId,
            double dominantSupplierShare,
            bool hasConcentration,
            double volumeDiscountPercent,
            SingleSourceRiskLevel riskLevel,
            string summaryText,
            string riskExplanation)
        {
            TotalComponents = totalComponents;
            ComponentCountBySupplier = countBySupplier ?? new Dictionary<string, int>();
            SupplierShareRatio = shareBySupplier ?? new Dictionary<string, double>();
            DominantSupplierId = dominantSupplierId ?? string.Empty;
            DominantSupplierShare = dominantSupplierShare;
            HasSingleSourceConcentration = hasConcentration;
            VolumeDiscountPercent = Math.Max(0.0, volumeDiscountPercent);
            RiskLevel = riskLevel;
            SummaryText = summaryText ?? string.Empty;
            RiskExplanation = riskExplanation ?? string.Empty;
        }

        public override string ToString() =>
            $"[SingleSourceReport: {RiskLevel} Dominant={DominantSupplierId} ({DominantSupplierShare:P0}) Disc={VolumeDiscountPercent:P0}]";
    }
}
