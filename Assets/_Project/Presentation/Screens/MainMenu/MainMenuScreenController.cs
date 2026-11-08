using System;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.MainMenu
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Main Menu (ScreenId.MainMenu = 1).
    /// Complies with GDD §14.1 (#1 Ana Menü) and M9-03.
    /// Provides access to New Campaign creation, Save/Continue, Workshop Quickstart,
    /// Glossary, Attributions, and Application Settings.
    /// </summary>
    public sealed class MainMenuScreenController : ScreenControllerBase<MainMenuViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MainMenu;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _versionLabel;
        private Label _statusLabel;
        private Button _newCampaignButton;
        private Button _continueButton;
        private Label _saveInfoLabel;
        private Button _quickMissionButton;
        private Button _glossaryButton;
        private Button _attributionsButton;
        private Button _settingsButton;
        private Button _quitButton;

        private TextField _agencyNameField;
        private TextField _seedField;

        public MainMenuScreenController(MainMenuViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-main-menu");
            root.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.paddingTop = 32;
            root.style.paddingBottom = 24;
            root.style.paddingLeft = 48;
            root.style.paddingRight = 48;

            // ── TOP: HERO & BRANDING ──────────────────────────────────────────
            var heroBlock = new VisualElement();
            heroBlock.style.alignItems = Align.Center;
            heroBlock.style.marginBottom = 24;

            var challengeBadge = new Label(ViewModel.ChallengeBadge.Value);
            challengeBadge.style.fontSize = 11;
            challengeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            challengeBadge.style.color = new Color(0.35f, 0.85f, 1f);
            challengeBadge.style.backgroundColor = new Color(0.05f, 0.15f, 0.3f, 0.8f);
            challengeBadge.style.paddingTop = 4;
            challengeBadge.style.paddingBottom = 4;
            challengeBadge.style.paddingLeft = 14;
            challengeBadge.style.paddingRight = 14;
            challengeBadge.style.borderTopLeftRadius = 12;
            challengeBadge.style.borderTopRightRadius = 12;
            challengeBadge.style.borderBottomLeftRadius = 12;
            challengeBadge.style.borderBottomRightRadius = 12;
            challengeBadge.style.borderTopWidth = 1;
            challengeBadge.style.borderBottomWidth = 1;
            challengeBadge.style.borderLeftWidth = 1;
            challengeBadge.style.borderRightWidth = 1;
            challengeBadge.style.borderTopColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
            challengeBadge.style.borderBottomColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
            challengeBadge.style.borderLeftColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
            challengeBadge.style.borderRightColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
            challengeBadge.style.marginBottom = 10;
            heroBlock.Add(challengeBadge);

            _titleLabel = new Label(ViewModel.Title.Value);
            _titleLabel.style.fontSize = 44;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.95f, 0.98f, 1f);
            _titleLabel.style.letterSpacing = 4;
            heroBlock.Add(_titleLabel);

            _subtitleLabel = new Label(ViewModel.Subtitle.Value);
            _subtitleLabel.style.fontSize = 14;
            _subtitleLabel.style.color = new Color(0.55f, 0.75f, 0.95f);
            _subtitleLabel.style.marginTop = 4;
            heroBlock.Add(_subtitleLabel);

            _versionLabel = new Label(ViewModel.Version.Value);
            _versionLabel.style.fontSize = 11;
            _versionLabel.style.color = new Color(0.4f, 0.5f, 0.65f);
            _versionLabel.style.marginTop = 2;
            heroBlock.Add(_versionLabel);

            root.Add(heroBlock);

            // ── CENTER: TWO COLUMNS (MENU BUTTONS & AGENCY PROFILE) ───────────
            var centerRow = new VisualElement();
            centerRow.style.flexDirection = FlexDirection.Row;
            centerRow.style.justifyContent = Justify.Center;
            centerRow.style.alignItems = Align.FlexStart;
            centerRow.style.flexGrow = 1;

            // --- Left Column: Actions ---
            var actionsCol = new VisualElement();
            actionsCol.style.width = 320;
            actionsCol.style.marginRight = 32;

            _newCampaignButton = CreateMenuButton("🚀  YENİ KAMPANYA", isPrimary: true);
            _newCampaignButton.clicked += () => ViewModel.NewCampaignCommand.Execute();
            actionsCol.Add(_newCampaignButton);

            var continueGroup = new VisualElement();
            continueGroup.style.marginBottom = 8;

            _continueButton = CreateMenuButton("💾  KAYITTAN DEVAM ET", isPrimary: false);
            _continueButton.clicked += () => ViewModel.ContinueCampaignCommand.Execute();
            continueGroup.Add(_continueButton);

            _saveInfoLabel = new Label();
            _saveInfoLabel.style.fontSize = 11;
            _saveInfoLabel.style.color = new Color(0.5f, 0.7f, 0.85f);
            _saveInfoLabel.style.marginTop = 2;
            _saveInfoLabel.style.paddingLeft = 8;
            continueGroup.Add(_saveInfoLabel);

            actionsCol.Add(continueGroup);

            _quickMissionButton = CreateMenuButton("🛠  HIZLI GÖREV / ATÖLYE", isPrimary: false);
            _quickMissionButton.clicked += () => ViewModel.QuickMissionCommand.Execute();
            actionsCol.Add(_quickMissionButton);

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new Color(0.18f, 0.22f, 0.32f, 0.7f);
            divider.style.marginTop = 10;
            divider.style.marginBottom = 10;
            actionsCol.Add(divider);

            _glossaryButton = CreateMenuButton("📖  SÖZLÜK & BİLGİ BANKASI", isPrimary: false);
            _glossaryButton.clicked += () => ViewModel.OpenGlossaryCommand.Execute();
            actionsCol.Add(_glossaryButton);

            _attributionsButton = CreateMenuButton("🛰  VERİ KAYNAKLARI & ATIFLAR", isPrimary: false);
            _attributionsButton.clicked += () => ViewModel.OpenAttributionsCommand.Execute();
            actionsCol.Add(_attributionsButton);

            _settingsButton = CreateMenuButton("⚙  AYARLAR", isPrimary: false);
            _settingsButton.clicked += () => ViewModel.OpenSettingsCommand.Execute();
            actionsCol.Add(_settingsButton);

            _quitButton = CreateMenuButton("✖  ÇIKIŞ", isPrimary: false, isDanger: true);
            _quitButton.clicked += () => ViewModel.QuitCommand.Execute();
            actionsCol.Add(_quitButton);

            centerRow.Add(actionsCol);

            // --- Right Column: Agency Setup Card ---
            var profileCard = new VisualElement();
            profileCard.style.width = 380;
            profileCard.style.backgroundColor = new Color(0.08f, 0.11f, 0.17f, 0.95f);
            profileCard.style.paddingTop = 20;
            profileCard.style.paddingBottom = 20;
            profileCard.style.paddingLeft = 24;
            profileCard.style.paddingRight = 24;
            profileCard.style.borderTopLeftRadius = 8;
            profileCard.style.borderTopRightRadius = 8;
            profileCard.style.borderBottomLeftRadius = 8;
            profileCard.style.borderBottomRightRadius = 8;
            profileCard.style.borderTopWidth = 1;
            profileCard.style.borderBottomWidth = 1;
            profileCard.style.borderLeftWidth = 1;
            profileCard.style.borderRightWidth = 1;
            profileCard.style.borderTopColor = new Color(0.2f, 0.28f, 0.42f, 0.8f);
            profileCard.style.borderBottomColor = new Color(0.2f, 0.28f, 0.42f, 0.8f);
            profileCard.style.borderLeftColor = new Color(0.2f, 0.28f, 0.42f, 0.8f);
            profileCard.style.borderRightColor = new Color(0.2f, 0.28f, 0.42f, 0.8f);

            var cardTitle = new Label("KAMPANYA KURULUM PROFİLİ");
            cardTitle.style.fontSize = 14;
            cardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            cardTitle.style.color = new Color(0.4f, 0.8f, 1f);
            cardTitle.style.marginBottom = 16;
            profileCard.Add(cardTitle);

            var agencyLabel = new Label("Ajans Adı:");
            agencyLabel.style.fontSize = 12;
            agencyLabel.style.color = new Color(0.7f, 0.8f, 0.9f);
            agencyLabel.style.marginBottom = 4;
            profileCard.Add(agencyLabel);

            _agencyNameField = new TextField { value = ViewModel.AgencyNameInput.Value };
            _agencyNameField.style.marginBottom = 12;
            _agencyNameField.RegisterValueChangedCallback(evt => ViewModel.AgencyNameInput.Value = evt.newValue);
            profileCard.Add(_agencyNameField);

            var seedLabel = new Label("Deterministik Simülasyon Tohumu (Seed):");
            seedLabel.style.fontSize = 12;
            seedLabel.style.color = new Color(0.7f, 0.8f, 0.9f);
            seedLabel.style.marginBottom = 4;
            profileCard.Add(seedLabel);

            _seedField = new TextField { value = ViewModel.SeedInput.Value.ToString() };
            _seedField.style.marginBottom = 16;
            _seedField.RegisterValueChangedCallback(evt =>
            {
                if (ulong.TryParse(evt.newValue, out ulong parsedSeed))
                {
                    ViewModel.SeedInput.Value = parsedSeed;
                }
            });
            profileCard.Add(_seedField);

            var resourcesBox = new VisualElement();
            resourcesBox.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.9f);
            resourcesBox.style.paddingTop = 10;
            resourcesBox.style.paddingBottom = 10;
            resourcesBox.style.paddingLeft = 14;
            resourcesBox.style.paddingRight = 14;
            resourcesBox.style.borderTopLeftRadius = 6;
            resourcesBox.style.borderTopRightRadius = 6;
            resourcesBox.style.borderBottomLeftRadius = 6;
            resourcesBox.style.borderBottomRightRadius = 6;
            resourcesBox.style.marginBottom = 14;

            var r1 = new Label("• Başlangıç Bütçesi: 500 M$");
            r1.style.fontSize = 12;
            r1.style.color = new Color(0.6f, 0.9f, 0.6f);
            resourcesBox.Add(r1);

            var r2 = new Label("• Ajans İtibarı: 30 / 100");
            r2.style.fontSize = 12;
            r2.style.color = new Color(0.9f, 0.75f, 0.4f);
            resourcesBox.Add(r2);

            var r3 = new Label("• Başlangıç Bölgesi: Cape Peninsula (Ekvator Yakını)");
            r3.style.fontSize = 12;
            r3.style.color = new Color(0.6f, 0.8f, 1f);
            resourcesBox.Add(r3);

            var r4 = new Label("• Başlangıç Tesisleri: 1 Rampa (Lv1), 1 Fabrika, 1 Ar-Ge");
            r4.style.fontSize = 12;
            r4.style.color = new Color(0.75f, 0.85f, 0.95f);
            resourcesBox.Add(r4);

            profileCard.Add(resourcesBox);

            var seedNote = new Label(
                "APOGEE simülasyonları deterministiktir. Aynı tohum değeri, fırlatma hava durumu, " +
                "arıza olasılıkları ve olay havuzunda tamamen aynı sonuçları üretir.");
            seedNote.style.fontSize = 11;
            seedNote.style.color = new Color(0.45f, 0.55f, 0.65f);
            seedNote.style.whiteSpace = WhiteSpace.Normal;
            profileCard.Add(seedNote);

            centerRow.Add(profileCard);
            root.Add(centerRow);

            // ── BOTTOM: STATUS & FOOTER ──────────────────────────────────────
            var bottomBlock = new VisualElement();
            bottomBlock.style.alignItems = Align.Center;
            bottomBlock.style.marginTop = 16;

            _statusLabel = new Label(ViewModel.StatusMessage.Value);
            _statusLabel.style.fontSize = 13;
            _statusLabel.style.color = new Color(0.35f, 0.85f, 1f);
            _statusLabel.style.marginBottom = 6;
            bottomBlock.Add(_statusLabel);

            var footerLabel = new Label("Birleşik Yörünge Ajansı · NASA Space Apps Challenge 2026 · Pure C# Deterministik Motor");
            footerLabel.style.fontSize = 11;
            footerLabel.style.color = new Color(0.35f, 0.45f, 0.55f);
            bottomBlock.Add(footerLabel);

            root.Add(bottomBlock);

            return root;
        }

        protected override void OnInitialized()
        {
            RegisterObservables();
        }

        protected override void OnShown(object context)
        {
            ViewModel?.RefreshSaveState();
            UpdateSaveButtonState();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null)
                {
                    _statusLabel.text = msg ?? string.Empty;
                }
            });

            ViewModel.HasSaveGame.Subscribe(_ => UpdateSaveButtonState());
            ViewModel.SaveGameInfo.Subscribe(info =>
            {
                if (_saveInfoLabel != null)
                {
                    _saveInfoLabel.text = info ?? string.Empty;
                }
            });

            ViewModel.AgencyNameInput.Subscribe(name =>
            {
                if (_agencyNameField != null && _agencyNameField.value != name)
                {
                    _agencyNameField.value = name;
                }
            });

            ViewModel.SeedInput.Subscribe(seed =>
            {
                if (_seedField != null && _seedField.value != seed.ToString())
                {
                    _seedField.value = seed.ToString();
                }
            });
        }

        private void UpdateSaveButtonState()
        {
            if (_continueButton == null) return;

            bool hasSave = ViewModel != null && ViewModel.HasSaveGame.Value;
            _continueButton.SetEnabled(hasSave);
            if (!hasSave && _saveInfoLabel != null)
            {
                _saveInfoLabel.text = "Kayıtlı kampanya bulunamadı";
            }
        }

        private static Button CreateMenuButton(string text, bool isPrimary = false, bool isDanger = false)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("apogee-button");

            btn.style.height = 42;
            btn.style.marginBottom = 8;
            btn.style.fontSize = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderBottomRightRadius = 6;

            if (isPrimary)
            {
                btn.AddToClassList("apogee-button--primary");
                btn.style.backgroundColor = new Color(0.12f, 0.45f, 0.75f, 1f);
                btn.style.color = new Color(0.95f, 0.98f, 1f);
                btn.style.fontSize = 14;
            }
            else if (isDanger)
            {
                btn.AddToClassList("apogee-button--danger");
                btn.style.backgroundColor = new Color(0.35f, 0.12f, 0.14f, 0.8f);
                btn.style.color = new Color(0.95f, 0.7f, 0.7f);
            }
            else
            {
                btn.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 0.9f);
                btn.style.color = new Color(0.85f, 0.9f, 0.98f);
            }

            return btn;
        }
    }
}
