namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Arguments provided at campaign creation time (agency name, starting region, seed).
    /// GDD §4.1 (campaign setup) and §4.2 (starting assets).
    /// M6-02.
    /// </summary>
    public sealed class CampaignSetupArgs
    {
        /// <summary>Player-chosen agency display name. Defaults to the canonical game name.</summary>
        public string AgencyName { get; set; } = "Birleşik Yörünge Ajansı";

        /// <summary>
        /// Catalog region ID for the starting launch pad (GDD §4.2).
        /// E.g. "cape_peninsula", "steppe_cosmodrome".
        /// </summary>
        public string StartingRegionId { get; set; } = "cape_peninsula";

        /// <summary>
        /// Deterministic seed for the whole campaign RNG stream.
        /// 0 means "auto-generate" (caller may substitute a time-derived value).
        /// </summary>
        public ulong Seed { get; set; } = 0;

        /// <summary>Starting treasury budget in M$. Default is 500 M$.</summary>
        public double StartingBudget { get; set; } = 500.0;

        /// <summary>Starting agency reputation (0-100 scale). Default is 30.0.</summary>
        public double StartingReputation { get; set; } = 30.0;

        /// <summary>Optional campaign identifier override (auto-generated if null).</summary>
        public string CampaignId { get; set; } = null;
    }
}
