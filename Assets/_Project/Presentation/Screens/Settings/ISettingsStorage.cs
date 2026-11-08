namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// Storage provider contract for persisting and loading player game settings (GDD §14.1 #19, M9-07).
    /// </summary>
    public interface ISettingsStorage
    {
        GameSettingsData LoadSettings();
        void SaveSettings(GameSettingsData data);
        void ResetSettings();
    }
}
