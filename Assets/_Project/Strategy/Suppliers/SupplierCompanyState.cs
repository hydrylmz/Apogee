using System;
using Apogee.Data;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Runtime state and active terms for a supplier company (GDD §9.1, §19.6, §19.13).
    /// Tracks dynamic relationship scores, contract statistics, and computes effective multipliers.
    /// </summary>
    public sealed class SupplierCompanyState
    {
        public string Id => Record.Id;
        public SupplierRecord Record { get; }

        /// <summary>
        /// Ongoing relationship score [0.0 - 1.0].
        /// Starts from <see cref="SupplierRecord.RelationshipStart"/> (typically 0.5).
        /// Better relations earn cost discounts and expedited deliveries.
        /// Poor relations cause price hikes and schedule delays.
        /// </summary>
        public double RelationshipScore { get; private set; }

        public int ContractsSigned { get; set; }
        public int ContractsFulfilled { get; set; }
        public double TotalSpendMUSD { get; set; }

        public SupplierCompanyState(SupplierRecord record, double? initialRelationship = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            RelationshipScore = Math.Max(0.0, Math.Min(1.0, initialRelationship ?? record.RelationshipStart));
        }

        /// <summary>
        /// Calculates the dynamic cost multiplier combining catalog base, ongoing relationship,
        /// agency reputation, and any single-source volume discount.
        /// </summary>
        public double CalculateEffectiveCostMultiplier(
            string category,
            double agencyReputation = 50.0,
            double volumeDiscount = 0.0)
        {
            double baseMult = 1.0;
            if (!string.IsNullOrWhiteSpace(category) &&
                Record.CostMultiplierByCategory != null &&
                Record.CostMultiplierByCategory.TryGetValue(category, out double m))
            {
                baseMult = m;
            }

            // Relationship impact: at 0.5 => 1.0; at 1.0 => 0.90 (-10%); at 0.0 => 1.10 (+10%)
            double relFactor = 1.0 - ((RelationshipScore - 0.5) * 0.20);

            // Reputation impact: at 50 => 1.0; at 100 => 0.95 (-5%); at 0 => 1.05 (+5%)
            double repFactor = 1.0 - (((agencyReputation - 50.0) / 50.0) * 0.05);

            // Single-source volume discount (e.g. 5% - 10%)
            double volFactor = Math.Max(0.5, 1.0 - Math.Max(0.0, volumeDiscount));

            return Math.Max(0.1, baseMult * relFactor * repFactor * volFactor);
        }

        /// <summary>
        /// Calculates the dynamic delivery-time multiplier combining catalog base and relationship.
        /// </summary>
        public double CalculateEffectiveDeliveryMultiplier(string category)
        {
            double baseMult = 1.0;
            if (!string.IsNullOrWhiteSpace(category) &&
                Record.DeliveryMultiplierByCategory != null &&
                Record.DeliveryMultiplierByCategory.TryGetValue(category, out double m))
            {
                baseMult = m;
            }

            // Relationship impact: at 0.5 => 1.0; at 1.0 => 0.925 (-7.5% faster); at 0.0 => 1.075 (+7.5% slower)
            double relFactor = 1.0 - ((RelationshipScore - 0.5) * 0.15);

            return Math.Max(0.2, baseMult * relFactor);
        }

        /// <summary>
        /// Calculates the dynamic reliability multiplier combining catalog base and relationship experience.
        /// </summary>
        public double CalculateEffectiveReliabilityMultiplier(string category)
        {
            double baseMult = 1.0;
            if (!string.IsNullOrWhiteSpace(category) &&
                Record.ReliabilityMultiplierByCategory != null &&
                Record.ReliabilityMultiplierByCategory.TryGetValue(category, out double m))
            {
                baseMult = m;
            }

            // Relationship impact: at 0.5 => 1.0; at 1.0 => 1.025 (+2.5% quality boost); at 0.0 => 0.975 (-2.5%)
            double relFactor = 1.0 + ((RelationshipScore - 0.5) * 0.05);

            return Math.Max(0.1, baseMult * relFactor);
        }

        /// <summary>
        /// Produces a concrete <see cref="SupplierSpec"/> tailored to the target category,
        /// factoring in the current company relationship and agency status.
        /// </summary>
        public SupplierSpec CreateSupplierSpec(
            string category,
            double agencyReputation = 50.0,
            double volumeDiscount = 0.0)
        {
            double cost = CalculateEffectiveCostMultiplier(category, agencyReputation, volumeDiscount);
            double del  = CalculateEffectiveDeliveryMultiplier(category);
            double rel  = CalculateEffectiveReliabilityMultiplier(category);

            return new SupplierSpec(
                id: Id,
                name: Record.NameKey ?? Id,
                costMultiplier: cost,
                deliveryMultiplier: del,
                reliabilityMultiplier: rel);
        }

        /// <summary>
        /// Adjusts the relationship score by delta, clamped to [0.0 - 1.0].
        /// </summary>
        public void AdjustRelationship(double delta)
        {
            RelationshipScore = Math.Max(0.0, Math.Min(1.0, RelationshipScore + delta));
        }

        /// <summary>
        /// Alias for AdjustRelationship (GDD §9.1).
        /// </summary>
        public void ModifyRelationship(double delta) => AdjustRelationship(delta);

        /// <summary>
        /// Resets the relationship score to an exact value (useful for Undo).
        /// </summary>
        public void SetRelationship(double score)
        {
            RelationshipScore = Math.Max(0.0, Math.Min(1.0, score));
        }

        public override string ToString() =>
            $"[SupplierState: {Id} Rel={RelationshipScore:F2} Signed={ContractsSigned} Fulfilled={ContractsFulfilled}]";
    }
}
