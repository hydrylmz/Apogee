using System;
using Apogee.Core.Audio;
using Apogee.Core.Localization;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// Consolidated application settings model covering Audio, Language, Display, and Gameplay (GDD §14.1 #19, §13.2, §15.4, §17.6).
    /// Pure C# model with safe clamping and clone support.
    /// </summary>
    public sealed class GameSettingsData
    {
        public const float DefaultMasterVolume = 1.0f;
        public const float DefaultMusicVolume  = 0.75f;
        public const float DefaultSfxVolume    = 0.85f;
        public const float DefaultUiVolume     = 0.90f;
        public const bool  DefaultIsMuted      = false;

        public const Language DefaultLanguage  = Language.TR;
        public const bool DefaultIsFullscreen  = true;
        public const bool DefaultShowHints     = true;

        private float _masterVolume = DefaultMasterVolume;
        private float _musicVolume  = DefaultMusicVolume;
        private float _sfxVolume    = DefaultSfxVolume;
        private float _uiVolume     = DefaultUiVolume;

        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set => _musicVolume = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float UiVolume
        {
            get => _uiVolume;
            set => _uiVolume = Math.Clamp(value, 0.0f, 1.0f);
        }

        public bool IsMuted { get; set; } = DefaultIsMuted;

        public Language Language { get; set; } = DefaultLanguage;

        public bool IsFullscreen { get; set; } = DefaultIsFullscreen;

        public bool ShowHints { get; set; } = DefaultShowHints;

        public GameSettingsData() { }

        public GameSettingsData(
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            float uiVolume,
            bool isMuted,
            Language language,
            bool isFullscreen,
            bool showHints)
        {
            MasterVolume = masterVolume;
            MusicVolume  = musicVolume;
            SfxVolume    = sfxVolume;
            UiVolume     = uiVolume;
            IsMuted      = isMuted;
            Language     = language;
            IsFullscreen = isFullscreen;
            ShowHints    = showHints;
        }

        public AudioSettingsData ToAudioSettings()
        {
            return new AudioSettingsData(MasterVolume, MusicVolume, SfxVolume, UiVolume, IsMuted);
        }

        public void CopyFromAudioSettings(AudioSettingsData audio)
        {
            if (audio == null) return;
            MasterVolume = audio.MasterVolume;
            MusicVolume  = audio.MusicVolume;
            SfxVolume    = audio.SfxVolume;
            UiVolume     = audio.UiVolume;
            IsMuted      = audio.IsMuted;
        }

        public GameSettingsData Clone()
        {
            return new GameSettingsData(
                MasterVolume,
                MusicVolume,
                SfxVolume,
                UiVolume,
                IsMuted,
                Language,
                IsFullscreen,
                ShowHints);
        }
    }
}
