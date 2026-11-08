using System;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// Pure in-memory settings storage implementation for unit testing and headless CI.
    /// </summary>
    public sealed class InMemorySettingsStorage : ISettingsStorage
    {
        private GameSettingsData _data;

        public InMemorySettingsStorage(GameSettingsData initial = null)
        {
            _data = initial?.Clone() ?? new GameSettingsData();
        }

        public GameSettingsData LoadSettings()
        {
            return _data.Clone();
        }

        public void SaveSettings(GameSettingsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _data = data.Clone();
        }

        public void ResetSettings()
        {
            _data = new GameSettingsData();
        }
    }
}
