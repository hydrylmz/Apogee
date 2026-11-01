using System.Collections.Generic;
using Newtonsoft.Json;

namespace Apogee.Data
{
    /// <summary>
    /// JSON-serializable catalog record for a single research tree node.
    /// Maps to GDD §9.2 (Research Tree branches and nodes) and §19.7 (Research Node schema).
    ///
    /// JSON key convention: camelCase (STANDARDS.md §1).
    /// Numeric fields use game-internal units as noted; "TODO-VERİ" placeholders are 0 where unconfirmed.
    /// </summary>
    public sealed class ResearchNodeRecord : ICatalogRecord
    {
        // ── Identity ──────────────────────────────────────────────────────────────

        /// <inheritdoc />
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; private set; }

        /// <summary>Localization key for the display name (STANDARDS.md §4).</summary>
        [JsonProperty("nameKey")]
        public string NameKey { get; private set; }

        /// <summary>Localization key for the description tooltip.</summary>
        [JsonProperty("descKey")]
        public string DescKey { get; private set; }

        /// <summary>Icon asset address (Addressables key or Resources path).</summary>
        [JsonProperty("icon")]
        public string Icon { get; private set; }

        // ── Tree structure ────────────────────────────────────────────────────────

        /// <summary>
        /// Branch name this node belongs to.
        /// GDD §9.2 branches: "Propulsion", "Structure", "EDL", "LifeSupport",
        /// "Power", "Comms", "Reliability".
        /// </summary>
        [JsonProperty("branch", Required = Required.Always)]
        public string Branch { get; private set; }

        /// <summary>
        /// IDs of research nodes that must be completed before this one can be started.
        /// Empty = available immediately (start-of-campaign nodes).
        /// </summary>
        [JsonProperty("prerequisites")]
        public List<string> Prerequisites { get; private set; } = new List<string>();

        // ── Cost ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Research point cost to complete this node.
        /// Provided by ResearchCenter monthly output (GDD §6.1, §9.2).
        /// </summary>
        [JsonProperty("researchPointCost")]
        public double ResearchPointCost { get; private set; }

        /// <summary>
        /// Calendar time (game-days) to complete this node once started.
        /// Actual duration is affected by ResearchCenter level; this is the base at level 1.
        /// </summary>
        [JsonProperty("baseDurationDays")]
        public int BaseDurationDays { get; private set; }

        /// <summary>
        /// Optional one-time budget cost in M$ charged when the research is started (e.g. equipment).
        /// 0 = free to start; only research points are required.
        /// </summary>
        [JsonProperty("startCostMUSD")]
        public double StartCostMUSD { get; private set; }

        // ── Effects ───────────────────────────────────────────────────────────────

        /// <summary>
        /// IDs of modules/components unlocked when this node is completed.
        /// Consumers (e.g. ResearchManager) cross-reference with ModuleRecord.researchRequiredId.
        /// </summary>
        [JsonProperty("unlocksModuleIds")]
        public List<string> UnlocksModuleIds { get; private set; } = new List<string>();

        /// <summary>
        /// Free-form stat modifier effects applied globally once this research is completed.
        /// Format: "statType:modifierType:value" — identical to ModuleRecord.statModifiers.
        /// e.g. "Reliability:PercentAdd:0.02"  →  +2% reliability on all components.
        /// </summary>
        [JsonProperty("globalStatModifiers")]
        public List<string> GlobalStatModifiers { get; private set; } = new List<string>();

        /// <summary>
        /// Human-readable summary of effects for UI display and debugging.
        /// Not used in calculations; purely descriptive.
        /// </summary>
        [JsonProperty("effectSummary")]
        public string EffectSummary { get; private set; }

        // ── Tier ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Technology tier [1-5]. Higher tiers take more time/points and unlock more capable tech.
        /// Tier 1 = campaign start; Tier 5 = late-game / crewed Mars prerequisite.
        /// </summary>
        [JsonProperty("tier")]
        public int Tier { get; private set; } = 1;

        public override string ToString() =>
            $"[{Branch} T{Tier}] {Id}  cost={ResearchPointCost}rp  dur={BaseDurationDays}d";
    }
}
