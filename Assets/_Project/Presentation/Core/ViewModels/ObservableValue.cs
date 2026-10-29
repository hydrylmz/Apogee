using System;
using System.Collections.Generic;

namespace Apogee.UI.Core.ViewModels
{
    /// <summary>
    /// Lightweight observable property container that allows UI views to reactively
    /// bind and unbind to ViewModel state changes without Unity-specific runtime data binding.
    /// </summary>
    public sealed class ObservableValue<T> : IDisposable
    {
        private T _value;
        private Action<T, T> _onValueChangedWithOld;
        private Action<T> _onValueChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                {
                    return;
                }

                T oldValue = _value;
                _value = value;
                _onValueChangedWithOld?.Invoke(oldValue, _value);
                _onValueChanged?.Invoke(_value);
            }
        }

        public ObservableValue(T initialValue = default)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Subscribes an action to value changes. Returns an IDisposable token to easily unsubscribe.
        /// </summary>
        public IDisposable Subscribe(Action<T> onValueChanged, bool notifyImmediately = true)
        {
            if (onValueChanged == null)
            {
                return EmptyDisposable.Instance;
            }

            _onValueChanged += onValueChanged;

            if (notifyImmediately)
            {
                onValueChanged(_value);
            }

            return new SubscriptionToken(() => _onValueChanged -= onValueChanged);
        }

        /// <summary>
        /// Subscribes an action that receives both old and new values.
        /// </summary>
        public IDisposable Subscribe(Action<T, T> onValueChangedWithOld, bool notifyImmediately = true)
        {
            if (onValueChangedWithOld == null)
            {
                return EmptyDisposable.Instance;
            }

            _onValueChangedWithOld += onValueChangedWithOld;

            if (notifyImmediately)
            {
                onValueChangedWithOld(default, _value);
            }

            return new SubscriptionToken(() => _onValueChangedWithOld -= onValueChangedWithOld);
        }

        public void Dispose()
        {
            _onValueChanged = null;
            _onValueChangedWithOld = null;
        }

        public override string ToString() => _value?.ToString() ?? string.Empty;

        public static implicit operator T(ObservableValue<T> observable) => observable != null ? observable.Value : default;

        private sealed class SubscriptionToken : IDisposable
        {
            private Action _unsubscribeAction;

            public SubscriptionToken(Action unsubscribeAction)
            {
                _unsubscribeAction = unsubscribeAction;
            }

            public void Dispose()
            {
                _unsubscribeAction?.Invoke();
                _unsubscribeAction = null;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
