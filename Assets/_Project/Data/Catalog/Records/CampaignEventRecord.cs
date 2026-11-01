using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// Trigger criteria for an event to be eligible in the campaign pool (GDD §19.10).
    /// </summary>
    public sealed class EventTriggerCondition
    {
        [JsonProperty("minYear")]
        public int? MinYear { get; private set; }

        [JsonProperty("maxYear")]
        public int? MaxYear { get; private set; }

        [JsonProperty("minBudget")]
        public double? MinBudget { get; private set; }

        [JsonProperty("maxBudget")]
        public double? MaxBudget { get; private set; }

        [JsonProperty("minReputation")]
        public double? MinReputation { get; private set; }

        [JsonProperty("maxReputation")]
        public double? MaxReputation { get; private set; }

        [JsonProperty("requiredSupplier")]
        public string RequiredSupplier { get; private set; }

        [JsonProperty("minSupplierRelationship")]
        public double? MinSupplierRelationship { get; private set; }

        [JsonProperty("maxSupplierRelationship")]
        public double? MaxSupplierRelationship { get; private set; }

        [JsonProperty("hasActiveResearch")]
        public bool? HasActiveResearch { get; private set; }

        [JsonProperty("hasActiveProduction")]
        public bool? HasActiveProduction { get; private set; }
    }

    /// <summary>
    /// Concrete gameplay modifications caused by selecting an event option (GDD §19.10).
    /// </summary>
    public sealed class EventOutcomeRecord
    {
        [JsonProperty("budgetDelta")]
        public double BudgetDelta { get; private set; }

        [JsonProperty("reputationDelta")]
        public double ReputationDelta { get; private set; }

        [JsonProperty("researchPointsDelta")]
        public double ResearchPointsDelta { get; private set; }

        [JsonProperty("knowledgePointsDelta")]
        public double KnowledgePointsDelta { get; private set; }

        [JsonProperty("supplierId")]
        public string SupplierId { get; private set; }

        [JsonProperty("supplierRelationshipDelta")]
        public double SupplierRelationshipDelta { get; private set; }

        [JsonProperty("summaryKey")]
        public string SummaryKey { get; private set; }
    }

    /// <summary>
    /// A single choice presented to the player in an event card (GDD §6.3, §10.2, §19.10).
    /// </summary>
    public sealed class EventOptionRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("textKey", Required = Required.Always)]
        public string TextKey { get; private set; }

        [JsonProperty("tooltipKey")]
        public string TooltipKey { get; private set; }

        [JsonProperty("visibleEffectSummary")]
        public string VisibleEffectSummary { get; private set; }

        [JsonProperty("outcome", Required = Required.Always)]
        public EventOutcomeRecord Outcome { get; private set; }
    }

    /// <summary>
    /// Catalog record for a campaign or operations event (GDD §19.10).
    /// </summary>
    public sealed class CampaignEventRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("titleKey", Required = Required.Always)]
        public string TitleKey { get; private set; }

        [JsonProperty("descKey", Required = Required.Always)]
        public string DescKey { get; private set; }

        [JsonProperty("category")]
        public string Category { get; private set; } = "campaign"; // "campaign", "supplier", "political", "research"

        [JsonProperty("baseWeight")]
        public double BaseWeight { get; private set; } = 10.0;

        [JsonProperty("isRepeatable")]
        public bool IsRepeatable { get; private set; } = false;

        [JsonProperty("cooldownDays")]
        public int CooldownDays { get; private set; } = 180;

        [JsonProperty("conditions")]
        public EventTriggerCondition Conditions { get; private set; } = new EventTriggerCondition();

        [JsonProperty("options")]
        public List<EventOptionRecord> Options { get; private set; } = new List<EventOptionRecord>();

        public override string ToString() => $"[CampaignEvent] {Id} ({Category}) BaseWeight={BaseWeight}";
    }
}
