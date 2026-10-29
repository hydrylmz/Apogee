using System;

namespace Apogee.UI.Core.ViewModels
{
    /// <summary>
    /// UI-level action command used to bind buttons and view interactions to ViewModel methods.
    /// </summary>
    public sealed class ViewActionCommand
    {
        private readonly Action _executeAction;
        private readonly Func<bool> _canExecutePredicate;

        public event Action CanExecuteChanged;

        public ViewActionCommand(Action executeAction, Func<bool> canExecutePredicate = null)
        {
            _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
            _canExecutePredicate = canExecutePredicate;
        }

        public bool CanExecute()
        {
            return _canExecutePredicate == null || _canExecutePredicate();
        }

        public void Execute()
        {
            if (CanExecute())
            {
                _executeAction();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke();
        }
    }
}
