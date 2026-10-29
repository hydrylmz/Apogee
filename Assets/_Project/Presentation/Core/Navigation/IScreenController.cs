using System;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Navigation
{
    /// <summary>
    /// Contract for all screen controllers in the UI Toolkit presentation layer.
    /// Controls lifecycle, visual display, and context passing.
    /// </summary>
    public interface IScreenController : IDisposable
    {
        ScreenId ScreenId { get; }
        VisualElement RootElement { get; }
        bool IsVisible { get; }

        void Initialize(VisualElement parentContainer);
        void OnEnter(object context = null);
        void OnExit();
    }
}
