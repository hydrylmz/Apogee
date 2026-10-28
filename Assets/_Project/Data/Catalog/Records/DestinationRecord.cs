using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a mission destination (target body).
    /// Maps to GDD §19.1 and §16.1.
    ///
    /// Distances, windows, and hazard values are gameplay-calibrated approximations
    /// sourced from NASA/JPL references (GDD §24). Unverified values are marked TODO-VERİ.
    /// </summary>
    public sealed class DestinationRecord : ICatalogRecord
    {
        // ── Identity ─────────────────────────────────────────────────────────────

        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>Localization key for display name.</summary>
        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        /// <summary>Localization key for description.</summary>
        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        /// <summary>Icon asset key.</summary>
        [JsonProperty("icon")]
        public string Icon { get; private set; }

        /// <summary>
        /// Destination type tag: "orbit", "body" (surface / atmospheric).
        /// GDD §19.1 "tür (yörünge/cisim)".
        /// </summary>
        [JsonProperty("bodyType")]
        public string BodyType { get; private set; }

        // ── Δv budget (m/s, approximate) ─────────────────────────────────────────

        /// <summary>
        /// Δv required for the ascent from Earth's surface to the departure orbit (m/s).
        /// Includes typical gravity and drag losses for an equatorial launch.
        /// GDD §16.1. TODO-VERİ: verify against JPL data.
        /// </summary>
        [JsonProperty("dvAscent")]
        public double DvAscent { get; private set; }

        /// <summary>
        /// Δv for the transfer (departure burn + mid-course corrections) (m/s).
        /// GDD §16.1 menzil profili.
        /// </summary>
        [JsonProperty("dvTransfer")]
        public double DvTransfer { get; private set; }

        /// <summary>
        /// Δv for orbit insertion / arrival burn at the destination (m/s).
        /// 0 for flyby missions.
        /// </summary>
        [JsonProperty("dvArrival")]
        public double DvArrival { get; private set; }

        /// <summary>
        /// Additional Δv required for powered landing (m/s). 0 if no landing.
        /// </summary>
        [JsonProperty("dvLanding")]
        public double DvLanding { get; private set; }

        /// <summary>
        /// Additional Δv required for ascent from surface and return trajectory (m/s).
        /// 0 for one-way missions.
        /// </summary>
        [JsonProperty("dvReturn")]
        public double DvReturn { get; private set; }

        /// <summary>
        /// Convenience: total Δv budget = dvAscent + dvTransfer + dvArrival + dvLanding + dvReturn.
        /// Not stored in JSON; computed at runtime.
        /// </summary>
        [JsonIgnore]
        public double DvTotal => DvAscent + DvTransfer + DvArrival + DvLanding + DvReturn;

        // ── Mission timeline ──────────────────────────────────────────────────────

        /// <summary>Minimum transit / cruise time in game-days.</summary>
        [JsonProperty("transitDaysMin")]
        public int TransitDaysMin { get; private set; }

        /// <summary>Maximum transit / cruise time in game-days.</summary>
        [JsonProperty("transitDaysMax")]
        public int TransitDaysMax { get; private set; }

        /// <summary>One-way signal delay in seconds (Earth-destination). GDD §16.1.</summary>
        [JsonProperty("commDelaySecondsMin")]
        public double CommDelaySecondsMin { get; private set; }

        /// <summary>Maximum one-way signal delay in seconds.</summary>
        [JsonProperty("commDelaySecondsMax")]
        public double CommDelaySecondsMax { get; private set; }

        // ── Launch windows ────────────────────────────────────────────────────────

        /// <summary>
        /// Approximate synodic period in game-days — how often a launch window repeats.
        /// 0 = continuous (e.g. LEO). GDD §9.4 / §16.1.
        /// </summary>
        [JsonProperty("windowPeriodDays")]
        public int WindowPeriodDays { get; private set; }

        /// <summary>
        /// Day-of-campaign offset for the first available window (game-day 0 = campaign start).
        /// </summary>
        [JsonProperty("windowFirstOffsetDays")]
        public int WindowFirstOffsetDays { get; private set; }

        /// <summary>Duration of each launch window in game-days.</summary>
        [JsonProperty("windowDurationDays")]
        public int WindowDurationDays { get; private set; }

        // ── Environment ───────────────────────────────────────────────────────────

        /// <summary>
        /// Solar flux multiplier relative to Earth (1.0 = 1 AU).
        /// Affects power generation. GDD §8.1 "güneş akısı(hedef uzaklığı)".
        /// </summary>
        [JsonProperty("solarFluxMultiplier")]
        public double SolarFluxMultiplier { get; private set; }

        /// <summary>
        /// Hazard severity tags and intensities.
        /// Keys: "radiation", "heat", "pressure", "dust", "atmosphere".
        /// Values: 0.0 (none) … 1.0 (maximum).
        /// GDD §19.1 "tehlike etiketleri ve şiddetleri".
        /// </summary>
        [JsonProperty("hazards")]
        public Dictionary<string, double> Hazards { get; private set; } = new Dictionary<string, double>();

        // ── Campaign lock ─────────────────────────────────────────────────────────

        /// <summary>
        /// Research node id(s) that must be completed before this destination is unlocked.
        /// Empty list = unlocked from campaign start (e.g. LEO, Moon).
        /// GDD §19.1 "kilit koşulu (araştırma/itibar)".
        /// </summary>
        [JsonProperty("unlockResearchIds")]
        public List<string> UnlockResearchIds { get; private set; } = new List<string>();

        /// <summary>Minimum reputation score needed to unlock this destination. 0 = no requirement.</summary>
        [JsonProperty("unlockReputationMin")]
        public double UnlockReputationMin { get; private set; }

        // ── Atmosphere (for EDL) ──────────────────────────────────────────────────

        /// <summary>
        /// Atmospheric surface pressure in kPa. 0 = vacuum / no atmosphere.
        /// Used by EDL calculations (GDD §8.1 EDL).
        /// </summary>
        [JsonProperty("atmospherePressureKpa")]
        public double AtmospherePressureKpa { get; private set; }

        /// <summary>
        /// Whether the destination has a usable atmosphere for aerobraking / parachutes.
        /// </summary>
        [JsonProperty("hasAtmosphere")]
        public bool HasAtmosphere { get; private set; }

        public override string ToString() =>
            $"[{Id}] dvTotal={DvTotal:0.#}m/s  transit={TransitDaysMin}-{TransitDaysMax}d  window={WindowPeriodDays}d";
    }
}
