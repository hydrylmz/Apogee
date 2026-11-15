using System;
using System.Collections.Generic;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Navigation
{
    /// <summary>
    /// Shared icon-tab navigation bar for the management screens (GDD §14.2).
    /// One consistent bar: back-to-map affordance on the left, the eight management
    /// tabs in the middle, an optional status slot on the right. Tabs show what a
    /// click will do via tooltip (interaction contract #5).
    /// </summary>
    public sealed class ScreenNavBar : VisualElement
    {
        public const string UssClassName = "apogee-navbar";

        /// <summary>The eight management screens in nav order.</summary>
        private static readonly (ScreenId Id, IconType Icon, string Label)[] Tabs =
        {
            (ScreenId.MissionComposer, IconType.Mission, "Misyon"),
            (ScreenId.Workshop, IconType.Workshop, "Atölye"),
            (ScreenId.SolarMap, IconType.SolarSystem, "Güneş Sistemi"),
            (ScreenId.ResearchTree, IconType.Research, "Ar-Ge Ağacı"),
            (ScreenId.ProductionQueue, IconType.Production, "Üretim"),
            (ScreenId.Contracts, IconType.Contracts, "Sözleşmeler"),
            (ScreenId.BaseManagement, IconType.Bases, "Üsler"),
            (ScreenId.MilestoneBoard, IconType.Milestones, "Hedefler")
        };

        private readonly IScreenNavigator _navigator;
        private readonly Dictionary<ScreenId, VisualElement> _tabElements = new();
        private Label _statusLabel;
        private VisualElement _trailingSlot;

        /// <summary>Right-side status slot. Screens bind their StatusMessage here.</summary>
        public Label StatusLabel => _statusLabel;

        /// <summary>
        /// Adds a trailing element (e.g. a screen-specific "Ana Menü" button) to the
        /// bar's right end, after the status slot. Keeps every screen's footer a
        /// single, identical 42px bar instead of ad-hoc wrapper rows.
        /// </summary>
        public void AddTrailingElement(VisualElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (_trailingSlot == null)
            {
                _trailingSlot = new VisualElement();
                _trailingSlot.AddToClassList("apogee-navbar__trailing");
                _trailingSlot.style.flexDirection = FlexDirection.Row;
                _trailingSlot.style.alignItems = Align.Center;
                _trailingSlot.style.marginLeft = 8;
                Add(_trailingSlot);
            }

            _trailingSlot.Add(element);
        }

        /// <summary>
        /// Creates the nav bar. <paramref name="activeScreen"/> is highlighted and
        /// inert; every other tab navigates through the shared <paramref name="navigator"/>.
        /// Optional <paramref name="backLabel"/>/<paramref name="backTooltip"/> override the
        /// back affordance text (e.g. "Ana Menü" on the CampaignMap itself).
        /// </summary>
        public ScreenNavBar(IScreenNavigator navigator, ScreenId activeScreen, ScreenId backTarget = ScreenId.CampaignMap,
                            string backLabel = null, string backTooltip = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            AddToClassList(UssClassName);
            BuildBackButton(backTarget, backLabel, backTooltip);
            BuildTabs(activeScreen);
            BuildStatusSlot();
        }

        private void BuildBackButton(ScreenId backTarget, string backLabel, string backTooltip)
        {
            var back = new VisualElement();
            back.AddToClassList("apogee-navbar__back");
            back.Add(IconElement.Create(IconType.Back, 14f));
            var label = new Label(backLabel ?? (backTarget == ScreenId.CampaignMap ? "Harita" : "Geri"));
            label.AddToClassList("apogee-navbar__back-label");
            back.Add(label);

            back.AddManipulator(new TooltipManipulator(new TooltipData(
                backTooltip ?? "Kampanya Haritası'na dön",
                backTooltip != null
                    ? "Mevcut ekrandan çıkıp ana menüye geri döner. Devam eden işlemler kaybolmaz."
                    : "Mevcut ekrandan çıkıp strateji haritasına geri döner. Devam eden işlemler kaybolmaz.")));

            back.RegisterCallback<ClickEvent>(_ =>
            {
                if (backTarget != ScreenId.None && _navigator.CurrentScreenId != backTarget)
                {
                    _navigator.GoTo(backTarget);
                }
            });

            Add(back);
        }

        private void BuildTabs(ScreenId activeScreen)
        {
            var tabsContainer = new VisualElement();
            tabsContainer.AddToClassList("apogee-navbar__tabs");

            foreach (var tab in Tabs)
            {
                var element = new VisualElement();
                element.AddToClassList("apogee-navbar__tab");
                element.Add(IconElement.Create(tab.Icon, 15f));

                var label = new Label(tab.Label);
                label.AddToClassList("apogee-navbar__tab-label");
                label.pickingMode = PickingMode.Ignore;
                element.Add(label);

                bool isActive = tab.Id == activeScreen;
                if (isActive)
                {
                    element.AddToClassList("apogee-navbar__tab--active");
                    element.AddManipulator(new TooltipManipulator(new TooltipData(
                        tab.Label,
                        "Şu an bu ekrandasiniz.")));
                }
                else
                {
                    element.AddManipulator(new TooltipManipulator(new TooltipData(
                        tab.Label,
                        $"'{tab.Label}' ekranına geçer. Açık olan ekran arka planda bekler.")));

                    ScreenId target = tab.Id;
                    element.RegisterCallback<ClickEvent>(_ => _navigator.GoTo(target));
                }

                _tabElements[tab.Id] = element;
                tabsContainer.Add(element);
            }

            Add(tabsContainer);
        }

        private void BuildStatusSlot()
        {
            _statusLabel = new Label(string.Empty);
            _statusLabel.AddToClassList("apogee-navbar__status");
            _statusLabel.pickingMode = PickingMode.Ignore;
            Add(_statusLabel);
        }

        /// <summary>
        /// Updates the status slot text (screen feedback line, e.g. command results).
        /// </summary>
        public void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message ?? string.Empty;
            }
        }
    }
}
