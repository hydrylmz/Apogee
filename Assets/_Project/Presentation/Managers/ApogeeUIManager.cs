using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Workshop;
using Apogee.UI.Screens.Comparison;
using Apogee.UI.Screens.Composer;
using Apogee.UI.Screens.Dossier;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.Operations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Managers
{
    /// <summary>
    /// Master UI Manager for APOGEE (Presentation Layer).
    /// Hooks into the UIDocument root, mounts the stylesheet theme,
    /// initializes screen navigation, and attaches the floating tooltip system.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("Apogee/UI/Apogee UI Manager")]
    public class ApogeeUIManager : MonoBehaviour
    {
        [Header("Stylesheets")]
        [SerializeField] private StyleSheet _themeStyleSheet;

        [Header("Initial Screen")]
        [SerializeField] private ScreenId _initialScreen = ScreenId.Workshop;

        public IScreenNavigator Navigator { get; private set; }
        public ITooltipService TooltipService => Core.Tooltips.TooltipService.Instance;

        private UIDocument _uiDocument;
        private VisualElement _screenContainer;
        private TooltipView _tooltipView;

        private WorkshopScreenController _workshopController;
        private ReviewScreenController _reviewController;
        private ComparisonScreenController _comparisonController;
        private MissionComposerScreenController _composerController;
        private MissionDossierScreenController _dossierController;
        private LaunchSequenceScreenController _launchController;
        private OperationsScreenController _operationsController;

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

            // Register default screens
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

            // Navigate to initial screen
            Navigator.GoTo(_initialScreen, null, addToHistory: false);
        }

        private void TeardownUI()
        {
            _workshopController?.Dispose();
            _reviewController?.Dispose();
            _comparisonController?.Dispose();
            _composerController?.Dispose();
            _dossierController?.Dispose();
            _launchController?.Dispose();
            _operationsController?.Dispose();
            _workshopController = null;
            _reviewController = null;
            _comparisonController = null;
            _composerController = null;
            _dossierController = null;
            _launchController = null;
            _operationsController = null;

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
