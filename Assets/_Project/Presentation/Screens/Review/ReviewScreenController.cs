using System.Collections.Generic;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Screens.Results;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Review
{
    /// <summary>
    /// Screen controller for Design Review (GDD §7.4 &amp; §14.1 item 12).
    ///
    /// Layout (GDD §14.3):
    ///   Header  — Blueprint name · GO / NO-GO badge · error/warning counts
    ///   Left    — Results Panel (7 telemetry bars with traffic-light color + "why?" tooltip)
    ///   Right   — Issue list (red/amber/cyan cards with reason + hint)
    ///   Footer  — Back to Workshop button
    ///
    /// Design rules (GDD §8.3):
    ///   - Formulas and raw coefficients are never shown to the player.
    ///   - Each bar shows outcome value + color; detailed "why?" is in the tooltip.
    ///   - Each issue card shows title + plain-language reason + hint.
    /// </summary>
    public class ReviewScreenController : ScreenControllerBase<ReviewViewModel>
    {
        public override ScreenId ScreenId => ScreenId.DesignReview;

        // ── Header elements ────────────────────────────────────────────────────────
        private Label          _statusLabel;
        private Label          _summaryBadge;
        private Label          _errorCountBadge;
        private Label          _warnCountBadge;

        // ── Results panel (left column) ────────────────────────────────────────────
        private VisualElement               _resultsBarsContainer;
        private List<ResultsBarView>        _barViews;

        // ── Issue list (right column) ──────────────────────────────────────────────
        private ScrollView _issueListContainer;

        // ── Footer ─────────────────────────────────────────────────────────────────
        private Button _backButton;

        public ReviewScreenController(ReviewViewModel viewModel) : base(viewModel) { }

        // ── View construction ──────────────────────────────────────────────────────

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop    = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft   = 20;
            root.style.paddingRight  = 20;

            // ── HEADER ────────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");

            var title = new Label("Tasarım İnceleme Raporu (Design Review)");
            title.AddToClassList("apogee-panel-title");
            header.Add(title);

            var headerBadges = new VisualElement();
            headerBadges.style.flexDirection = FlexDirection.Row;
            headerBadges.style.alignItems    = Align.Center;

            // GO / NO-GO traffic-light badge
            _summaryBadge = new Label("UYGUN (GO)");
            _summaryBadge.AddToClassList("apogee-badge");
            _summaryBadge.AddToClassList("apogee-badge--green");
            _summaryBadge.style.marginRight = 8;
            headerBadges.Add(_summaryBadge);

            // Error count badge
            _errorCountBadge = new Label("0 Engel");
            _errorCountBadge.AddToClassList("apogee-badge");
            _errorCountBadge.AddToClassList("apogee-badge--red");
            _errorCountBadge.style.marginRight = 4;
            headerBadges.Add(_errorCountBadge);

            // Warning count badge
            _warnCountBadge = new Label("0 Uyarı");
            _warnCountBadge.AddToClassList("apogee-badge");
            _warnCountBadge.AddToClassList("apogee-badge--amber");
            headerBadges.Add(_warnCountBadge);

            header.Add(headerBadges);
            root.Add(header);

            // ── STATUS TEXT ───────────────────────────────────────────────────────
            _statusLabel = new Label(ViewModel.StatusSummary.Value);
            _statusLabel.AddToClassList("apogee-panel-title");
            _statusLabel.style.fontSize   = 14;
            _statusLabel.style.marginTop  = 10;
            _statusLabel.style.marginLeft = 4;
            root.Add(_statusLabel);

            // ── BODY (two-column) ─────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow      = 1;
            body.style.marginTop     = 10;

            // ── LEFT — Results Panel (GDD §8.3) ──────────────────────────────────
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("apogee-panel");
            leftPanel.style.width       = Length.Percent(40);
            leftPanel.style.marginRight = 10;

            var resultsTitle = new Label("İstatistik Özeti");
            resultsTitle.AddToClassList("apogee-panel-title");
            resultsTitle.style.fontSize    = 13;
            resultsTitle.style.marginBottom = 10;
            leftPanel.Add(resultsTitle);

            _resultsBarsContainer = new VisualElement();
            _resultsBarsContainer.style.flexGrow = 1;
            BuildResultBars();
            leftPanel.Add(_resultsBarsContainer);

            body.Add(leftPanel);

            // ── RIGHT — Issue List (GDD §7.4 red/amber/green cards) ──────────────
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("apogee-panel");
            rightPanel.style.flexGrow = 1;

            var issuesTitle = new Label("Doğrulama Bulguları");
            issuesTitle.AddToClassList("apogee-panel-title");
            issuesTitle.style.fontSize     = 13;
            issuesTitle.style.marginBottom = 10;
            rightPanel.Add(issuesTitle);

            _issueListContainer = new ScrollView();
            _issueListContainer.style.flexGrow = 1;
            rightPanel.Add(_issueListContainer);

            BuildIssueList();

            body.Add(rightPanel);
            root.Add(body);

            // ── FOOTER ────────────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.flexDirection  = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.marginTop      = 14;

            _backButton       = new Button(() => ViewModel.BackToWorkshopCommand.Execute());
            _backButton.text  = "← Atölyeye Geri Dön";
            _backButton.AddToClassList("apogee-button");
            _backButton.AddToClassList("apogee-button--secondary");
            footer.Add(_backButton);

            root.Add(footer);
            return root;
        }

        // ── Subscriptions ──────────────────────────────────────────────────────────

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

            ViewModel.ErrorCount.Subscribe(count =>
            {
                _errorCountBadge.text = $"{count} Engel";
                _errorCountBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            });

            ViewModel.WarningCount.Subscribe(count =>
            {
                _warnCountBadge.text = $"{count} Uyarı";
                _warnCountBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }

        // ── Results bars builder ───────────────────────────────────────────────────

        private void BuildResultBars()
        {
            _resultsBarsContainer.Clear();
            _barViews = new List<ResultsBarView>();

            foreach (var bar in ViewModel.ResultBars)
            {
                var barView = new ResultsBarView(bar);
                barView.style.marginBottom = 8;
                _resultsBarsContainer.Add(barView);
                _barViews.Add(barView);
            }
        }

        // ── Issue list builder ─────────────────────────────────────────────────────

        private void BuildIssueList()
        {
            _issueListContainer.Clear();

            if (ViewModel.Issues.Count == 0)
            {
                var emptyLabel = new Label("Tüm kontroller geçti — sorun bulunamadı.");
                emptyLabel.AddToClassList("apogee-stat-label");
                emptyLabel.style.marginTop     = 8;
                emptyLabel.style.color         = new Color(0.43f, 0.91f, 0.72f); // green-light
                emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                emptyLabel.style.whiteSpace    = WhiteSpace.Normal;
                _issueListContainer.Add(emptyLabel);
                return;
            }

            foreach (var issue in ViewModel.Issues)
            {
                _issueListContainer.Add(BuildIssueCard(issue));
            }
        }

        private VisualElement BuildIssueCard(ReviewIssueItem issue)
        {
            var card = new VisualElement();
            card.AddToClassList("apogee-panel");
            card.AddToClassList("apogee-panel--surface");
            card.style.marginBottom  = 8;
            card.style.paddingTop    = 8;
            card.style.paddingBottom = 8;

            // Left accent border to reinforce severity color
            if (issue.Severity == "Error")
            {
                card.style.borderLeftColor = new Color(0.94f, 0.27f, 0.27f);
                card.style.borderLeftWidth = 3;
            }
            else if (issue.Severity == "Warning")
            {
                card.style.borderLeftColor = new Color(0.96f, 0.62f, 0.04f);
                card.style.borderLeftWidth = 3;
            }
            else
            {
                card.style.borderLeftColor = new Color(0.02f, 0.71f, 0.83f);
                card.style.borderLeftWidth = 3;
            }

            // Header row: title + severity badge
            var cardHeader = new VisualElement();
            cardHeader.style.flexDirection  = FlexDirection.Row;
            cardHeader.style.justifyContent = Justify.SpaceBetween;
            cardHeader.style.alignItems     = Align.Center;

            var cardTitle = new Label(issue.Title);
            cardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            cardTitle.style.fontSize = 12;
            cardHeader.Add(cardTitle);

            var badge = new Label(SeverityLabel(issue.Severity));
            badge.AddToClassList("apogee-badge");
            badge.AddToClassList(SeverityBadgeCss(issue.Severity));
            cardHeader.Add(badge);

            card.Add(cardHeader);

            // Description (plain language, GDD §8.3)
            var descLabel = new Label(issue.Description);
            descLabel.AddToClassList("apogee-stat-label");
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginTop  = 4;
            card.Add(descLabel);

            // Reason hint (why + what to look at)
            if (!string.IsNullOrEmpty(issue.Reason) && issue.Reason != issue.Description)
            {
                var reasonLabel = new Label($"İpucu: {issue.Reason}");
                reasonLabel.AddToClassList("apogee-tooltip-reason");
                card.Add(reasonLabel);
            }

            // Tooltip on hover
            var severity = issue.Severity == "Error"   ? TooltipSeverity.Error
                         : issue.Severity == "Warning" ? TooltipSeverity.Warning
                         : TooltipSeverity.Info;

            card.AddManipulator(new TooltipManipulator(() =>
                TooltipData.CreateWithReason(
                    issue.Title,
                    issue.Description,
                    issue.Reason,
                    severity)));

            return card;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static string SeverityLabel(string severity) => severity switch
        {
            "Error"   => "ENGEL",
            "Warning" => "UYARI",
            _         => "BİLGİ"
        };

        private static string SeverityBadgeCss(string severity) => severity switch
        {
            "Error"   => "apogee-badge--red",
            "Warning" => "apogee-badge--amber",
            _         => "apogee-badge--cyan"
        };
    }
}
