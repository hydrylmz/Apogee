using System;
using UnityEngine;

namespace Apogee.UI.Screens.MainMenu
{
    /// <summary>
    /// Storage provider contract for saving and loading campaign state snapshots (GDD §17.5, M6-04, M9-03).
    /// </summary>
    public interface ICampaignSaveStorage
    {
        bool HasSave();
        string LoadSave();
        void Save(string json);
        void DeleteSave();
    }

    /// <summary>
    /// In-memory implementation of save storage, ideal for unit testing and headless CI.
    /// </summary>
    public sealed class InMemoryCampaignSaveStorage : ICampaignSaveStorage
    {
        private string _savedJson;

        public InMemoryCampaignSaveStorage(string initialJson = null)
        {
            _savedJson = initialJson;
        }

        public bool HasSave() => !string.IsNullOrEmpty(_savedJson);
        public string LoadSave() => _savedJson;
        public void Save(string json) => _savedJson = json;
        public void DeleteSave() => _savedJson = null;
    }

    /// <summary>
    /// PlayerPrefs-backed persistent storage for desktop and WebGL platforms.
    /// In WebGL, Unity automatically persists PlayerPrefs to IndexedDB in the browser.
    /// </summary>
    public sealed class PlayerPrefsCampaignSaveStorage : ICampaignSaveStorage
    {
        public const string DefaultSaveKey = "apogee_campaign_quicksave_v1";
        private readonly string _saveKey;

        public PlayerPrefsCampaignSaveStorage(string saveKey = DefaultSaveKey)
        {
            _saveKey = saveKey ?? DefaultSaveKey;
        }

        public bool HasSave()
        {
            try
            {
                return PlayerPrefs.HasKey(_saveKey) && !string.IsNullOrEmpty(PlayerPrefs.GetString(_saveKey));
            }
            catch
            {
                return false;
            }
        }

        public string LoadSave()
        {
            try
            {
                return PlayerPrefs.GetString(_saveKey, null);
            }
            catch
            {
                return null;
            }
        }

        public void Save(string json)
        {
            try
            {
                PlayerPrefs.SetString(_saveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerPrefsCampaignSaveStorage] Could not persist campaign save: {ex.Message}");
            }
        }

        public void DeleteSave()
        {
            try
            {
                PlayerPrefs.DeleteKey(_saveKey);
                PlayerPrefs.Save();
            }
            catch
            {
                // Ignored
            }
        }
    }
}
