using Newtonsoft.Json;
using Apogee.Design;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a structural material.
    /// Maps to GDD §16.4 and §19.5 (id, mass multiplier, cost multiplier, build time multiplier, research prerequisite).
    /// </summary>
    public sealed class MaterialRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        [JsonProperty("massMultiplier")]
        public double MassMultiplier { get; private set; } = 1.0;

        [JsonProperty("costMultiplier")]
        public double CostMultiplier { get; private set; } = 1.0;

        [JsonProperty("buildTimeMultiplier")]
        public double BuildTimeMultiplier { get; private set; } = 1.0;

        [JsonProperty("requiredResearchId")]
        public string RequiredResearchId { get; private set; }

        [JsonProperty("heatResistanceClass")]
        public string HeatResistanceClass { get; private set; } = "standard";

        public MaterialRecord() { }

        public MaterialRecord(
            string id,
            string nameKey,
            string descKey = null,
            double massMultiplier = 1.0,
            double costMultiplier = 1.0,
            double buildTimeMultiplier = 1.0,
            string requiredResearchId = null,
            string heatResistanceClass = "standard")
        {
            Id = id;
            NameKey = nameKey;
            DescKey = descKey;
            MassMultiplier = massMultiplier > 0 ? massMultiplier : 1.0;
            CostMultiplier = costMultiplier > 0 ? costMultiplier : 1.0;
            BuildTimeMultiplier = buildTimeMultiplier > 0 ? buildTimeMultiplier : 1.0;
            RequiredResearchId = requiredResearchId;
            HeatResistanceClass = heatResistanceClass ?? "standard";
        }

        public MaterialSpec ToMaterialSpec()
        {
            return new MaterialSpec(
                id: Id,
                name: NameKey ?? Id,
                massMultiplier: MassMultiplier,
                costMultiplier: CostMultiplier,
                buildTimeMultiplier: BuildTimeMultiplier,
                requiredResearchId: RequiredResearchId
            );
        }

        public override string ToString() => $"[MaterialRecord '{Id}' Mass x{MassMultiplier:0.##} Cost x{CostMultiplier:0.##}]";
    }
}
