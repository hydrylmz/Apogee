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
using Apogee.UI.Screens.Dossier;
using Apogee.UI.Screens.Events;
using Apogee.UI.Screens.Glossary;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.MainMenu;
using Apogee.UI.Screens.Operations;
using Apogee.UI.Screens.Report;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Workshop;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Managers
{
    /// <summary>
    /// Master UI Manager for APOGEE (Presentation Layer).
    /// Hooks into the UIDocument root, mounts the stylesheet theme,
    /// initializes screen navigation, and attaches the floating tooltip system.
    /// Manages screen controllers for all 19 screens (GDD §14.1, M9-01).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Apogee/UI/Apogee UI Manager")]
    public class ApogeeUIManager : MonoBehaviour
    {
        [Header("Stylesheets")]
        [SerializeField] private StyleSheet _themeStyleSheet;

        [Header("Initial Screen")]
        [SerializeField] private ScreenId _initialScreen = ScreenId.MainMenu;

        public IScreenNavigator Navigator { get; private set; }
        public ITooltipService TooltipService => Core.Tooltips.TooltipService.Instance;

        private UIDocument _uiDocument;
        private VisualElement _screenContainer;
        private TooltipView _tooltipView;

        // Controllers
        private MainMenuScreenController _mainMenuController;
        private CampaignMapScreenController _campaignMapController;
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

        // Controller accessors
        public MainMenuScreenController MainMenuController => _mainMenuController;
        public CampaignMapScreenController CampaignMapController => _campaignMapController;
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
        public Apogee.UI.Bootstrap.CampaignBootstrapper Bootstrapper => Apogee.UI.Bootstrap.CampaignBootstrapper.Instance;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            Navigator = new ScreenNavigator();
        }

        private void OnEnable()
        {
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

            var composerVm = new MissionComposerViewModel(Navigator);
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

            // Bind campaign if bootstrapper has one active
            Bootstrapper?.BindToUI();

            // Navigate to initial screen
            Navigator.GoTo(_initialScreen, null, addToHistory: false);
        }

        private GlossaryManager LoadOrCreateGlossaryManager()
        {
            try
            {
                string path = Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json", "glossary.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
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
                string path = Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json", "attribution_data.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
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
                string path = Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json", "regions.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var catalog = CatalogLoader.Load<RegionRecord>(json);
                    if (catalog != null && catalog.All != null)
                    {
                        return catalog.All;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApogeeUIManager] Could not load regions.json: {ex.Message}");
            }
            return Array.Empty<RegionRecord>();
        }

        private void TeardownUI()
        {
            _mainMenuController?.Dispose();
            _campaignMapController?.Dispose();
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

            _mainMenuController = null;
            _campaignMapController = null;
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
