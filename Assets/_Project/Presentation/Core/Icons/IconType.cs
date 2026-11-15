namespace Apogee.UI.Core.Icons
{
    /// <summary>
    /// Vector icon identifiers for the APOGEE UI design system (GDD §14).
    /// All icons are drawn programmatically via <see cref="IconLibrary"/> using Painter2D;
    /// they are single-color and tinted through the host element's <c>color</c> style.
    /// </summary>
    public enum IconType
    {
        None = 0,

        // --- Primary navigation icons (8) ---
        Mission,        // Orbit arc + target reticle
        Workshop,       // Wrench (purs/tereza ayagi)
        SolarSystem,    // Orbit rings + planet dot
        Research,       // Branching node tree
        Production,     // Gear
        Contracts,      // Sealed document
        Bases,          // Radar antenna + waves
        Milestones,     // Flag planted on a summit ("Apogee" nod)

        // --- Auxiliary icons ---
        Back,           // Left-pointing arrow
        Budget,         // Banknote
        Reputation,     // Five-pointed star (agency reputation)
        ResearchPoints, // Erlenmeyer flask (research points, RP)
        KnowledgePoints,// Open book (knowledge points, KP)
        Time,           // Clock
        Risk,           // Diamond with exclamation mark
        Warning,        // Triangle with exclamation mark
        Approve,        // Check mark
        Lock,           // Padlock
        Sign,           // Signature stroke
        Plus,           // Plus
        Cancel          // Cross
    }
}
