using System;
using UnityEngine;

namespace Apogee.UI.Core.Tooltips
{
    public interface ITooltipService
    {
        bool IsVisible { get; }
        TooltipData CurrentData { get; }
        event Action<TooltipData, Vector2> OnTooltipRequested;
        event Action OnTooltipDismissed;

        void ShowTooltip(TooltipData data, Vector2 screenPosition);
        void HideTooltip();
    }

    /// <summary>
    /// Central tooltip coordination service.
    /// UI elements fire show/hide events here; the TooltipView overlay listens and renders.
    /// </summary>
    public sealed class TooltipService : ITooltipService
    {
        private static ITooltipService _instance;
        public static ITooltipService Instance => _instance ??= new TooltipService();

        public bool IsVisible { get; private set; }
        public TooltipData CurrentData { get; private set; }

        public event Action<TooltipData, Vector2> OnTooltipRequested;
        public event Action OnTooltipDismissed;

        public static void SetInstance(ITooltipService instance)
        {
            _instance = instance;
        }

        public void ShowTooltip(TooltipData data, Vector2 screenPosition)
        {
            if (data == null)
            {
                HideTooltip();
                return;
            }

            CurrentData = data;
            IsVisible = true;
            OnTooltipRequested?.Invoke(data, screenPosition);
        }

        public void HideTooltip()
        {
            if (!IsVisible && CurrentData == null)
            {
                return;
            }

            IsVisible = false;
            CurrentData = null;
            OnTooltipDismissed?.Invoke();
        }
    }
}
