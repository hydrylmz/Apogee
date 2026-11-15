using System;
using Apogee.UI.Core.Icons;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Contracts
{
    /// <summary>
    /// Contracts-themed signing popup: the offer is presented as a paper contract
    /// sheet — terms list, clause text, a signature line with a drawn stroke and
    /// a wax seal. "İmzala ve Mühürle" executes the contract; the outcome is
    /// written explicitly in the clause (interaction contract #2). Blocks input
    /// behind a dim backdrop until dismissed.
    /// </summary>
    public sealed class SignatureDialog : VisualElement
    {
        public const string BackdropClassName = "contract-sign-backdrop";
        public const string PanelClassName = "contract-sign-panel";

        private readonly Action _onSign;
        private readonly Action _onDismiss;
        private readonly Button _confirmButton;

        private SignatureDialog(
            ContractsViewModel viewModel,
            ContractOfferItemViewModel offer,
            string photoCaption,
            Texture2D photoTexture,
            Action onSign,
            Action onDismiss)
        {
            _onSign = onSign;
            _onDismiss = onDismiss;

            AddToClassList(BackdropClassName);
            pickingMode = PickingMode.Position;

            var panel = new VisualElement();
            panel.AddToClassList(PanelClassName);
            panel.style.width = 470;

            // ── Header: title + reference no ─────────────────────────────────
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var sealBadge = new VisualElement();
            sealBadge.style.width = 22;
            sealBadge.style.height = 22;
            sealBadge.style.marginRight = 8;
            sealBadge.Add(IconElement.Create(IconType.Contracts, 16f));
            sealBadge.style.color = new Color(0.643f, 0.463f, 0.122f);     // brass ink
            headerRow.Add(sealBadge);

            var title = new Label("TEDARİK SÖZLEŞMESİ");
            title.AddToClassList("contract-sign-title");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(title);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            var refLabel = new Label($"REF: {offer.OfferId.ToUpperInvariant()}");
            refLabel.AddToClassList("contract-sign-ref");
            refLabel.style.fontSize = 9;
            headerRow.Add(refLabel);

            panel.Add(headerRow);

            // ── Photo + terms side by side ───────────────────────────────────
            var bodyRow = new VisualElement();
            bodyRow.style.flexDirection = FlexDirection.Row;
            bodyRow.style.marginTop = 10;

            if (photoTexture != null)
            {
                var photo = new VisualElement();
                photo.style.width = 120;
                photo.style.height = 96;
                photo.style.flexShrink = 0;
                photo.style.marginRight = 12;
                photo.style.borderTopLeftRadius = 2;
                photo.style.borderTopRightRadius = 2;
                photo.style.borderBottomLeftRadius = 2;
                photo.style.borderBottomRightRadius = 2;
                photo.style.overflow = Overflow.Hidden;
                photo.style.backgroundImage = new StyleBackground(photoTexture);
                photo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                photo.style.backgroundColor = new Color(0.847f, 0.796f, 0.659f);
                if (!string.IsNullOrEmpty(photoCaption))
                {
                    photo.tooltip = photoCaption;
                }
                bodyRow.Add(photo);
            }

            var terms = new VisualElement();
            terms.style.flexGrow = 1;
            AddTerm(terms, "Tedarikçi", offer.SupplierName);
            AddTerm(terms, "Kalem", offer.ItemName);
            AddTerm(terms, "Maliyet", $"{offer.BaseCost:F1} M$");
            AddTerm(terms, "Teslim", $"{offer.DeliveryDays} gün");
            AddTerm(terms, "Güvenilirlik", $"x{offer.ReliabilityMultiplier:F2} — {ReliabilityNote(offer.ReliabilityMultiplier)}");
            bodyRow.Add(terms);

            panel.Add(bodyRow);
            // ── Clause: explicit outcome ─────────────────────────────────────
            var clause = new Label(
                "İmzalandığı anda maliyet bütçeden düşülür ve kalem teslim kuyruğuna girer. " +
                "Teslimden önce iptal edilebilir: ödenen tutarın yarısı iade edilir, " +
                "tedarikçiyle ilişki puanı düşer.");
            clause.AddToClassList("contract-sign-clause");
            clause.style.fontSize = 10;
            clause.style.marginTop = 10;
            clause.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(clause);

            // ── Signature area: stroke above line, seal on the right ─────────
            var signArea = new VisualElement();
            signArea.style.flexDirection = FlexDirection.Row;
            signArea.style.alignItems = Align.FlexEnd;
            signArea.style.marginTop = 14;

            var signBlock = new VisualElement();
            signBlock.style.flexGrow = 1;
            signBlock.style.marginRight = 14;

            var stroke = new SignStrokeView();
            stroke.style.height = 26;
            stroke.style.marginBottom = 2;
            signBlock.Add(stroke);

            var line = new VisualElement();
            line.AddToClassList("contract-sign-line");
            line.style.height = 1;
            signBlock.Add(line);

            var caption = new Label("Satın Alma Direktörü imzası");
            caption.AddToClassList("contract-sign-caption");
            caption.style.fontSize = 9;
            caption.style.marginTop = 3;
            signBlock.Add(caption);

            signArea.Add(signBlock);
            signArea.Add(new WaxSealView(38f));
            panel.Add(signArea);

            // ── Buttons ──────────────────────────────────────────────────────
            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            buttonsRow.style.justifyContent = Justify.FlexEnd;
            buttonsRow.style.alignItems = Align.Center;
            buttonsRow.style.marginTop = 16;

            var cancelBtn = new Button(Dismiss) { text = "Vazgeç" };
            cancelBtn.AddToClassList("apogee-button");
            cancelBtn.style.marginRight = 8;
            buttonsRow.Add(cancelBtn);

            _confirmButton = new Button(Sign) { text = $"İmzala ve Mühürle · {offer.BaseCost:F1} M$" };
            _confirmButton.AddToClassList("apogee-button");
            _confirmButton.AddToClassList("contract-sign-confirm");

            if (viewModel != null && viewModel.Budget.Value < offer.BaseCost)
            {
                _confirmButton.SetEnabled(false);
                _confirmButton.AddToClassList("contract-sign-confirm--disabled");
                _confirmButton.tooltip = $"Bütçe yetersiz: {offer.BaseCost:F1} M$ gerekli, kasada {viewModel.Budget.Value:F1} M$ var.";
            }

            buttonsRow.Add(_confirmButton);
            panel.Add(buttonsRow);

            Add(panel);

            // Backdrop click dismisses; clicks inside the paper do not leak.
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                {
                    Dismiss();
                }
            });
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            focusable = true;
        }

        /// <summary>
        /// Opens the signing dialog over <paramref name="host"/> and removes itself
        /// when the user signs or dismisses. <paramref name="onSign"/> runs after
        /// the dialog is removed; the view model call remains the caller's duty.
        /// </summary>
        public static SignatureDialog Show(
            VisualElement host,
            ContractsViewModel viewModel,
            ContractOfferItemViewModel offer,
            string photoCaption = null,
            Texture2D photoTexture = null,
            Action onSign = null,
            Action onDismiss = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (offer == null) throw new ArgumentNullException(nameof(offer));

            var dialog = new SignatureDialog(viewModel, offer, photoCaption, photoTexture, onSign, onDismiss);
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
                    Dismiss();
                }
                else
                {
                    Sign();
                }

                evt.StopPropagation();
            }
        }

        /// <summary>Programmatic sign (same path as the İmzala ve Mühürle button).</summary>
        public void Sign()
        {
            Close();
            _onSign?.Invoke();
        }

        /// <summary>Programmatic dismiss (same path as the Vazgeç button / backdrop click).</summary>
        public void Dismiss()
        {
            Close();
            _onDismiss?.Invoke();
        }

        private void Close()
        {
            RemoveFromHierarchy();
        }

        private static void AddTerm(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var key = new Label(label);
            key.AddToClassList("contract-sign-caption");
            key.style.fontSize = 10;
            key.style.width = 82;
            row.Add(key);

            var val = new Label(value);
            val.AddToClassList("paper-title");
            val.style.fontSize = 11;
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.whiteSpace = WhiteSpace.Normal;
            val.style.flexGrow = 1;
            row.Add(val);

            parent.Add(row);
        }

        private static string ReliabilityNote(double multiplier) => multiplier switch
        {
            >= 1.10 => "yüksek güvenilirlik, pahalı",
            >= 0.98 => "standart güvenilirlik",
            _ => "gecikme riski yüksek"
        };
    }
}
