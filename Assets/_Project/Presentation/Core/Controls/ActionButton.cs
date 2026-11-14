using System;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Tooltips;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Controls
{
    /// <summary>
    /// Composite action button implementing the "bastığımda ne olacak?" contract:
    /// icon + action label + price + duration on one line (e.g. "İnşa Et · 12 M$ · 20 gün"),
    /// a hover tooltip describing the result, and an explicit reason + warning tooltip
    /// when the action is unavailable.
    /// </summary>
    public sealed class ActionButton : VisualElement
    {
        public const string UssClassName = "apogee-action-btn";

        private readonly Action _onClick;
        private readonly Label _labelElement;
        private readonly Label _priceElement;
        private readonly Label _durationElement;
        private readonly Func<TooltipData> _tooltipFactory;

        private string _resultDescription;
        private bool _available = true;
        private string _unavailableReason;

        /// <summary>
        /// Creates the button. <paramref name="resultDescription"/> feeds the hover
        /// tooltip ("buna basarsam şu olur"); keep it outcome-oriented.
        /// </summary>
        public ActionButton(IconType icon, string label, Action onClick, string resultDescription = null)
        {
            _onClick = onClick ?? throw new ArgumentNullException(nameof(onClick));
            _resultDescription = resultDescription;

            AddToClassList(UssClassName);
            Add(IconElement.Create(icon, 15f));

            _labelElement = new Label(label ?? string.Empty);
            _labelElement.AddToClassList("apogee-action-btn--label");
            _labelElement.pickingMode = PickingMode.Ignore;
            Add(_labelElement);

            _priceElement = new Label(string.Empty);
            _priceElement.AddToClassList("apogee-action-btn--meta");
            _priceElement.pickingMode = PickingMode.Ignore;
            _priceElement.style.display = DisplayStyle.None;
            Add(_priceElement);

            _durationElement = new Label(string.Empty);
            _durationElement.AddToClassList("apogee-action-btn--meta");
            _durationElement.pickingMode = PickingMode.Ignore;
            _durationElement.style.display = DisplayStyle.None;
            Add(_durationElement);

            _tooltipFactory = BuildTooltip;
            this.AddManipulator(new TooltipManipulator(_tooltipFactory));
            RegisterCallback<ClickEvent>(OnClicked);
        }

        /// <summary>Variants: null, "primary", "green", "danger".</summary>
        public void SetVariant(string variant)
        {
            RemoveFromClassList("apogee-action-btn--primary");
            RemoveFromClassList("apogee-action-btn--green");
            RemoveFromClassList("apogee-action-btn--danger");
            if (!string.IsNullOrEmpty(variant))
            {
                AddToClassList($"apogee-action-btn--{variant}");
            }
        }

        public void SetLabel(string label)
        {
            _labelElement.text = label ?? string.Empty;
        }

        /// <summary>Sets the price segment, e.g. "12 M$". Null hides it.</summary>
        public void SetPrice(string priceText)
        {
            SetMeta(_priceElement, priceText);
        }

        /// <summary>Sets the duration segment, e.g. "20 gün". Null hides it.</summary>
        public void SetDuration(string durationText)
        {
            SetMeta(_durationElement, durationText);
        }

        /// <summary>
        /// Enables/disables the button. A disabled button stays hoverable so the
        /// player can read <paramref name="unavailableReason"/> in its tooltip.
        /// </summary>
        public void SetAvailable(bool available, string unavailableReason = null)
        {
            _available = available;
            _unavailableReason = available ? null : unavailableReason;

            if (available)
            {
                RemoveFromClassList("apogee-action-btn--disabled");
            }
            else
            {
                AddToClassList("apogee-action-btn--disabled");
            }
        }

        public void SetResultDescription(string resultDescription)
        {
            _resultDescription = resultDescription;
        }

        private void SetMeta(Label element, string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            element.text = visible ? text : string.Empty;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private TooltipData BuildTooltip()
        {
            if (!_available)
            {
                return new TooltipData(
                    _labelElement.text,
                    _resultDescription ?? string.Empty,
                    reason: _unavailableReason ?? "Şu anda kullanılamıyor.",
                    severity: TooltipSeverity.Warning);
            }

            string content = _resultDescription;
            if (!string.IsNullOrEmpty(_priceElement.text))
            {
                content = string.IsNullOrEmpty(content)
                    ? $"Maliyet: {_priceElement.text}"
                    : $"{content} (Maliyet: {_priceElement.text})";
            }

            return new TooltipData(_labelElement.text, content ?? string.Empty, severity: TooltipSeverity.Info);
        }

        private void OnClicked(ClickEvent evt)
        {
            if (!_available)
            {
                return;
            }

            evt.StopPropagation();
            _onClick();
        }
    }
}
