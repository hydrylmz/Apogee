using System;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Dialogs
{
    /// <summary>
    /// Modal confirmation overlay for irreversible actions (cancel, finalize, sign...).
    /// Writes the outcome explicitly (interaction contract #2): title, consequence text,
    /// then confirm/cancel. Blocks input behind a dim backdrop until dismissed.
    /// </summary>
    public sealed class ConfirmDialog : VisualElement
    {
        public const string BackdropClassName = "apogee-confirm-backdrop";

        private readonly Action _onConfirm;
        private readonly Action _onCancel;

        private ConfirmDialog(
            string title,
            string message,
            string confirmLabel,
            string cancelLabel,
            Action onConfirm,
            Action onCancel,
            bool destructive)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // The dialog root IS the dim backdrop: fullscreen overlay, blocks input behind it.
            AddToClassList(BackdropClassName);
            pickingMode = PickingMode.Position;

            var panel = new VisualElement();
            panel.AddToClassList("apogee-confirm-panel");

            // Title row with warning icon.
            var titleRow = new VisualElement();
            titleRow.AddToClassList("apogee-confirm-title-row");
            titleRow.Add(IconElement.Create(destructive ? IconType.Warning : IconType.Risk, 20f));
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("apogee-confirm-title");
            titleRow.Add(titleLabel);
            panel.Add(titleRow);

            var messageLabel = new Label(message ?? string.Empty);
            messageLabel.AddToClassList("apogee-confirm-message");
            panel.Add(messageLabel);

            var buttonsRow = new VisualElement();
            buttonsRow.AddToClassList("apogee-confirm-buttons");

            var cancelBtn = new Button(Cancel) { text = string.IsNullOrEmpty(cancelLabel) ? "Vazgeç" : cancelLabel };
            cancelBtn.AddToClassList("apogee-button");
            cancelBtn.style.marginRight = 8;
            buttonsRow.Add(cancelBtn);

            var confirmBtn = new Button(Confirm) { text = string.IsNullOrEmpty(confirmLabel) ? "Onayla" : confirmLabel };
            confirmBtn.AddToClassList("apogee-button");
            if (destructive)
            {
                confirmBtn.AddToClassList("apogee-button--danger");
            }
            else
            {
                confirmBtn.AddToClassList("apogee-button--primary");
            }

            buttonsRow.Add(confirmBtn);
            panel.Add(buttonsRow);

            Add(panel);

            // Clicking the dim backdrop cancels; clicks inside the panel do not leak.
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                {
                    Cancel();
                }
            });
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            focusable = true;
        }
        /// <summary>
        /// Shows a modal confirmation over <paramref name="host"/> and removes itself
        /// when the user confirms or cancels.
        /// </summary>
        /// <param name="host">Overlay parent; usually the screen root or the UI root.</param>
        /// <param name="title">What is being asked, e.g. "Tesisi yükselt".</param>
        /// <param name="message">Explicit outcome, e.g. "12 M$ harcanır; bu işlem geri alınamaz."</param>
        /// <param name="confirmLabel">Confirm button text, e.g. "Yükselt · 12 M$".</param>
        /// <param name="onConfirm">Executed when the user confirms (dialog already removed).</param>
        /// <param name="onCancel">Optional callback when the user cancels.</param>
        /// <param name="destructive">Red-styled confirm button for lossy actions.</param>
        public static ConfirmDialog Show(
            VisualElement host,
            string title,
            string message,
            string confirmLabel,
            Action onConfirm,
            Action onCancel = null,
            bool destructive = false)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var dialog = new ConfirmDialog(title, message, confirmLabel, null, onConfirm, onCancel, destructive);
            host.Add(dialog);
            dialog.Focus();
            return dialog;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Cancel();
                }
                else
                {
                    Confirm();
                }

                evt.StopPropagation();
            }
        }

        /// <summary>Programmatic confirm (same path as the confirm button).</summary>
        public void Confirm()
        {
            Close();
            _onConfirm?.Invoke();
        }

        /// <summary>Programmatic cancel (same path as the cancel button / backdrop / Esc).</summary>
        public void Cancel()
        {
            Close();
            _onCancel?.Invoke();
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
