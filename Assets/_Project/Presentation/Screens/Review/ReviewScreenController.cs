using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Review
{
    /// <summary>
    /// Screen controller for Design Review (GDD §7.4 & §14.1).
    /// Displays validation report, issues, and Go/No-Go status with return navigation.
    /// </summary>
    public class ReviewScreenController : ScreenControllerBase<ReviewViewModel>
    {
        public override ScreenId ScreenId => ScreenId.DesignReview;

        private Label _statusLabel;
        private Label _summaryBadge;
        private ScrollView _issueListContainer;
        private Button _backButton;

        public ReviewScreenController(ReviewViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // Top Header
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");

            var title = new Label("Tasarım İnceleme Raporu (Design Review)");
            title.AddToClassList("apogee-panel-title");
            header.Add(title);

            _summaryBadge = new Label("UYGUN (GO)");
            _summaryBadge.AddToClassList("apogee-badge");
            _summaryBadge.AddToClassList("apogee-badge--green");
            header.Add(_summaryBadge);

            root.Add(header);

            // Body
            var bodyPanel = new VisualElement();
            bodyPanel.AddToClassList("apogee-panel");
            bodyPanel.style.flexGrow = 1;
            bodyPanel.style.marginTop = 12;

            _statusLabel = new Label();
            _statusLabel.AddToClassList("apogee-panel-title");
            _statusLabel.style.fontSize = 16;
            _statusLabel.style.marginBottom = 12;
            bodyPanel.Add(_statusLabel);

            _issueListContainer = new ScrollView();
            _issueListContainer.style.flexGrow = 1;
            bodyPanel.Add(_issueListContainer);

            // Populate issue items
            foreach (var issue in ViewModel.Issues)
            {
                var itemBox = new VisualElement();
                itemBox.AddToClassList("apogee-panel");
                itemBox.AddToClassList("apogee-panel--surface");
                itemBox.style.marginBottom = 8;
                itemBox.style.paddingTop = 8;
                itemBox.style.paddingBottom = 8;

                var itemHeader = new VisualElement();
                itemHeader.style.flexDirection = FlexDirection.Row;
                itemHeader.style.justifyContent = Justify.SpaceBetween;

                var itemTitle = new Label(issue.Title);
                itemTitle.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                itemHeader.Add(itemTitle);

                var badge = new Label(issue.Severity);
                badge.AddToClassList("apogee-badge");
                if (issue.Severity == "Error")
                {
                    badge.AddToClassList("apogee-badge--red");
                }
                else if (issue.Severity == "Warning")
                {
                    badge.AddToClassList("apogee-badge--amber");
                }
                else
                {
                    badge.AddToClassList("apogee-badge--cyan");
                }
                itemHeader.Add(badge);
                itemBox.Add(itemHeader);

                var itemDesc = new Label(issue.Description);
                itemDesc.AddToClassList("apogee-stat-label");
                itemDesc.style.whiteSpace = WhiteSpace.Normal;
                itemDesc.style.marginTop = 4;
                itemBox.Add(itemDesc);

                if (!string.IsNullOrEmpty(issue.Reason))
                {
                    var itemReason = new Label($"İpucu: {issue.Reason}");
                    itemReason.AddToClassList("apogee-tooltip-reason");
                    itemBox.Add(itemReason);
                }

                // Attach tooltip
                itemBox.AddManipulator(new TooltipManipulator(() =>
                    TooltipData.CreateWithReason(
                        issue.Title,
                        issue.Description,
                        issue.Reason,
                        issue.Severity == "Error" ? TooltipSeverity.Error : TooltipSeverity.Warning)));

                _issueListContainer.Add(itemBox);
            }

            // Footer with Back button
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexStart;
            footer.style.marginTop = 16;

            _backButton = new Button(() => ViewModel.BackToWorkshopCommand.Execute());
            _backButton.text = "← Atölyeye Geri Dön";
            _backButton.AddToClassList("apogee-button");
            _backButton.AddToClassList("apogee-button--secondary");
            footer.Add(_backButton);

            bodyPanel.Add(footer);
            root.Add(bodyPanel);

            return root;
        }

        protected override void OnInitialized()
        {
            ViewModel.StatusSummary.Subscribe(val => _statusLabel.text = val);
            ViewModel.IsValid.Subscribe(valid =>
            {
                _summaryBadge.text = valid ? "UYGUN (GO)" : "ENGEL VAR (NO-GO)";
                _summaryBadge.RemoveFromClassList("apogee-badge--green");
                _summaryBadge.RemoveFromClassList("apogee-badge--red");
                _summaryBadge.AddToClassList(valid ? "apogee-badge--green" : "apogee-badge--red");
            });
        }
    }
}
