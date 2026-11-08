using System;
using Apogee.Core.Localization;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Settings
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Settings Screen (ScreenId.Settings = 18, GDD §14.1 #19, M9-07).
    /// Provides reactive controls for Master/Music/SFX/UI volume, TR/EN localization, Fullscreen toggle, and Hints.
    /// </summary>
    public sealed class SettingsScreenController : ScreenControllerBase<SettingsViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Settings;

        // Visual controls
        private Label  _titleLabel;
        private Label  _subtitleLabel;
        private Button _backButton;

        // Audio controls
        private Label  _audioHeaderLabel;
        private Slider _masterSlider;
        private Label  _masterValLabel;
        private Slider _musicSlider;
        private Label  _musicValLabel;
        private Slider _sfxSlider;
        private Label  _sfxValLabel;
        private Slider _uiSlider;
        private Label  _uiValLabel;
        private Toggle _muteToggle;

        // Language controls
        private Label  _langHeaderLabel;
        private Button _langTrButton;
        private Button _langEnButton;

        // Display controls
        private Label  _displayHeaderLabel;
        private Toggle _fullscreenToggle;

        // Gameplay controls
        private Label  _gameplayHeaderLabel;
        private Toggle _hintsToggle;
        private Label  _hintsDescLabel;

        // Bottom actions
        private Button _applyButton;
        private Button _resetButton;
        private Label  _statusLabel;

        private IDisposable _masterSub;
        private IDisposable _musicSub;
        private IDisposable _sfxSub;
        private IDisposable _uiSub;
        private IDisposable _muteSub;
        private IDisposable _langSub;
        private IDisposable _fullscreenSub;
        private IDisposable _hintsSub;
        private IDisposable _statusSub;

        public SettingsScreenController(SettingsViewModel viewModel) : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-settings");
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);
            root.style.height = Length.Percent(100);
            root.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1.0f);

            // ── Top Header ──────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection    = FlexDirection.Row;
            header.style.justifyContent   = Justify.SpaceBetween;
            header.style.alignItems       = Align.Center;
            header.style.paddingTop       = 16;
            header.style.paddingBottom    = 16;
            header.style.paddingLeft      = 24;
            header.style.paddingRight     = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.24f, 0.35f, 0.8f);

            var titleBlock = new VisualElement();
            _titleLabel = new Label();
            _titleLabel.style.fontSize                = 22;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color                   = new Color(0.9f, 0.95f, 1.0f);

            _subtitleLabel = new Label();
            _subtitleLabel.style.fontSize  = 12;
            _subtitleLabel.style.color     = new Color(0.45f, 0.7f, 0.95f);
            _subtitleLabel.style.marginTop = 2;

            titleBlock.Add(_titleLabel);
            titleBlock.Add(_subtitleLabel);

            _backButton = new Button(() => ViewModel?.CloseCommand?.Execute());
            _backButton.AddToClassList("apogee-btn");
            _backButton.AddToClassList("apogee-btn--secondary");

            header.Add(titleBlock);
            header.Add(_backButton);
            root.Add(header);

            // ── Scrollable Body ─────────────────────────────────────────────────────
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingTop = 16;
            scrollView.style.paddingBottom = 24;
            scrollView.style.paddingLeft = 24;
            scrollView.style.paddingRight = 24;

            var content = new VisualElement();
            content.style.maxWidth = 900;
            content.style.width = Length.Percent(100);
            content.style.alignSelf = Align.Center;

            // ── Audio Card ──
            var audioCard = CreateCard();
            _audioHeaderLabel = CreateSectionTitle("🔊 SES DÜZEYLERİ");
            audioCard.Add(_audioHeaderLabel);

            _masterSlider = CreateVolumeSlider("Ana Ses Düzeyi", out _masterValLabel, (v) => ViewModel?.SetMasterVolume(v));
            _musicSlider  = CreateVolumeSlider("Müzik Düzeyi",   out _musicValLabel,  (v) => ViewModel?.SetMusicVolume(v));
            _sfxSlider    = CreateVolumeSlider("Ses Efektleri",  out _sfxValLabel,    (v) => ViewModel?.SetSfxVolume(v));
            _uiSlider     = CreateVolumeSlider("Arayüz Sesleri", out _uiValLabel,     (v) => ViewModel?.SetUiVolume(v));

            audioCard.Add(_masterSlider);
            audioCard.Add(_musicSlider);
            audioCard.Add(_sfxSlider);
            audioCard.Add(_uiSlider);

            _muteToggle = new Toggle("Tüm Sesleri Kapat (Sessiz)");
            _muteToggle.style.marginTop = 12;
            _muteToggle.style.color = new Color(0.85f, 0.9f, 0.98f);
            _muteToggle.RegisterValueChangedCallback(evt => ViewModel?.SetMuted(evt.newValue));
            audioCard.Add(_muteToggle);

            content.Add(audioCard);

            // ── Language Card ──
            var langCard = CreateCard();
            _langHeaderLabel = CreateSectionTitle("🌐 DİL / LANGUAGE");
            langCard.Add(_langHeaderLabel);

            var langRow = new VisualElement();
            langRow.style.flexDirection = FlexDirection.Row;
            langRow.style.marginTop = 8;

            _langTrButton = new Button(() => ViewModel?.SetLanguage(Language.TR)) { text = "Türkçe (TR)" };
            _langTrButton.AddToClassList("apogee-btn");
            _langTrButton.style.marginRight = 12;
            _langTrButton.style.minWidth = 140;

            _langEnButton = new Button(() => ViewModel?.SetLanguage(Language.EN)) { text = "English (EN)" };
            _langEnButton.AddToClassList("apogee-btn");
            _langEnButton.style.minWidth = 140;

            langRow.Add(_langTrButton);
            langRow.Add(_langEnButton);
            langCard.Add(langRow);

            content.Add(langCard);

            // ── Display Card ──
            var displayCard = CreateCard();
            _displayHeaderLabel = CreateSectionTitle("🖥️ GÖRÜNTÜ VE EKRAN");
            displayCard.Add(_displayHeaderLabel);

            _fullscreenToggle = new Toggle("Tam Ekran Modu");
            _fullscreenToggle.style.marginTop = 8;
            _fullscreenToggle.style.color = new Color(0.85f, 0.9f, 0.98f);
            _fullscreenToggle.RegisterValueChangedCallback(evt => ViewModel?.SetFullscreen(evt.newValue));
            displayCard.Add(_fullscreenToggle);

            content.Add(displayCard);

            // ── Gameplay & Assistance Card ──
            var gameplayCard = CreateCard();
            _gameplayHeaderLabel = CreateSectionTitle("🎓 OYNANIŞ VE YARDIM");
            gameplayCard.Add(_gameplayHeaderLabel);

            _hintsToggle = new Toggle("Eğitim ve Yardım İpuçları");
            _hintsToggle.style.marginTop = 8;
            _hintsToggle.style.color = new Color(0.85f, 0.9f, 0.98f);
            _hintsToggle.RegisterValueChangedCallback(evt => ViewModel?.SetShowHints(evt.newValue));
            gameplayCard.Add(_hintsToggle);

            _hintsDescLabel = new Label("Tasarım atölyesi ve rota hesaplamalarında yol gösterici ipuçlarını görüntüler.");
            _hintsDescLabel.style.fontSize = 11;
            _hintsDescLabel.style.color = new Color(0.5f, 0.65f, 0.8f);
            _hintsDescLabel.style.marginLeft = 24;
            _hintsDescLabel.style.marginTop = 2;
            _hintsDescLabel.style.whiteSpace = WhiteSpace.Normal;
            gameplayCard.Add(_hintsDescLabel);

            content.Add(gameplayCard);

            // ── Bottom Action Controls ──
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.justifyContent = Justify.SpaceBetween;
            actionRow.style.alignItems = Align.Center;
            actionRow.style.marginTop = 20;

            var leftBtns = new VisualElement();
            leftBtns.style.flexDirection = FlexDirection.Row;

            _resetButton = new Button(() => ViewModel?.ResetDefaultsCommand?.Execute());
            _resetButton.AddToClassList("apogee-btn");
            _resetButton.AddToClassList("apogee-btn--secondary");
            _resetButton.style.marginRight = 12;

            _applyButton = new Button(() => ViewModel?.ApplySettingsCommand?.Execute());
            _applyButton.AddToClassList("apogee-btn");
            _applyButton.AddToClassList("apogee-btn--primary");

            leftBtns.Add(_resetButton);
            leftBtns.Add(_applyButton);
            actionRow.Add(leftBtns);

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 12;
            _statusLabel.style.color = new Color(0.2f, 0.9f, 0.5f);
            actionRow.Add(_statusLabel);

            content.Add(actionRow);

            scrollView.Add(content);
            root.Add(scrollView);

            BindViewModelEvents();
            UpdateAllLocalizedTexts();

            return root;
        }

        private VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.AddToClassList("apogee-card");
            card.style.backgroundColor = new Color(0.08f, 0.12f, 0.20f, 0.9f);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;

            var borderCol = new Color(0.2f, 0.28f, 0.42f, 0.8f);
            card.style.borderTopColor = borderCol;
            card.style.borderBottomColor = borderCol;
            card.style.borderLeftColor = borderCol;
            card.style.borderRightColor = borderCol;

            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.marginBottom = 16;
            return card;
        }

        private Label CreateSectionTitle(string text)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 14;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new Color(0.4f, 0.8f, 1.0f);
            lbl.style.marginBottom = 8;
            return lbl;
        }

        private Slider CreateVolumeSlider(string labelText, out Label valueLabel, Action<float> onValueChanged)
        {
            var slider = new Slider(labelText, 0.0f, 1.0f);
            slider.style.marginTop = 6;
            slider.style.marginBottom = 6;
            slider.style.color = new Color(0.85f, 0.9f, 0.98f);

            var valLbl = new Label("100%");
            valLbl.style.minWidth = 45;
            valLbl.style.unityTextAlign = TextAnchor.MiddleRight;
            valLbl.style.color = new Color(0.4f, 0.8f, 1.0f);
            valLbl.style.fontSize = 12;
            slider.Add(valLbl);
            valueLabel = valLbl;

            slider.RegisterValueChangedCallback(evt =>
            {
                valLbl.text = $"{Mathf.RoundToInt(evt.newValue * 100f)}%";
                onValueChanged?.Invoke(evt.newValue);
            });

            return slider;
        }

        private void BindViewModelEvents()
        {
            if (ViewModel == null) return;

            _masterSub = ViewModel.MasterVolume.Subscribe(v =>
            {
                if (_masterSlider != null && Math.Abs(_masterSlider.value - v) > 0.005f)
                    _masterSlider.value = v;
                if (_masterValLabel != null)
                    _masterValLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            _musicSub = ViewModel.MusicVolume.Subscribe(v =>
            {
                if (_musicSlider != null && Math.Abs(_musicSlider.value - v) > 0.005f)
                    _musicSlider.value = v;
                if (_musicValLabel != null)
                    _musicValLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            _sfxSub = ViewModel.SfxVolume.Subscribe(v =>
            {
                if (_sfxSlider != null && Math.Abs(_sfxSlider.value - v) > 0.005f)
                    _sfxSlider.value = v;
                if (_sfxValLabel != null)
                    _sfxValLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            _uiSub = ViewModel.UiVolume.Subscribe(v =>
            {
                if (_uiSlider != null && Math.Abs(_uiSlider.value - v) > 0.005f)
                    _uiSlider.value = v;
                if (_uiValLabel != null)
                    _uiValLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });

            _muteSub = ViewModel.IsMuted.Subscribe(m =>
            {
                if (_muteToggle != null && _muteToggle.value != m)
                    _muteToggle.value = m;
            });

            _langSub = ViewModel.CurrentLanguage.Subscribe(lang =>
            {
                UpdateLanguageButtonVisuals(lang);
                UpdateAllLocalizedTexts();
            });

            _fullscreenSub = ViewModel.IsFullscreen.Subscribe(fs =>
            {
                if (_fullscreenToggle != null && _fullscreenToggle.value != fs)
                    _fullscreenToggle.value = fs;
            });

            _hintsSub = ViewModel.ShowHints.Subscribe(h =>
            {
                if (_hintsToggle != null && _hintsToggle.value != h)
                    _hintsToggle.value = h;
            });

            _statusSub = ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null)
                    _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void UpdateLanguageButtonVisuals(Language active)
        {
            if (_langTrButton == null || _langEnButton == null) return;

            if (active == Language.TR)
            {
                _langTrButton.RemoveFromClassList("apogee-btn--secondary");
                _langTrButton.AddToClassList("apogee-btn--primary");

                _langEnButton.RemoveFromClassList("apogee-btn--primary");
                _langEnButton.AddToClassList("apogee-btn--secondary");
            }
            else
            {
                _langEnButton.RemoveFromClassList("apogee-btn--secondary");
                _langEnButton.AddToClassList("apogee-btn--primary");

                _langTrButton.RemoveFromClassList("apogee-btn--primary");
                _langTrButton.AddToClassList("apogee-btn--secondary");
            }
        }

        private void UpdateAllLocalizedTexts()
        {
            var loc = ViewModel?.LocalizationManager;
            if (loc == null) return;

            if (_titleLabel != null)          _titleLabel.text = loc.Get("settings.title");
            if (_subtitleLabel != null)       _subtitleLabel.text = loc.Get("settings.subtitle");
            if (_backButton != null)          _backButton.text = loc.Get("settings.btn.back");
            if (_audioHeaderLabel != null)    _audioHeaderLabel.text = loc.Get("settings.audio.title");
            if (_masterSlider != null)        _masterSlider.label = loc.Get("settings.audio.master");
            if (_musicSlider != null)         _musicSlider.label = loc.Get("settings.audio.music");
            if (_sfxSlider != null)           _sfxSlider.label = loc.Get("settings.audio.sfx");
            if (_uiSlider != null)            _uiSlider.label = loc.Get("settings.audio.ui");
            if (_muteToggle != null)          _muteToggle.text = loc.Get("settings.audio.mute");
            if (_langHeaderLabel != null)     _langHeaderLabel.text = loc.Get("settings.language.title");
            if (_displayHeaderLabel != null)  _displayHeaderLabel.text = loc.Get("settings.display.title");
            if (_fullscreenToggle != null)    _fullscreenToggle.text = loc.Get("settings.display.fullscreen");
            if (_gameplayHeaderLabel != null) _gameplayHeaderLabel.text = loc.Get("settings.gameplay.title");
            if (_hintsToggle != null)         _hintsToggle.text = loc.Get("settings.hints.label");
            if (_hintsDescLabel != null)      _hintsDescLabel.text = loc.Get("settings.hints.desc");
            if (_applyButton != null)         _applyButton.text = loc.Get("settings.btn.apply");
            if (_resetButton != null)         _resetButton.text = loc.Get("settings.btn.reset");
        }

        public override void Dispose()
        {
            _masterSub?.Dispose();
            _musicSub?.Dispose();
            _sfxSub?.Dispose();
            _uiSub?.Dispose();
            _muteSub?.Dispose();
            _langSub?.Dispose();
            _fullscreenSub?.Dispose();
            _hintsSub?.Dispose();
            _statusSub?.Dispose();
            base.Dispose();
        }
    }
}
