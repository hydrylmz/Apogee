namespace Apogee.Strategy
{
    /// <summary>
    /// Categories of operations managed within the agency production and testing queue (GDD §9.3).
    /// </summary>
    public enum ProductionJobType
    {
        /// <summary>In-house manufacturing of individual rocket or spacecraft modules.</summary>
        ComponentFabrication = 0,

        /// <summary>Full integration and stacking of rocket stages, payload, and fairing.</summary>
        VehicleAssembly = 1,

        /// <summary>Ground static fire, qualification testing, and reliability certification in a Test Facility.</summary>
        SystemTesting = 2
    }
}
