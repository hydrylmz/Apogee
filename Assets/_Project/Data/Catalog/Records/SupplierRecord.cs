using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a supplier company.
    /// Maps to GDD §9.1 and §19.6.
    ///
    /// Companies affect cost, delivery time, reliability, and special capabilities
    /// for each component they manufacture.
    /// </summary>
    public sealed class SupplierRecord : ICatalogRecord
    {
        // ── Identity ─────────────────────────────────────────────────────────────

        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>Localization key for display name.</summary>
        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        /// <summary>Localization key for tagline / description.</summary>
        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        /// <summary>Color hex string for the company's stripe on parts (e.g. "#FF6A00").</summary>
        [JsonProperty("brandColor")]
        public string BrandColor { get; private set; }

        /// <summary>Icon / logo asset key.</summary>
        [JsonProperty("icon")]
        public string Icon { get; private set; }

        // ── Starting relation ─────────────────────────────────────────────────────

        /// <summary>
        /// Initial relationship score [0.0 – 1.0].
        /// Higher = better terms from the start.
        /// GDD §9.1 "ilişki başlangıç puanı".
        /// </summary>
        [JsonProperty("relationshipStart")]
        public double RelationshipStart { get; private set; }

        // ── Per-category multipliers ──────────────────────────────────────────────

        /// <summary>
        /// Cost multipliers per module category.
        /// Keys match ModuleRecord.Category strings.
        /// Values: 1.0 = base cost; &lt;1.0 = cheaper; &gt;1.0 = more expensive.
        /// GDD §9.1 "kategori bazlı fiyat çarpanları".
        /// </summary>
        [JsonProperty("costMultiplierByCategory")]
        public Dictionary<string, double> CostMultiplierByCategory { get; private set; }
            = new Dictionary<string, double>();

        /// <summary>
        /// Delivery-time multipliers per module category.
        /// Values: &lt;1.0 = faster; &gt;1.0 = slower.
        /// </summary>
        [JsonProperty("deliveryMultiplierByCategory")]
        public Dictionary<string, double> DeliveryMultiplierByCategory { get; private set; }
            = new Dictionary<string, double>();

        /// <summary>
        /// Reliability multipliers per module category.
        /// Values: &lt;1.0 = less reliable; &gt;1.0 = more reliable.
        /// Applied as a flat factor to base component reliability.
        /// </summary>
        [JsonProperty("reliabilityMultiplierByCategory")]
        public Dictionary<string, double> ReliabilityMultiplierByCategory { get; private set; }
            = new Dictionary<string, double>();

        // ── Special capabilities ──────────────────────────────────────────────────

        /// <summary>
        /// Capability tags that only this company can provide.
        /// E.g. "crewed-certification", "reusable-first-stage".
        /// GDD §9.1 "özel yetenekler".
        /// </summary>
        [JsonProperty("specialCapabilities")]
        public List<string> SpecialCapabilities { get; private set; } = new List<string>();

        // ── Strength / weakness tags ──────────────────────────────────────────────

        /// <summary>Human-readable strength category keys. GDD §9.1 table.</summary>
        [JsonProperty("strengthTags")]
        public List<string> StrengthTags { get; private set; } = new List<string>();

        /// <summary>Human-readable weakness category keys. GDD §9.1 table.</summary>
        [JsonProperty("weaknessTags")]
        public List<string> WeaknessTags { get; private set; } = new List<string>();

        // ── Event tendencies ──────────────────────────────────────────────────────

        /// <summary>
        /// Probability weight modifiers for company-related events.
        /// Keys match event id prefixes; values: multiplier on base event weight.
        /// GDD §19.6 "olay eğilimleri".
        /// </summary>
        [JsonProperty("eventWeightModifiers")]
        public Dictionary<string, double> EventWeightModifiers { get; private set; }
            = new Dictionary<string, double>();

        public override string ToString() => $"[Supplier] {Id}  rel={RelationshipStart:0.##}";
    }
}
