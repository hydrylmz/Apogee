using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// Represents a plain-language glossary term record (GDD §13.2, §14.1 #18, §23).
    /// </summary>
    public sealed class GlossaryTermRecord : ICatalogRecord
    {
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        [JsonProperty("term")]
        public string Term { get; private set; }

        [JsonProperty("plainTitle")]
        public string PlainTitle { get; private set; }

        [JsonProperty("category")]
        public string Category { get; private set; }

        [JsonProperty("summary")]
        public string Summary { get; private set; }

        [JsonProperty("detailedExplanation")]
        public string DetailedExplanation { get; private set; }

        [JsonProperty("whyItMatters")]
        public string WhyItMatters { get; private set; }

        public GlossaryTermRecord() { }

        public GlossaryTermRecord(
            string id,
            string term,
            string plainTitle,
            string category,
            string summary,
            string detailedExplanation,
            string whyItMatters)
        {
            Id = id;
            Term = term;
            PlainTitle = plainTitle;
            Category = category;
            Summary = summary;
            DetailedExplanation = detailedExplanation;
            WhyItMatters = whyItMatters;
        }

        public override string ToString() => $"[{Category}] {Term} ({PlainTitle}): {Summary}";
    }
}
