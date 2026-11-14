using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Screens.Launch;

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
        public string SeveritySymbol => IsError ? "X" : IsWarning ? "!" : "OK";

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
    /// ViewModel for the Design Review screen (GDD §7.4 & §14.1 item 12, §21 M2-05).
    ///
    /// Accepts a <see cref="Blueprint"/> + <see cref="ValidationContext"/> to run
    /// the full validator pipeline, producing:
    ///  1. A traffic-light issue list (Kırmızı: Engel, Sarı: Uyarı, Yeşil: Tamam)
    ///  2. Filter tabs (Tümü / Engeller / Uyarılar / Tamamlanan)
    ///  3. GO / NO-GO status decision and counts
    ///  4. 7 telemetry bars with color coding via <see cref="ResultsPanelViewModel"/>
    ///  5. Launch navigation command when vehicle is cleared for launch (GO).
    /// </summary>
    public class ReviewViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        public IScreenNavigator Navigator => _navigator;

        public ObservableValue<string>           StatusSummary   { get; } = new("Tasarım İnceleme Bekleniyor");
        public ObservableValue<string>           GoNoGoBadgeText { get; } = new("UYGUN (GO)");
        public ObservableValue<bool>             IsValid         { get; } = new(true);
        public ObservableValue<int>              ErrorCount      { get; } = new(0);
        public ObservableValue<int>              WarningCount    { get; } = new(0);
        public ObservableValue<int>              PassCount       { get; } = new(0);
        public ObservableValue<ReviewFilterMode> CurrentFilter   { get; } = new(ReviewFilterMode.All);

        public Blueprint         ActiveBlueprint { get; private set; }
        public ValidationContext CurrentContext  { get; private set; }

        public List<ReviewIssueItem> Issues { get; } = new();

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

        public IReadOnlyList<ResultBarViewModel> ResultBars { get; private set; } =
            Array.Empty<ResultBarViewModel>();

        public event Action OnIssuesChanged;

        public ViewActionCommand BackToWorkshopCommand { get; }
        public ViewActionCommand ProceedToLaunchCommand { get; }

        public ReviewViewModel(IScreenNavigator navigator = null)
            : this(navigator, null, null) { }

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

            ProceedToLaunchCommand = new ViewActionCommand(
                ExecuteProceedToLaunch,
                () => IsValid.Value && ActiveBlueprint != null);

            IsValid.Subscribe(_ => ProceedToLaunchCommand.RaiseCanExecuteChanged());

            if (blueprint != null)
            {
                RunValidation(blueprint, context ?? new ValidationContext());
            }
            else
            {
                LoadPlaceholderData();
            }
        }

        private void ExecuteProceedToLaunch()
        {
            if (_navigator == null || ActiveBlueprint == null || !IsValid.Value) return;

            var stats = BlueprintEvaluator.Evaluate(ActiveBlueprint, CurrentContext?.MissionProfile);
            bool isCrewed = CurrentContext?.MissionProfile != null && CurrentContext.MissionProfile.IsCrewed;

            var launchArgs = new LaunchNavigationArgs(
                blueprint: ActiveBlueprint,
                stats: stats,
                missionName: CurrentContext?.MissionProfile?.TargetDestinationId ?? "Fırlatma Görevi",
                launchPadBaseId: "base_cape_pad",
                isCrewed: isCrewed
            );

            _navigator.GoTo(ScreenId.LaunchSequence, launchArgs);
        }

        public void SetFilter(ReviewFilterMode filter)
        {
            if (CurrentFilter.Value == filter) return;
            CurrentFilter.Value = filter;
            OnIssuesChanged?.Invoke();
        }

        public void RunValidation(Blueprint blueprint, ValidationContext context = null)
        {
            if (blueprint == null) return;

            ActiveBlueprint = blueprint;
            CurrentContext  = context ?? new ValidationContext();

            var report = BlueprintValidator.Validate(blueprint, CurrentContext);
            ResultBars = ResultsPanelViewModel.Build(report);

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
                    ruleId:       issue.RuleId.ToString(),
                    hint:         issue.Hint,
                    currentValue: issue.CurrentValue,
                    targetValue:  issue.TargetValue
                ));
            }

            int errors   = report.ErrorCount;
            int warnings = report.WarningCount;
            int passed   = report.InfoCount;

            ErrorCount.Value   = errors;
            WarningCount.Value = warnings;
            PassCount.Value    = passed;

            bool isGo = report.IsValid;
            IsValid.Value = isGo;

            if (isGo && warnings == 0)
            {
                StatusSummary.Value   = "Tasarım Fırlatmaya Uygun";
                GoNoGoBadgeText.Value = "UYGUN (GO)";
            }
            else if (isGo)
            {
                StatusSummary.Value   = $"Fırlatılabilir ama {warnings} uyarı var — dikkat edin";
                GoNoGoBadgeText.Value = "ŞARTLI GO (UYARILARLA)";
            }
            else
            {
                StatusSummary.Value   = $"{errors} engel var — tasarımı düzeltin";
                GoNoGoBadgeText.Value = "ENGEL VAR (NO-GO)";
            }

            ProceedToLaunchCommand.RaiseCanExecuteChanged();
            OnIssuesChanged?.Invoke();
        }

        private void LoadPlaceholderData()
        {
            Issues.Clear();
            Issues.Add(new ReviewIssueItem(
                title:       "Tasarım Yüklenmedi",
                description: "Henüz incelenecek bir araç tasarımı seçilmedi.",
                reason:      "Atölyeden bir roket tasarlayıp 'İncelemeye Git' butonunu kullanın.",
                severity:    "Info",
                ruleId:      "ReviewInit"
            ));

            ErrorCount.Value      = 0;
            WarningCount.Value    = 0;
            PassCount.Value       = 1;
            IsValid.Value         = false;
            GoNoGoBadgeText.Value = "BEKLİYOR";
            StatusSummary.Value   = "İnceleme başlatılmadı.";
            ProceedToLaunchCommand.RaiseCanExecuteChanged();
            OnIssuesChanged?.Invoke();
        }
    }
}
