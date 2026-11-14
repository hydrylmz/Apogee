using System;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.UI.Core.Flight;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// Consolidated application settings model covering Audio, Language, Display, Gameplay,
    /// and Flight Theatre preferences (GDD §14.1 #19, §13.2, §15.4, §17.6, §3.5).
    /// Pure C# model with safe clamping and clone support.
    /// New fields (CaptionMode, ReduceMotion, FontScale) default gracefully so old save files load without corruption.
    /// </summary>
    public sealed class GameSettingsData
    {
        public const float DefaultMasterVolume = 1.0f;
        public const float DefaultMusicVolume  = 0.75f;
        public const float DefaultSfxVolume    = 0.85f;
        public const float DefaultUiVolume     = 0.90f;
        public const bool  DefaultIsMuted      = false;

        public const Language           DefaultLanguage    = Language.TR;
        public const bool               DefaultIsFullscreen= true;
        public const bool               DefaultShowHints   = true;

        // ── Flight Theatre (§3.5) ─────────────────────────────────────────────
        public const FlightCaptionMode  DefaultCaptionMode = FlightCaptionMode.Subtitle;
        public const bool               DefaultReduceMotion= false;
        public const float              DefaultFontScale   = 1.0f;
        public const bool               DefaultUse3DStage  = true;

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

        // ── Flight Theatre preferences (§3.5) — additive; old saves default gracefully ──
        public FlightCaptionMode CaptionMode  { get; set; } = DefaultCaptionMode;
        public bool              ReduceMotion { get; set; } = DefaultReduceMotion;
        public bool              Use3DStage   { get; set; } = DefaultUse3DStage;
        private float            _fontScale   = DefaultFontScale;
        public float FontScale
        {
            get => _fontScale;
            set => _fontScale = Math.Clamp(value, 0.5f, 2.0f);
        }

        public GameSettingsData() { }

        public GameSettingsData(
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            float uiVolume,
            bool isMuted,
            Language language,
            bool isFullscreen,
            bool showHints,
            FlightCaptionMode captionMode  = DefaultCaptionMode,
            bool              reduceMotion = DefaultReduceMotion,
            float             fontScale    = DefaultFontScale,
            bool              use3DStage   = DefaultUse3DStage)
        {
            MasterVolume = masterVolume;
            MusicVolume  = musicVolume;
            SfxVolume    = sfxVolume;
            UiVolume     = uiVolume;
            IsMuted      = isMuted;
            Language     = language;
            IsFullscreen = isFullscreen;
            ShowHints    = showHints;
            CaptionMode  = captionMode;
            ReduceMotion = reduceMotion;
            FontScale    = fontScale;
            Use3DStage   = use3DStage;
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
                ShowHints,
                CaptionMode,
                ReduceMotion,
                FontScale,
                Use3DStage);
        }
    }
}
