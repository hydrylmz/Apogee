using System;
using System.Collections.Generic;
using Apogee.Design;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Slide-out right side drawer for selecting and equipping modules onto the active vehicle slot (GDD §7.3, §14.3).
    /// Features card previews, supplier color tags, lock badges, hover ghost delta triggers, and equip commands.
    /// </summary>
    public class HangarPartDrawer : VisualElement
    {
        private readonly WorkshopViewModel _viewModel;
        private readonly Label _titleLabel;
        private readonly Label _categoryLabel;
        private readonly ScrollView _cardsScroll;
        private readonly VisualElement _cardsContainer;

        public event Action<VehicleSlotId?, DesignComponent> OptionHovered;
        public event Action OptionUnhovered;
        public event Action DrawerClosed;

        public HangarPartDrawer(WorkshopViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            AddToClassList("apogee-hangar-drawer");
            style.position = Position.Absolute;
            style.right = 0;
            style.top = 0;
            style.bottom = 0;
            style.width = 380;
            style.backgroundColor = new Color(0.06f, 0.10f, 0.18f, 0.96f);
            style.borderLeftWidth = 1;
            style.borderLeftColor = new Color(0.18f, 0.28f, 0.42f);
            style.paddingTop = 16;
            style.paddingBottom = 16;
            style.paddingLeft = 16;
            style.paddingRight = 16;
            style.display = DisplayStyle.None; // Hidden by default until a slot is selected

            // ── Drawer Header ────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.15f, 0.24f, 0.36f);

            var titleBox = new VisualElement();
            _titleLabel = new Label("Uyumlu Modüller");
            _titleLabel.AddToClassList("apogee-panel-title");
            _titleLabel.style.fontSize = 15;
            _titleLabel.style.color = new Color(0.20f, 0.75f, 0.95f);
            titleBox.Add(_titleLabel);

            _categoryLabel = new Label("Kategori: Tüm Parçalar");
            _categoryLabel.style.fontSize = 10;
            _categoryLabel.style.color = new Color(0.45f, 0.60f, 0.75f);
            titleBox.Add(_categoryLabel);
            header.Add(titleBox);

            var closeBtn = new Button(() =>
            {
                CloseDrawer();
                DrawerClosed?.Invoke();
            });
            closeBtn.text = "X";
            closeBtn.AddToClassList("apogee-button");
            closeBtn.style.width = 28;
            closeBtn.style.height = 28;
            closeBtn.style.fontSize = 12;
            closeBtn.style.paddingLeft = 0;
            closeBtn.style.paddingRight = 0;
            closeBtn.style.paddingTop = 0;
            closeBtn.style.paddingBottom = 0;
            closeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(closeBtn);
            Add(header);

            // ── Cards Scroll Area ────────────────────────────────────────────────────
            _cardsScroll = new ScrollView();
            _cardsScroll.style.flexGrow = 1;
            _cardsContainer = new VisualElement();
            _cardsContainer.style.flexDirection = FlexDirection.Column;
            _cardsScroll.Add(_cardsContainer);
            Add(_cardsScroll);

            // Bind to ViewModel available options
            _viewModel.AvailableOptions.Subscribe(PopulateOptions);
            _viewModel.SelectedSlot.Subscribe(OnSlotSelectionChanged);
        }

        public void OpenDrawer()
        {
            style.display = DisplayStyle.Flex;
        }

        public void CloseDrawer()
        {
            style.display = DisplayStyle.None;
            OptionUnhovered?.Invoke();
        }

        private void OnSlotSelectionChanged(VehicleSlotViewModel slot)
        {
            if (slot == null)
            {
                CloseDrawer();
            }
            else
            {
                _titleLabel.text = slot.DisplayName;
                _categoryLabel.text = $"Kategori: {slot.CategoryTag.ToUpperInvariant()} • {(slot.IsRequired ? "Zorunlu Yuva" : "İsteğe Bağlı")}";
                OpenDrawer();
            }
        }

        private void PopulateOptions(IReadOnlyList<ModuleOptionViewModel> options)
        {
            _cardsContainer.Clear();

            if (options == null || options.Count == 0)
            {
                var emptyLabel = new Label("Bu yuva için uyumlu modül bulunamadı.");
                emptyLabel.style.color = new Color(0.50f, 0.60f, 0.72f);
                emptyLabel.style.marginTop = 20;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _cardsContainer.Add(emptyLabel);
                return;
            }

            foreach (var opt in options)
            {
                var card = CreateOptionCard(opt);
                _cardsContainer.Add(card);
            }
        }

        private VisualElement CreateOptionCard(ModuleOptionViewModel opt)
        {
            var card = new VisualElement();
            card.AddToClassList("apogee-hangar-card");
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 10;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            SetBorderWidth(card.style, 1);
            card.style.position = Position.Relative;

            bool isEquipped = opt.IsCurrentlyEquipped.Value;
            bool isLocked = opt.IsLocked;

            if (isEquipped)
            {
                card.style.backgroundColor = new Color(0.08f, 0.22f, 0.32f);
                SetBorderColor(card.style, new Color(0.06f, 0.85f, 0.95f));
            }
            else if (isLocked)
            {
                card.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f);
                SetBorderColor(card.style, new Color(0.20f, 0.25f, 0.32f));
                card.style.opacity = 0.65f;
            }
            else
            {
                card.style.backgroundColor = new Color(0.09f, 0.14f, 0.22f);
                SetBorderColor(card.style, new Color(0.18f, 0.28f, 0.40f));
            }

            // Top Row: Title + Badges
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;

            var nameLabel = new Label(opt.Name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = isEquipped ? new Color(0.40f, 0.85f, 1.0f) : (isLocked ? new Color(0.60f, 0.65f, 0.72f) : Color.white);
            nameLabel.style.flexGrow = 1;
            topRow.Add(nameLabel);

            if (isEquipped)
            {
                var equippedBadge = new Label("OK TAKILI");
                equippedBadge.AddToClassList("apogee-badge");
                equippedBadge.AddToClassList("apogee-badge--cyan");
                equippedBadge.style.fontSize = 9;
                topRow.Add(equippedBadge);
            }
            else if (isLocked)
            {
                var lockBadge = new Label("KİLİTLİ");
                lockBadge.AddToClassList("apogee-badge");
                lockBadge.AddToClassList("apogee-badge--amber");
                lockBadge.style.fontSize = 9;
                topRow.Add(lockBadge);
            }
            card.Add(topRow);

            // Stats grid (Mass, Cost, Reliability, Power, etc.)
            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.flexWrap = Wrap.Wrap;
            statsRow.style.marginTop = 6;

            AddStatPill(statsRow, "Kütle", $"{opt.DryMass:F1} t");
            AddStatPill(statsRow, "Maliyet", $"${opt.Cost:F1}M");
            AddStatPill(statsRow, "Güvenilirlik", $"%{opt.ReliabilityPct:F0}");

            if (opt.Thrust > 0)
            {
                AddStatPill(statsRow, "İtki", $"{opt.Thrust:F0} kN");
                AddStatPill(statsRow, "Isp", $"{opt.Isp:F0} s");
            }
            if (opt.PowerGeneration > 0)
            {
                AddStatPill(statsRow, "Üretim", $"+{opt.PowerGeneration:F1} kW");
            }
            if (opt.PowerConsumption > 0)
            {
                AddStatPill(statsRow, "Tüketim", $"-{opt.PowerConsumption:F1} kW");
            }
            card.Add(statsRow);

            // Lock Reason if locked
            if (isLocked && !string.IsNullOrEmpty(opt.LockReason))
            {
                var lockReasonLabel = new Label($"Gereksinim: {opt.LockReason}");
                lockReasonLabel.style.fontSize = 9;
                lockReasonLabel.style.color = new Color(0.95f, 0.65f, 0.15f);
                lockReasonLabel.style.marginTop = 4;
                card.Add(lockReasonLabel);
            }

            // Supplier Colored bottom stripe
            var supId = opt.Component?.Supplier?.Id;
            var supHex = CraftCrossSectionLayout.ResolveSupplierColor(supId);
            var stripe = new VisualElement();
            stripe.style.height = 3;
            stripe.style.marginTop = 8;
            stripe.style.borderBottomLeftRadius = 6;
            stripe.style.borderBottomRightRadius = 6;
            if (ColorUtility.TryParseHtmlString(supHex, out var stripeCol))
            {
                stripe.style.backgroundColor = stripeCol;
            }
            card.Add(stripe);

            // Card Interactions
            card.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (!isEquipped && !isLocked)
                {
                    SetBorderColor(card.style, new Color(0.06f, 0.85f, 0.95f));
                    card.style.backgroundColor = new Color(0.12f, 0.20f, 0.32f);
                }

                opt.OnHover?.Invoke();
                OptionHovered?.Invoke(_viewModel.SelectedSlot.Value?.SlotId, opt.Component);
            });

            card.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (!isEquipped && !isLocked)
                {
                    SetBorderColor(card.style, new Color(0.18f, 0.28f, 0.40f));
                    card.style.backgroundColor = new Color(0.09f, 0.14f, 0.22f);
                }

                opt.OnUnhover?.Invoke();
                OptionUnhovered?.Invoke();
            });

            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && !isLocked)
                {
                    opt.SelectCommand?.Execute();
                }
            });

            return card;
        }

        private static void SetBorderWidth(IStyle style, float width)
        {
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
        }

        private static void SetBorderColor(IStyle style, Color color)
        {
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }

        private void AddStatPill(VisualElement container, string label, string val)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.marginRight = 8;
            pill.style.marginBottom = 3;

            var l = new Label($"{label}: ");
            l.style.fontSize = 10;
            l.style.color = new Color(0.50f, 0.62f, 0.75f);
            pill.Add(l);

            var v = new Label(val);
            v.style.fontSize = 10;
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            v.style.color = new Color(0.85f, 0.92f, 1.0f);
            pill.Add(v);

            container.Add(pill);
        }
    }
}
