using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Tooltips
{
    /// <summary>
    /// UI Toolkit manipulator that triggers the central TooltipService when hovering elements.
    /// Supports static TooltipData or dynamic factory delegate.
    /// </summary>
    public sealed class TooltipManipulator : PointerManipulator
    {
        private readonly Func<TooltipData> _dataFactory;
        private readonly ITooltipService _service;
        private bool _isHovered;

        public TooltipManipulator(TooltipData staticData, ITooltipService service = null)
        {
            if (staticData == null) throw new ArgumentNullException(nameof(staticData));
            _dataFactory = () => staticData;
            _service = service ?? TooltipService.Instance;
        }

        public TooltipManipulator(Func<TooltipData> dataFactory, ITooltipService service = null)
        {
            _dataFactory = dataFactory ?? throw new ArgumentNullException(nameof(dataFactory));
            _service = service ?? TooltipService.Instance;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);

            if (_isHovered)
            {
                _isHovered = false;
                _service.HideTooltip();
            }
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            _isHovered = true;
            TooltipData data = _dataFactory();
            if (data != null)
            {
                _service.ShowTooltip(data, evt.position);
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_isHovered)
            {
                _isHovered = false;
                _service.HideTooltip();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isHovered)
            {
                TooltipData data = _dataFactory();
                if (data != null)
                {
                    _service.ShowTooltip(data, evt.position);
                }
            }
        }
    }
}
