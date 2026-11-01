namespace Apogee.Strategy
{
    /// <summary>
    /// Risk severity levels for single-source supplier concentration (GDD §9.1).
    /// </summary>
    public enum SingleSourceRiskLevel
    {
        /// <summary>Components distributed across diverse suppliers; no concentration vulnerability.</summary>
        None = 0,

        /// <summary>Slight supplier concentration; minor risk exposure.</summary>
        Low = 1,

        /// <summary>Moderate supplier concentration (>= 50%); triggers initial volume discount but increases failure impact.</summary>
        Moderate = 2,

        /// <summary>Heavy concentration on one supplier (>= 60%); noticeable risk of cascade delays and quality issues.</summary>
        High = 3,

        /// <summary>Near-total or total dependency on a single supplier (>= 80%); maximum volume discount but severe operational vulnerability.</summary>
        Critical = 4
    }
}
