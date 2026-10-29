using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a mission type profile.
    /// Maps to GDD §19.2 and §5.1 / §7.6 / §7.7 (id, name, mandatory module categories, reliability threshold, EDL requirement, crewed status).
    /// </summary>
    public sealed class MissionTypeRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        [JsonProperty("requiredModuleCategories")]
        public List<string> RequiredModuleCategories { get; private set; } = new List<string>();

        [JsonProperty("minReliabilityThreshold")]
        public double MinReliabilityThreshold { get; private set; } = 0.80;

        [JsonProperty("requiresEdl")]
        public bool RequiresEdl { get; private set; } = false;

        [JsonProperty("isCrewed")]
        public bool IsCrewed { get; private set; } = false;

        [JsonProperty("rewardMultiplier")]
        public double RewardMultiplier { get; private set; } = 1.0;

        [JsonProperty("riskMultiplier")]
        public double RiskMultiplier { get; private set; } = 1.0;

        public MissionTypeRecord() { }

        public MissionTypeRecord(
            string id,
            string nameKey,
            string descKey = null,
            IEnumerable<string> requiredModuleCategories = null,
            double minReliabilityThreshold = 0.80,
            bool requiresEdl = false,
            bool isCrewed = false,
            double rewardMultiplier = 1.0,
            double riskMultiplier = 1.0)
        {
            Id = id;
            NameKey = nameKey;
            DescKey = descKey;
            if (requiredModuleCategories != null)
                RequiredModuleCategories.AddRange(requiredModuleCategories);
            MinReliabilityThreshold = minReliabilityThreshold;
            RequiresEdl = requiresEdl;
            IsCrewed = isCrewed;
            RewardMultiplier = rewardMultiplier;
            RiskMultiplier = riskMultiplier;
        }

        public override string ToString() => $"[MissionTypeRecord '{Id}' EDL={RequiresEdl} Crewed={IsCrewed} MinRel={MinReliabilityThreshold:P0}]";
    }
}
