using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Design;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Left vertical navigation and status rail for the rocket in the Hangar (GDD §14.3, §14.4).
    /// Displays a vertical schematic miniature of all rocket sections with accessibility pips
    /// (equipped, empty, required warning) and supports two-way selection synchronization.
    /// </summary>
    public class HangarSectionRail : VisualElement
    {
        private readonly WorkshopViewModel _viewModel;
        private readonly ScrollView _scroll;
        private readonly VisualElement _itemsContainer;
        private readonly Dictionary<VehicleSlotId, VisualElement> _slotElements = new();

        public event Action<VehicleSlotId> SlotSelected;

        public HangarSectionRail(WorkshopViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            AddToClassList("apogee-hangar-rail");
            style.width = 160;
            style.minWidth = 130;
            style.maxWidth = 450;
            style.flexShrink = 0;
            style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.90f);
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(0.12f, 0.20f, 0.32f);
            style.paddingTop = 12;
            style.paddingBottom = 12;
            style.paddingLeft = 8;
            style.paddingRight = 8;

            var titleLabel = new Label("BÖLÜM RAYI");
            titleLabel.AddToClassList("apogee-panel-title");
            titleLabel.style.fontSize = 11;
            titleLabel.style.color = new Color(0.30f, 0.65f, 0.85f);
            titleLabel.style.marginBottom = 8;
            titleLabel.style.paddingLeft = 4;
            Add(titleLabel);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _itemsContainer = new VisualElement();
            _itemsContainer.style.flexDirection = FlexDirection.Column;
            _scroll.Add(_itemsContainer);
            Add(_scroll);

            BuildRailItems();

            _viewModel.SelectedSlot.Subscribe(OnSlotSelectionChanged);
        }

        public void Refresh()
        {
            BuildRailItems();
        }

        private void BuildRailItems()
        {
            _itemsContainer.Clear();
            _slotElements.Clear();

            // Group slots logically (Top to bottom: Payload, Stage 3, Stage 2, Stage 1)
            var groups = new[]
            {
                ("FAYDALI YÜK", new[]
                {
                    VehicleSlotId.PayloadBus,
                    VehicleSlotId.PayloadPower,
                    VehicleSlotId.PayloadThermal,
                    VehicleSlotId.PayloadComms,
                    VehicleSlotId.PayloadAdcs,
                    VehicleSlotId.PayloadEdl,
                    VehicleSlotId.PayloadInstrument1,
                    VehicleSlotId.PayloadInstrument2,
                    VehicleSlotId.PayloadCabin,
                    VehicleSlotId.PayloadLifeSupport,
                    VehicleSlotId.PayloadAbortSystem
                }),
                ("AŞAMA 3", new[]
                {
                    VehicleSlotId.Stage3Tank,
                    VehicleSlotId.Stage3Engine
                }),
                ("AŞAMA 2", new[]
                {
                    VehicleSlotId.Stage2Tank,
                    VehicleSlotId.Stage2Engine
                }),
                ("AŞAMA 1 (KALKIŞ)", new[]
                {
                    VehicleSlotId.Stage1Tank,
                    VehicleSlotId.Stage1Engine
                })
            };

            var activeBlueprint = _viewModel.ActiveBlueprint;
            bool isCrewed = activeBlueprint?.Payload?.Cabin != null ||
                            activeBlueprint?.Payload?.LifeSupport != null ||
                            activeBlueprint?.Payload?.AbortSystem != null;

            foreach (var (groupName, slotIds) in groups)
            {
                // Only show Stage 3 if blueprint has 3 stages or stage 3 slots
                if (groupName == "AŞAMA 3" && (activeBlueprint?.Stages?.Count ?? 0) < 3 &&
                    activeBlueprint?.GetComponentInSlot(VehicleSlotId.Stage3Engine) == null &&
                    activeBlueprint?.GetComponentInSlot(VehicleSlotId.Stage3Tank) == null)
                {
                    continue;
                }

                var header = new Label(groupName);
                header.style.fontSize = 9;
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.color = new Color(0.40f, 0.52f, 0.68f);
                header.style.marginTop = 8;
                header.style.marginBottom = 4;
                header.style.paddingLeft = 4;
                _itemsContainer.Add(header);

                foreach (var slotId in slotIds)
                {
                    // Skip crewed slots if mission is not crewed
                    if (!isCrewed && (slotId == VehicleSlotId.PayloadCabin || slotId == VehicleSlotId.PayloadLifeSupport || slotId == VehicleSlotId.PayloadAbortSystem))
                    {
                        continue;
                    }

                    var item = CreateRailSlotItem(slotId);
                    _slotElements[slotId] = item;
                    _itemsContainer.Add(item);
                }
            }

            OnSlotSelectionChanged(_viewModel.SelectedSlot.Value);
        }

        private VisualElement CreateRailSlotItem(VehicleSlotId slotId)
        {
            var def = VehicleSlotDefinition.Get(slotId);
            var equipped = _viewModel.ActiveBlueprint?.GetComponentInSlot(slotId);
            bool isEquipped = equipped != null;

            var item = new VisualElement();
            item.AddToClassList("apogee-rail-item");
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 6;
            item.style.marginBottom = 3;
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            SetBorderWidth(item.style, 1);
            SetBorderColor(item.style, new Color(0.12f, 0.18f, 0.28f));
            item.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f);

            // Status Pip (Accessibility: Icon + Color)
            var pip = new Label("");
            pip.style.fontSize = 10;
            pip.style.marginRight = 6;

            if (isEquipped)
            {
                pip.style.color = new Color(0.10f, 0.85f, 0.50f); // Green equipped
            }
            else if (def.IsRequired)
            {
                pip.style.color = new Color(0.95f, 0.50f, 0.15f); // Amber warning required
            }
            else
            {
                pip.style.color = new Color(0.40f, 0.50f, 0.65f); // Grey optional empty
            }
            item.Add(pip);

            var label = new Label(def.DisplayName);
            label.style.fontSize = 10;
            label.style.color = isEquipped ? new Color(0.85f, 0.90f, 0.98f) : new Color(0.55f, 0.65f, 0.75f);
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.flexGrow = 1;
            item.Add(label);

            item.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _viewModel.SelectSlot(slotId);
                    SlotSelected?.Invoke(slotId);
                }
            });

            item.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (_viewModel.SelectedSlot.Value?.SlotId != slotId)
                {
                    item.style.backgroundColor = new Color(0.10f, 0.18f, 0.28f);
                }
            });

            item.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (_viewModel.SelectedSlot.Value?.SlotId != slotId)
                {
                    item.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f);
                }
            });

            return item;
        }

        private void OnSlotSelectionChanged(VehicleSlotViewModel slot)
        {
            var selectedId = slot?.SlotId;

            foreach (var kvp in _slotElements)
            {
                bool isSelected = selectedId.HasValue && kvp.Key == selectedId.Value;
                if (isSelected)
                {
                    kvp.Value.style.backgroundColor = new Color(0.08f, 0.25f, 0.38f);
                    SetBorderColor(kvp.Value.style, new Color(0.06f, 0.85f, 0.95f));
                }
                else
                {
                    kvp.Value.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f);
                    SetBorderColor(kvp.Value.style, new Color(0.12f, 0.18f, 0.28f));
                }
            }
        }

        private static void SetBorderWidth(IStyle style, float width)
        {
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
        }

        private static void SetBorderColor(IStyle style, Color color)
        {
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }
    }
}
