using Apogee.Design;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Results
{
    /// <summary>
    /// Reusable UI Toolkit component that renders a single telemetry stat bar
    /// from a <see cref="ResultBarViewModel"/> (GDD §8.3, §14.3).
    ///
    /// Visual anatomy (GDD §14.3 layout):
    ///   Label          Value text
    ///   [█████░░░░░░░░]   ← fill bar with traffic-light color
    ///   (optional) tiny "why?" tooltip on hover via TooltipManipulator
    ///
    /// Status → CSS class mapping:
    ///   Good    → apogee-bar-fill--green
    ///   Warning → apogee-bar-fill--amber
    ///   Error   → apogee-bar-fill--red
    ///   Neutral → apogee-bar-fill (default cyan)
    /// </summary>
    public sealed class ResultsBarView : VisualElement
    {
        // ── Child elements ─────────────────────────────────────────────────────────

        private readonly Label          _labelEl;
        private readonly Label          _valueEl;
        private readonly VisualElement  _barContainer;
        private readonly VisualElement  _barFill;
        private readonly Label          _reasonEl;

        // ── Fill-color CSS classes ─────────────────────────────────────────────────
        private const string CssBarGreen   = "apogee-bar-fill--green";
        private const string CssBarAmber   = "apogee-bar-fill--amber";
        private const string CssBarRed     = "apogee-bar-fill--red";

        // ── Constructor ────────────────────────────────────────────────────────────

        public ResultsBarView(ResultBarViewModel bar)
        {
            AddToClassList("apogee-results-bar");

            // ── Row: Label + Value ────────────────────────────────────────────────
            var headerRow = new VisualElement();
            headerRow.AddToClassList("apogee-stat-row");

            _labelEl = new Label(bar?.Label ?? string.Empty);
            _labelEl.AddToClassList("apogee-stat-label");

            _valueEl = new Label(bar?.ValueText ?? string.Empty);
            _valueEl.AddToClassList("apogee-stat-value");
            _valueEl.AddToClassList("apogee-results-bar__value");

            headerRow.Add(_labelEl);
            headerRow.Add(_valueEl);
            Add(headerRow);

            // ── Bar ───────────────────────────────────────────────────────────────
            _barContainer = new VisualElement();
            _barContainer.AddToClassList("apogee-bar-container");

            _barFill = new VisualElement();
            _barFill.AddToClassList("apogee-bar-fill");

            _barContainer.Add(_barFill);
            Add(_barContainer);

            // ── Reason text (inline hint below bar) ───────────────────────────────
            _reasonEl = new Label(string.Empty);
            _reasonEl.AddToClassList("apogee-results-bar__reason");
            _reasonEl.style.display = DisplayStyle.None;
            Add(_reasonEl);

            // ── Tooltip on hover via TooltipManipulator ───────────────────────────
            // Attach manipulator with lazy getter so it reads the latest reason text
            this.AddManipulator(new TooltipManipulator(() =>
            {
                if (bar == null || string.IsNullOrEmpty(bar.ReasonText))
                    return null;

                var severity = bar.Status == ResultBarStatus.Error   ? TooltipSeverity.Error
                             : bar.Status == ResultBarStatus.Warning ? TooltipSeverity.Warning
                             : TooltipSeverity.Info;

                return TooltipData.CreateWithReason(
                    bar.Label,
                    bar.ValueText,
                    bar.ReasonText,
                    severity);
            }));

            // ── Apply initial data ────────────────────────────────────────────────
            if (bar != null)
            {
                Apply(bar);
            }
        }

        // ── Public update API ──────────────────────────────────────────────────────

        /// <summary>
        /// Refreshes all visual elements from the given bar model.
        /// Called by parent view whenever blueprint stats change.
        /// </summary>
        public void Apply(ResultBarViewModel bar)
        {
            if (bar == null) return;

            _labelEl.text = bar.Label;
            _valueEl.text = bar.ValueText;

            // Fill width
            _barFill.style.width = Length.Percent(bar.FillRatio * 100f);

            // Traffic-light color
            _barFill.RemoveFromClassList(CssBarGreen);
            _barFill.RemoveFromClassList(CssBarAmber);
            _barFill.RemoveFromClassList(CssBarRed);

            switch (bar.Status)
            {
                case ResultBarStatus.Good:
                    _barFill.AddToClassList(CssBarGreen);
                    break;
                case ResultBarStatus.Warning:
                    _barFill.AddToClassList(CssBarAmber);
                    break;
                case ResultBarStatus.Error:
                    _barFill.AddToClassList(CssBarRed);
                    break;
                default:
                    // Neutral → default cyan (no extra class)
                    break;
            }

            // Inline reason text visibility
            if (!string.IsNullOrEmpty(bar.ReasonText))
            {
                _reasonEl.text = bar.ReasonText;
                _reasonEl.style.display = DisplayStyle.Flex;
            }
            else
            {
                _reasonEl.style.display = DisplayStyle.None;
            }

            // Value-label color to reinforce status
            _valueEl.RemoveFromClassList("apogee-results-bar__value--good");
            _valueEl.RemoveFromClassList("apogee-results-bar__value--warn");
            _valueEl.RemoveFromClassList("apogee-results-bar__value--error");

            switch (bar.Status)
            {
                case ResultBarStatus.Good:    _valueEl.AddToClassList("apogee-results-bar__value--good");    break;
                case ResultBarStatus.Warning: _valueEl.AddToClassList("apogee-results-bar__value--warn");    break;
                case ResultBarStatus.Error:   _valueEl.AddToClassList("apogee-results-bar__value--error");   break;
            }
        }
    }
}
