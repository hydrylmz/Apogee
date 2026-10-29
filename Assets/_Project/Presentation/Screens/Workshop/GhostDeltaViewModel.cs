using Apogee.Design;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Reactive ViewModel holding active ghost delta metrics (GDD §7.3, §13.2).
    /// </summary>
    public sealed class GhostDeltaViewModel : ViewModelBase
    {
        public ObservableValue<bool> IsActive { get; } = new(false);
        public ObservableValue<double> DeltaDeltaV { get; } = new(0.0);
        public ObservableValue<double> DeltaMassTons { get; } = new(0.0);
        public ObservableValue<double> DeltaCostMillions { get; } = new(0.0);
        public ObservableValue<double> DeltaReliabilityPercent { get; } = new(0.0);
        public ObservableValue<double> DeltaPowerMarginKw { get; } = new(0.0);
        public ObservableValue<string> SummaryText { get; } = new(string.Empty);

        public void ApplyDelta(GhostDelta delta)
        {
            if (delta == null || !delta.HasChanges)
            {
                Clear();
                return;
            }

            DeltaDeltaV.Value = delta.DeltaDeltaV;
            DeltaMassTons.Value = delta.DeltaWetMassTons;
            DeltaCostMillions.Value = delta.DeltaCostMillions;
            DeltaReliabilityPercent.Value = delta.DeltaReliability * 100.0;
            DeltaPowerMarginKw.Value = delta.DeltaPowerMarginKw;
            SummaryText.Value = delta.GetSummaryText();
            IsActive.Value = true;
        }

        public void Clear()
        {
            if (!IsActive.Value) return;

            DeltaDeltaV.Value = 0.0;
            DeltaMassTons.Value = 0.0;
            DeltaCostMillions.Value = 0.0;
            DeltaReliabilityPercent.Value = 0.0;
            DeltaPowerMarginKw.Value = 0.0;
            SummaryText.Value = string.Empty;
            IsActive.Value = false;
        }
    }
}
