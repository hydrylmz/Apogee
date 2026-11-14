using System;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.UI.Core.Flight;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using UnityEngine;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// ViewModel for the Settings Screen (ScreenId.Settings = 18, GDD §14.1 #19, §13.2, §15.4, §17.6).
    /// Manages audio volume levels, active language (TR/EN), fullscreen toggle, and tutorial hints.
    /// </summary>
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private readonly IAudioService _audioService;
        private readonly ILocalizationManager _localizationManager;
        private readonly ISettingsStorage _storage;
        private readonly Action<bool> _onFullscreenToggle;

        // ── Observables ──────────────────────────────────────────────────────────
        public ObservableValue<float> MasterVolume { get; } = new(GameSettingsData.DefaultMasterVolume);
        public ObservableValue<float> MusicVolume  { get; } = new(GameSettingsData.DefaultMusicVolume);
        public ObservableValue<float> SfxVolume    { get; } = new(GameSettingsData.DefaultSfxVolume);
        public ObservableValue<float> UiVolume     { get; } = new(GameSettingsData.DefaultUiVolume);
        public ObservableValue<bool>  IsMuted      { get; } = new(GameSettingsData.DefaultIsMuted);

        public ObservableValue<Language> CurrentLanguage { get; } = new(GameSettingsData.DefaultLanguage);
        public ObservableValue<bool>     IsFullscreen    { get; } = new(GameSettingsData.DefaultIsFullscreen);
        public ObservableValue<bool>     ShowHints       { get; } = new(GameSettingsData.DefaultShowHints);

        public ObservableValue<string> StatusMessage { get; } = new(string.Empty);
        public ObservableValue<bool>   IsDirty       { get; } = new(false);

        // ── Flight Theatre Observables (GDD §3.5) ────────────────────────────────
        public ObservableValue<FlightCaptionMode> CaptionMode  { get; } = new(GameSettingsData.DefaultCaptionMode);
        public ObservableValue<bool>              ReduceMotion { get; } = new(GameSettingsData.DefaultReduceMotion);
        public ObservableValue<float>             FontScale    { get; } = new(GameSettingsData.DefaultFontScale);
        public ObservableValue<bool>              Use3DStage   { get; } = new(GameSettingsData.DefaultUse3DStage);

        // ── Commands ─────────────────────────────────────────────────────────────
        public ViewActionCommand ApplySettingsCommand  { get; }
        public ViewActionCommand ResetDefaultsCommand  { get; }
        public ViewActionCommand CloseCommand          { get; }

        public IAudioService AudioService => _audioService;
        public ILocalizationManager LocalizationManager => _localizationManager;
        public ISettingsStorage Storage => _storage;

        public SettingsViewModel(
            IScreenNavigator navigator,
            IAudioService audioService = null,
            ILocalizationManager localizationManager = null,
            ISettingsStorage storage = null,
            Action<bool> onFullscreenToggle = null)
        {
            _navigator           = navigator;
            _audioService        = audioService ?? new NullAudioService();
            _localizationManager = localizationManager ?? Apogee.Core.Localization.LocalizationManager.Instance;
            _storage             = storage ?? new PlayerPrefsSettingsStorage();

            _onFullscreenToggle = onFullscreenToggle ?? (isFullscreen =>
            {
                try
                {
                    Screen.fullScreen = isFullscreen;
                }
                catch
                {
                    // Safe fallback for tests / headless environments
                }
            });

            ApplySettingsCommand = new ViewActionCommand(ApplySettings);
            ResetDefaultsCommand = new ViewActionCommand(ResetToDefaults);
            CloseCommand         = new ViewActionCommand(ExecuteClose);

            LoadInitialSettings();
        }

        public void LoadInitialSettings()
        {
            var data = _storage.LoadSettings() ?? new GameSettingsData();

            MasterVolume.Value    = data.MasterVolume;
            MusicVolume.Value     = data.MusicVolume;
            SfxVolume.Value       = data.SfxVolume;
            UiVolume.Value        = data.UiVolume;
            IsMuted.Value         = data.IsMuted;
            CurrentLanguage.Value = data.Language;
            IsFullscreen.Value    = data.IsFullscreen;
            ShowHints.Value       = data.ShowHints;

            IsDirty.Value         = false;
            StatusMessage.Value   = string.Empty;

            // Flight Theatre
            CaptionMode.Value  = data.CaptionMode;
            ReduceMotion.Value = data.ReduceMotion;
            FontScale.Value    = data.FontScale;
            Use3DStage.Value   = data.Use3DStage;

            // Sync with services
            SyncAudioToService();
            _localizationManager.SetLanguage(data.Language);
            _onFullscreenToggle?.Invoke(data.IsFullscreen);
        }

        public void SetMasterVolume(float volume)
        {
            volume = Math.Clamp(volume, 0.0f, 1.0f);
            if (Math.Abs(MasterVolume.Value - volume) > 0.001f)
            {
                MasterVolume.Value = volume;
                IsDirty.Value = true;
                SyncAudioToService();
            }
        }

        public void SetMusicVolume(float volume)
        {
            volume = Math.Clamp(volume, 0.0f, 1.0f);
            if (Math.Abs(MusicVolume.Value - volume) > 0.001f)
            {
                MusicVolume.Value = volume;
                IsDirty.Value = true;
                SyncAudioToService();
            }
        }

        public void SetSfxVolume(float volume)
        {
            volume = Math.Clamp(volume, 0.0f, 1.0f);
            if (Math.Abs(SfxVolume.Value - volume) > 0.001f)
            {
                SfxVolume.Value = volume;
                IsDirty.Value = true;
                SyncAudioToService();
            }
        }

        public void SetUiVolume(float volume)
        {
            volume = Math.Clamp(volume, 0.0f, 1.0f);
            if (Math.Abs(UiVolume.Value - volume) > 0.001f)
            {
                UiVolume.Value = volume;
                IsDirty.Value = true;
                SyncAudioToService();
            }
        }

        public void SetMuted(bool muted)
        {
            if (IsMuted.Value != muted)
            {
                IsMuted.Value = muted;
                IsDirty.Value = true;
                SyncAudioToService();
            }
        }

        public void ToggleMute() => SetMuted(!IsMuted.Value);

        public void SetLanguage(Language language)
        {
            if (CurrentLanguage.Value != language)
            {
                CurrentLanguage.Value = language;
                IsDirty.Value = true;
                _localizationManager.SetLanguage(language);
            }
        }

        public void SetFullscreen(bool fullscreen)
        {
            if (IsFullscreen.Value != fullscreen)
            {
                IsFullscreen.Value = fullscreen;
                IsDirty.Value = true;
                _onFullscreenToggle?.Invoke(fullscreen);
            }
        }

        public void ToggleFullscreen() => SetFullscreen(!IsFullscreen.Value);

        public void SetShowHints(bool show)
        {
            if (ShowHints.Value != show)
            {
                ShowHints.Value = show;
                IsDirty.Value = true;
            }
        }

        public void ToggleShowHints() => SetShowHints(!ShowHints.Value);

        public void ApplySettings()
        {
            var data = new GameSettingsData(
                MasterVolume.Value,
                MusicVolume.Value,
                SfxVolume.Value,
                UiVolume.Value,
                IsMuted.Value,
                CurrentLanguage.Value,
                IsFullscreen.Value,
                ShowHints.Value,
                CaptionMode.Value,
                ReduceMotion.Value,
                FontScale.Value,
                Use3DStage.Value);

            _storage.SaveSettings(data);
            SyncAudioToService();
            _localizationManager.SetLanguage(data.Language);
            _onFullscreenToggle?.Invoke(data.IsFullscreen);

            IsDirty.Value = false;
            var savedText = _localizationManager.Get("settings.status.saved");
            if (string.IsNullOrEmpty(savedText) || savedText.StartsWith("?"))
            {
                savedText = (data.Language == Language.EN) ? "Settings saved successfully." : "Ayarlar başarıyla kaydedildi.";
            }
            StatusMessage.Value = savedText;
        }

        public void ResetToDefaults()
        {
            _storage.ResetSettings();

            MasterVolume.Value    = GameSettingsData.DefaultMasterVolume;
            MusicVolume.Value     = GameSettingsData.DefaultMusicVolume;
            SfxVolume.Value       = GameSettingsData.DefaultSfxVolume;
            UiVolume.Value        = GameSettingsData.DefaultUiVolume;
            IsMuted.Value         = GameSettingsData.DefaultIsMuted;
            CurrentLanguage.Value = GameSettingsData.DefaultLanguage;
            IsFullscreen.Value    = GameSettingsData.DefaultIsFullscreen;
            ShowHints.Value       = GameSettingsData.DefaultShowHints;
            CaptionMode.Value     = GameSettingsData.DefaultCaptionMode;
            ReduceMotion.Value    = GameSettingsData.DefaultReduceMotion;
            FontScale.Value       = GameSettingsData.DefaultFontScale;
            Use3DStage.Value      = GameSettingsData.DefaultUse3DStage;

            ApplySettings();
        }

        // ── Flight Theatre Setters ────────────────────────────────────────────────
        public void SetCaptionMode(FlightCaptionMode mode)
        {
            if (CaptionMode.Value != mode)
            {
                CaptionMode.Value = mode;
                IsDirty.Value = true;
            }
        }

        public void SetReduceMotion(bool reduce)
        {
            if (ReduceMotion.Value != reduce)
            {
                ReduceMotion.Value = reduce;
                IsDirty.Value = true;
            }
        }

        public void SetFontScale(float scale)
        {
            scale = Math.Clamp(scale, 0.5f, 2.0f);
            if (Math.Abs(FontScale.Value - scale) > 0.001f)
            {
                FontScale.Value = scale;
                IsDirty.Value = true;
            }
        }

        public void SetUse3DStage(bool use3D)
        {
            if (Use3DStage.Value != use3D)
            {
                Use3DStage.Value = use3D;
                IsDirty.Value = true;
            }
        }

        private void ExecuteClose()
        {
            if (IsDirty.Value)
            {
                ApplySettings();
            }

            if (_navigator != null && _navigator.CanGoBack)
            {
                _navigator.GoBack();
            }
            else
            {
                _navigator?.GoTo(ScreenId.MainMenu);
            }
        }

        private void SyncAudioToService()
        {
            var audioData = new AudioSettingsData(
                MasterVolume.Value,
                MusicVolume.Value,
                SfxVolume.Value,
                UiVolume.Value,
                IsMuted.Value);

            _audioService.SetSettings(audioData);
            _audioService.SetMute(IsMuted.Value);
        }
    }
}
