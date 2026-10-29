using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Review
{
    /// <summary>
    /// Filtering categories for the Design Review issue list (GDD §7.4 & §21 M2-05).
    /// </summary>
    public enum ReviewFilterMode
    {
        All,
        Errors,
        Warnings,
        Passed
    }

    /// <summary>
    /// Represents a single validation finding entry in the Design Review traffic-light list (GDD §7.4).
    /// Provides plain language "Neden" (Reason) and "Neye bakmalı" (Hint) guidance in compliance with GDD §8.3.
    /// </summary>
    public sealed class ReviewIssueItem
    {
        public string Title        { get; }
        public string Description  { get; }
        public string Reason       { get; }
        public string Hint         { get; }
        /// <summary>"Error" | "Warning" | "Info"</summary>
        public string Severity     { get; }
        public string RuleId       { get; }
        public double? CurrentValue { get; }
        public double? TargetValue  { get; }

        public bool IsError   => string.Equals(Severity, "Error", StringComparison.OrdinalIgnoreCase);
        public bool IsWarning => string.Equals(Severity, "Warning", StringComparison.OrdinalIgnoreCase);
        public bool IsPass    => string.Equals(Severity, "Info", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(Severity, "Pass", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(Severity, "Success", StringComparison.OrdinalIgnoreCase);

        public string SeverityBadgeText => IsError ? "ENGEL" : IsWarning ? "UYARI" : "TAMAM";
        public string SeveritySymbol    => IsError ? "⛔" : IsWarning ? "▲" : "✓";

        public ReviewIssueItem(
            string title,
            string description,
            string reason,
            string severity,
            string ruleId = null,
            string hint = null,
            double? currentValue = null,
            double? targetValue = null)
        {
            Title        = title ?? string.Empty;
            Description  = description ?? string.Empty;
            Reason       = reason ?? string.Empty;
            Hint         = hint ?? (description != reason ? description : string.Empty);
            Severity     = severity ?? "Info";
            RuleId       = ruleId ?? string.Empty;
            CurrentValue = currentValue;
            TargetValue  = targetValue;
        }
    }

    /// <summary>
    /// ViewModel for the Design Review screen (GDD §7.4 &amp; §14.1 item 12, §21 M2-05).
    ///
    /// Accepts a <see cref="Blueprint"/> + <see cref="ValidationContext"/> to run
    /// the full 9-validator pipeline, producing:
    ///  1. A traffic-light issue list (Kırmızı: Engel, Sarı: Uyarı, Yeşil: Tamam)
    ///  2. Filter tabs (Tümü / Engeller / Uyarılar / Tamamlanan)
    ///  3. GO / NO-GO status decision and counts
    ///  4. 7 telemetry bars with color coding via <see cref="ResultsPanelViewModel"/>
    /// </summary>
    public class ReviewViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        // ── Observable state ───────────────────────────────────────────────────────

        public ObservableValue<string>           StatusSummary   { get; } = new("Tasarım İnceleme Bekleniyor");
        public ObservableValue<string>           GoNoGoBadgeText { get; } = new("UYGUN (GO)");
        public ObservableValue<bool>             IsValid         { get; } = new(true);
        public ObservableValue<int>              ErrorCount      { get; } = new(0);
        public ObservableValue<int>              WarningCount    { get; } = new(0);
        public ObservableValue<int>              PassCount       { get; } = new(0);
        public ObservableValue<ReviewFilterMode> CurrentFilter   { get; } = new(ReviewFilterMode.All);

        // ── Active Data & Findings ─────────────────────────────────────────────────

        public Blueprint         ActiveBlueprint { get; private set; }
        public ValidationContext CurrentContext  { get; private set; }

        public List<ReviewIssueItem> Issues { get; } = new();

        /// <summary>
        /// Issues filtered according to <see cref="CurrentFilter"/>.
        /// </summary>
        public IReadOnlyList<ReviewIssueItem> FilteredIssues
        {
            get
            {
                return CurrentFilter.Value switch
                {
                    ReviewFilterMode.Errors   => Issues.Where(i => i.IsError).ToList(),
                    ReviewFilterMode.Warnings => Issues.Where(i => i.IsWarning).ToList(),
                    ReviewFilterMode.Passed   => Issues.Where(i => i.IsPass).ToList(),
                    _                         => Issues
                };
            }
        }

        // ── Results Panel bars (GDD §8.3) ─────────────────────────────────────────

        /// <summary>
        /// Ordered list of 7 telemetry bars produced by <see cref="ResultsPanelViewModel.Build"/>.
        /// Populated from the ValidationReport.
        /// </summary>
        public IReadOnlyList<ResultBarViewModel> ResultBars { get; private set; } =
            Array.Empty<ResultBarViewModel>();

        // ── Events & Navigation ───────────────────────────────────────────────────

        public event Action OnIssuesChanged;

        public ViewActionCommand BackToWorkshopCommand { get; }

        // ── Constructor overloads ──────────────────────────────────────────────────

        /// <summary>
        /// Default / placeholder constructor (used when no blueprint is initially provided).
        /// </summary>
        public ReviewViewModel(IScreenNavigator navigator = null)
            : this(navigator, null, null) { }

        /// <summary>
        /// Full constructor: runs the validator pipeline and builds results bars.
        /// </summary>
        public ReviewViewModel(
            IScreenNavigator  navigator,
            Blueprint         blueprint,
            ValidationContext context)
        {
            _navigator = navigator;

            BackToWorkshopCommand = new ViewActionCommand(() =>
            {
                if (_navigator != null && _navigator.CanGoBack)
                    _navigator.GoBack();
                else
                    _navigator?.GoTo(ScreenId.Workshop);
            });

            if (blueprint != null)
            {
                RunValidation(blueprint, context ?? new ValidationContext());
            }
            else
            {
                LoadPlaceholderData();
            }
        }

        // ── Filter Management ──────────────────────────────────────────────────────

        public void SetFilter(ReviewFilterMode filter)
        {
            if (CurrentFilter.Value == filter) return;
            CurrentFilter.Value = filter;
            OnIssuesChanged?.Invoke();
        }

        // ── Validation pipeline ────────────────────────────────────────────────────

        /// <summary>
        /// Runs <see cref="BlueprintValidator"/> and populates issues + results bars.
        /// Re-evaluates whenever the vehicle design changes.
        /// </summary>
        public void RunValidation(Blueprint blueprint, ValidationContext context = null)
        {
            if (blueprint == null) return;

            ActiveBlueprint = blueprint;
            CurrentContext  = context ?? new ValidationContext();

            // --- Run validators (pure function) ---
            var report = BlueprintValidator.Validate(blueprint, CurrentContext);

            // --- Build results bars (pure factory) ---
            ResultBars = ResultsPanelViewModel.Build(report);

            // --- Map ValidationReport → ReviewIssueItems ---
            Issues.Clear();

            foreach (var issue in report.Issues)
            {
                var severity = issue.IsError   ? "Error"
                             : issue.IsWarning ? "Warning"
                             : "Info";

                var description = !string.IsNullOrEmpty(issue.Hint)
                    ? issue.Hint
                    : issue.Reason;

                Issues.Add(new ReviewIssueItem(
                    title:        issue.Title,
                    description:  description,
                    reason:       issue.Reason,
                    severity:     severity,
                    ruleId:       issue.RuleId,
                    hint:         issue.Hint,
                    currentValue: issue.CurrentValue,
                    targetValue:  issue.TargetValue));
            }

            // --- Update observable summary fields ---
            ErrorCount.Value   = report.ErrorCount;
            WarningCount.Value = report.WarningCount;
            PassCount.Value    = report.InfoCount;
            IsValid.Value      = report.IsValid;

            if (report.IsValid && !report.HasWarnings)
            {
                StatusSummary.Value   = "Tasarım Fırlatmaya Uygun";
                GoNoGoBadgeText.Value = "UYGUN (GO)";
            }
            else if (report.IsValid)
            {
                StatusSummary.Value   = $"Fırlatılabilir ama {report.WarningCount} uyarı var — dikkat edin";
                GoNoGoBadgeText.Value = "ŞARTLI GO (UYARILARLA)";
            }
            else
            {
                StatusSummary.Value   = $"{report.ErrorCount} engel var — tasarımı düzeltin";
                GoNoGoBadgeText.Value = "ENGEL VAR (NO-GO)";
            }

            OnIssuesChanged?.Invoke();
        }

        // ── Placeholder ────────────────────────────────────────────────────────────

        private void LoadPlaceholderData()
        {
            Issues.Clear();

            Issues.Add(new ReviewIssueItem(
                title:       "Menzil İhtiyacı Karşılandı",
                description: "İtki konfigürasyonu hedef için uygun menzile sahip.",
                reason:      "Roket Delta-V kapasitesi hedef Ay iniş gereksiniminin (13 500 m/s) üzerindedir.",
                severity:    "Info",
                ruleId:      ValidationRuleId.DeltaV,
                hint:        "Uygun güvenlik marjı (%12.4) korunmaktadır."));

            Issues.Add(new ReviewIssueItem(
                title:       "Düşük Güç Marjı Uyarısı",
                description: "Ay kutup gölgesinde batarya kapasitesi %15 sınırındadır.",
                reason:      "Gölge geçişlerinde batarya rezervi kritik seviyeye inebilir.",
                severity:    "Warning",
                ruleId:      ValidationRuleId.Power,
                hint:        "Gölge süresince bilimsel aletlerin kapatılması veya batarya modülü eklenmesi gerekebilir."));

            Issues.Add(new ReviewIssueItem(
                title:       "Rampa Altyapısı Uyumlu",
                description: "Fırlatma kulesi ve rampa tesisinde uyumsuzluk yoktur.",
                reason:      "Araç Seviye 2 rampa tesis standartlarıyla tam uyumludur.",
                severity:    "Info",
                ruleId:      ValidationRuleId.LaunchPad,
                hint:        "Mevcut kule ve yer altyapısı bu 3 aşamalı roketi desteklemektedir."));

            ErrorCount.Value      = 0;
            WarningCount.Value    = 1;
            PassCount.Value       = 2;
            IsValid.Value         = true;
            StatusSummary.Value   = "Tasarım Fırlatmaya Uygun (Örnek Veri)";
            GoNoGoBadgeText.Value = "ŞARTLI GO (UYARILARLA)";

            var stubStats = new BlueprintStats(new StatCollection());
            ResultBars    = ResultsPanelViewModel.Build(stubStats);

            OnIssuesChanged?.Invoke();
        }
    }
}
