using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents an individual modular section or component node in the vehicle cross-section (GDD §7.2, §15.1).
    /// Holds structural metadata, snap anchors, linked slot info, and display telemetry.
    /// </summary>
    public sealed class CrossSectionNode
    {
        public string Id { get; }
        public VehicleSlotId? SlotId { get; }
        public CrossSectionPartType PartType { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public int StageIndex { get; } // -1 = Payload, 0 = Stage 1, 1 = Stage 2, 2 = Stage 3
        public DesignComponent Component { get; }
        public SupplierSpec Supplier { get; }
        public MaterialSpec Material { get; }
        public bool IsEmptySlot { get; }
        public double RelativeWidth { get; }
        public double RelativeHeight { get; }
        public string SupplierColorHex { get; }
        public SnapPoint TopSnap { get; }
        public SnapPoint BottomSnap { get; }

        public CrossSectionNode(
            string id,
            VehicleSlotId? slotId,
            CrossSectionPartType partType,
            string title,
            string subtitle = "",
            int stageIndex = -1,
            DesignComponent component = null,
            SupplierSpec supplier = null,
            MaterialSpec material = null,
            bool isEmptySlot = false,
            double relativeWidth = 1.0,
            double relativeHeight = 1.0,
            string supplierColorHex = "#38bdf8",
            SnapPoint topSnap = default,
            SnapPoint bottomSnap = default)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SlotId = slotId;
            PartType = partType;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            StageIndex = stageIndex;
            Component = component;
            Supplier = supplier;
            Material = material;
            IsEmptySlot = isEmptySlot;
            RelativeWidth = Math.Clamp(relativeWidth, 0.2, 1.2);
            RelativeHeight = Math.Max(0.2, relativeHeight);
            SupplierColorHex = supplierColorHex ?? "#38bdf8";
            TopSnap = topSnap;
            BottomSnap = bottomSnap;
        }

        public CrossSectionNode WithSnapConnections(bool topConnected, bool bottomConnected)
        {
            return new CrossSectionNode(
                Id,
                SlotId,
                PartType,
                Title,
                Subtitle,
                StageIndex,
                Component,
                Supplier,
                Material,
                IsEmptySlot,
                RelativeWidth,
                RelativeHeight,
                SupplierColorHex,
                TopSnap.WithConnection(topConnected),
                BottomSnap.WithConnection(bottomConnected));
        }

        public override string ToString() =>
            $"CrossSectionNode({Id}, Type={PartType}, Stage={StageIndex}, Slot={SlotId}, Empty={IsEmptySlot})";
    }
}
