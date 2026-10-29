using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Screen controller for the Design Workshop (GDD §7 & §14.3).
    /// </summary>
    public class WorkshopScreenController : ScreenControllerBase<WorkshopViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Workshop;

        private Label _targetLabel;
        private Label _blueprintNameLabel;
        private Label _deltaVValueLabel;
        private VisualElement _deltaVBarFill;
        private Label _massValueLabel;
        private Label _costValueLabel;
        private Label _reliabilityValueLabel;
        private Button _reviewButton;

        public WorkshopScreenController(WorkshopViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // Header strip (GDD §14.3)
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");

            _blueprintNameLabel = new Label("APOGEE Atölye");
            _blueprintNameLabel.AddToClassList("apogee-panel-title");
            header.Add(_blueprintNameLabel);

            _targetLabel = new Label("Hedef: Ay");
            _targetLabel.AddToClassList("apogee-badge");
            _targetLabel.AddToClassList("apogee-badge--cyan");
            header.Add(_targetLabel);

            root.Add(header);

            // Main body area (3 columns: Slots | 2D Rocket Preview | Telemetry Panel)
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.marginTop = 12;

            // Left: Slot placeholder
            var slotColumn = new VisualElement();
            slotColumn.AddToClassList("apogee-panel");
            slotColumn.style.width = Length.Percent(25);
            slotColumn.style.marginRight = 8;
            var slotTitle = new Label("Bileşen Slotları");
            slotTitle.AddToClassList("apogee-panel-title");
            slotTitle.style.fontSize = 14;
            slotColumn.Add(slotTitle);

            var slotHint = new Label("M2-02 ile detaylı slot seçici burada yer alacak.");
            slotHint.AddToClassList("apogee-stat-label");
            slotHint.style.whiteSpace = WhiteSpace.Normal;
            slotHint.style.marginTop = 8;
            slotColumn.Add(slotHint);
            mainContent.Add(slotColumn);

            // Center: 2D Rocket / Craft cross-section placeholder
            var centerColumn = new VisualElement();
            centerColumn.AddToClassList("apogee-panel");
            centerColumn.AddToClassList("apogee-panel--surface");
            centerColumn.style.flexGrow = 1;
            centerColumn.style.marginRight = 8;
            centerColumn.style.alignItems = Align.Center;
            centerColumn.style.justifyContent = Justify.Center;

            var craftTitle = new Label("Roket ve Araç Kesit Görünümü");
            craftTitle.AddToClassList("apogee-panel-title");
            craftTitle.style.color = new UnityEngine.Color(0.22f, 0.74f, 0.97f);
            centerColumn.Add(craftTitle);

            var craftSubtitle = new Label("M2-03 ile modüler canlı montaj önizlemesi eklenecek.");
            craftSubtitle.AddToClassList("apogee-stat-label");
            craftSubtitle.style.marginTop = 6;
            centerColumn.Add(craftSubtitle);
            mainContent.Add(centerColumn);

            // Right: Telemetry & Result Panel (GDD §14.3)
            var telemetryColumn = new VisualElement();
            telemetryColumn.AddToClassList("apogee-panel");
            telemetryColumn.style.width = Length.Percent(30);

            var telemetryTitle = new Label("Canlı İstatistikler");
            telemetryTitle.AddToClassList("apogee-panel-title");
            telemetryTitle.style.fontSize = 14;
            telemetryColumn.Add(telemetryTitle);

            // Delta-V Bar
            var deltaVRow = new VisualElement();
            deltaVRow.AddToClassList("apogee-stat-row");
            deltaVRow.style.marginTop = 12;
            var deltaVLabel = new Label("Menzil (Δv)");
            deltaVLabel.AddToClassList("apogee-stat-label");
            _deltaVValueLabel = new Label("0 / 0 m/s");
            _deltaVValueLabel.AddToClassList("apogee-stat-value");
            deltaVRow.Add(deltaVLabel);
            deltaVRow.Add(_deltaVValueLabel);
            telemetryColumn.Add(deltaVRow);

            var barContainer = new VisualElement();
            barContainer.AddToClassList("apogee-bar-container");
            _deltaVBarFill = new VisualElement();
            _deltaVBarFill.AddToClassList("apogee-bar-fill");
            _deltaVBarFill.AddToClassList("apogee-bar-fill--green");
            barContainer.Add(_deltaVBarFill);
            telemetryColumn.Add(barContainer);

            // Attach Tooltip to Delta-V
            barContainer.AddManipulator(new TooltipManipulator(() =>
                TooltipData.CreateWithReason(
                    "Menzil (Δv)",
                    $"Mevcut: {ViewModel.TotalDeltaV.Value:N0} m/s / Gereken: {ViewModel.TargetDeltaV.Value:N0} m/s",
                    "Roketin yön ve hız değiştirebilme kapasitesi. Hedef yörünge ve iniş için yeterlidir.",
                    TooltipSeverity.Success)));

            // Mass Row
            var massRow = new VisualElement();
            massRow.AddToClassList("apogee-stat-row");
            var massLabel = new Label("Kütle (Islak)");
            massLabel.AddToClassList("apogee-stat-label");
            _massValueLabel = new Label("0 t");
            _massValueLabel.AddToClassList("apogee-stat-value");
            massRow.Add(massLabel);
            massRow.Add(_massValueLabel);
            telemetryColumn.Add(massRow);
            massRow.AddManipulator(new TooltipManipulator(() =>
                TooltipData.CreateSimple("Kütle", $"{ViewModel.TotalMassTons.Value:F1} ton. Fırlatıcı ve yakıt dahil toplam kalkış kütlesi.")));

            // Cost Row
            var costRow = new VisualElement();
            costRow.AddToClassList("apogee-stat-row");
            var costLabel = new Label("Tahmini Maliyet");
            costLabel.AddToClassList("apogee-stat-label");
            _costValueLabel = new Label("$0 M");
            _costValueLabel.AddToClassList("apogee-stat-value");
            costRow.Add(costLabel);
            costRow.Add(_costValueLabel);
            telemetryColumn.Add(costRow);
            costRow.AddManipulator(new TooltipManipulator(() =>
                TooltipData.CreateSimple("Maliyet", $"${ViewModel.TotalCostMillions.Value:F1}M. Tedarikçi çarpanları ve montaj dahil toplam bütçe.")));

            // Reliability Row
            var relRow = new VisualElement();
            relRow.AddToClassList("apogee-stat-row");
            var relLabel = new Label("Güvenilirlik");
            relLabel.AddToClassList("apogee-stat-label");
            _reliabilityValueLabel = new Label("%0");
            _reliabilityValueLabel.AddToClassList("apogee-stat-value");
            relRow.Add(relLabel);
            relRow.Add(_reliabilityValueLabel);
            telemetryColumn.Add(relRow);

            // Review button at bottom
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            telemetryColumn.Add(spacer);

            _reviewButton = new Button(() => ViewModel.GoToReviewCommand.Execute());
            _reviewButton.text = "Tasarım İncelemesine Git (Review) →";
            _reviewButton.AddToClassList("apogee-button");
            _reviewButton.AddToClassList("apogee-button--primary");
            _reviewButton.style.marginTop = 16;
            telemetryColumn.Add(_reviewButton);

            mainContent.Add(telemetryColumn);
            root.Add(mainContent);

            return root;
        }

        protected override void OnInitialized()
        {
            ViewModel.BlueprintName.Subscribe(val => _blueprintNameLabel.text = val);
            ViewModel.TargetDestination.Subscribe(val => _targetLabel.text = $"Hedef: {val}");

            ViewModel.TotalDeltaV.Subscribe(val =>
            {
                _deltaVValueLabel.text = $"{val:N0} / {ViewModel.TargetDeltaV.Value:N0} m/s";
                float ratio = (float)(val / System.Math.Max(1.0, ViewModel.TargetDeltaV.Value));
                _deltaVBarFill.style.width = Length.Percent(System.Math.Min(100f, ratio * 100f));
            });

            ViewModel.TotalMassTons.Subscribe(val => _massValueLabel.text = $"{val:F1} t");
            ViewModel.TotalCostMillions.Subscribe(val => _costValueLabel.text = $"${val:F1} M");
            ViewModel.ReliabilityPercent.Subscribe(val => _reliabilityValueLabel.text = $"%{val:F1}");
        }
    }
}
