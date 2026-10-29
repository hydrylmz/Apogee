using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Apogee.UI.Core.ViewModels
{
    /// <summary>
    /// Base class for all UI ViewModels in APOGEE (GDD §14.5).
    /// Provides change notification and lifecycle/disposal management for UI Toolkit views.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        private readonly List<IDisposable> _disposables = new();
        private bool _isDisposed;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Updates a backing field and raises PropertyChanged if the value changed.
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Registers a disposable resource to be disposed when this ViewModel is disposed.
        /// </summary>
        public void RegisterDisposable(IDisposable disposable)
        {
            if (disposable != null && !_disposables.Contains(disposable))
            {
                _disposables.Add(disposable);
            }
        }

        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    _disposables[i]?.Dispose();
                }
                catch
                {
                    // Suppress exceptions during disposal
                }
            }
            _disposables.Clear();
            PropertyChanged = null;
        }
    }
}
