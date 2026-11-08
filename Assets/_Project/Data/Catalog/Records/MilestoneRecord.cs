using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// Data contract for agency campaign milestones (GDD §4.4, §19.13, §21 M6-01).
    /// Milestones define recommended objectives on the agency quest board with rewards in
    /// budget, reputation, and knowledge points.
    /// </summary>
    [Serializable]
    public sealed class MilestoneRecord : ICatalogRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("target_destination_id")]
        public string TargetDestinationId { get; set; }

        [JsonProperty("target_mission_type_id")]
        public string TargetMissionTypeId { get; set; }

        [JsonProperty("is_crewed")]
        public bool? IsCrewed { get; set; }

        [JsonProperty("requires_sample_return")]
        public bool? RequiresSampleReturn { get; set; }

        [JsonProperty("required_milestone_ids")]
        public List<string> RequiredMilestoneIds { get; set; } = new List<string>();

        [JsonProperty("reward_budget")]
        public double RewardBudget { get; set; }

        [JsonProperty("reward_reputation")]
        public double RewardReputation { get; set; }

        [JsonProperty("reward_knowledge_points")]
        public double RewardKnowledgePoints { get; set; }

        [JsonProperty("order_index")]
        public int OrderIndex { get; set; }
    }
}
