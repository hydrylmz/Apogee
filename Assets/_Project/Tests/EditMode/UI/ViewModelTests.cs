using System;
using System.Collections.Generic;
using Apogee.UI.Core.ViewModels;
using NUnit.Framework;

namespace Apogee.Tests.UI
{
    [TestFixture]
    public class ViewModelTests
    {
        private class TestViewModel : ViewModelBase
        {
            private string _name;
            private int _count;

            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            public int Count
            {
                get => _count;
                set => SetProperty(ref _count, value);
            }
        }

        private class MockDisposable : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }

        [Test]
        public void ViewModelBase_SetProperty_FiresPropertyChanged_WhenValueChanges()
        {
            var vm = new TestViewModel();
            var changedProps = new List<string>();
            vm.PropertyChanged += (sender, args) => changedProps.Add(args.PropertyName);

            vm.Name = "Falcon";
            vm.Count = 42;

            Assert.AreEqual(2, changedProps.Count);
            Assert.Contains(nameof(TestViewModel.Name), changedProps);
            Assert.Contains(nameof(TestViewModel.Count), changedProps);
        }

        [Test]
        public void ViewModelBase_SetProperty_DoesNotFire_WhenValueIsSame()
        {
            var vm = new TestViewModel { Name = "Apollo" };
            var changedProps = new List<string>();
            vm.PropertyChanged += (sender, args) => changedProps.Add(args.PropertyName);

            vm.Name = "Apollo"; // same value

            Assert.AreEqual(0, changedProps.Count);
        }

        [Test]
        public void ViewModelBase_RegisterDisposable_DisposesAllOnDispose()
        {
            var vm = new TestViewModel();
            var d1 = new MockDisposable();
            var d2 = new MockDisposable();

            vm.RegisterDisposable(d1);
            vm.RegisterDisposable(d2);

            Assert.IsFalse(d1.IsDisposed);
            Assert.IsFalse(d2.IsDisposed);

            vm.Dispose();

            Assert.IsTrue(d1.IsDisposed);
            Assert.IsTrue(d2.IsDisposed);
        }

        [Test]
        public void ObservableValue_Subscribe_ReceivesInitialAndSubsequentValues()
        {
            var obs = new ObservableValue<int>(10);
            var received = new List<int>();

            using var sub = obs.Subscribe(val => received.Add(val), notifyImmediately: true);

            obs.Value = 20;
            obs.Value = 30;
            obs.Value = 30; // identical, should not trigger

            Assert.AreEqual(3, received.Count);
            Assert.AreEqual(10, received[0]);
            Assert.AreEqual(20, received[1]);
            Assert.AreEqual(30, received[2]);
        }

        [Test]
        public void ObservableValue_Unsubscribe_StopsReceivingEvents()
        {
            var obs = new ObservableValue<string>("Start");
            var received = new List<string>();

            var sub = obs.Subscribe(val => received.Add(val), notifyImmediately: false);
            obs.Value = "Second";
            sub.Dispose(); // Unsubscribe
            obs.Value = "Third";

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("Second", received[0]);
        }

        [Test]
        public void ObservableValue_OldAndNewValueSubscription_TracksTransitions()
        {
            var obs = new ObservableValue<int>(100);
            int lastOld = -1;
            int lastNew = -1;

            using var sub = obs.Subscribe((oldVal, newVal) =>
            {
                lastOld = oldVal;
                lastNew = newVal;
            }, notifyImmediately: false);

            obs.Value = 250;

            Assert.AreEqual(100, lastOld);
            Assert.AreEqual(250, lastNew);
        }

        [Test]
        public void ViewActionCommand_Execute_InvokesAction_WhenCanExecuteIsTrue()
        {
            bool executed = false;
            var cmd = new ViewActionCommand(() => executed = true, () => true);

            Assert.IsTrue(cmd.CanExecute());
            cmd.Execute();
            Assert.IsTrue(executed);
        }

        [Test]
        public void ViewActionCommand_Execute_Blocks_WhenCanExecuteIsFalse()
        {
            bool executed = false;
            var cmd = new ViewActionCommand(() => executed = true, () => false);

            Assert.IsFalse(cmd.CanExecute());
            cmd.Execute();
            Assert.IsFalse(executed);
        }
    }
}
