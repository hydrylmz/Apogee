using System;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Persistence;
using Apogee.UI.Bootstrap;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.MainMenu
{
    /// <summary>
    /// ViewModel for the Main Menu Screen (ScreenId.MainMenu = 1).
    /// Complies with GDD §4.1, §14.1 (#1 Ana Menü), and M9-03.
    /// Manages campaign setup arguments, save loading, and navigation to core application screens.
    /// </summary>
    public sealed class MainMenuViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private ICampaignLifecycleHandler _lifecycleHandler;
        private readonly ICampaignSaveStorage _saveStorage;

        // Branding & Version
        public ObservableValue<string> Title { get; } = new("APOGEE");
        public ObservableValue<string> Subtitle { get; } = new("Uzay Ajansı ve Misyon Tasarım Simülasyonu");
        public ObservableValue<string> ChallengeBadge { get; } = new("NASA SPACE APPS CHALLENGE 2026");
        public ObservableValue<string> Version { get; } = new("v1.0.0 — 2026.1 LTS");

        // Status & Save Info
        public ObservableValue<string> StatusMessage { get; } = new(string.Empty);
        public ObservableValue<bool> HasSaveGame { get; } = new(false);
        public ObservableValue<string> SaveGameInfo { get; } = new(string.Empty);

        // Campaign Setup Inputs
        public ObservableValue<string> AgencyNameInput { get; } = new("Birleşik Yörünge Ajansı");
        public ObservableValue<string> StartingRegionInput { get; } = new("cape_peninsula");
        public ObservableValue<ulong> SeedInput { get; } = new(42UL);
        public ObservableValue<double> StartingBudget { get; } = new(500.0);
        public ObservableValue<double> StartingReputation { get; } = new(30.0);

        // Commands
        public ViewActionCommand NewCampaignCommand { get; }
        public ViewActionCommand ContinueCampaignCommand { get; }
        public ViewActionCommand QuickMissionCommand { get; }
        public ViewActionCommand OpenGlossaryCommand { get; }
        public ViewActionCommand OpenAttributionsCommand { get; }
        public ViewActionCommand OpenSettingsCommand { get; }
        public ViewActionCommand QuitCommand { get; }

        // Events
        public event Action OnQuitRequested;
        public event Action<CampaignState> OnCampaignStarted;
        public event Action<CampaignState> OnCampaignLoaded;

        public ICampaignSaveStorage SaveStorage => _saveStorage;
        public ICampaignLifecycleHandler LifecycleHandler => _lifecycleHandler;

        public MainMenuViewModel(
            IScreenNavigator navigator,
            ICampaignLifecycleHandler lifecycleHandler = null,
            ICampaignSaveStorage saveStorage = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _lifecycleHandler = lifecycleHandler;
            _saveStorage = saveStorage ?? new PlayerPrefsCampaignSaveStorage();

            NewCampaignCommand = new ViewActionCommand(ExecuteNewCampaign);
            ContinueCampaignCommand = new ViewActionCommand(ExecuteContinueCampaign);
            QuickMissionCommand = new ViewActionCommand(ExecuteQuickMission);
            OpenGlossaryCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Glossary));
            OpenAttributionsCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Attribution));
            OpenSettingsCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Settings));
            QuitCommand = new ViewActionCommand(ExecuteQuit);

            RefreshSaveState();
        }

        public void BindLifecycleHandler(ICampaignLifecycleHandler handler)
        {
            _lifecycleHandler = handler;
        }

        public void RefreshSaveState()
        {
            if (_saveStorage != null && _saveStorage.HasSave())
            {
                string json = _saveStorage.LoadSave();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        var data = CampaignSaveSerializer.FromJson(json);
                        HasSaveGame.Value = true;
                        SaveGameInfo.Value = $"{data.AgencyName ?? "Ajans"} · Gün {data.ElapsedDays} · Bütçe: {data.Budget:F0} M$";
                        return;
                    }
                    catch
                    {
                        // Fallback on invalid parse
                    }
                }
            }

            HasSaveGame.Value = false;
            SaveGameInfo.Value = string.Empty;
        }

        public void ExecuteNewCampaign()
        {
            var args = new CampaignSetupArgs
            {
                AgencyName = string.IsNullOrWhiteSpace(AgencyNameInput.Value) ? "Birleşik Yörünge Ajansı" : AgencyNameInput.Value,
                StartingRegionId = string.IsNullOrWhiteSpace(StartingRegionInput.Value) ? "cape_peninsula" : StartingRegionInput.Value,
                Seed = SeedInput.Value != 0 ? SeedInput.Value : 42UL,
                StartingBudget = StartingBudget.Value > 0 ? StartingBudget.Value : 500.0,
                StartingReputation = StartingReputation.Value > 0 ? StartingReputation.Value : 30.0
            };

            CampaignState campaign = null;
            if (_lifecycleHandler != null)
            {
                campaign = _lifecycleHandler.StartNewCampaign(args);
            }
            else
            {
                campaign = CampaignFactory.CreateDefault(args);
            }

            StatusMessage.Value = $"Yeni kampanya başlatıldı: {args.AgencyName}";
            OnCampaignStarted?.Invoke(campaign);

            // Navigate to Campaign Map (or fallback to Workshop if CampaignMap is not yet loaded)
            bool navigated = _navigator.GoTo(ScreenId.CampaignMap);
            if (!navigated)
            {
                _navigator.GoTo(ScreenId.Workshop);
            }
        }

        public void ExecuteContinueCampaign()
        {
            if (!_saveStorage.HasSave())
            {
                StatusMessage.Value = "Kayıtlı kampanya bulunamadı.";
                return;
            }

            string json = _saveStorage.LoadSave();
            if (string.IsNullOrWhiteSpace(json))
            {
                StatusMessage.Value = "Kayıt dosyası boş veya bozuk.";
                return;
            }

            try
            {
                CampaignState campaign = null;
                if (_lifecycleHandler != null)
                {
                    campaign = _lifecycleHandler.LoadCampaign(json);
                }
                else
                {
                    var saveData = CampaignSaveSerializer.FromJson(json);
                    campaign = new CampaignState(saveData.Seed, saveData.CampaignId);
                    CampaignSaveSerializer.RestoreFromSaveData(campaign, saveData);
                }

                StatusMessage.Value = $"Kampanya yüklendi: {campaign.AgencyName}";
                OnCampaignLoaded?.Invoke(campaign);

                bool navigated = _navigator.GoTo(ScreenId.CampaignMap);
                if (!navigated)
                {
                    _navigator.GoTo(ScreenId.Workshop);
                }
            }
            catch (Exception ex)
            {
                StatusMessage.Value = $"Kayıt yükleme hatası: {ex.Message}";
            }
        }

        public void ExecuteQuickMission()
        {
            StatusMessage.Value = "Hızlı Görev / Tasarım Atölyesi açılıyor...";
            _navigator.GoTo(ScreenId.Workshop);
        }

        public void ExecuteQuit()
        {
            OnQuitRequested?.Invoke();
#if UNITY_STANDALONE
            UnityEngine.Application.Quit();
#endif
        }
    }
}
