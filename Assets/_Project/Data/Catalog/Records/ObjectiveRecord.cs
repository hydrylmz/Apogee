using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// A player-selectable scientific or operational objective used by the Mission Composer.
    /// Numeric requirements are content data, never hard-coded in the composer.
    /// GDD §5.2, §19.3, M3-01.
    /// </summary>
    public sealed class ObjectiveRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        /// <summary>Destination ids that may offer this objective.</summary>
        [JsonProperty("destinationIds")]
        public List<string> DestinationIds { get; private set; } = new List<string>();

        /// <summary>Mission type ids that may select this objective. Empty means any type.</summary>
        [JsonProperty("missionTypeIds")]
        public List<string> MissionTypeIds { get; private set; } = new List<string>();

        [JsonProperty("requiredInstrumentCategories")]
        public List<string> RequiredInstrumentCategories { get; private set; } = new List<string>();

        [JsonProperty("minimumDataGb")]
        public double MinimumDataGb { get; private set; }

        [JsonProperty("minimumDurationDays")]
        public int MinimumDurationDays { get; private set; }

        [JsonProperty("scienceValue")]
        public double ScienceValue { get; private set; }

        [JsonProperty("difficultyPoints")]
        public double DifficultyPoints { get; private set; }

        [JsonProperty("requiredModuleCategories")]
        public List<string> RequiredModuleCategories { get; private set; } = new List<string>();

        [JsonProperty("successCriterionType")]
        public string SuccessCriterionType { get; private set; }

        [JsonProperty("successCriterionThreshold")]
        public double SuccessCriterionThreshold { get; private set; }

        /// <summary>
        /// Provenance note for provisional balance values. Must be replaced by a cited source
        /// before release when the GDD does not provide an authoritative number.
        /// </summary>
        [JsonProperty("dataSourceNote")]
        public string DataSourceNote { get; private set; }
    }
}
