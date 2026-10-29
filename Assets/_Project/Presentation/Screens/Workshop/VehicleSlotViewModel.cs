using System;
using Apogee.Design;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// ViewModel representing a single vehicle component slot in the Design Workshop (GDD §7.2, §14.3).
    /// </summary>
    public sealed class VehicleSlotViewModel : ViewModelBase
    {
        public VehicleSlotDefinition Definition { get; }
        public VehicleSlotId SlotId => Definition.Id;
        public string DisplayName => Definition.DisplayName;
        public string CategoryTag => Definition.CategoryTag;
        public string GroupName => Definition.GroupName;
        public bool IsRequired => Definition.IsRequired;

        public ObservableValue<DesignComponent> EquippedComponent { get; } = new(null);
        public ObservableValue<string> EquippedName { get; } = new("Boş - Modül Seçin");
        public ObservableValue<string> SpecsSummary { get; } = new(string.Empty);
        public ObservableValue<bool> IsSelected { get; } = new(false);

        public ViewActionCommand SelectSlotCommand { get; }

        public VehicleSlotViewModel(VehicleSlotDefinition definition, Action onSelected = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            EquippedName.Value = IsRequired ? "Boş (Zorunlu) - Seçin" : "Boş (İsteğe Bağlı)";

            SelectSlotCommand = new ViewActionCommand(() =>
            {
                onSelected?.Invoke();
            });

            EquippedComponent.Subscribe(comp =>
            {
                if (comp != null)
                {
                    EquippedName.Value = comp.Name;
                    var culture = System.Globalization.CultureInfo.InvariantCulture;
                    SpecsSummary.Value = $"{comp.EffectiveDryMass.ToString("F2", culture)}t · ${comp.EffectiveCost.ToString("F1", culture)}M · %{comp.EffectiveReliability * 100:F0}";
                }
                else
                {
                    EquippedName.Value = IsRequired ? "Boş (Zorunlu) - Seçin" : "Boş (İsteğe Bağlı)";
                    SpecsSummary.Value = string.Empty;
                }
            });
        }

        public void SetEquipped(DesignComponent component)
        {
            EquippedComponent.Value = component;
        }
    }
}
