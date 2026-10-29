namespace Apogee.Design
{
    /// <summary>
    /// Severity classification for design review validation issues.
    /// Maps directly to Design Review traffic light system (GDD §7.4).
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Information or confirmation that a requirement has been met (Green / Tamam).
        /// </summary>
        Info = 0,

        /// <summary>
        /// Warning indicating narrow safety margin or elevated risk (Yellow / Uyarı).
        /// Does not prevent launch but warns the player.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Critical blocking defect or constraint violation (Red / Engel).
        /// Prevents vehicle approval and launch.
        /// </summary>
        Error = 2
    }
}
