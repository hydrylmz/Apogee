using System;
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
    /// Screen controller for Design Review (GDD §7.4 &amp; §14.1 item 12, §21 M2-05).
    ///
    /// Layout (GDD §14.3):
    ///   Header  — Blueprint name · GO / NO-GO badge · error/warning/pass counts
    ///   Left    — Results Panel (7 telemetry bars with traffic-light color + "why?" tooltip)
    ///   Right   — Traffic-light issue list (Kırmızı: Engel, Sarı: Uyarı, Yeşil: Tamam)
    ///             with interactive category filtering (Tümü / Engeller / Uyarılar / Tamamlanan)
    ///   Footer  — Back to Workshop button
    ///
    /// Design rules (GDD §8.3 &amp; §7.4):
    ///   - Formulas and raw coefficients are never shown to the player.
    ///   - Each finding explicitly states "Neden" (Reason) and "Neye bakmalı" (Hint).
    ///   - Real-time updates when navigating from the Workshop with active vehicle designs.
    /// </summary>
    public class ReviewScreenController : ScreenControllerBase<ReviewViewModel>
    {
        public override ScreenId ScreenId => ScreenId.DesignReview;

        // ── Header elements ────────────────────────────────────────────────────────
        private Label  _statusLabel;
        private Label  _summaryBadge;
        private Label  _errorCountBadge;
        private Label  _warnCountBadge;
        private Label  _passCountBadge;

        // ── Results panel (left column) ────────────────────────────────────────────
        private VisualElement        _resultsBarsContainer;
        private List<ResultsBarView> _barViews = new();

        // ── Issue list & Filters (right column) ────────────────────────────────────
        private VisualElement _filterToolbar;
        private Button        _filterAllBtn;
        private Button        _filterErrorsBtn;
        private Button        _filterWarningsBtn;
        private Button        _filterPassedBtn;
        private ScrollView    _issueListContainer;

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
            _summaryBadge = new Label(ViewModel.GoNoGoBadgeText.Value);
            _summaryBadge.AddToClassList("apogee-badge");
            UpdateGoNoGoBadgeStyle(ViewModel.GoNoGoBadgeText.Value);
            _summaryBadge.style.marginRight = 8;
            headerBadges.Add(_summaryBadge);

            // Error count badge (Kırmızı)
            _errorCountBadge = new Label($"{ViewModel.ErrorCount.Value} Engel");
            _errorCountBadge.AddToClassList("apogee-badge");
            _errorCountBadge.AddToClassList("apogee-badge--red");
            _errorCountBadge.style.marginRight = 4;
            _errorCountBadge.style.display     = ViewModel.ErrorCount.Value > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            headerBadges.Add(_errorCountBadge);

            // Warning count badge (Sarı)
            _warnCountBadge = new Label($"{ViewModel.WarningCount.Value} Uyarı");
            _warnCountBadge.AddToClassList("apogee-badge");
            _warnCountBadge.AddToClassList("apogee-badge--amber");
            _warnCountBadge.style.marginRight = 4;
            _warnCountBadge.style.display     = ViewModel.WarningCount.Value > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            headerBadges.Add(_warnCountBadge);

            // Pass count badge (Yeşil)
            _passCountBadge = new Label($"{ViewModel.PassCount.Value} Tamam");
            _passCountBadge.AddToClassList("apogee-badge");
            _passCountBadge.AddToClassList("apogee-badge--green");
            headerBadges.Add(_passCountBadge);

            header.Add(headerBadges);
            root.Add(header);

            // ── STATUS TEXT ───────────────────────────────────────────────────────
            _statusLabel = new Label(ViewModel.StatusSummary.Value);
            _statusLabel.AddToClassList("apogee-panel-title");
            _statusLabel.style.fontSize   = 13;
            _statusLabel.style.marginTop  = 10;
            _statusLabel.style.marginLeft = 4;
            root.Add(_statusLabel);

            // ── BODY (two-column) ─────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow      = 1;
            body.style.marginTop     = 10;

            // ── LEFT — Results Panel (GDD §8.3 & §14.3) ──────────────────────────
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("apogee-panel");
            leftPanel.style.width       = Length.Percent(38);
            leftPanel.style.marginRight = 10;

            var resultsTitle = new Label("İstatistik Özeti (Telemetri)");
            resultsTitle.AddToClassList("apogee-panel-title");
            resultsTitle.style.fontSize     = 13;
            resultsTitle.style.marginBottom = 10;
            leftPanel.Add(resultsTitle);

            _resultsBarsContainer = new VisualElement();
            _resultsBarsContainer.style.flexGrow = 1;
            BuildResultBars();
            leftPanel.Add(_resultsBarsContainer);

            body.Add(leftPanel);

            // ── RIGHT — Issue List & Filters (GDD §7.4 & §21 M2-05) ──────────────
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("apogee-panel");
            rightPanel.style.flexGrow = 1;

            var issuesTitle = new Label("Doğrulama Bulguları (Kırmızı / Sarı / Yeşil)");
            issuesTitle.AddToClassList("apogee-panel-title");
            issuesTitle.style.fontSize     = 13;
            issuesTitle.style.marginBottom = 8;
            rightPanel.Add(issuesTitle);

            // Filter Toolbar (All / Errors / Warnings / Passed)
            _filterToolbar = new VisualElement();
            _filterToolbar.AddToClassList("apogee-review-filter-bar");
            _filterToolbar.style.flexDirection  = FlexDirection.Row;
            _filterToolbar.style.marginBottom   = 8;
            _filterToolbar.style.alignItems     = Align.Center;

            _filterAllBtn = CreateFilterButton("Tümü", ReviewFilterMode.All);
            _filterErrorsBtn = CreateFilterButton("⛔ Engeller", ReviewFilterMode.Errors);
            _filterWarningsBtn = CreateFilterButton("▲ Uyarılar", ReviewFilterMode.Warnings);
            _filterPassedBtn = CreateFilterButton("✓ Tamamlanan", ReviewFilterMode.Passed);

            _filterToolbar.Add(_filterAllBtn);
            _filterToolbar.Add(_filterErrorsBtn);
            _filterToolbar.Add(_filterWarningsBtn);
            _filterToolbar.Add(_filterPassedBtn);
            rightPanel.Add(_filterToolbar);

            // Scrollable Issue List Container
            _issueListContainer = new ScrollView();
            _issueListContainer.style.flexGrow = 1;
            rightPanel.Add(_issueListContainer);

            BuildIssueList();
            UpdateFilterToolbarState();

            body.Add(rightPanel);
            root.Add(body);

            // ── FOOTER ────────────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.flexDirection  = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.marginTop      = 12;

            _backButton      = new Button(() => ViewModel.BackToWorkshopCommand.Execute());
            _backButton.text = "← Atölyeye Geri Dön";
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

            ViewModel.GoNoGoBadgeText.Subscribe(val =>
            {
                _summaryBadge.text = val;
                UpdateGoNoGoBadgeStyle(val);
            });

            ViewModel.ErrorCount.Subscribe(count =>
            {
                _errorCountBadge.text = $"{count} Engel";
                _errorCountBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateFilterToolbarState();
            });

            ViewModel.WarningCount.Subscribe(count =>
            {
                _warnCountBadge.text = $"{count} Uyarı";
                _warnCountBadge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateFilterToolbarState();
            });

            ViewModel.PassCount.Subscribe(count =>
            {
                _passCountBadge.text = $"{count} Tamam";
                UpdateFilterToolbarState();
            });

            ViewModel.OnIssuesChanged += RefreshView;
        }

        public override void Dispose()
        {
            if (ViewModel != null)
            {
                ViewModel.OnIssuesChanged -= RefreshView;
            }
            base.Dispose();
        }

        protected override void OnShown(object context)
        {
            base.OnShown(context);

            if (context is ReviewNavigationArgs args)
            {
                ViewModel.RunValidation(args.Blueprint, args.Context);
            }
            else if (context is Blueprint bp)
            {
                ViewModel.RunValidation(bp);
            }

            RefreshView();
        }

        private void RefreshView()
        {
            BuildResultBars();
            BuildIssueList();
            UpdateFilterToolbarState();
        }

        // ── Filter Toolbar ─────────────────────────────────────────────────────────

        private Button CreateFilterButton(string label, ReviewFilterMode mode)
        {
            var btn = new Button(() => ViewModel.SetFilter(mode));
            btn.text = label;
            btn.AddToClassList("apogee-button");
            btn.AddToClassList("apogee-review-filter-btn");
            btn.style.marginRight = 6;
            btn.style.fontSize    = 11;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop   = 3;
            btn.style.paddingBottom = 3;
            return btn;
        }

        private void UpdateFilterToolbarState()
        {
            if (_filterToolbar == null) return;

            var cur = ViewModel.CurrentFilter.Value;

            _filterAllBtn.text      = $"Tümü ({ViewModel.Issues.Count})";
            _filterErrorsBtn.text   = $"⛔ Engeller ({ViewModel.ErrorCount.Value})";
            _filterWarningsBtn.text = $"▲ Uyarılar ({ViewModel.WarningCount.Value})";
            _filterPassedBtn.text   = $"✓ Tamam ({ViewModel.PassCount.Value})";

            SetButtonActive(_filterAllBtn,      cur == ReviewFilterMode.All);
            SetButtonActive(_filterErrorsBtn,   cur == ReviewFilterMode.Errors);
            SetButtonActive(_filterWarningsBtn, cur == ReviewFilterMode.Warnings);
            SetButtonActive(_filterPassedBtn,   cur == ReviewFilterMode.Passed);
        }

        private static void SetButtonActive(Button btn, bool isActive)
        {
            if (isActive)
            {
                btn.AddToClassList("apogee-review-filter-btn--active");
                btn.style.borderBottomWidth = 2;
                btn.style.borderBottomColor = new Color(0.02f, 0.71f, 0.83f); // cyan
            }
            else
            {
                btn.RemoveFromClassList("apogee-review-filter-btn--active");
                btn.style.borderBottomWidth = 0;
            }
        }

        private void UpdateGoNoGoBadgeStyle(string badgeText)
        {
            _summaryBadge.RemoveFromClassList("apogee-badge--green");
            _summaryBadge.RemoveFromClassList("apogee-badge--amber");
            _summaryBadge.RemoveFromClassList("apogee-badge--red");

            if (badgeText.Contains("NO-GO") || badgeText.Contains("ENGEL"))
            {
                _summaryBadge.AddToClassList("apogee-badge--red");
            }
            else if (badgeText.Contains("ŞARTLI") || badgeText.Contains("UYARI"))
            {
                _summaryBadge.AddToClassList("apogee-badge--amber");
            }
            else
            {
                _summaryBadge.AddToClassList("apogee-badge--green");
            }
        }

        // ── Results bars builder ───────────────────────────────────────────────────

        private void BuildResultBars()
        {
            if (_resultsBarsContainer == null) return;

            _resultsBarsContainer.Clear();
            _barViews.Clear();

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
            if (_issueListContainer == null) return;

            _issueListContainer.Clear();

            var filtered = ViewModel.FilteredIssues;

            if (filtered.Count == 0)
            {
                var emptyContainer = new VisualElement();
                emptyContainer.style.paddingTop   = 16;
                emptyContainer.style.paddingLeft  = 8;
                emptyContainer.style.alignItems   = Align.Center;

                string emptyMessage = ViewModel.CurrentFilter.Value switch
                {
                    ReviewFilterMode.Errors   => "✓ Fırlatmayı engelleyen hiçbir hata bulunmuyor. Tasarım güvenli görünüyor.",
                    ReviewFilterMode.Warnings => "✓ Hiçbir sistem uyarısı bulunmuyor. Tüm marjlar ideal aralıkta.",
                    ReviewFilterMode.Passed   => "Henüz başarıyla tamamlanmış kontrol kaydı bulunmuyor.",
                    _                         => "Tüm kontroller geçti — tasarım fırlatmaya hazır!"
                };

                var emptyLabel = new Label(emptyMessage);
                emptyLabel.AddToClassList("apogee-stat-label");
                emptyLabel.style.color                      = new Color(0.43f, 0.91f, 0.72f); // green
                emptyLabel.style.unityFontStyleAndWeight    = FontStyle.Italic;
                emptyLabel.style.whiteSpace                 = WhiteSpace.Normal;
                emptyLabel.style.fontSize                   = 12;

                emptyContainer.Add(emptyLabel);
                _issueListContainer.Add(emptyContainer);
                return;
            }

            foreach (var issue in filtered)
            {
                _issueListContainer.Add(BuildIssueCard(issue));
            }
        }

        private VisualElement BuildIssueCard(ReviewIssueItem issue)
        {
            var card = new VisualElement();
            card.AddToClassList("apogee-panel");
            card.AddToClassList("apogee-panel--surface");
            card.AddToClassList("apogee-review-card");
            card.style.marginBottom  = 8;
            card.style.paddingTop    = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft   = 12;
            card.style.paddingRight  = 12;

            // Severity left-border accent & style class (GDD §7.4: Kırmızı / Sarı / Yeşil)
            if (issue.IsError)
            {
                card.AddToClassList("apogee-review-card--error");
                card.style.borderLeftColor = new Color(0.94f, 0.27f, 0.27f); // red
                card.style.borderLeftWidth = 4;
            }
            else if (issue.IsWarning)
            {
                card.AddToClassList("apogee-review-card--warning");
                card.style.borderLeftColor = new Color(0.96f, 0.62f, 0.04f); // amber
                card.style.borderLeftWidth = 4;
            }
            else
            {
                card.AddToClassList("apogee-review-card--pass");
                card.style.borderLeftColor = new Color(0.06f, 0.73f, 0.51f); // green
                card.style.borderLeftWidth = 4;
            }

            // Card Header row: [Icon + Title] ................... [Severity Badge]
            var cardHeader = new VisualElement();
            cardHeader.style.flexDirection  = FlexDirection.Row;
            cardHeader.style.justifyContent = Justify.SpaceBetween;
            cardHeader.style.alignItems     = Align.Center;

            var titleWithIcon = $"{issue.SeveritySymbol}  {issue.Title}";
            var cardTitle = new Label(titleWithIcon);
            cardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            cardTitle.style.fontSize               = 12;
            cardHeader.Add(cardTitle);

            var badge = new Label(issue.SeverityBadgeText);
            badge.AddToClassList("apogee-badge");
            badge.AddToClassList(SeverityBadgeCss(issue.Severity));
            cardHeader.Add(badge);

            card.Add(cardHeader);

            // Neden (Reason) section (GDD §7.4 & §8.3)
            if (!string.IsNullOrEmpty(issue.Reason))
            {
                var reasonRow = new VisualElement();
                reasonRow.style.marginTop = 4;

                var reasonLabel = new Label($"Neden: {issue.Reason}");
                reasonLabel.AddToClassList("apogee-stat-label");
                reasonLabel.AddToClassList("apogee-review-reason");
                reasonLabel.style.whiteSpace = WhiteSpace.Normal;
                reasonLabel.style.fontSize   = 11;
                reasonRow.Add(reasonLabel);

                card.Add(reasonRow);
            }

            // Neye bakmalı (Hint) section (GDD §7.4 & §8.3)
            if (!string.IsNullOrEmpty(issue.Hint))
            {
                var hintRow = new VisualElement();
                hintRow.style.marginTop = 3;

                var hintLabel = new Label($"Neye bakmalı: {issue.Hint}");
                hintLabel.AddToClassList("apogee-tooltip-reason");
                hintLabel.AddToClassList("apogee-review-hint");
                hintLabel.style.whiteSpace               = WhiteSpace.Normal;
                hintLabel.style.unityFontStyleAndWeight  = FontStyle.Italic;
                hintLabel.style.fontSize                 = 10;
                hintLabel.style.color                    = new Color(0.60f, 0.65f, 0.72f);
                hintRow.Add(hintLabel);

                card.Add(hintRow);
            }

            // Interactive Tooltip on hover
            var tooltipSeverity = issue.IsError   ? TooltipSeverity.Error
                                : issue.IsWarning ? TooltipSeverity.Warning
                                : TooltipSeverity.Info;

            card.AddManipulator(new TooltipManipulator(() =>
                TooltipData.CreateWithReason(
                    issue.Title,
                    issue.Reason,
                    issue.Hint,
                    tooltipSeverity)));

            return card;
        }

        private static string SeverityBadgeCss(string severity)
        {
            if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase))
                return "apogee-badge--red";
            if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase))
                return "apogee-badge--amber";
            return "apogee-badge--green";
        }
    }
}
