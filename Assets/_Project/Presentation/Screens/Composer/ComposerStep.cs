namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// Wizard steps for the Mission Composer (GDD §5.1, §14.1 item 5, §21 M3-04).
    /// Step sequence: Destination -> Mission Type -> Objectives -> Schedule & Budget.
    /// </summary>
    public enum ComposerStep
    {
        Destination = 0,
        MissionType = 1,
        Objectives = 2,
        ScheduleAndBudget = 3
    }
}
