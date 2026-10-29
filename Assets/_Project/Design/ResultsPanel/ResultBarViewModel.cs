using System;

namespace Apogee.Design
{
    /// <summary>
    /// Pure C# model for a single telemetry bar in the Results Panel (GDD §8.3).
    /// Holds display-ready state: label, fill ratio [0-1], status (traffic light), unit-less
    /// value string, and a short "why?" reason for the tooltip.
    /// No UnityEngine references.
    /// </summary>
    public sealed class ResultBarViewModel
    {
        // ── Immutable identity ─────────────────────────────────────────────────────

        /// <summary>Unique key (e.g. "delta_v", "mass", "reliability").</summary>
        public string Key { get; }

        /// <summary>Human-readable label shown next to the bar (e.g. "Menzil (Δv)").</summary>
        public string Label { get; }

        // ── Live state ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Bar fill ratio in [0, 1]. 0 = empty, 1 = full.
        /// Clipped to [0, 1] on assignment.
        /// </summary>
        public float FillRatio
        {
            get => _fillRatio;
            set => _fillRatio = Math.Max(0f, Math.Min(1f, value));
        }
        private float _fillRatio;

        /// <summary>
        /// Traffic-light color code derived from validation results.
        /// </summary>
        public ResultBarStatus Status { get; set; } = ResultBarStatus.Neutral;

        /// <summary>
        /// Compact value string shown to the right of the bar
        /// (e.g. "14 350 / 13 500 m/s", "%94", "$48.2 M").
        /// GDD §8.3: formulas not shown, only outcome values.
        /// </summary>
        public string ValueText { get; set; } = string.Empty;

        /// <summary>
        /// One-sentence "why?" tooltip text (GDD §13.2, §8.3).
        /// Explains the metric in plain language without exposing raw coefficients.
        /// </summary>
        public string ReasonText { get; set; } = string.Empty;

        // ── Constructor ────────────────────────────────────────────────────────────

        public ResultBarViewModel(string key, string label)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be empty.", nameof(key));
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));

            Key = key;
            Label = label;
        }

        // ── Factory helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a bar with Good status and computed fill ratio.
        /// </summary>
        public static ResultBarViewModel Good(string key, string label, float fill, string valueText, string reasonText = null)
            => Create(key, label, fill, ResultBarStatus.Good, valueText, reasonText);

        /// <summary>
        /// Creates a bar with Warning status and computed fill ratio.
        /// </summary>
        public static ResultBarViewModel Warn(string key, string label, float fill, string valueText, string reasonText = null)
            => Create(key, label, fill, ResultBarStatus.Warning, valueText, reasonText);

        /// <summary>
        /// Creates a bar with Error (blocking) status and computed fill ratio.
        /// </summary>
        public static ResultBarViewModel Error(string key, string label, float fill, string valueText, string reasonText = null)
            => Create(key, label, fill, ResultBarStatus.Error, valueText, reasonText);

        private static ResultBarViewModel Create(string key, string label, float fill, ResultBarStatus status, string valueText, string reasonText)
        {
            return new ResultBarViewModel(key, label)
            {
                FillRatio   = fill,
                Status      = status,
                ValueText   = valueText ?? string.Empty,
                ReasonText  = reasonText ?? string.Empty
            };
        }

        public override string ToString() =>
            $"[ResultBar:{Key}] {Status} fill={FillRatio:P0} val='{ValueText}'";
    }
}
