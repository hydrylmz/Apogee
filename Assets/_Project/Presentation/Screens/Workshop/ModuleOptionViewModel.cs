using System;
using Apogee.Design;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// ViewModel for an individual selectable module card inside the module picker (GDD §7.3).
    /// Supports hover trigger for hayalet delta and selection via command.
    /// </summary>
    public sealed class ModuleOptionViewModel : ViewModelBase
    {
        public DesignComponent Component { get; }
        public string Id => Component.Id;
        public string Name => Component.Name;
        public string Category => Component.Category;
        public double DryMass => Component.EffectiveDryMass;
        public double Cost => Component.EffectiveCost;
        public double ReliabilityPct => Component.EffectiveReliability * 100.0;
        public double PowerGeneration => Component.PowerGeneration;
        public double PowerConsumption => Component.PowerConsumption;
        public double Isp => Component.Isp;
        public double Thrust => Component.Thrust;

        public bool IsLocked { get; }
        public string LockReason { get; }

        public ObservableValue<bool> IsCurrentlyEquipped { get; } = new(false);

        public Action OnHover { get; }
        public Action OnUnhover { get; }
        public ViewActionCommand SelectCommand { get; }

        public ModuleOptionViewModel(
            DesignComponent component,
            bool isLocked = false,
            string lockReason = null,
            Action onHover = null,
            Action onUnhover = null,
            Action onSelect = null)
        {
            Component = component ?? throw new ArgumentNullException(nameof(component));
            IsLocked = isLocked;
            LockReason = lockReason;
            OnHover = onHover;
            OnUnhover = onUnhover;

            SelectCommand = new ViewActionCommand(
                () => onSelect?.Invoke(),
                () => !IsLocked
            );
        }
    }
}
