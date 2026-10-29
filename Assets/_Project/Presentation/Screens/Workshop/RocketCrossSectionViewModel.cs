using System;
using System.Collections.Generic;
using Apogee.Design;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// ViewModel for the interactive 2D rocket and spacecraft cross-section view (GDD §7.2, §14.3, §15.1).
    /// Manages CAD assembly layout nodes, slot selection sync, and hover callout state.
    /// Acceptance criterion: 3-stage rocket + payload automatically assembled and snapped.
    /// </summary>
    public class RocketCrossSectionViewModel : ViewModelBase
    {
        private CraftCrossSectionLayout _currentLayout;

        public CraftCrossSectionLayout CurrentLayout => _currentLayout;

        public ObservableValue<IReadOnlyList<CrossSectionNode>> Nodes { get; } =
            new(Array.Empty<CrossSectionNode>());

        public ObservableValue<VehicleSlotId?> SelectedSlotId { get; } = new(null);

        public ObservableValue<CrossSectionNode> SelectedNode { get; } = new(null);

        public ObservableValue<CrossSectionNode> HoveredNode { get; } = new(null);

        public ObservableValue<int> StageCount { get; } = new(1);

        public ObservableValue<bool> HasPayload { get; } = new(true);

        public ObservableValue<string> StatusSummary { get; } = new("Modüler Montaj Hazır");

        public event Action<VehicleSlotId> SlotSelected;

        public RocketCrossSectionViewModel(Blueprint initialBlueprint = null)
        {
            if (initialBlueprint != null)
            {
                UpdateLayout(initialBlueprint);
            }
        }

        public void UpdateLayout(Blueprint blueprint)
        {
            if (blueprint == null)
            {
                _currentLayout = new CraftCrossSectionLayout(Array.Empty<CrossSectionNode>(), 0, false);
                Nodes.Value = Array.Empty<CrossSectionNode>();
                StageCount.Value = 0;
                HasPayload.Value = false;
                StatusSummary.Value = "Tasarım yüklenmedi";
                return;
            }

            _currentLayout = CraftCrossSectionLayout.Build(blueprint);
            Nodes.Value = _currentLayout.Nodes;
            StageCount.Value = _currentLayout.StageCount;
            HasPayload.Value = _currentLayout.HasPayload;

            StatusSummary.Value = $"{_currentLayout.StageCount} Aşamalı Fırlatıcı + Faydalı Yük ({_currentLayout.Nodes.Count} Parça)";

            // Re-sync selected node if slot is currently selected
            if (SelectedSlotId.Value.HasValue)
            {
                SyncSelectedNode(SelectedSlotId.Value.Value);
            }
        }

        public void SetSelectedSlot(VehicleSlotId? slotId)
        {
            SelectedSlotId.Value = slotId;
            if (slotId.HasValue)
            {
                SyncSelectedNode(slotId.Value);
            }
            else
            {
                SelectedNode.Value = null;
            }
        }

        public void RequestSelectPart(CrossSectionNode node)
        {
            if (node == null) return;

            SelectedNode.Value = node;

            if (node.SlotId.HasValue)
            {
                SelectedSlotId.Value = node.SlotId.Value;
                SlotSelected?.Invoke(node.SlotId.Value);
            }
        }

        public void SetHoveredNode(CrossSectionNode node)
        {
            HoveredNode.Value = node;
        }

        private void SyncSelectedNode(VehicleSlotId slotId)
        {
            var nodes = Nodes.Value;
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                if (node.SlotId == slotId)
                {
                    SelectedNode.Value = node;
                    return;
                }
            }
            SelectedNode.Value = null;
        }
    }
}
