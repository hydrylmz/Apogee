using System;
using System.Collections.Generic;
using Apogee.UI.Core.Navigation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    [TestFixture]
    public class NavigationTests
    {
        private class MockScreenController : IScreenController
        {
            public ScreenId ScreenId { get; }
            public VisualElement RootElement { get; } = new();
            public bool IsVisible { get; private set; }
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public object LastContext { get; private set; }

            public MockScreenController(ScreenId id)
            {
                ScreenId = id;
            }

            public void Initialize(VisualElement parentContainer)
            {
                parentContainer.Add(RootElement);
            }

            public void OnEnter(object context = null)
            {
                IsVisible = true;
                EnterCount++;
                LastContext = context;
            }

            public void OnExit()
            {
                IsVisible = false;
                ExitCount++;
            }

            public void Dispose()
            {
                RootElement.RemoveFromHierarchy();
            }
        }

        [Test]
        public void ScreenNavigator_RegisterAndGoTo_TransitionsScreen()
        {
            var navigator = new ScreenNavigator();
            var workshopScreen = new MockScreenController(ScreenId.Workshop);
            var reviewScreen = new MockScreenController(ScreenId.DesignReview);

            navigator.RegisterScreen(workshopScreen);
            navigator.RegisterScreen(reviewScreen);

            bool res = navigator.GoTo(ScreenId.Workshop);

            Assert.IsTrue(res);
            Assert.AreEqual(ScreenId.Workshop, navigator.CurrentScreenId);
            Assert.AreEqual(workshopScreen, navigator.CurrentScreen);
            Assert.AreEqual(1, workshopScreen.EnterCount);
            Assert.IsTrue(workshopScreen.IsVisible);
        }

        [Test]
        public void ScreenNavigator_History_PushesAndGoesBack()
        {
            var navigator = new ScreenNavigator();
            var workshop = new MockScreenController(ScreenId.Workshop);
            var review = new MockScreenController(ScreenId.DesignReview);

            navigator.RegisterScreen(workshop);
            navigator.RegisterScreen(review);

            navigator.GoTo(ScreenId.Workshop);
            Assert.IsFalse(navigator.CanGoBack);

            navigator.GoTo(ScreenId.DesignReview);
            Assert.IsTrue(navigator.CanGoBack);
            Assert.AreEqual(ScreenId.DesignReview, navigator.CurrentScreenId);

            bool wentBack = navigator.GoBack();
            Assert.IsTrue(wentBack);
            Assert.AreEqual(ScreenId.Workshop, navigator.CurrentScreenId);
            Assert.IsFalse(navigator.CanGoBack);
        }

        [Test]
        public void ScreenNavigator_GoBack_WhenEmpty_ReturnsFalse()
        {
            var navigator = new ScreenNavigator();
            Assert.IsFalse(navigator.GoBack());
        }

        [Test]
        public void ScreenNavigator_ClearHistory_EmptiesStack()
        {
            var navigator = new ScreenNavigator();
            var s1 = new MockScreenController(ScreenId.Workshop);
            var s2 = new MockScreenController(ScreenId.DesignReview);

            navigator.RegisterScreen(s1);
            navigator.RegisterScreen(s2);

            navigator.GoTo(ScreenId.Workshop);
            navigator.GoTo(ScreenId.DesignReview);
            Assert.IsTrue(navigator.CanGoBack);

            navigator.ClearHistory();
            Assert.IsFalse(navigator.CanGoBack);
        }

        [Test]
        public void ScreenNavigator_Transitions_FireOnScreenChangedEvent()
        {
            var navigator = new ScreenNavigator();
            var s1 = new MockScreenController(ScreenId.Workshop);
            var s2 = new MockScreenController(ScreenId.DesignReview);

            navigator.RegisterScreen(s1);
            navigator.RegisterScreen(s2);

            var transitions = new List<(ScreenId from, ScreenId to)>();
            navigator.OnScreenChanged += (from, to) => transitions.Add((from, to));

            navigator.GoTo(ScreenId.Workshop);
            navigator.GoTo(ScreenId.DesignReview);

            Assert.AreEqual(2, transitions.Count);
            Assert.AreEqual((ScreenId.None, ScreenId.Workshop), transitions[0]);
            Assert.AreEqual((ScreenId.Workshop, ScreenId.DesignReview), transitions[1]);
        }

        [Test]
        public void ScreenNavigator_UnregisteredOrInvalidScreen_ReturnsFalse()
        {
            var navigator = new ScreenNavigator();
            Assert.IsFalse(navigator.GoTo(ScreenId.None));
            Assert.IsFalse(navigator.GoTo(ScreenId.Workshop)); // Not registered
        }
    }
}
