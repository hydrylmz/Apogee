using System;
using System.Collections.Generic;

namespace Apogee.UI.Core.Navigation
{
    public interface IScreenNavigator
    {
        ScreenId CurrentScreenId { get; }
        IScreenController CurrentScreen { get; }
        bool CanGoBack { get; }
        IReadOnlyCollection<ScreenId> History { get; }

        event Action<ScreenId, ScreenId> OnScreenChanged;

        void RegisterScreen(IScreenController screenController);
        void UnregisterScreen(ScreenId screenId);
        bool GoTo(ScreenId targetScreenId, object context = null, bool addToHistory = true);
        bool GoBack();
        void ClearHistory();
    }

    /// <summary>
    /// Central screen navigator and router for APOGEE UI.
    /// Manages active screen switching, history stack, and lifecycle hooks.
    /// </summary>
    public sealed class ScreenNavigator : IScreenNavigator
    {
        private readonly Dictionary<ScreenId, IScreenController> _screens = new();
        private readonly Stack<ScreenId> _history = new();

        public ScreenId CurrentScreenId { get; private set; } = ScreenId.None;
        public IScreenController CurrentScreen => _screens.TryGetValue(CurrentScreenId, out var screen) ? screen : null;
        public bool CanGoBack => _history.Count > 0;
        public IReadOnlyCollection<ScreenId> History => _history;

        public event Action<ScreenId, ScreenId> OnScreenChanged;

        public void RegisterScreen(IScreenController screenController)
        {
            if (screenController == null) throw new ArgumentNullException(nameof(screenController));
            if (screenController.ScreenId == ScreenId.None)
            {
                throw new ArgumentException("ScreenId cannot be None.", nameof(screenController));
            }

            _screens[screenController.ScreenId] = screenController;
        }

        public void UnregisterScreen(ScreenId screenId)
        {
            _screens.Remove(screenId);
        }

        public bool GoTo(ScreenId targetScreenId, object context = null, bool addToHistory = true)
        {
            if (targetScreenId == ScreenId.None)
            {
                return false;
            }

            if (!_screens.TryGetValue(targetScreenId, out var targetScreen))
            {
                return false;
            }

            if (targetScreenId == CurrentScreenId)
            {
                return true; // Already on this screen
            }

            ScreenId previousScreenId = CurrentScreenId;

            // Exit current screen
            if (_screens.TryGetValue(previousScreenId, out var currentScreen))
            {
                currentScreen.OnExit();
                if (addToHistory && previousScreenId != ScreenId.None)
                {
                    _history.Push(previousScreenId);
                }
            }

            // Enter new screen
            CurrentScreenId = targetScreenId;
            targetScreen.OnEnter(context);

            OnScreenChanged?.Invoke(previousScreenId, targetScreenId);
            return true;
        }

        public bool GoBack()
        {
            if (!CanGoBack)
            {
                return false;
            }

            ScreenId previousScreenId = _history.Pop();
            return GoTo(previousScreenId, null, addToHistory: false);
        }

        public void ClearHistory()
        {
            _history.Clear();
        }
    }
}
