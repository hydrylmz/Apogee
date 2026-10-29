namespace Apogee.Design
{
    /// <summary>
    /// User-facing claim level categories based on mission difficulty (GDD §5.3 "İddia Seviyesi").
    /// Represents the ambition, expected prestige/rewards, and operational risk profile of the mission.
    /// </summary>
    public enum MissionClaimLevel
    {
        /// <summary>Baseline or low-complexity orbital missions (Score &lt; 5.0).</summary>
        Low = 0,

        /// <summary>Moderate exploration missions, e.g. lunar orbiters or simple landers (Score 5.0 - 12.9).</summary>
        Medium = 1,

        /// <summary>Ambitious interplanetary probes, rovers, or sample return (Score 13.0 - 24.9).</summary>
        High = 2,

        /// <summary>Extreme complexity, deep-space multi-objective or crewed expeditions (Score &gt;= 25.0).</summary>
        Extreme = 3
    }
}
