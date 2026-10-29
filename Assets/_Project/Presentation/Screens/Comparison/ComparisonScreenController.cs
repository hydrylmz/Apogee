using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Comparison
{
    /// <summary>
    /// Screen controller for the Blueprint Comparison screen (GDD §7.5 & §14.1 #17).
    /// Side-by-side stat bars and slot diff table.
    /// </summary>
    public sealed class ComparisonScreenController : ScreenControllerBase<ComparisonViewModel>
    {
        public override ScreenId ScreenId => ScreenId.Comparison;

        private Label _labelA;
        private Label _labelB;
        private Label _summaryLabel;
        private VisualElement _statContainer;
        private VisualElement _slotContainer;
        private Button _toggleSlotsButton;

        public ComparisonScreenController(ComparisonViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;

            // ── Header ──────────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.style.marginBottom = 12;

            var backButton = new Button(() => ViewModel.BackCommand.Execute());
            backButton.text = "← Geri";
            backButton.AddToClassList("apogee-button");
            header.Add(backButton);

            var titleLabel = new Label("Tasarım Karşılaştırması");
            titleLabel.AddToClassList("apogee-panel-title");
            titleLabel.style.flexGrow = 1;
            titleLabel.style.marginLeft = 12;
            header.Add(titleLabel);

            root.Add(header);

            // ── Summary strip ────────────────────────────────────────────────
            var summaryPanel = new VisualElement();
            summaryPanel.AddToClassList("apogee-panel");
            summaryPanel.AddToClassList("apogee-panel--surface");
            summaryPanel.style.flexDirection = FlexDirection.Row;
            summaryPanel.style.alignItems = Align.Center;
            summaryPanel.style.marginBottom = 12;

            _labelA = new Label();
            _labelA.AddToClassList("apogee-badge");
            _labelA.AddToClassList("apogee-badge--cyan");
            _labelA.style.marginRight = 8;

            _labelB = new Label();
            _labelB.AddToClassList("apogee-badge");
            _labelB.AddToClassList("apogee-badge--amber");
            _labelB.style.marginRight = 12;

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("apogee-stat-label");
            _summaryLabel.style.flexGrow = 1;

            summaryPanel.Add(_labelA);
            summaryPanel.Add(new Label("vs") { style = { marginRight = 8, color = UnityEngine.Color.gray } });
            summaryPanel.Add(_labelB);
            summaryPanel.Add(_summaryLabel);

            root.Add(summaryPanel);

            // ── Main 2-column layout ─────────────────────────────────────────
            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.flexGrow = 1;
            columns.style.overflow = Overflow.Hidden;

            // Left: Stat comparisons
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("apogee-panel");
            leftPanel.style.flexGrow = 1;
            leftPanel.style.marginRight = 8;

            var statsHeader = new Label("İstatistik Karşılaştırması");
            statsHeader.AddToClassList("apogee-panel-title");
            statsHeader.style.marginBottom = 8;
            leftPanel.Add(statsHeader);

            // Stat column headers
            var headerRow = BuildStatHeaderRow();
            leftPanel.Add(headerRow);

            var statsScroll = new ScrollView(ScrollViewMode.Vertical);
            statsScroll.style.flexGrow = 1;
            _statContainer = statsScroll.contentContainer;
            leftPanel.Add(statsScroll);

            // Right: Slot comparisons
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("apogee-panel");
            rightPanel.style.width = 360;

            var slotHeader = new VisualElement();
            slotHeader.style.flexDirection = FlexDirection.Row;
            slotHeader.style.alignItems = Align.Center;
            slotHeader.style.marginBottom = 8;

            var slotsTitle = new Label("Slot Değişimleri");
            slotsTitle.AddToClassList("apogee-panel-title");
            slotsTitle.style.flexGrow = 1;
            slotHeader.Add(slotsTitle);

            _toggleSlotsButton = new Button(() => ViewModel.ToggleSlotFilterCommand.Execute());
            _toggleSlotsButton.AddToClassList("apogee-button");
            _toggleSlotsButton.style.fontSize = 10;
            slotHeader.Add(_toggleSlotsButton);
            rightPanel.Add(slotHeader);

            var slotsScroll = new ScrollView(ScrollViewMode.Vertical);
            slotsScroll.style.flexGrow = 1;
            _slotContainer = slotsScroll.contentContainer;
            rightPanel.Add(slotsScroll);

            columns.Add(leftPanel);
            columns.Add(rightPanel);
            root.Add(columns);

            return root;
        }

        protected override void OnInitialized()
        {
            ViewModel.LabelA.Subscribe(v => { if (_labelA != null) _labelA.text = v; });
            ViewModel.LabelB.Subscribe(v => { if (_labelB != null) _labelB.text = v; });
            ViewModel.SummaryText.Subscribe(v => { if (_summaryLabel != null) _summaryLabel.text = v; });
            ViewModel.StatComparisons.Subscribe(v => RebuildStats());
            ViewModel.SlotComparisons.Subscribe(v => RebuildSlots());
            ViewModel.ChangedSlotsOnly.Subscribe(v => RebuildSlots());
            ViewModel.ShowAllSlots.Subscribe(v =>
            {
                if (_toggleSlotsButton != null)
                    _toggleSlotsButton.text = v ? "Yalnız Değişenler" : "Tümünü Göster";
                RebuildSlots();
            });
        }

        protected override void OnShown(object context)
        {
            if (context is ComparisonNavigationArgs args)
            {
                ViewModel.ApplyComparison(args);
            }
        }

        // ── Builders ───────────────────────────────────────────────────────

        private VisualElement BuildStatHeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingBottom = 4;
            row.style.marginBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f);

            void AddHeader(string text, int flexGrow)
            {
                var lbl = new Label(text);
                lbl.AddToClassList("apogee-stat-label");
                lbl.style.flexGrow = flexGrow;
                lbl.style.fontSize = 10;
                lbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                row.Add(lbl);
            }

            AddHeader("İstatistik", 2);
            AddHeader("A", 1);
            AddHeader("B", 1);
            AddHeader("Δ", 1);

            return row;
        }

        private void RebuildStats()
        {
            if (_statContainer == null) return;
            _statContainer.Clear();

            var items = ViewModel.StatComparisons.Value;
            if (items == null) return;

            foreach (var item in items)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new UnityEngine.Color(0.18f, 0.18f, 0.18f);

                AddCell(row, item.Label, 2, false, null);
                AddCell(row, item.FormattedValueA, 1, false, null);
                AddCell(row, item.FormattedValueB, 1, false, null);

                // Delta cell color
                UnityEngine.Color? deltaColor = null;
                if (item.IsBetter == true) deltaColor = new UnityEngine.Color(0.22f, 0.85f, 0.42f);
                else if (item.IsBetter == false) deltaColor = new UnityEngine.Color(0.9f, 0.25f, 0.25f);

                AddCell(row, item.FormattedDelta, 1, true, deltaColor);
                _statContainer.Add(row);
            }
        }

        private void RebuildSlots()
        {
            if (_slotContainer == null) return;
            _slotContainer.Clear();

            var items = ViewModel.ShowAllSlots.Value
                ? ViewModel.SlotComparisons.Value
                : ViewModel.ChangedSlotsOnly.Value;

            if (items == null || items.Count == 0)
            {
                var empty = new Label(ViewModel.ShowAllSlots.Value
                    ? "Slot bulunamadı."
                    : "Değişen slot yok. ✓");
                empty.AddToClassList("apogee-stat-label");
                empty.style.marginTop = 12;
                _slotContainer.Add(empty);
                return;
            }

            foreach (var item in items)
            {
                var card = new VisualElement();
                card.AddToClassList("apogee-review-card");
                card.style.marginBottom = 6;

                // Side accent color by status
                switch (item.Status)
                {
                    case SlotComparisonStatus.Changed:
                        card.AddToClassList("apogee-review-card--warning");
                        break;
                    case SlotComparisonStatus.AddedInB:
                        card.AddToClassList("apogee-review-card--pass");
                        break;
                    case SlotComparisonStatus.RemovedInB:
                        card.AddToClassList("apogee-review-card--error");
                        break;
                    default:
                        card.style.borderLeftWidth = 2;
                        card.style.borderLeftColor = new UnityEngine.Color(0.3f, 0.3f, 0.3f);
                        break;
                }

                var slotLabel = new Label(item.SlotName);
                slotLabel.AddToClassList("apogee-stat-label");
                slotLabel.style.fontSize = 10;
                slotLabel.style.marginBottom = 3;
                card.Add(slotLabel);

                var compRow = new VisualElement();
                compRow.style.flexDirection = FlexDirection.Row;
                compRow.style.alignItems = Align.Center;

                var nameA = new Label(item.NameA);
                nameA.AddToClassList("apogee-badge");
                nameA.AddToClassList("apogee-badge--cyan");
                nameA.AddToClassList("apogee-badge--mini");
                nameA.style.flexGrow = 1;
                compRow.Add(nameA);

                var arrow = new Label("→");
                arrow.style.marginLeft = 4;
                arrow.style.marginRight = 4;
                arrow.style.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
                compRow.Add(arrow);

                var nameB = new Label(item.NameB);
                nameB.AddToClassList("apogee-badge");
                nameB.AddToClassList("apogee-badge--amber");
                nameB.AddToClassList("apogee-badge--mini");
                nameB.style.flexGrow = 1;
                compRow.Add(nameB);

                card.Add(compRow);
                _slotContainer.Add(card);
            }
        }

        private static void AddCell(VisualElement row, string text, int flexGrow, bool bold, UnityEngine.Color? color)
        {
            var lbl = new Label(text ?? "-");
            lbl.style.flexGrow = flexGrow;
            lbl.style.fontSize = 11;
            if (bold) lbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            if (color.HasValue) lbl.style.color = color.Value;
            row.Add(lbl);
        }
    }
}
