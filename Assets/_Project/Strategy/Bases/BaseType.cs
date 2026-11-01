namespace Apogee.Strategy
{
    /// <summary>
    /// Types of agency operational bases that can be established in regions across the World Map.
    /// Maps directly to GDD §6.1 "Üs Türleri" and §19.8.
    /// </summary>
    public enum BaseType
    {
        /// <summary>
        /// Launch pad facility. Level (1-5) determines vehicle stage compatibility and maximum liftoff mass.
        /// Location latitude grants equatorial Earth rotation ?v assistance.
        /// </summary>
        LaunchPad = 0,

        /// <summary>
        /// Integration and manufacturing facility. Level increases monthly production capacity throughput.
        /// Proximity to launch pad reduces component transit time and logistics costs.
        /// </summary>
        Factory = 1,

        /// <summary>
        /// Research and technology development laboratory. Generates research points per tick/month.
        /// </summary>
        ResearchCenter = 2,

        /// <summary>
        /// Deep space and orbital tracking antenna network. Broadens downlink windows and reduces data bottlenecks.
        /// </summary>
        GroundStation = 3,

        /// <summary>
        /// Systems testing and qualification stand. Accelerates component testing and increases vehicle baseline reliability.
        /// </summary>
        TestFacility = 4
    }
}
