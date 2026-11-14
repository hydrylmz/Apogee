using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Bases
{
    /// <summary>
    /// ViewModel for an individual base facility card (GDD §6.1, §19.8).
    /// </summary>
    public sealed class BaseFacilityItemViewModel
    {
        public string Id { get; }
        public string RegionId { get; }
        public BaseType Type { get; }
        public string TypeName { get; }
        public int Level { get; }
        public int MaxLevel => BaseLevelSpec.MaxLevel;
        public double MonthlyMaintenanceCost { get; }
        public double CapacityRating { get; }
        public bool CanUpgrade { get; }
        public double NextUpgradeCost { get; }

        public BaseFacilityItemViewModel(BaseFacility facility, double laborMultiplier = 1.0)
        {
            if (facility == null) throw new ArgumentNullException(nameof(facility));

            Id = facility.Id;
            RegionId = facility.RegionId;
            Type = facility.Type;
            TypeName = GetBaseTypeName(facility.Type);
            Level = facility.Level;
            MonthlyMaintenanceCost = facility.MonthlyMaintenanceCost;
            CapacityRating = facility.CapacityRating;
            CanUpgrade = facility.CanUpgrade;
            NextUpgradeCost = facility.CanUpgrade
                ? BaseLevelSpec.GetUpgradeCost(facility.Type, facility.Level + 1, laborMultiplier)
                : 0.0;
        }

        public static string GetBaseTypeName(BaseType type) => type switch
        {
            BaseType.LaunchPad => "Fırlatma Rampası",
            BaseType.Factory => "Fabrika & Montaj",
            BaseType.ResearchCenter => "Ar-Ge Laboratuvarı",
            BaseType.GroundStation => "Yer Takip İstasyonu",
            BaseType.TestFacility => "Sistem Test Tesisi",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// ViewModel for the Strategic Base Management Screen (ScreenId.BaseManagement = 11).
    /// Complies with GDD §6.1, §14.1 (#10 Üs Yönetimi), and M9-05.
    /// Exposes facilities, maintenance costs, and dispatches BuildBase / UpgradeBase commands.
    /// </summary>
    public sealed class BaseManagementViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;

        /// <summary>
        /// Fallback region ids. Must match the ids in Data/Catalog/Json/regions.json
        /// (guiana_coast, cape_peninsula, steppe_cosmodrome, pacific_atoll,
        /// nordic_range, mediterranean_hub, anatolian_plateau, outback_station).
        /// </summary>
        private static readonly string[] DefaultRegions = new[]
        {
            "cape_peninsula", "guiana_coast", "steppe_cosmodrome", "pacific_atoll",
            "nordic_range", "mediterranean_hub", "anatolian_plateau", "outback_station"
        };

        public ObservableValue<double> Budget { get; } = new(0.0);
        public ObservableValue<double> TotalMonthlyMaintenance { get; } = new(0.0);
        public ObservableValue<int> TotalBasesCount { get; } = new(0);

        public ObservableValue<IReadOnlyList<BaseFacilityItemViewModel>> Facilities { get; } =
            new(Array.Empty<BaseFacilityItemViewModel>());

        public ObservableValue<BaseFacilityItemViewModel> SelectedFacility { get; } = new(null);
        public ObservableValue<IReadOnlyList<string>> AvailableRegions { get; } = new(DefaultRegions);
        public ObservableValue<string> SelectedRegionId { get; } = new("cape_peninsula");
        public ObservableValue<BaseType> SelectedBaseType { get; } = new(BaseType.LaunchPad);
        public ObservableValue<double> SelectedBuildCost { get; } = new(0.0);

        public ObservableValue<string> StatusMessage { get; } = new("Hazır.");

        public ViewActionCommand BackCommand { get; }
        public ViewActionCommand UpgradeSelectedCommand { get; }
        public ViewActionCommand BuildSelectedCommand { get; }

        public CampaignState Campaign => _campaign;

        public BaseManagementViewModel(IScreenNavigator navigator, CampaignState campaign = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            BackCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.CampaignMap));
            UpgradeSelectedCommand = new ViewActionCommand(UpgradeSelectedFacility);
            BuildSelectedCommand = new ViewActionCommand(BuildSelectedFacility);

            UpdateBuildCostPreview();

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
        }

        public void BindCampaign(CampaignState campaign)
        {
            _campaign = campaign;
            Refresh();
        }

        public void Refresh()
        {
            if (_campaign == null)
            {
                Budget.Value = 0.0;
                TotalMonthlyMaintenance.Value = 0.0;
                TotalBasesCount.Value = 0;
                Facilities.Value = Array.Empty<BaseFacilityItemViewModel>();
                return;
            }

            Budget.Value = _campaign.Budget;

            var list = new List<BaseFacilityItemViewModel>();
            double totalUpkeep = 0.0;

            if (_campaign.Bases != null)
            {
                foreach (var facility in _campaign.Bases.Values)
                {
                    var item = new BaseFacilityItemViewModel(facility);
                    list.Add(item);
                    totalUpkeep += facility.MonthlyMaintenanceCost;
                }
            }

            Facilities.Value = list.OrderBy(f => f.RegionId).ThenBy(f => f.Type).ToList();
            TotalMonthlyMaintenance.Value = totalUpkeep;
            TotalBasesCount.Value = list.Count;

            if (SelectedFacility.Value != null)
            {
                SelectedFacility.Value = list.FirstOrDefault(f => f.Id == SelectedFacility.Value.Id) ?? list.FirstOrDefault();
            }
            else
            {
                SelectedFacility.Value = list.FirstOrDefault();
            }

            UpdateBuildCostPreview();
        }

        public void SelectFacility(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId)) return;
            var match = Facilities.Value.FirstOrDefault(f => string.Equals(f.Id, facilityId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedFacility.Value = match;
            }
        }

        public void SetRegionForConstruction(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return;
            SelectedRegionId.Value = regionId;
            UpdateBuildCostPreview();
        }

        public void SetBaseTypeForConstruction(BaseType type)
        {
            SelectedBaseType.Value = type;
            UpdateBuildCostPreview();
        }

        /// <summary>
        /// Returns how many facilities of the given type exist in the given region.
        /// Used by the construction panel to show per-type counts (e.g. "Fırlatma Rampası (1)").
        /// </summary>
        public int GetFacilityCount(string regionId, BaseType type)
        {
            if (_campaign?.Bases == null || string.IsNullOrEmpty(regionId)) return 0;

            int count = 0;
            foreach (var facility in _campaign.Bases.Values)
            {
                if (facility.Type == type &&
                    string.Equals(facility.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        private void UpdateBuildCostPreview()
        {
            SelectedBuildCost.Value = BaseLevelSpec.GetUpgradeCost(SelectedBaseType.Value, targetLevel: 1, laborCostMultiplier: 1.0);
        }

        private void UpgradeSelectedFacility()
        {
            if (SelectedFacility.Value == null) return;
            UpgradeFacility(SelectedFacility.Value.Id);
        }

        public bool UpgradeFacility(string baseId)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            var cmd = new UpgradeBaseCommand(baseId);
            var result = _campaign.Execute(cmd, PlayerRole.AgencyDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Tesis seviyesi yükseltildi: {baseId}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"Yükseltilemedi: {result.Message}";
                return false;
            }
        }

        private void BuildSelectedFacility()
        {
            string reg = SelectedRegionId.Value ?? "cape_peninsula";
            BaseType type = SelectedBaseType.Value;
            BuildFacility(reg, type);
        }

        public bool BuildFacility(string regionId, BaseType type)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            string baseId = $"base_{regionId}_{type.ToString().ToLower()}_{_campaign.Bases.Count + 1}";
            var cmd = new BuildBaseCommand(baseId, regionId, type);
            var result = _campaign.Execute(cmd, PlayerRole.AgencyDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Yeni tesis inşa edildi: {baseId} ({type})";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"İnşa edilemedi: {result.Message}";
                return false;
            }
        }
    }
}
