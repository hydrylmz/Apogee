using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Evaluates eligibility and weighted probabilities for campaign events (GDD §10.3, §19.10).
    /// Pure functional calculation without side effects.
    /// </summary>
    public static class EventEvaluator
    {
        /// <summary>
        /// Checks whether a given event's conditions are satisfied by the current campaign state.
        /// </summary>
        public static bool IsEventEligible(
            CampaignEventRecord eventRecord,
            CampaignState state,
            IReadOnlyDictionary<string, int> eventCooldowns,
            IReadOnlyCollection<string> completedNonRepeatableEvents)
        {
            if (eventRecord == null || state == null)
                return false;

            // Check if already completed non-repeatable event
            if (!eventRecord.IsRepeatable && completedNonRepeatableEvents != null)
            {
                foreach (var completedId in completedNonRepeatableEvents)
                {
                    if (string.Equals(completedId, eventRecord.Id, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // Check cooldown
            if (eventCooldowns != null && eventCooldowns.TryGetValue(eventRecord.Id, out int cooldown) && cooldown > 0)
                return false;

            var cond = eventRecord.Conditions;
            if (cond == null)
                return true;

            int currentYear = state.Time.CurrentDate.Year;
            if (cond.MinYear.HasValue && currentYear < cond.MinYear.Value)
                return false;
            if (cond.MaxYear.HasValue && currentYear > cond.MaxYear.Value)
                return false;

            if (cond.MinBudget.HasValue && state.Budget < cond.MinBudget.Value)
                return false;
            if (cond.MaxBudget.HasValue && state.Budget > cond.MaxBudget.Value)
                return false;

            if (cond.MinReputation.HasValue && state.Reputation < cond.MinReputation.Value)
                return false;
            if (cond.MaxReputation.HasValue && state.Reputation > cond.MaxReputation.Value)
                return false;

            if (!string.IsNullOrEmpty(cond.RequiredSupplier))
            {
                var company = state.Suppliers?.GetCompany(cond.RequiredSupplier);
                if (company == null)
                    return false;

                if (cond.MinSupplierRelationship.HasValue && company.RelationshipScore < cond.MinSupplierRelationship.Value)
                    return false;
                if (cond.MaxSupplierRelationship.HasValue && company.RelationshipScore > cond.MaxSupplierRelationship.Value)
                    return false;
            }

            if (cond.HasActiveResearch.HasValue)
            {
                bool hasActiveResearch = !string.IsNullOrEmpty(state.Research?.ActiveNodeId);
                if (cond.HasActiveResearch.Value != hasActiveResearch)
                    return false;
            }

            if (cond.HasActiveProduction.HasValue)
            {
                bool hasActiveJobs = false;
                if (state.Production != null)
                {
                    foreach (var _ in state.Production.ActiveJobs)
                    {
                        hasActiveJobs = true;
                        break;
                    }
                }
                if (cond.HasActiveProduction.Value != hasActiveJobs)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Computes effective weight for an event taking supplier eventWeightModifiers into account.
        /// GDD §19.6 "eventWeightModifiers: keys match event id prefixes; values: multiplier on base event weight".
        /// </summary>
        public static double CalculateEffectiveWeight(CampaignEventRecord eventRecord, CampaignState state)
        {
            if (eventRecord == null)
                return 0.0;

            double weight = Math.Max(0.1, eventRecord.BaseWeight);

            if (state?.Suppliers != null)
            {
                // Check if any supplier has a modifier matching this event
                foreach (var company in state.Suppliers.AllCompanies)
                {
                    var record = company.Record;
                    if (record?.EventWeightModifiers == null)
                        continue;

                    foreach (var kvp in record.EventWeightModifiers)
                    {
                        string normalizedEventId = eventRecord.Id.Replace('_', '-');
                        string normalizedKey = kvp.Key.Replace('_', '-');

                        if (normalizedEventId.StartsWith(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                            normalizedEventId.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                            eventRecord.Id.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                            eventRecord.Id.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            weight *= Math.Max(0.1, kvp.Value);
                        }
                    }
                }
            }

            return weight;
        }
    }
}
