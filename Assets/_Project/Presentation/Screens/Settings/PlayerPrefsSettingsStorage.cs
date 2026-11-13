using System;
using Apogee.Core.Localization;
using Apogee.UI.Core.Flight;
using UnityEngine;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// PlayerPrefs-backed persistent storage for game settings on Desktop and WebGL (GDD §14.1 #19, M9-07).
    /// New keys (CaptionMode, ReduceMotion, FontScale) are read with HasKey guards so old save files
    /// load without corruption — missing keys fall back to GameSettingsData defaults. (§3.5)
    /// </summary>
    public sealed class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        public const string KeyMaster      = "apogee_settings_master";
        public const string KeyMusic       = "apogee_settings_music";
        public const string KeySfx         = "apogee_settings_sfx";
        public const string KeyUi          = "apogee_settings_ui";
        public const string KeyMute        = "apogee_settings_mute";
        public const string KeyLanguage    = "apogee_settings_language";
        public const string KeyFullscreen  = "apogee_settings_fullscreen";
        public const string KeyShowHints   = "apogee_settings_hints";
        // Flight Theatre — new keys (§3.5)
        public const string KeyCaptionMode = "apogee_settings_captionmode";
        public const string KeyReduceMotion= "apogee_settings_reducemotion";
        public const string KeyFontScale   = "apogee_settings_fontscale";

        public GameSettingsData LoadSettings()
        {
            var data = new GameSettingsData();
            try
            {
                if (PlayerPrefs.HasKey(KeyMaster))
                    data.MasterVolume = PlayerPrefs.GetFloat(KeyMaster, GameSettingsData.DefaultMasterVolume);

                if (PlayerPrefs.HasKey(KeyMusic))
                    data.MusicVolume = PlayerPrefs.GetFloat(KeyMusic, GameSettingsData.DefaultMusicVolume);

                if (PlayerPrefs.HasKey(KeySfx))
                    data.SfxVolume = PlayerPrefs.GetFloat(KeySfx, GameSettingsData.DefaultSfxVolume);

                if (PlayerPrefs.HasKey(KeyUi))
                    data.UiVolume = PlayerPrefs.GetFloat(KeyUi, GameSettingsData.DefaultUiVolume);

                if (PlayerPrefs.HasKey(KeyMute))
                    data.IsMuted = PlayerPrefs.GetInt(KeyMute, 0) == 1;

                if (PlayerPrefs.HasKey(KeyLanguage))
                {
                    int langInt = PlayerPrefs.GetInt(KeyLanguage, (int)GameSettingsData.DefaultLanguage);
                    data.Language = Enum.IsDefined(typeof(Language), langInt) ? (Language)langInt : GameSettingsData.DefaultLanguage;
                }

                if (PlayerPrefs.HasKey(KeyFullscreen))
                    data.IsFullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;

                if (PlayerPrefs.HasKey(KeyShowHints))
                    data.ShowHints = PlayerPrefs.GetInt(KeyShowHints, 1) == 1;

                // ── Flight Theatre (§3.5) — read only when key exists; default otherwise ──
                if (PlayerPrefs.HasKey(KeyCaptionMode))
                {
                    int cm = PlayerPrefs.GetInt(KeyCaptionMode, (int)GameSettingsData.DefaultCaptionMode);
                    data.CaptionMode = Enum.IsDefined(typeof(FlightCaptionMode), cm)
                        ? (FlightCaptionMode)cm
                        : GameSettingsData.DefaultCaptionMode;
                }

                if (PlayerPrefs.HasKey(KeyReduceMotion))
                    data.ReduceMotion = PlayerPrefs.GetInt(KeyReduceMotion, 0) == 1;

                if (PlayerPrefs.HasKey(KeyFontScale))
                    data.FontScale = PlayerPrefs.GetFloat(KeyFontScale, GameSettingsData.DefaultFontScale);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerPrefsSettingsStorage] Error loading settings: {ex.Message}");
            }

            return data;
        }

        public void SaveSettings(GameSettingsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            try
            {
                PlayerPrefs.SetFloat(KeyMaster,       data.MasterVolume);
                PlayerPrefs.SetFloat(KeyMusic,        data.MusicVolume);
                PlayerPrefs.SetFloat(KeySfx,          data.SfxVolume);
                PlayerPrefs.SetFloat(KeyUi,           data.UiVolume);
                PlayerPrefs.SetInt  (KeyMute,         data.IsMuted ? 1 : 0);
                PlayerPrefs.SetInt  (KeyLanguage,     (int)data.Language);
                PlayerPrefs.SetInt  (KeyFullscreen,   data.IsFullscreen ? 1 : 0);
                PlayerPrefs.SetInt  (KeyShowHints,    data.ShowHints ? 1 : 0);
                // Flight Theatre
                PlayerPrefs.SetInt  (KeyCaptionMode,  (int)data.CaptionMode);
                PlayerPrefs.SetInt  (KeyReduceMotion, data.ReduceMotion ? 1 : 0);
                PlayerPrefs.SetFloat(KeyFontScale,    data.FontScale);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerPrefsSettingsStorage] Error saving settings: {ex.Message}");
            }
        }

        public void ResetSettings()
        {
            try
            {
                PlayerPrefs.DeleteKey(KeyMaster);
                PlayerPrefs.DeleteKey(KeyMusic);
                PlayerPrefs.DeleteKey(KeySfx);
                PlayerPrefs.DeleteKey(KeyUi);
                PlayerPrefs.DeleteKey(KeyMute);
                PlayerPrefs.DeleteKey(KeyLanguage);
                PlayerPrefs.DeleteKey(KeyFullscreen);
                PlayerPrefs.DeleteKey(KeyShowHints);
                PlayerPrefs.DeleteKey(KeyCaptionMode);
                PlayerPrefs.DeleteKey(KeyReduceMotion);
                PlayerPrefs.DeleteKey(KeyFontScale);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerPrefsSettingsStorage] Error resetting settings: {ex.Message}");
            }
        }
    }
}
