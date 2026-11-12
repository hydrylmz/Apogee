#pragma warning disable CS0649
using System;
using System.Collections.Generic;
using System.IO;
using Apogee.Core.Attribution;
using Apogee.Core.Education;
using Apogee.Data;
using Apogee.UI.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Milestones;
using Apogee.UI.Screens.Attribution;
using Apogee.UI.Screens.CampaignMap;
using Apogee.UI.Screens.Comparison;
using Apogee.UI.Screens.Composer;
using Apogee.UI.Screens.Contracts;
using Apogee.UI.Screens.Dossier;
using Apogee.UI.Screens.Events;
using Apogee.UI.Screens.Glossary;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.MainMenu;
using Apogee.UI.Screens.Operations;
using Apogee.UI.Screens.Production;
using Apogee.UI.Screens.Report;
using Apogee.UI.Screens.Research;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.SolarMap;
using Apogee.UI.Screens.Workshop;
using Apogee.UI.Screens.Bases;
using Apogee.UI.Screens.Settings;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Managers
{
    /// <summary>
    /// Master UI Manager for APOGEE (Presentation Layer).
    /// Hooks into the UIDocument root, mounts the stylesheet theme,
    /// initializes screen navigation, and attaches the floating tooltip system.
    /// Manages screen controllers for all 23 screens (GDD §14.1, M9-01, M9-05).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Apogee/UI/Apogee UI Manager")]
    public class ApogeeUIManager : MonoBehaviour
    {
        [Header("Stylesheets")]
        [SerializeField] private StyleSheet _themeStyleSheet;

        [Header("Initial Screen")]
        [SerializeField] private ScreenId _initialScreen = ScreenId.MainMenu;

        [Header("Catalog Overrides (Optional for Inspector / WebGL)")]
        [SerializeField] private TextAsset _regionsJson;
        [SerializeField] private TextAsset _destinationsJson;
        [SerializeField] private TextAsset _glossaryJson;
        [SerializeField] private TextAsset _attributionJson;
        [SerializeField] private TextAsset _localeTrJson;
        [SerializeField] private TextAsset _localeEnJson;

        public IScreenNavigator Navigator { get; private set; }
        public ITooltipService TooltipService => Core.Tooltips.TooltipService.Instance;

        private UIDocument _uiDocument;
        private VisualElement _screenContainer;
        private TooltipView _tooltipView;

        // Controllers
        private MainMenuScreenController _mainMenuController;
        private CampaignMapScreenController _campaignMapController;
        private SolarMapScreenController _solarMapController;
        private WorkshopScreenController _workshopController;
        private ReviewScreenController _reviewController;
        private ComparisonScreenController _comparisonController;
        private MissionComposerScreenController _composerController;
        private MissionDossierScreenController _dossierController;
        private LaunchSequenceScreenController _launchController;
        private OperationsScreenController _operationsController;
        private MissionReportScreenController _reportController;
        private GlossaryScreenController _glossaryController;
        private AttributionScreenController _attributionController;
        private EventPopupScreenController _eventPopupController;
        private MilestoneBoardScreenController _milestoneBoardController;
        private CampaignEndScreenController _campaignEndController;
        private ResearchTreeScreenController _researchTreeController;
        private BaseManagementScreenController _baseManagementController;
        private ProductionQueueScreenController _productionQueueController;
        private ContractsScreenController _contractsController;
        private SettingsScreenController _settingsController;

        // Controller accessors
        public MainMenuScreenController MainMenuController => _mainMenuController;
        public CampaignMapScreenController CampaignMapController => _campaignMapController;
        public SolarMapScreenController SolarMapController => _solarMapController;
        public WorkshopScreenController WorkshopController => _workshopController;
        public ReviewScreenController ReviewController => _reviewController;
        public ComparisonScreenController ComparisonController => _comparisonController;
        public MissionComposerScreenController ComposerController => _composerController;
        public MissionDossierScreenController DossierController => _dossierController;
        public LaunchSequenceScreenController LaunchController => _launchController;
        public OperationsScreenController OperationsController => _operationsController;
        public MissionReportScreenController ReportController => _reportController;
        public GlossaryScreenController GlossaryController => _glossaryController;
        public AttributionScreenController AttributionController => _attributionController;
        public EventPopupScreenController EventPopupController => _eventPopupController;
        public MilestoneBoardScreenController MilestoneBoardController => _milestoneBoardController;
        public CampaignEndScreenController CampaignEndController => _campaignEndController;
        public ResearchTreeScreenController ResearchTreeController => _researchTreeController;
        public BaseManagementScreenController BaseManagementController => _baseManagementController;
        public ProductionQueueScreenController ProductionQueueController => _productionQueueController;
        public ContractsScreenController ContractsController => _contractsController;
        public SettingsScreenController SettingsController => _settingsController;
        public Apogee.UI.Bootstrap.CampaignBootstrapper Bootstrapper => Apogee.UI.Bootstrap.CampaignBootstrapper.Instance;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            Navigator = new ScreenNavigator();
            InitializeLocalization();
        }

        private void OnEnable()
        {
            InitializeLocalization();
            InitializeUI();
        }

        private void OnDisable()
        {
            TeardownUI();
        }

        private void InitializeUI()
        {
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            root.Clear();
            root.AddToClassList("apogee-root");

            // Attach theme stylesheet if provided
            if (_themeStyleSheet != null && !root.styleSheets.Contains(_themeStyleSheet))
            {
                root.styleSheets.Add(_themeStyleSheet);
            }

            // Screen container
            _screenContainer = new VisualElement();
            _screenContainer.name = "apogee-screen-container";
            _screenContainer.style.flexGrow = 1;
            _screenContainer.style.width = Length.Percent(100);
            _screenContainer.style.height = Length.Percent(100);
            root.Add(_screenContainer);

            // Floating Tooltip View
            _tooltipView = new TooltipView();
            root.Add(_tooltipView);

            // Register screens
            var mainMenuVm = new MainMenuViewModel(Navigator, Bootstrapper);
            _mainMenuController = new MainMenuScreenController(mainMenuVm);
            _mainMenuController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_mainMenuController);

            // Campaign Map (ScreenId.CampaignMap = 3)
            var regions = LoadOrCreateRegions();
            var campaignMapVm = new CampaignMapViewModel(Navigator, Bootstrapper?.CurrentCampaign, regions);
            _campaignMapController = new CampaignMapScreenController(campaignMapVm);
            _campaignMapController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_campaignMapController);

            // Solar System Map (ScreenId.SolarMap = 4, GDD §6.2, §14.1 #4, M9-06)
            var destinations = LoadOrCreateDestinations();
            var solarMapVm = new SolarMapViewModel(Navigator, Bootstrapper?.CurrentCampaign, destinations);
            _solarMapController = new SolarMapScreenController(solarMapVm);
            _solarMapController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_solarMapController);

            var workshopVm = new WorkshopViewModel(Navigator);
            _workshopController = new WorkshopScreenController(workshopVm);
            _workshopController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_workshopController);

            var reviewVm = new ReviewViewModel(Navigator);
            _reviewController = new ReviewScreenController(reviewVm);
            _reviewController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_reviewController);

            var comparisonVm = new ComparisonViewModel(Navigator);
            _comparisonController = new ComparisonScreenController(comparisonVm);
            _comparisonController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_comparisonController);

            var composerVm = new MissionComposerViewModel(Navigator, destinations: destinations);
            _composerController = new MissionComposerScreenController(composerVm);
            _composerController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_composerController);

            var dossierVm = new MissionDossierViewModel(Navigator);
            _dossierController = new MissionDossierScreenController(dossierVm);
            _dossierController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_dossierController);

            var launchVm = new LaunchSequenceViewModel(Navigator);
            _launchController = new LaunchSequenceScreenController(launchVm);
            _launchController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_launchController);

            var operationsVm = new OperationsViewModel { Navigator = (ScreenNavigator)Navigator };
            _operationsController = new OperationsScreenController(operationsVm);
            _operationsController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_operationsController);

            var reportVm = new MissionReportViewModel { Navigator = Navigator };
            _reportController = new MissionReportScreenController(reportVm);
            _reportController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_reportController);

            // Glossary (ScreenId.Glossary = 17)
            var glossaryManager = LoadOrCreateGlossaryManager();
            var glossaryVm = new GlossaryViewModel(glossaryManager, Navigator);
            _glossaryController = new GlossaryScreenController(glossaryVm);
            _glossaryController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_glossaryController);

            // Attribution (ScreenId.Attribution = 23)
            var attributionManager = LoadOrCreateAttributionManager();
            var attributionVm = new AttributionViewModel(attributionManager, Navigator);
            _attributionController = new AttributionScreenController(attributionVm);
            _attributionController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_attributionController);

            // Event Popup (ScreenId.EventPopup = 20)
            var eventPopupVm = new EventPopupViewModel();
            _eventPopupController = new EventPopupScreenController(eventPopupVm);
            _eventPopupController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_eventPopupController);

            // Milestone Board (ScreenId.MilestoneBoard = 21)
            _milestoneBoardController = new MilestoneBoardScreenController(Navigator);
            _milestoneBoardController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_milestoneBoardController);

            // Campaign End (ScreenId.CampaignEnd = 22)
            var campaignEndVm = new CampaignEndViewModel(null);
            _campaignEndController = new CampaignEndScreenController(campaignEndVm);
            _campaignEndController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_campaignEndController);

            // Contracts (ScreenId.Contracts = 9, GDD §9.1, §14.1 #8, M9-05)
            var contractsVm = new ContractsViewModel(Navigator, Bootstrapper?.CurrentCampaign);
            _contractsController = new ContractsScreenController(contractsVm);
            _contractsController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_contractsController);

            // Research Tree (ScreenId.ResearchTree = 10, GDD §9.2, §14.1 #9, M9-05)
            var researchVm = new ResearchTreeViewModel(Navigator, Bootstrapper?.CurrentCampaign);
            _researchTreeController = new ResearchTreeScreenController(researchVm);
            _researchTreeController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_researchTreeController);

            // Base Management (ScreenId.BaseManagement = 11, GDD §6.1, §14.1 #10, M9-05)
            var baseMgmtVm = new BaseManagementViewModel(Navigator, Bootstrapper?.CurrentCampaign);
            _baseManagementController = new BaseManagementScreenController(baseMgmtVm);
            _baseManagementController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_baseManagementController);

            // Production Queue (ScreenId.ProductionQueue = 12, GDD §9.3, §14.1 #11, M9-05)
            var productionVm = new ProductionQueueViewModel(Navigator, Bootstrapper?.CurrentCampaign);
            _productionQueueController = new ProductionQueueScreenController(productionVm);
            _productionQueueController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_productionQueueController);

            // Settings (ScreenId.Settings = 18, GDD §14.1 #19, M9-07)
            var audioService = FindObjectOfType<Apogee.UI.Audio.UnityAudioService>() as IAudioService ?? new NullAudioService();
            var settingsVm = new SettingsViewModel(Navigator, audioService, LocalizationManager.Instance);
            _settingsController = new SettingsScreenController(settingsVm);
            _settingsController.Initialize(_screenContainer);
            Navigator.RegisterScreen(_settingsController);

            // Bind campaign if bootstrapper has one active
            Bootstrapper?.BindToUI();

            // Navigate to initial screen
            Navigator.GoTo(_initialScreen, null, addToHistory: false);
        }

        private const string FallbackRegionsJson = @"[
  { ""id"": ""guiana_coast"", ""nameKey"": ""region.guiana_coast.name"", ""descKey"": ""region.guiana_coast.desc"", ""icon"": ""icon_region_equatorial"", ""latitude"": 5.2, ""longitude"": -52.8, ""equatorialBonusDv"": 460.0, ""laborCostMultiplier"": 1.1, ""stability"": 0.95, ""weatherRisk"": 0.25, ""logisticsAccessibility"": 0.85, ""commCoverageRating"": 0.8, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""ground_station"", ""test_facility""] },
  { ""id"": ""cape_peninsula"", ""nameKey"": ""region.cape_peninsula.name"", ""descKey"": ""region.cape_peninsula.desc"", ""icon"": ""icon_region_cape"", ""latitude"": 28.5, ""longitude"": -80.6, ""equatorialBonusDv"": 408.0, ""laborCostMultiplier"": 1.25, ""stability"": 0.98, ""weatherRisk"": 0.35, ""logisticsAccessibility"": 0.98, ""commCoverageRating"": 0.9, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""steppe_cosmodrome"", ""nameKey"": ""region.steppe_cosmodrome.name"", ""descKey"": ""region.steppe_cosmodrome.desc"", ""icon"": ""icon_region_steppe"", ""latitude"": 45.9, ""longitude"": 63.3, ""equatorialBonusDv"": 323.0, ""laborCostMultiplier"": 0.8, ""stability"": 0.85, ""weatherRisk"": 0.15, ""logisticsAccessibility"": 0.75, ""commCoverageRating"": 0.85, ""allowedBaseTypes"": [""launch_pad"", ""factory"", ""test_facility"", ""ground_station""] },
  { ""id"": ""pacific_atoll"", ""nameKey"": ""region.pacific_atoll.name"", ""descKey"": ""region.pacific_atoll.desc"", ""icon"": ""icon_region_island"", ""latitude"": 0.5, ""longitude"": -160.0, ""equatorialBonusDv"": 465.0, ""laborCostMultiplier"": 1.4, ""stability"": 0.99, ""weatherRisk"": 0.3, ""logisticsAccessibility"": 0.5, ""commCoverageRating"": 0.75, ""allowedBaseTypes"": [""launch_pad"", ""ground_station"", ""test_facility""] },
  { ""id"": ""nordic_range"", ""nameKey"": ""region.nordic_range.name"", ""descKey"": ""region.nordic_range.desc"", ""icon"": ""icon_region_polar"", ""latitude"": 67.8, ""longitude"": 21.0, ""equatorialBonusDv"": 175.0, ""laborCostMultiplier"": 1.2, ""stability"": 0.99, ""weatherRisk"": 0.4, ""logisticsAccessibility"": 0.9, ""commCoverageRating"": 0.95, ""allowedBaseTypes"": [""launch_pad"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""mediterranean_hub"", ""nameKey"": ""region.mediterranean_hub.name"", ""descKey"": ""region.mediterranean_hub.desc"", ""icon"": ""icon_region_industrial"", ""latitude"": 37.0, ""longitude"": 15.0, ""equatorialBonusDv"": 370.0, ""laborCostMultiplier"": 0.95, ""stability"": 0.92, ""weatherRisk"": 0.1, ""logisticsAccessibility"": 0.95, ""commCoverageRating"": 0.9, ""allowedBaseTypes"": [""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""anatolian_plateau"", ""nameKey"": ""region.anatolian_plateau.name"", ""descKey"": ""region.anatolian_plateau.desc"", ""icon"": ""icon_region_highland"", ""latitude"": 39.9, ""longitude"": 32.8, ""equatorialBonusDv"": 356.0, ""laborCostMultiplier"": 0.75, ""stability"": 0.9, ""weatherRisk"": 0.12, ""logisticsAccessibility"": 0.92, ""commCoverageRating"": 0.88, ""allowedBaseTypes"": [""factory"", ""research_center"", ""ground_station"", ""test_facility""] },
  { ""id"": ""outback_station"", ""nameKey"": ""region.outback_station.name"", ""descKey"": ""region.outback_station.desc"", ""icon"": ""icon_region_desert"", ""latitude"": -31.1, ""longitude"": 136.8, ""equatorialBonusDv"": 398.0, ""laborCostMultiplier"": 1.15, ""stability"": 0.99, ""weatherRisk"": 0.05, ""logisticsAccessibility"": 0.7, ""commCoverageRating"": 0.98, ""allowedBaseTypes"": [""launch_pad"", ""ground_station"", ""test_facility"", ""research_center""] }
]";

        public string ReadCatalogJson(string filename, TextAsset overrideAsset = null)
        {
            if (overrideAsset != null && !string.IsNullOrEmpty(overrideAsset.text))
            {
                return overrideAsset.text;
            }

            try
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
                var res = Resources.Load<TextAsset>("Catalog/Json/" + nameWithoutExt);
                if (res != null && !string.IsNullOrEmpty(res.text))
                {
                    return res.text;
                }
            }
            catch
            {
                // Resources.Load may not be available in non-Unity test harness
            }

            try
            {
                string catalogDir = Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json");
                string path = Path.Combine(catalogDir, filename);
                if (File.Exists(path)) return File.ReadAllText(path);

                string[] fallbackDirs = new[]
                {
                    Path.Combine(Application.dataPath, "Data", "Catalog", "Json"),
                    Path.Combine(Application.streamingAssetsPath, "Catalog", "Json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "Catalog", "Json")
                };

                foreach (var dir in fallbackDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        string p = Path.Combine(dir, filename);
                        if (File.Exists(p)) return File.ReadAllText(p);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not read catalog file '{filename}': {ex.Message}");
            }

            return null;
        }

        public void InitializeLocalization()
        {
            try
            {
                var loc = LocalizationManager.Instance;
                string trJson = ReadCatalogJson("locale_tr.json", _localeTrJson);
                string enJson = ReadCatalogJson("locale_en.json", _localeEnJson);

                if (!string.IsNullOrEmpty(trJson))
                {
                    loc.LoadLocale(Language.TR, trJson);
                }

                if (!string.IsNullOrEmpty(enJson))
                {
                    loc.LoadLocale(Language.EN, enJson);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load localization catalogs: {ex.Message}");
            }
        }

        private GlossaryManager LoadOrCreateGlossaryManager()
        {
            try
            {
                string json = ReadCatalogJson("glossary.json", _glossaryJson);
                if (!string.IsNullOrEmpty(json))
                {
                    var catalog = CatalogLoader.Load<GlossaryTermRecord>(json);
                    if (catalog != null && catalog.All != null)
                    {
                        return new GlossaryManager(catalog.All);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load glossary.json: {ex.Message}");
            }
            return new GlossaryManager(Array.Empty<GlossaryTermRecord>());
        }

        private AttributionManager LoadOrCreateAttributionManager()
        {
            try
            {
                string json = ReadCatalogJson("attribution_data.json", _attributionJson);
                if (!string.IsNullOrEmpty(json))
                {
                    var catalog = CatalogLoader.Load<AttributionRecord>(json);
                    if (catalog != null && catalog.All != null)
                    {
                        return new AttributionManager(catalog.All);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load attribution_data.json: {ex.Message}");
            }
            return new AttributionManager(Array.Empty<AttributionRecord>());
        }

        private IReadOnlyList<RegionRecord> LoadOrCreateRegions()
        {
            try
            {
                string json = ReadCatalogJson("regions.json", _regionsJson);
                if (string.IsNullOrEmpty(json))
                {
                    json = FallbackRegionsJson;
                }

                var catalog = CatalogLoader.Load<RegionRecord>(json);
                if (catalog != null && catalog.All != null && catalog.All.Count > 0)
                {
                    return catalog.All;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load regions.json: {ex.Message}");
            }

            try
            {
                var fallbackCatalog = CatalogLoader.Load<RegionRecord>(FallbackRegionsJson);
                if (fallbackCatalog != null && fallbackCatalog.All != null)
                {
                    return fallbackCatalog.All;
                }
            }
            catch {}

            return Array.Empty<RegionRecord>();
        }

        private IEnumerable<DestinationRecord> LoadOrCreateDestinations()
        {
            try
            {
                string json = ReadCatalogJson("destinations.json", _destinationsJson);
                if (!string.IsNullOrEmpty(json))
                {
                    var catalog = CatalogLoader.Load<DestinationRecord>(json);
                    if (catalog != null && catalog.All != null)
                    {
                        return catalog.All;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load destinations.json: {ex.Message}");
            }
            return null;
        }

        private void TeardownUI()
        {
            _mainMenuController?.Dispose();
            _campaignMapController?.Dispose();
            _solarMapController?.Dispose();
            _workshopController?.Dispose();
            _reviewController?.Dispose();
            _comparisonController?.Dispose();
            _composerController?.Dispose();
            _dossierController?.Dispose();
            _launchController?.Dispose();
            _operationsController?.Dispose();
            _reportController?.Dispose();
            _glossaryController?.Dispose();
            _attributionController?.Dispose();
            _eventPopupController?.Dispose();
            _milestoneBoardController?.Dispose();
            _campaignEndController?.Dispose();
            _contractsController?.Dispose();
            _researchTreeController?.Dispose();
            _baseManagementController?.Dispose();
            _productionQueueController?.Dispose();

            _mainMenuController = null;
            _campaignMapController = null;
            _solarMapController = null;
            _workshopController = null;
            _reviewController = null;
            _comparisonController = null;
            _composerController = null;
            _dossierController = null;
            _launchController = null;
            _operationsController = null;
            _reportController = null;
            _glossaryController = null;
            _attributionController = null;
            _eventPopupController = null;
            _milestoneBoardController = null;
            _campaignEndController = null;
            _contractsController = null;
            _researchTreeController = null;
            _baseManagementController = null;
            _productionQueueController = null;

            if (_tooltipView != null)
            {
                _tooltipView.RemoveFromHierarchy();
                _tooltipView = null;
            }

            if (_screenContainer != null)
            {
                _screenContainer.RemoveFromHierarchy();
                _screenContainer = null;
            }
        }
    }
}
