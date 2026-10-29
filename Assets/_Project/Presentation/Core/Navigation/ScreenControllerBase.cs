using System;
using Apogee.UI.Core.ViewModels;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Navigation
{
    /// <summary>
    /// Base screen controller integrating UI Toolkit VisualElement with a typed ViewModelBase.
    /// </summary>
    public abstract class ScreenControllerBase<TViewModel> : IScreenController
        where TViewModel : ViewModelBase
    {
        public abstract ScreenId ScreenId { get; }
        public VisualElement RootElement { get; protected set; }
        public TViewModel ViewModel { get; protected set; }
        public bool IsVisible => RootElement != null && RootElement.style.display == DisplayStyle.Flex;

        protected ScreenControllerBase(TViewModel viewModel)
        {
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public virtual void Initialize(VisualElement parentContainer)
        {
            if (parentContainer == null) throw new ArgumentNullException(nameof(parentContainer));

            RootElement = CreateView();
            if (RootElement == null)
            {
                throw new InvalidOperationException($"Screen {ScreenId} CreateView() returned null.");
            }

            RootElement.style.display = DisplayStyle.None;
            RootElement.style.flexGrow = 1;
            RootElement.style.width = Length.Percent(100);
            RootElement.style.height = Length.Percent(100);

            parentContainer.Add(RootElement);
            OnInitialized();
        }

        public virtual void OnEnter(object context = null)
        {
            if (RootElement != null)
            {
                RootElement.style.display = DisplayStyle.Flex;
            }

            OnShown(context);
        }

        public virtual void OnExit()
        {
            if (RootElement != null)
            {
                RootElement.style.display = DisplayStyle.None;
            }

            OnHidden();
        }

        protected abstract VisualElement CreateView();
        protected virtual void OnInitialized() { }
        protected virtual void OnShown(object context) { }
        protected virtual void OnHidden() { }

        public virtual void Dispose()
        {
            OnExit();
            RootElement?.RemoveFromHierarchy();
            RootElement = null;
            ViewModel?.Dispose();
        }
    }
}
