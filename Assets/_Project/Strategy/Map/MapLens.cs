namespace Apogee.Strategy
{
    /// <summary>
    /// Lens modes for viewing strategic overlays on the World Map.
    /// Maps to GDD §6.1 "Harita lensleri: Lojistik, Haberleşme kapsama, Fırlatma verimi (enlem), Araştırma, Risk".
    /// </summary>
    public enum MapLens
    {
        /// <summary>Standard view showing geography, base locations, and general overview.</summary>
        Overview = 0,

        /// <summary>Displays supply chain routes, transport costs, and logistics accessibility ratings.</summary>
        Logistics = 1,

        /// <summary>Displays orbital coverage cone, ground station network visibility, and comm pass durations.</summary>
        CommsCoverage = 2,

        /// <summary>Displays latitude-based Earth rotation ?v assistance bonuses and orbital inclination feasibility.</summary>
        LaunchEfficiency = 3,

        /// <summary>Displays scientific research output capacity, academic centers, and tech development nodes.</summary>
        Research = 4,

        /// <summary>Displays geopolitical volatility, weather scrub probabilities, and operational risk factors.</summary>
        Risk = 5
    }
}
