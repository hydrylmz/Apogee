using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a geopolitical/geographic region on the World Map.
    /// Maps directly to GDD §6.1 and §19.9 ("id, ad, harita poligonu/konumu, enlem, iklim/hava riski, işçilik/maliyet çarpanı, istikrar, kullanılabilir üs türleri").
    /// </summary>
    public sealed class RegionRecord : ICatalogRecord
    {
        // ── Identity ─────────────────────────────────────────────────────────────

        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>Localization key for display name.</summary>
        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        /// <summary>Localization key for region description and lore.</summary>
        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        /// <summary>Icon or flag asset key.</summary>
        [JsonProperty("icon")]
        public string Icon { get; private set; }

        // ── Geographic & Astrodynamic Properties ──────────────────────────────────

        /// <summary>
        /// Latitude in degrees (-90 to +90).
        /// Equator = 0°. Proximity to equator gives Earth rotation bonus (effective ?v bonus for prograde launches, GDD §6.1).
        /// High latitude allows efficient polar/retrograde orbits.
        /// </summary>
        [JsonProperty("latitude")]
        public double Latitude { get; private set; }

        /// <summary>Longitude in degrees (-180 to +180) for strategic globe placement.</summary>
        [JsonProperty("longitude")]
        public double Longitude { get; private set; }

        /// <summary>
        /// Approximate Earth rotational velocity assistance (m/s) at this latitude.
        /// Maximum at equator (~465 m/s * cos(lat)). Computed or overrideable.
        /// </summary>
        [JsonProperty("equatorialBonusDv")]
        public double EquatorialBonusDv { get; private set; }

        // ── Geopolitical & Economic Multipliers ───────────────────────────────────

        /// <summary>
        /// Local labor and manufacturing cost multiplier (e.g. 0.9 = 10% cheaper, 1.2 = 20% more expensive).
        /// Affects base building, expansion, and launch integration costs.
        /// </summary>
        [JsonProperty("laborCostMultiplier")]
        public double LaborCostMultiplier { get; private set; } = 1.0;

        /// <summary>
        /// Geopolitical stability score [0.0 - 1.0].
        /// 1.0 = absolute stability. Low stability increases random disruption/strike events.
        /// </summary>
        [JsonProperty("stability")]
        public double Stability { get; private set; } = 1.0;

        /// <summary>
        /// Weather & climate launch scrub/delay risk [0.0 - 1.0].
        /// 0.0 = completely arid/calm skies. High values increase weather scrub chances on launch windows.
        /// </summary>
        [JsonProperty("weatherRisk")]
        public double WeatherRisk { get; private set; } = 0.1;

        /// <summary>
        /// Logistics and supply chain accessibility score [0.0 - 1.0].
        /// Affects component transport time and cost between factory and launch pad.
        /// </summary>
        [JsonProperty("logisticsAccessibility")]
        public double LogisticsAccessibility { get; private set; } = 1.0;

        /// <summary>
        /// Ground station communication line-of-sight coverage score [0.0 - 1.0].
        /// High values provide wider orbital pass coverage angles.
        /// </summary>
        [JsonProperty("commCoverageRating")]
        public double CommCoverageRating { get; private set; } = 0.8;

        // ── Permitted Base Types ──────────────────────────────────────────────────

        /// <summary>
        /// Base types permitted in this region:
        /// "launch_pad", "factory", "research_center", "ground_station", "test_facility".
        /// GDD §6.1, §19.9.
        /// </summary>
        [JsonProperty("allowedBaseTypes")]
        public List<string> AllowedBaseTypes { get; private set; } = new List<string>();

        public override string ToString() =>
            $"RegionRecord[{Id}] lat={Latitude:0.#}°, cost={LaborCostMultiplier:0.##}x, stability={Stability:0.##}";
    }
}
