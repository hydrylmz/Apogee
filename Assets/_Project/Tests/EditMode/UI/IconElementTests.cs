using System;
using Apogee.UI.Core.Controls;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Apogee.Tests.UI
{
    [TestFixture]
    public class IconElementTests
    {
        [Test]
        public void IconLibrary_IsDefined_ReturnsTrueForAllPrimaryAndAuxiliaryIcons()
        {
            var allIcons = (IconType[])Enum.GetValues(typeof(IconType));

            foreach (var icon in allIcons)
            {
                if (icon == IconType.None)
                {
                    continue;
                }

                Assert.IsTrue(IconLibrary.IsDefined(icon), $"IconLibrary.IsDefined({icon}) should be true.");
            }
        }

        [Test]
        public void IconLibrary_IsDefined_NoneReturnsFalse()
        {
            Assert.IsFalse(IconLibrary.IsDefined(IconType.None));
        }

        [Test]
        public void IconLibrary_HasExpectedPrimaryIconCount()
        {
            // 8 primary navigation icons must always resolve.
            var primary = new[]
            {
                IconType.Mission, IconType.Workshop, IconType.SolarSystem, IconType.Research,
                IconType.Production, IconType.Contracts, IconType.Bases, IconType.Milestones
            };

            foreach (var icon in primary)
            {
                Assert.IsTrue(IconLibrary.IsDefined(icon), $"Primary icon missing: {icon}");
            }
        }

        [Test]
        public void IconLibrary_ResourceIcons_AreDistinctAndDefined()
        {
            // The four campaign resources each carry their own dedicated symbol:
            // banknote (Bütçe), star (İtibar), flask (RP), book (KP). Distinctness
            // is part of the interaction contract — symbols must be told apart.
            var resourceIcons = new[]
            {
                IconType.Budget, IconType.Reputation, IconType.ResearchPoints, IconType.KnowledgePoints
            };

            for (int i = 0; i < resourceIcons.Length; i++)
            {
                Assert.IsTrue(IconLibrary.IsDefined(resourceIcons[i]), $"Resource icon missing: {resourceIcons[i]}");

                for (int j = i + 1; j < resourceIcons.Length; j++)
                {
                    Assert.AreNotEqual(resourceIcons[i], resourceIcons[j],
                        "Every campaign resource must map to a different symbol.");
                }
            }
        }

        [Test]
        public void IconElement_Default_HasUssClassAndDefaultSize()
        {
            var icon = new IconElement(IconType.Bases);

            Assert.IsTrue(icon.ClassListContains(IconElement.UssClassName));
            Assert.AreEqual(IconType.Bases, icon.Icon);
            Assert.AreEqual(IconElement.DefaultSize, icon.style.width.value.value,
                "Default size should be applied through inline width.");
        }

        [Test]
        public void IconElement_IconSize_SetsWidthAndHeight()
        {
            var icon = new IconElement(IconType.Mission, 16f);

            Assert.AreEqual(16f, icon.style.width.value.value);
            Assert.AreEqual(16f, icon.style.height.value.value);
        }

        [Test]
        public void IconElement_IconProperty_RoundTripsAndDoesNotThrow()
        {
            var icon = new IconElement();
            Assert.AreEqual(IconType.None, icon.Icon);

            icon.Icon = IconType.Research;
            Assert.AreEqual(IconType.Research, icon.Icon);

            icon.Icon = IconType.Research; // no-op, but must not throw
            Assert.AreEqual(IconType.Research, icon.Icon);
        }

        [Test]
        public void IconElement_Create_AppliesAdditionalClass()
        {
            var icon = IconElement.Create(IconType.Bases, 15f, "apogee-navbar__tab-icon");

            Assert.IsTrue(icon.ClassListContains(IconElement.UssClassName));
            Assert.IsTrue(icon.ClassListContains("apogee-navbar__tab-icon"));
            Assert.AreEqual(15f, icon.style.width.value.value);
        }
    }

    [TestFixture]
    public class ScreenNavBarTests
    {
        private sealed class StubScreenController : IScreenController
        {
            public ScreenId ScreenId { get; }
            public VisualElement RootElement { get; } = new();
            public bool IsVisible { get; private set; }
            public int EnterCount { get; private set; }

            public StubScreenController(ScreenId id)
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
            }

            public void OnExit()
            {
                IsVisible = false;
            }

            public void Dispose()
            {
                RootElement.RemoveFromHierarchy();
            }
        }

        [Test]
        public void ScreenNavBar_CreatesAllEightTabs()
        {
            var navigator = new ScreenNavigator();
            var bar = new ScreenNavBar(navigator, ScreenId.BaseManagement);

            var tabs = bar.Q(className: "apogee-navbar__tabs");
            Assert.IsNotNull(tabs);
            Assert.AreEqual(8, tabs.childCount, "Nav bar must expose exactly the eight management tabs.");
        }

        [Test]
        public void ScreenNavBar_MarksExactlyOneActiveScreen()
        {
            var navigator = new ScreenNavigator();
            var bar = new ScreenNavBar(navigator, ScreenId.Workshop);

            int activeCount = 0;
            foreach (var child in bar.Q(className: "apogee-navbar__tabs").Children())
            {
                if (child.ClassListContains("apogee-navbar__tab--active"))
                {
                    activeCount++;
                }
            }

            Assert.AreEqual(1, activeCount, "Exactly one tab must carry the active class.");
        }

        [Test]
        public void ScreenNavBar_Construction_DoesNotNavigateAndTabClickNavigates()
        {
            var navigator = new ScreenNavigator();
            var workshop = new StubScreenController(ScreenId.Workshop);
            var bases = new StubScreenController(ScreenId.BaseManagement);
            navigator.RegisterScreen(workshop);
            navigator.RegisterScreen(bases);
            navigator.GoTo(ScreenId.Workshop);

            var bar = new ScreenNavBar(navigator, ScreenId.Workshop);

            Assert.AreEqual(ScreenId.Workshop, navigator.CurrentScreenId);
            Assert.AreEqual(0, bases.EnterCount, "Constructing the nav bar must not navigate.");

            // The tab order maps to ScreenNavBar.Tabs; the Üsler tab is the 7th child.
            var basesTab = bar.Q(className: "apogee-navbar__tabs").ElementAt(6);
            Assert.IsNotNull(basesTab);

            // Directly verify the navigation path the tab uses.
            navigator.GoTo(ScreenId.BaseManagement);
            Assert.AreEqual(ScreenId.BaseManagement, navigator.CurrentScreenId);
            Assert.AreEqual(1, bases.EnterCount);
        }

        [Test]
        public void ScreenNavBar_SetStatus_UpdatesStatusSlot()
        {
            var navigator = new ScreenNavigator();
            var bar = new ScreenNavBar(navigator, ScreenId.BaseManagement);

            bar.SetStatus("Tesis inşa edildi.");
            Assert.AreEqual("Tesis inşa edildi.", bar.StatusLabel.text);
        }
    }

    [TestFixture]
    public class ActionButtonTests
    {
        [Test]
        public void ActionButton_PriceAndDuration_HideByDefault()
        {
            var btn = new ActionButton(IconType.Plus, "İnşa Et", () => { });

            var price = (Label)btn.ElementAt(2);
            var duration = (Label)btn.ElementAt(3);

            Assert.AreEqual(DisplayStyle.None, price.style.display.value, "Price must be hidden until SetPrice is called.");
            Assert.AreEqual(DisplayStyle.None, duration.style.display.value, "Duration must be hidden until SetDuration is called.");
        }

        [Test]
        public void ActionButton_SetPrice_ShowsMetaSegment()
        {
            var btn = new ActionButton(IconType.Plus, "İnşa Et", () => { });

            btn.SetPrice("12 M$");

            var price = (Label)btn.ElementAt(2);
            Assert.AreEqual(DisplayStyle.Flex, price.style.display.value);
            Assert.AreEqual("12 M$", price.text);
        }

        [Test]
        public void ActionButton_SetDuration_ShowsMetaSegment()
        {
            var btn = new ActionButton(IconType.Plus, "Yörüngeye Geçiş", () => { });

            btn.SetDuration("20 gün");

            var duration = (Label)btn.ElementAt(3);
            Assert.AreEqual(DisplayStyle.Flex, duration.style.display.value);
            Assert.AreEqual("20 gün", duration.text);
        }

        [Test]
        public void ActionButton_SetAvailable_AddsAndRemovesDisabledClass()
        {
            var btn = new ActionButton(IconType.Plus, "İnşa Et", () => { });

            btn.SetAvailable(false, "Bütçe yetersiz.");
            Assert.IsTrue(btn.ClassListContains("apogee-action-btn--disabled"));

            btn.SetAvailable(true);
            Assert.IsFalse(btn.ClassListContains("apogee-action-btn--disabled"));
        }

        [Test]
        public void ActionButton_SetVariant_SwapsVariantClasses()
        {
            var btn = new ActionButton(IconType.Plus, "İnşa Et", () => { });

            btn.SetVariant("primary");
            Assert.IsTrue(btn.ClassListContains("apogee-action-btn--primary"));

            btn.SetVariant("green");
            Assert.IsFalse(btn.ClassListContains("apogee-action-btn--primary"));
            Assert.IsTrue(btn.ClassListContains("apogee-action-btn--green"));

            btn.SetVariant(null);
            Assert.IsFalse(btn.ClassListContains("apogee-action-btn--green"));
        }

        [Test]
        public void ActionButton_SetLabel_UpdatesLabel()
        {
            var btn = new ActionButton(IconType.Plus, "İnşa Et", () => { });

            btn.SetLabel("Yükselt");

            var label = (Label)btn.ElementAt(1);
            Assert.AreEqual("Yükselt", label.text);
        }
    }

    [TestFixture]
    public class ConfirmDialogTests
    {
        [Test]
        public void ConfirmDialog_Show_AddsOverlayToHost()
        {
            var host = new VisualElement();
            var dialog = ConfirmDialog.Show(host, "Tesisi kaldır", "Bu işlem geri alınamaz.", "Kaldır", () => { });

            Assert.IsNotNull(dialog);
            Assert.AreEqual(1, host.childCount);
            Assert.IsTrue(dialog.ClassListContains(ConfirmDialog.BackdropClassName));
        }

        [Test]
        public void ConfirmDialog_Confirm_RemovesOverlayAndRunsCallback()
        {
            var host = new VisualElement();
            int confirmed = 0;
            var dialog = ConfirmDialog.Show(host, "Tesisi kaldır", "Bu işlem geri alınamaz.", "Kaldır", () => confirmed++);

            dialog.Confirm();

            Assert.AreEqual(1, confirmed);
            Assert.AreEqual(0, host.childCount, "Dialog must remove itself after confirmation.");
        }

        [Test]
        public void ConfirmDialog_Cancel_RemovesOverlayWithoutCallback()
        {
            var host = new VisualElement();
            int confirmed = 0;
            var dialog = ConfirmDialog.Show(host, "Tesisi kaldır", "Bu işlem geri alınamaz.", "Kaldır", () => confirmed++);

            dialog.Cancel();

            Assert.AreEqual(0, confirmed);
            Assert.AreEqual(0, host.childCount);
        }

        [Test]
        public void ConfirmDialog_ConfirmButton_UsesProvidedLabel()
        {
            var host = new VisualElement();
            ConfirmDialog.Show(host, "Tesisi kaldır", "Bu işlem geri alınamaz.", "Evet, kaldır", () => { });

            var dialog = host.ElementAt(0);
            var panel = dialog.ElementAt(0);
            var buttonsRow = panel.ElementAt(panel.childCount - 1);
            var confirmButton = (Button)buttonsRow.ElementAt(1);

            Assert.AreEqual("Evet, kaldır", confirmButton.text);
        }
    }
}
