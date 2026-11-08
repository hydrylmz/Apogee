using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// Attribution category for grouping on the attribution screen.
    /// </summary>
    public enum AttributionCategory
    {
        NasaData,
        OpenSourceLibrary,
        AudioAsset,
        ArtAsset,
        Other
    }

    /// <summary>
    /// License type for an attributed asset or data source.
    /// </summary>
    public enum LicenseType
    {
        PublicDomain,
        CreativeCommonsCC0,
        CreativeCommonsBY,
        MIT,
        Apache2,
        Boost,
        Commercial,
        Other
    }

    /// <summary>
    /// A single attribution entry for a data source, library, or asset used in APOGEE (GDD §17.7, M8-06).
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class AttributionRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("category")]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public AttributionCategory Category { get; private set; }

        [JsonProperty("title")]
        public string Title { get; private set; }

        [JsonProperty("description")]
        public string Description { get; private set; }

        [JsonProperty("source")]
        public string Source { get; private set; }

        [JsonProperty("url")]
        public string Url { get; private set; }

        [JsonProperty("licenseType")]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public LicenseType LicenseType { get; private set; }

        [JsonProperty("licenseText")]
        public string LicenseText { get; private set; }

        /// <summary>
        /// List of internal system names that use this asset/library.
        /// </summary>
        [JsonProperty("usedInSystems")]
        public string[] UsedInSystems { get; private set; }

        public AttributionRecord() { }

        public AttributionRecord(
            string id,
            AttributionCategory category,
            string title,
            string description,
            string source,
            string url,
            LicenseType licenseType,
            string licenseText,
            string[] usedInSystems = null)
        {
            Id = id;
            Category = category;
            Title = title;
            Description = description;
            Source = source;
            Url = url;
            LicenseType = licenseType;
            LicenseText = licenseText;
            UsedInSystems = usedInSystems ?? System.Array.Empty<string>();
        }

        public override string ToString() => $"[{Category}] {Title} — {Source} ({LicenseType})";
    }
}
