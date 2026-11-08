using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.CampaignMap
{
    /// <summary>
    /// ViewModel for the Strategic World Map Screen (ScreenId.CampaignMap = 3).
    /// Complies with GDD §6.1, §6.3, §14.1 (#3 Kampanya Ana Ekranı), and §21 M9-04.
    /// Manages strategic regions, map lenses, time controls, base infrastructure, and agency HUD.
    /// </summary>
    public sealed class CampaignMapViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;
        private readonly List<RegionMapItemViewModel> _regionItems = new();

        // ── HUD Observables ──────────────────────────────────────────────────
        public ObservableValue<string> DateDisplay { get; } = new(string.Empty);
        public ObservableValue<TimeSpeed> CurrentSpeed { get; } = new(TimeSpeed.Paused);
        public ObservableValue<double> Budget { get; } = new(0.0);
        public ObservableValue<double> Reputation { get; } = new(0.0);
        public ObservableValue<double> ResearchPoints { get; } = new(0.0);
        public ObservableValue<double> KnowledgePoints { get; } = new(0.0);
        public ObservableValue<string> AgencyName { get; } = new(string.Empty);

        // ── Map & Lens Observables ───────────────────────────────────────────
        public ObservableValue<MapLens> ActiveLens { get; } = new(MapLens.Overview);
        public ObservableValue<IReadOnlyList<RegionMapItemViewModel>> Regions { get; } = new(Array.Empty<RegionMapItemViewModel>());
        public ObservableValue<RegionMapItemViewModel> SelectedRegion { get; } = new(null);
        public ObservableValue<double> SelectedRegionScore { get; } = new(0.0);
        public ObservableValue<string> SelectedRegionDescription { get; } = new(string.Empty);
        public ObservableValue<string> StatusMessage { get; } = new(string.Empty);

        // ── Time & Lens Commands ─────────────────────────────────────────────
        public ViewActionCommand AdvanceDayCommand { get; }
        public ViewActionCommand PauseCommand { get; }
        public ViewActionCommand Speed1xCommand { get; }
        public ViewActionCommand Speed2xCommand { get; }
        public ViewActionCommand Speed3xCommand { get; }

        // ── Navigation Commands ──────────────────────────────────────────────
        public ViewActionCommand GoToComposerCommand { get; }
        public ViewActionCommand GoToWorkshopCommand { get; }
        public ViewActionCommand GoToSolarMapCommand { get; }
        public ViewActionCommand GoToResearchCommand { get; }
        public ViewActionCommand GoToBaseManagementCommand { get; }
        public ViewActionCommand GoToProductionCommand { get; }
        public ViewActionCommand GoToContractsCommand { get; }
        public ViewActionCommand GoToMilestonesCommand { get; }
        public ViewActionCommand GoToMainMenuCommand { get; }

        public CampaignState Campaign => _campaign;

        public CampaignMapViewModel(
            IScreenNavigator navigator,
            CampaignState campaign = null,
            IEnumerable<RegionRecord> regionRecords = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            // Time commands
            AdvanceDayCommand = new ViewActionCommand(() => AdvanceDays(1));
            PauseCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Paused));
            Speed1xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Normal));
            Speed2xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Fast));
            Speed3xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.VeryFast));

            // Navigation commands
            GoToComposerCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.MissionComposer));
            GoToWorkshopCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Workshop));
            GoToSolarMapCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.SolarMap));
            GoToResearchCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.ResearchTree));
            GoToBaseManagementCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.BaseManagement));
            GoToProductionCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.ProductionQueue));
            GoToContractsCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Contracts));
            GoToMilestonesCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.MilestoneBoard));
            GoToMainMenuCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.MainMenu));

            // Populate region list
            if (regionRecords != null)
            {
                foreach (var rec in regionRecords)
                {
                    _regionItems.Add(new RegionMapItemViewModel(rec, ActiveLens.Value));
                }
                Regions.Value = _regionItems.ToList();
            }

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
        }

        public void BindCampaign(CampaignState campaign)
        {
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));

            ActiveLens.Value = _campaign.ActiveMapLens;
            RefreshHUD();
            RefreshRegions();

            // Select initial region
            string targetId = !string.IsNullOrEmpty(_campaign.SelectedRegionId)
                ? _campaign.SelectedRegionId
                : (_regionItems.Count > 0 ? _regionItems[0].Id : null);

            if (!string.IsNullOrEmpty(targetId))
            {
                SelectRegion(targetId, updateCommandBus: false);
            }
        }

        public void RefreshHUD()
        {
            if (_campaign == null) return;

            DateDisplay.Value = $"{_campaign.Time.CurrentDate} (Gün {_campaign.Time.ElapsedDays})";
            CurrentSpeed.Value = _campaign.Time.Speed;
            Budget.Value = _campaign.Budget;
            Reputation.Value = _campaign.Reputation;
            ResearchPoints.Value = _campaign.ResearchPoints;
            KnowledgePoints.Value = _campaign.KnowledgePoints;
            AgencyName.Value = _campaign.AgencyName;
        }

        public void RefreshRegions()
        {
            if (_campaign == null) return;

            // Group bases by RegionId
            var basesByRegion = _campaign.Bases.Values
                .GroupBy(b => b.RegionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in _regionItems)
            {
                item.UpdateLens(ActiveLens.Value);
                if (basesByRegion.TryGetValue(item.Id, out var bases))
                {
                    item.UpdateBases(bases);
                }
                else
                {
                    item.UpdateBases(null);
                }
            }

            Regions.Value = _regionItems.ToList();

            if (SelectedRegion.Value != null)
            {
                UpdateSelectedRegionDetail(SelectedRegion.Value);
            }
        }

        public void SetSpeed(TimeSpeed speed)
        {
            if (_campaign == null) return;

            var cmd = new SetTimeSpeedCommand(speed);
            _campaign.Execute(cmd);

            CurrentSpeed.Value = _campaign.Time.Speed;
            StatusMessage.Value = $"Zaman hızı: {speed}";
        }

        public void AdvanceDays(int days = 1)
        {
            if (_campaign == null || days <= 0) return;

            var cmd = new AdvanceTimeCommand(days);
            var result = _campaign.Execute(cmd);

            if (result.Success)
            {
                RefreshHUD();
                RefreshRegions();
                StatusMessage.Value = $"Zaman {days} gün ilerletildi ({_campaign.Time.CurrentDate}).";
            }
            else
            {
                StatusMessage.Value = $"Zaman ilerletilemedi: {result.Message}";
            }
        }

        public void SetMapLens(MapLens lens)
        {
            if (_campaign == null)
            {
                ActiveLens.Value = lens;
                RefreshRegions();
                return;
            }

            var cmd = new SetMapLensCommand(lens);
            _campaign.Execute(cmd);

            ActiveLens.Value = _campaign.ActiveMapLens;
            RefreshRegions();
            StatusMessage.Value = $"Harita lensi: {lens}";
        }

        public void SelectRegion(string regionId, bool updateCommandBus = true)
        {
            if (string.IsNullOrWhiteSpace(regionId)) return;

            var item = _regionItems.FirstOrDefault(r => string.Equals(r.Id, regionId, StringComparison.OrdinalIgnoreCase));
            if (item == null) return;

            if (updateCommandBus && _campaign != null)
            {
                var cmd = new SelectRegionCommand(regionId);
                _campaign.Execute(cmd);
            }
            else if (_campaign != null)
            {
                _campaign.SelectedRegionId = regionId;
            }

            foreach (var r in _regionItems)
            {
                r.IsSelected = (r == item);
            }

            SelectedRegion.Value = item;
            UpdateSelectedRegionDetail(item);
            StatusMessage.Value = $"Bölge seçildi: {item.Name}";
        }

        private void UpdateSelectedRegionDetail(RegionMapItemViewModel item)
        {
            if (item == null)
            {
                SelectedRegionScore.Value = 0.0;
                SelectedRegionDescription.Value = string.Empty;
                return;
            }

            SelectedRegionScore.Value = RegionLensEvaluator.EvaluateScore(item.Record, ActiveLens.Value);
            SelectedRegionDescription.Value = RegionLensEvaluator.GetLensDescription(item.Record, ActiveLens.Value);
        }
    }
}
