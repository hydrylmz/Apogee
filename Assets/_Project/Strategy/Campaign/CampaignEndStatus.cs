namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Lifecycle end/termination status of a campaign.
    /// GDD §4.1, §4.5, §19.13, §21 M6-02.
    /// </summary>
    public enum CampaignEndStatus
    {
        /// <summary>Campaign is actively ongoing.</summary>
        Active = 0,

        /// <summary>Campaign ended via budget bankruptcy (Budget <= 0 and Reputation <= 0). GDD §4.5.</summary>
        Bankruptcy = 1,

        /// <summary>Campaign reached natural 30-year boundary (2061-01-01). GDD §4.1.</summary>
        TimeExpired = 2,

        /// <summary>Agency accomplished all 8 core milestones (Ultimate Victory). GDD §4.4.</summary>
        AllMilestonesCompleted = 3,

        /// <summary>Campaign retired voluntarily by the player/agency director.</summary>
        VoluntaryRetirement = 4
    }
}
