using System.Collections.Generic;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Review
{
    /// <summary>
    /// Represents a single validation finding entry in the Design Review list (GDD §7.4).
    /// </summary>
    public sealed class ReviewIssueItem
    {
        public string Title       { get; }
        public string Description { get; }
        public string Reason      { get; }
        /// <summary>"Error" | "Warning" | "Info"</summary>
        public string Severity    { get; }

        public ReviewIssueItem(string title, string description, string reason, string severity)
        {
            Title       = title;
            Description = description;
            Reason      = reason;
            Severity    = severity;
        }
    }

    /// <summary>
    /// ViewModel for the Design Review screen (GDD §7.4 &amp; §14.1 item 12).
    ///
    /// Accepts an optional <see cref="Blueprint"/> + <see cref="ValidationContext"/> to run
    /// the real validator pipeline, producing both:
    ///  1. A traffic-light issue list (Kırmızı/Sarı/Yeşil)
    ///  2. A Results Panel bar list via <see cref="ResultsPanelViewModel"/>
    ///
    /// Falls back to placeholder data when no blueprint is supplied (e.g. during UI tests
    /// that don't need a full game state).
    /// </summary>
    public class ReviewViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        // ── Observable state ───────────────────────────────────────────────────────

        public ObservableValue<string> StatusSummary { get; } = new("Tasarım İnceleme Bekleniyor");
        public ObservableValue<bool>   IsValid       { get; } = new(true);
        public ObservableValue<int>    ErrorCount    { get; } = new(0);
        public ObservableValue<int>    WarningCount  { get; } = new(0);

        // ── Issue list (Design Review red/amber/green list) ────────────────────────

        public List<ReviewIssueItem> Issues { get; } = new();

        // ── Results Panel bars (GDD §8.3) ─────────────────────────────────────────

        /// <summary>
        /// Ordered list of telemetry bars produced by <see cref="ResultsPanelViewModel.Build"/>.
        /// Populated from the ValidationReport; empty when no blueprint is provided.
        /// </summary>
        public IReadOnlyList<ResultBarViewModel> ResultBars { get; private set; } =
            System.Array.Empty<ResultBarViewModel>();

        // ── Navigation ─────────────────────────────────────────────────────────────

        public ViewActionCommand BackToWorkshopCommand { get; }

        // ── Constructor overloads ──────────────────────────────────────────────────

        /// <summary>
        /// Placeholder constructor (no real blueprint) — used in early M2 UI tests.
        /// </summary>
        public ReviewViewModel(IScreenNavigator navigator = null)
            : this(navigator, null, null) { }

        /// <summary>
        /// Full constructor: runs the validator pipeline and builds results bars.
        /// </summary>
        /// <param name="navigator">Screen navigator for back-navigation.</param>
        /// <param name="blueprint">Blueprint to validate. If null, placeholder data is shown.</param>
        /// <param name="context">
        /// Validation context (mission profile, launch pad). If null a default context is used.
        /// </param>
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

        // ── Validation pipeline ────────────────────────────────────────────────────

        /// <summary>
        /// Runs <see cref="BlueprintValidator"/> and populates issues + results bars.
        /// Can be called again after blueprint changes (e.g. user edits between reviews).
        /// </summary>
        public void RunValidation(Blueprint blueprint, ValidationContext context = null)
        {
            if (blueprint == null) return;

            context ??= new ValidationContext();

            // --- Run validators (pure function) ---
            var report = BlueprintValidator.Validate(blueprint, context);

            // --- Build results bars (pure factory) ---
            ResultBars = ResultsPanelViewModel.Build(report);

            // --- Map ValidationReport → ReviewIssueItems ---
            Issues.Clear();

            foreach (var issue in report.Issues)
            {
                var severity = issue.IsError   ? "Error"
                             : issue.IsWarning ? "Warning"
                             : "Info";

                // Hint is the "what to look at" guidance from the validator
                var description = string.IsNullOrEmpty(issue.Hint)
                    ? issue.Reason
                    : issue.Hint;

                Issues.Add(new ReviewIssueItem(
                    title:       issue.Title,
                    description: description,
                    reason:      issue.Reason,
                    severity:    severity));
            }

            // --- Update observable summary fields ---
            ErrorCount.Value   = report.ErrorCount;
            WarningCount.Value = report.WarningCount;
            IsValid.Value      = report.IsValid;

            if (report.IsValid && !report.HasWarnings)
            {
                StatusSummary.Value = "Tasarım Fırlatmaya Uygun";
            }
            else if (report.IsValid)
            {
                StatusSummary.Value = $"Fırlatılabilir ama {report.WarningCount} uyarı var — dikkat edin";
            }
            else
            {
                StatusSummary.Value = $"{report.ErrorCount} engel var — tasarımı düzeltin";
            }
        }

        // ── Placeholder ────────────────────────────────────────────────────────────

        private void LoadPlaceholderData()
        {
            // Minimal placeholder so the screen is not empty during early navigation tests.
            Issues.Add(new ReviewIssueItem(
                "Menzil İhtiyacı Karşılandı",
                "Roket Delta-V kapasitesi hedef Ay iniş gereksiniminin (13 500 m/s) üzerindedir.",
                "Uygun marj mevcut.",
                "Info"));

            Issues.Add(new ReviewIssueItem(
                "Düşük Güç Marjı Uyarısı",
                "Ay kutup gölgesinde batarya kapasitesi %15 sınırındadır.",
                "Gölge süresince bilimsel aletlerin kapatılması gerekebilir.",
                "Warning"));

            WarningCount.Value  = 1;
            IsValid.Value       = true;
            StatusSummary.Value = "Tasarım Fırlatmaya Uygun (Örnek Veri)";

            // Build placeholder bars from a stub stats object
            var stubStats = new BlueprintStats(new StatCollection());
            ResultBars    = ResultsPanelViewModel.Build(stubStats);
        }
    }
}
