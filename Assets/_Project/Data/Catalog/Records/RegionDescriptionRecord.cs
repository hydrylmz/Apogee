using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for the in-game encyclopedia entry of a strategic region
    /// (BaseManagement radar console detail card + hover blip tooltip).
    ///
    /// Companion to <see cref="RegionRecord"/>; one record per region id.
    /// Text is Turkish; NASA-sourced descriptions are translations of public-domain NASA
    /// material and must cite the source. Non-NASA sites (Baikonur, Kourou, Esrange, Woomera)
    /// carry their own cited summaries (GDD attribution policy).
    ///
    /// JSON shape (bare array accepted by CatalogLoader):
    ///   [ { "id": "regdesc_cape_peninsula", "regionId": "cape_peninsula",
    ///       "siteName": "Kennedy Uzay Merkezi", "shortDescription": "...",
    ///       "fullDescription": "...", "source": "NASA", "sourceUrl": "...", "order": 0 } ]
    /// </summary>
    public sealed class RegionDescriptionRecord : ICatalogRecord
    {
        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>
        /// Owning region id. Must match an existing <see cref="RegionRecord.Id"/>
        /// (case-insensitive lookup at runtime).
        /// </summary>
        [JsonProperty("regionId", Required = Required.Always)]
        public string RegionId { get; private set; }

        /// <summary>Real-world site name, e.g. "Kennedy Uzay Merkezi".</summary>
        [JsonProperty("siteName")]
        public string SiteName { get; private set; } = string.Empty;

        /// <summary>
        /// One-liner shown on radar blip hover (tooltip content). Keep under ~90 chars.
        /// </summary>
        [JsonProperty("shortDescription")]
        public string ShortDescription { get; private set; } = string.Empty;

        /// <summary>
        /// Full Turkish description shown in the radar detail card. NASA-sourced text
        /// is a translation of public-domain material.
        /// </summary>
        [JsonProperty("fullDescription")]
        public string FullDescription { get; private set; } = string.Empty;

        /// <summary>
        /// Attribution string, e.g. "NASA" or "ESA". Rendered as the detail card's
        /// credit line ("Kaynak: {Source}").
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; private set; } = string.Empty;

        /// <summary>Canonical source URL for the attribution entry.</summary>
        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; private set; } = string.Empty;

        /// <summary>Sort hint (stable ordering when multiple records exist per region).</summary>
        [JsonProperty("order")]
        public int Order { get; private set; }

        public override string ToString() =>
            $"RegionDescriptionRecord[{Id}] region={RegionId}, site={SiteName}";
    }
}
