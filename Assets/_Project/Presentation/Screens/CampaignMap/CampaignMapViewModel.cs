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

        public void EnsureRegionsLoaded(IEnumerable<RegionRecord> fallbackRecords = null)
        {
            if (_regionItems.Count > 0) return;

            if (fallbackRecords == null)
            {
                try
                {
                    const string fallbackJson = @"[
  { ""id"": ""guiana_coast"", ""nameKey"": ""region.guiana_coast.name"", ""descKey"": ""region.guiana_coast.desc"", ""icon"": ""icon_region_equatorial"", ""latitude"": 5.2, ""longitude"": -52.8, ""equatorialBonusDv"": 460.0, ""laborCostMultiplier"": 1.1, ""stability"": 0.95, ""weatherRisk"": 0.25, ""logisticsAccessibility"": 0.85, ""commCoverageRating"": 0.8, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""ground_station"", ""test_facility""] },
  { ""id"": ""cape_peninsula"", ""nameKey"": ""region.cape_peninsula.name"", ""descKey"": ""region.cape_peninsula.desc"", ""icon"": ""icon_region_cape"", ""latitude"": 28.5, ""longitude"": -80.6, ""equatorialBonusDv"": 408.0, ""laborCostMultiplier"": 1.25, ""stability"": 0.98, ""weatherRisk"": 0.35, ""logisticsAccessibility"": 0.98, ""commCoverageRating"": 0.9, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""steppe_cosmodrome"", ""nameKey"": ""region.steppe_cosmodrome.name"", ""descKey"": ""region.steppe_cosmodrome.desc"", ""icon"": ""icon_region_steppe"", ""latitude"": 45.9, ""longitude"": 63.3, ""equatorialBonusDv"": 323.0, ""laborCostMultiplier"": 0.8, ""stability"": 0.85, ""weatherRisk"": 0.15, ""logisticsAccessibility"": 0.75, ""commCoverageRating"": 0.85, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""test_facility"", ""ground_station""] },
  { ""id"": ""pacific_atoll"", ""nameKey"": ""region.pacific_atoll.name"", ""descKey"": ""region.pacific_atoll.desc"", ""icon"": ""icon_region_island"", ""latitude"": 0.5, ""longitude"": -160.0, ""equatorialBonusDv"": 465.0, ""laborCostMultiplier"": 1.4, ""stability"": 0.99, ""weatherRisk"": 0.3, ""logisticsAccessibility"": 0.5, ""commCoverageRating"": 0.75, ""allowedBaseTypes"": [""launch_pad"", ""ground_station"", ""test_facility""] },
  { ""id"": ""nordic_range"", ""nameKey"": ""region.nordic_range.name"", ""descKey"": ""region.nordic_range.desc"", ""icon"": ""icon_region_polar"", ""latitude"": 67.8, ""longitude"": 21.0, ""equatorialBonusDv"": 175.0, ""laborCostMultiplier"": 1.2, ""stability"": 0.99, ""weatherRisk"": 0.4, ""logisticsAccessibility"": 0.9, ""commCoverageRating"": 0.95, ""allowedBaseTypes"": [""launch_pad"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""mediterranean_hub"", ""nameKey"": ""region.mediterranean_hub.name"", ""descKey"": ""region.mediterranean_hub.desc"", ""icon"": ""icon_region_industrial"", ""latitude"": 37.0, ""longitude"": 15.0, ""equatorialBonusDv"": 370.0, ""laborCostMultiplier"": 0.95, ""stability"": 0.92, ""weatherRisk"": 0.1, ""logisticsAccessibility"": 0.95, ""commCoverageRating"": 0.9, ""allowedBaseTypes"": [""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""anatolian_plateau"", ""nameKey"": ""region.anatolian_plateau.name"", ""descKey"": ""region.anatolian_plateau.desc"", ""icon"": ""icon_region_highland"", ""latitude"": 39.9, ""longitude"": 32.8, ""equatorialBonusDv"": 356.0, ""laborCostMultiplier"": 0.75, ""stability"": 0.9, ""weatherRisk"": 0.12, ""logisticsAccessibility"": 0.92, ""commCoverageRating"": 0.88, ""allowedBaseTypes"": [""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""outback_station"", ""nameKey"": ""region.outback_station.name"", ""descKey"": ""region.outback_station.desc"", ""icon"": ""icon_region_desert"", ""latitude"": -31.1, ""longitude"": 136.8, ""equatorialBonusDv"": 398.0, ""laborCostMultiplier"": 1.15, ""stability"": 0.99, ""weatherRisk"": 0.05, ""logisticsAccessibility"": 0.7, ""commCoverageRating"": 0.98, ""allowedBaseTypes"": [""launch_pad"", ""ground_station"", ""test_facility"", ""research_center""] }
]";
                    var catalog = Apogee.Data.CatalogLoader.Load<RegionRecord>(fallbackJson);
                    fallbackRecords = catalog?.All;
                }
                catch {}
            }

            if (fallbackRecords != null)
            {
                foreach (var rec in fallbackRecords)
                {
                    _regionItems.Add(new RegionMapItemViewModel(rec, ActiveLens.Value));
                }
                Regions.Value = _regionItems.ToList();
                if (SelectedRegion.Value == null && _regionItems.Count > 0)
                {
                    SelectRegion(_regionItems[0].Id, updateCommandBus: false);
                }
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
            // StatusMessage intentionally left blank for lens switch (no user-visible debug text)
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
