namespace Apogee.Design
{
    /// <summary>
    /// Traffic-light status for a single telemetry bar in the Results Panel (GDD §8.3, §7.4).
    /// Controls color coding shown to the player:
    /// Green = comfortable margin, Amber = borderline / warning, Red = insufficient / blocking.
    /// </summary>
    public enum ResultBarStatus
    {
        /// <summary>Metric is well within tolerance. Shown in green.</summary>
        Good,

        /// <summary>Metric is near its limit. Shown in amber.</summary>
        Warning,

        /// <summary>Metric violates a hard constraint. Shown in red.</summary>
        Error,

        /// <summary>No data available (e.g. stat not computed yet). Shown as neutral muted.</summary>
        Neutral
    }
}
