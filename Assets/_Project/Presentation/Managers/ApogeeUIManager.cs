using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Workshop;
using Apogee.UI.Screens.Comparison;
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

            // Navigate to initial screen
            Navigator.GoTo(_initialScreen, null, addToHistory: false);
        }

        private void TeardownUI()
        {
            _workshopController?.Dispose();
            _reviewController?.Dispose();
            _comparisonController?.Dispose();
            _workshopController = null;
            _reviewController = null;
            _comparisonController = null;

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
