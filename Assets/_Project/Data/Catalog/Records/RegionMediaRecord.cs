using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for one aerial/overhead photograph attached to a strategic region
    /// on the World Map (CampaignMap inspector "GÖRÜNTÜLER" section).
    ///
    /// Companion to <see cref="RegionRecord"/>; a region may have zero or more media records.
    /// All photos must be public-domain or permissive-license (CC BY / CC BY-SA) real aerial imagery,
    /// and must carry a non-empty <c>credit</c> string (GDD attribution policy).
    ///
    /// JSON shape (bare array accepted by CatalogLoader):
    ///   [ { "id": "guiana_coast_1", "regionId": "guiana_coast",
    ///       "path": "Map/Photos/guiana_kourou", "caption": "...", "credit": "ESA, CC BY-SA",
    ///       "order": 0, "isRepresentative": true } ]
    /// </summary>
    public sealed class RegionMediaRecord : ICatalogRecord
    {
        // ── Identity ─────────────────────────────────────────────────────────────

        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>
        /// Owning region id. Must match an existing <see cref="RegionRecord.Id"/>
        /// (case-insensitive lookup at runtime).
        /// </summary>
        [JsonProperty("regionId", Required = Required.Always)]
        public string RegionId { get; private set; }

        // ── Asset Reference ──────────────────────────────────────────────────────

        /// <summary>
        /// Asset path relative to a <c>Resources/</c> folder, WITHOUT extension
        /// (e.g. "Map/Photos/guiana_kourou" → "Assets/Resources/Map/Photos/guiana_kourou.jpg").
        /// Consistent with the "Icon asset address (Addressables key or Resources path)" convention
        /// used by ModuleRecord / ResearchNodeRecord.
        /// </summary>
        [JsonProperty("path")]
        public string Path { get; private set; } = string.Empty;

        /// <summary>Short human-readable caption shown under the photo.</summary>
        [JsonProperty("caption")]
        public string Caption { get; private set; } = string.Empty;

        /// <summary>
        /// Attribution / license string (e.g. "NASA, public domain", "ESA, CC BY-SA").
        /// Must never be empty — enforced by schema tests.
        /// </summary>
        [JsonProperty("credit")]
        public string Credit { get; private set; } = string.Empty;

        // ── Presentation Hints ───────────────────────────────────────────────────

        /// <summary>Sort order within the region's photo strip (ascending).</summary>
        [JsonProperty("order")]
        public int Order { get; private set; }

        /// <summary>
        /// True when the photo is a representative/nearby alternative aerial view
        /// rather than the actual real-world site (caption should carry a "temsilî" note).
        /// </summary>
        [JsonProperty("isRepresentative")]
        public bool IsRepresentative { get; private set; }

        public override string ToString() =>
            $"RegionMediaRecord[{Id}] region={RegionId}, path={Path}";
    }
}
