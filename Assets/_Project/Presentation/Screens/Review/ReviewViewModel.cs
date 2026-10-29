using System.Collections.Generic;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Review
{
    public sealed class ReviewIssueItem
    {
        public string Title { get; }
        public string Description { get; }
        public string Reason { get; }
        public string Severity { get; } // "Error" | "Warning" | "Info"

        public ReviewIssueItem(string title, string description, string reason, string severity)
        {
            Title = title;
            Description = description;
            Reason = reason;
            Severity = severity;
        }
    }

    /// <summary>
    /// ViewModel for the Design Review screen (GDD §7.4 & §14.1 item 12).
    /// </summary>
    public class ReviewViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        public ObservableValue<string> StatusSummary { get; } = new("Tasarım Fırlatmaya Uygun");
        public ObservableValue<bool> IsValid { get; } = new(true);
        public ObservableValue<int> ErrorCount { get; } = new(0);
        public ObservableValue<int> WarningCount { get; } = new(1);

        public List<ReviewIssueItem> Issues { get; } = new();

        public ViewActionCommand BackToWorkshopCommand { get; }

        public ReviewViewModel(IScreenNavigator navigator = null)
        {
            _navigator = navigator;

            Issues.Add(new ReviewIssueItem(
                "Menzil İhtiyacı Karşılandı",
                "Roket Delta-V kapasitesi hedef Ay iniş gereksiniminin (13,500 m/s) üzerindedir.",
                "Uygun marj mevcut.",
                "Info"));

            Issues.Add(new ReviewIssueItem(
                "Düşük Güç Marjı Uyarısı",
                "Ay kutup gölgesinde batarya kapasitesi %15 sınırındadır.",
                "Gölge süresince bilimsel aletlerin kapatılması gerekebilir.",
                "Warning"));

            BackToWorkshopCommand = new ViewActionCommand(() =>
            {
                if (_navigator != null && _navigator.CanGoBack)
                {
                    _navigator.GoBack();
                }
                else
                {
                    _navigator?.GoTo(ScreenId.Workshop);
                }
            });
        }
    }
}
