using System;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// ViewModel for the Design Workshop screen (GDD §7 & §14.3).
    /// Holds live reactive telemetry and navigation commands.
    /// </summary>
    public class WorkshopViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;

        public ObservableValue<string> BlueprintName { get; } = new("Yeni Ay Gezgini (Rover)");
        public ObservableValue<string> TargetDestination { get; } = new("Ay (Lunar Surface)");
        public ObservableValue<double> TotalDeltaV { get; } = new(13500.0);
        public ObservableValue<double> TargetDeltaV { get; } = new(13500.0);
        public ObservableValue<double> TotalMassTons { get; } = new(182.5);
        public ObservableValue<double> TotalCostMillions { get; } = new(64.2);
        public ObservableValue<double> ReliabilityPercent { get; } = new(88.5);

        public ViewActionCommand GoToReviewCommand { get; }

        public WorkshopViewModel(IScreenNavigator navigator = null)
        {
            _navigator = navigator;
            GoToReviewCommand = new ViewActionCommand(() =>
            {
                _navigator?.GoTo(ScreenId.DesignReview);
            });
        }
    }
}
