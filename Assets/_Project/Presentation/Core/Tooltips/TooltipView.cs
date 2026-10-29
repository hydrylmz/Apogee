using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Tooltips
{
    /// <summary>
    /// UI Toolkit VisualElement representing the floating tooltip overlay.
    /// Listens to TooltipService and clamps position within parent screen boundaries.
    /// </summary>
    public class TooltipView : VisualElement
    {
        private readonly Label _titleLabel;
        private readonly Label _bodyLabel;
        private readonly Label _reasonLabel;
        private readonly Label _deltaLabel;
        private readonly VisualElement _headerElement;
        private readonly ITooltipService _service;

        private const float CursorOffset = 16f;

        public TooltipView(ITooltipService service = null)
        {
            _service = service ?? TooltipService.Instance;

            AddToClassList("apogee-tooltip");
            pickingMode = PickingMode.Ignore;

            _headerElement = new VisualElement();
            _headerElement.AddToClassList("apogee-tooltip-header");
            _headerElement.pickingMode = PickingMode.Ignore;

            _titleLabel = new Label();
            _titleLabel.AddToClassList("apogee-tooltip-title");
            _titleLabel.pickingMode = PickingMode.Ignore;
            _headerElement.Add(_titleLabel);
            Add(_headerElement);

            _bodyLabel = new Label();
            _bodyLabel.AddToClassList("apogee-tooltip-body");
            _bodyLabel.pickingMode = PickingMode.Ignore;
            Add(_bodyLabel);

            _deltaLabel = new Label();
            _deltaLabel.AddToClassList("apogee-tooltip-delta");
            _deltaLabel.pickingMode = PickingMode.Ignore;
            Add(_deltaLabel);

            _reasonLabel = new Label();
            _reasonLabel.AddToClassList("apogee-tooltip-reason");
            _reasonLabel.pickingMode = PickingMode.Ignore;
            Add(_reasonLabel);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            _service.OnTooltipRequested += OnTooltipRequested;
            _service.OnTooltipDismissed += OnTooltipDismissed;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _service.OnTooltipRequested -= OnTooltipRequested;
            _service.OnTooltipDismissed -= OnTooltipDismissed;
        }

        private void OnTooltipRequested(TooltipData data, Vector2 screenPosition)
        {
            if (data == null)
            {
                OnTooltipDismissed();
                return;
            }

            _titleLabel.text = data.Title;
            _headerElement.style.display = string.IsNullOrEmpty(data.Title) ? DisplayStyle.None : DisplayStyle.Flex;

            _bodyLabel.text = data.Content;
            _bodyLabel.style.display = string.IsNullOrEmpty(data.Content) ? DisplayStyle.None : DisplayStyle.Flex;

            if (!string.IsNullOrEmpty(data.DeltaText))
            {
                _deltaLabel.text = data.DeltaText;
                _deltaLabel.style.display = DisplayStyle.Flex;
                _deltaLabel.RemoveFromClassList("apogee-tooltip-delta--positive");
                _deltaLabel.RemoveFromClassList("apogee-tooltip-delta--negative");

                if (data.IsPositiveDelta.HasValue)
                {
                    _deltaLabel.AddToClassList(data.IsPositiveDelta.Value
                        ? "apogee-tooltip-delta--positive"
                        : "apogee-tooltip-delta--negative");
                }
            }
            else
            {
                _deltaLabel.style.display = DisplayStyle.None;
            }

            if (!string.IsNullOrEmpty(data.Reason))
            {
                _reasonLabel.text = $"Neden: {data.Reason}";
                _reasonLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _reasonLabel.style.display = DisplayStyle.None;
            }

            // Severity styling
            RemoveFromClassList("apogee-tooltip--warning");
            RemoveFromClassList("apogee-tooltip--error");
            if (data.Severity == TooltipSeverity.Warning) AddToClassList("apogee-tooltip--warning");
            if (data.Severity == TooltipSeverity.Error) AddToClassList("apogee-tooltip--error");

            AddToClassList("apogee-tooltip--visible");

            UpdatePosition(screenPosition);
        }

        private void OnTooltipDismissed()
        {
            RemoveFromClassList("apogee-tooltip--visible");
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            float targetX = screenPosition.x + CursorOffset;
            float targetY = screenPosition.y + CursorOffset;

            // Clamping against parent layout if available
            if (parent != null && parent.layout.width > 0 && parent.layout.height > 0)
            {
                float parentWidth = parent.layout.width;
                float parentHeight = parent.layout.height;
                float tooltipWidth = layout.width > 0 ? layout.width : 220f;
                float tooltipHeight = layout.height > 0 ? layout.height : 100f;

                if (targetX + tooltipWidth > parentWidth)
                {
                    targetX = screenPosition.x - tooltipWidth - CursorOffset;
                    if (targetX < 0) targetX = 8f;
                }

                if (targetY + tooltipHeight > parentHeight)
                {
                    targetY = screenPosition.y - tooltipHeight - CursorOffset;
                    if (targetY < 0) targetY = 8f;
                }
            }

            style.left = targetX;
            style.top = targetY;
        }
    }
}
