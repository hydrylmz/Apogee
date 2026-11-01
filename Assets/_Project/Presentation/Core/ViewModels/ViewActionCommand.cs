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

    /// <summary>
    /// UI-level parameterized action command used to bind buttons and view interactions to ViewModel methods.
    /// </summary>
    public sealed class ViewActionCommand<T>
    {
        private readonly Action<T> _executeAction;
        private readonly Func<T, bool> _canExecutePredicate;

        public event Action CanExecuteChanged;

        public ViewActionCommand(Action<T> executeAction, Func<T, bool> canExecutePredicate = null)
        {
            _executeAction = executeAction ?? throw new ArgumentNullException(nameof(executeAction));
            _canExecutePredicate = canExecutePredicate;
        }

        public bool CanExecute(T parameter)
        {
            return _canExecutePredicate == null || _canExecutePredicate(parameter);
        }

        public void Execute(T parameter)
        {
            if (CanExecute(parameter))
            {
                _executeAction(parameter);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke();
        }
    }
}
