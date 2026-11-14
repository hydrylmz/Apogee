using System;
using System.Collections.Generic;
using Apogee.Design;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// UI Toolkit view component rendering the interactive 2D modular cross-section of the rocket stack
    /// and spacecraft payload (GDD §7.2, §14.3, §15.1).
    /// Features auto-snapped modules, supplier color stripes, dashed empty slots, and two-way slot selection sync.
    /// Acceptance criterion: 3-stage rocket + payload automatically assembled and snapped.
    /// </summary>
    public class RocketCrossSectionView : VisualElement
    {
        private readonly RocketCrossSectionViewModel _viewModel;

        private Label _headerBadge;
        private Label _cadStatusLabel;
        private VisualElement _stackContainer;
        private ScrollView _scrollView;
        private Label _footerCalloutLabel;

        private readonly Dictionary<string, VisualElement> _partElements = new();

        public RocketCrossSectionView(RocketCrossSectionViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            AddToClassList("apogee-cad-view");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            BuildLayout();
            BindViewModel();
        }

        private void BuildLayout()
        {
            // ── Top CAD Control Bar ─────────────────────────────────────────────────
            var topBar = new VisualElement();
            topBar.AddToClassList("apogee-cad-topbar");
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.FlexStart;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingTop = 10;
            topBar.style.paddingBottom = 12;
            topBar.style.paddingLeft = 4;
            topBar.style.paddingRight = 4;
            topBar.style.borderBottomWidth = 1;
            topBar.style.borderBottomColor = new Color(0.08f, 0.18f, 0.30f);
            topBar.style.marginBottom = 4;

            var titleBox = new VisualElement();
            titleBox.style.flexDirection = FlexDirection.Column;
            titleBox.style.flexShrink = 1;
            titleBox.style.minWidth = 0;

            var title = new Label("Modüler Kesit Montajı");
            title.AddToClassList("apogee-panel-title");
            title.style.fontSize = 14;
            title.style.color = new Color(0.22f, 0.78f, 1.0f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            title.style.overflow = Overflow.Hidden;
            title.style.textOverflow = TextOverflow.Ellipsis;
            titleBox.Add(title);

            _cadStatusLabel = new Label("3 Aşamalı Fırlatıcı + Yük");
            _cadStatusLabel.style.fontSize = 10;
            _cadStatusLabel.style.color = new Color(0.42f, 0.56f, 0.72f);
            _cadStatusLabel.style.marginTop = 1;
            _cadStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            titleBox.Add(_cadStatusLabel);
            topBar.Add(titleBox);

            var badgeRow = new VisualElement();
            badgeRow.style.flexDirection = FlexDirection.Row;
            badgeRow.style.alignItems = Align.Center;
            badgeRow.style.flexShrink = 0;
            badgeRow.style.marginLeft = StyleKeyword.Auto;

            _headerBadge = new Label("SNAP: KENETLİ");
            _headerBadge.AddToClassList("apogee-badge");
            _headerBadge.AddToClassList("apogee-badge--cyan");
            _headerBadge.style.fontSize = 10;
            _headerBadge.style.paddingLeft = 8;
            _headerBadge.style.paddingRight = 8;
            _headerBadge.style.paddingTop = 4;
            _headerBadge.style.paddingBottom = 4;
            _headerBadge.style.whiteSpace = WhiteSpace.NoWrap;
            _headerBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badgeRow.Add(_headerBadge);
            topBar.Add(badgeRow);

            Add(topBar);

            // ── Main Blueprint CAD Viewport with Grid & Stack ───────────────────────
            var viewportContainer = new VisualElement();
            viewportContainer.AddToClassList("apogee-cad-viewport");
            viewportContainer.style.flexGrow = 1;
            viewportContainer.style.flexDirection = FlexDirection.Row;
            viewportContainer.style.marginTop = 6;
            viewportContainer.style.position = Position.Relative;
            viewportContainer.style.backgroundColor = new Color(0.025f, 0.045f, 0.08f);
            SetBorderWidth(viewportContainer.style, 1);
            SetBorderColor(viewportContainer.style, new Color(0.10f, 0.20f, 0.34f));
            viewportContainer.style.borderTopLeftRadius = 10;
            viewportContainer.style.borderTopRightRadius = 10;
            viewportContainer.style.borderBottomLeftRadius = 10;
            viewportContainer.style.borderBottomRightRadius = 10;

            // Height Ruler (Left side) — genişletilmiş, daha okunaklı
            var ruler = new VisualElement();
            ruler.AddToClassList("apogee-cad-ruler");
            ruler.style.width = 48;
            ruler.style.borderRightWidth = 1;
            ruler.style.borderRightColor = new Color(0.08f, 0.16f, 0.28f);
            ruler.style.justifyContent = Justify.SpaceBetween;
            ruler.style.paddingTop = 20;
            ruler.style.paddingBottom = 20;
            ruler.style.paddingLeft = 6;
            ruler.style.paddingRight = 4;
            ruler.style.backgroundColor = new Color(0.02f, 0.04f, 0.07f);

            var rLabels = new[] { "65m", "50m", "35m", "20m", "5m", "0m" };
            foreach (var r in rLabels)
            {
                var rulerRow = new VisualElement();
                rulerRow.style.flexDirection = FlexDirection.Row;
                rulerRow.style.alignItems = Align.Center;

                var tick = new VisualElement();
                tick.style.width = 6;
                tick.style.height = 1;
                tick.style.backgroundColor = new Color(0.22f, 0.38f, 0.55f);
                tick.style.marginRight = 3;
                rulerRow.Add(tick);

                var lbl = new Label(r);
                lbl.style.fontSize = 9;
                lbl.style.color = new Color(0.30f, 0.42f, 0.58f);
                rulerRow.Add(lbl);

                ruler.Add(rulerRow);
            }
            viewportContainer.Add(ruler);

            // Scrollable Assembly Area
            _scrollView = new ScrollView();
            _scrollView.style.flexGrow = 1;
            _scrollView.style.paddingTop = 24;
            _scrollView.style.paddingBottom = 24;
            _scrollView.style.paddingLeft = 32;
            _scrollView.style.paddingRight = 32;

            _stackContainer = new VisualElement();
            _stackContainer.AddToClassList("apogee-cad-stack");
            _stackContainer.style.alignItems = Align.Center;
            _stackContainer.style.width = Length.Percent(100);

            _scrollView.Add(_stackContainer);
            viewportContainer.Add(_scrollView);

            Add(viewportContainer);

            // ── Footer Callout strip ────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.AddToClassList("apogee-cad-footer");
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 4;
            footer.style.marginTop = 6;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.08f, 0.15f, 0.25f);

            var footerIcon = new Label("");
            footerIcon.style.fontSize = 11;
            footerIcon.style.color = new Color(0.22f, 0.78f, 1.0f);
            footerIcon.style.marginRight = 6;
            footer.Add(footerIcon);

            _footerCalloutLabel = new Label("Bileşen detaylarını görmek veya düzenlemek için bir parçaya tıklayın.");
            _footerCalloutLabel.style.fontSize = 11;
            _footerCalloutLabel.style.color = new Color(0.42f, 0.56f, 0.70f);
            _footerCalloutLabel.style.flexGrow = 1;
            footer.Add(_footerCalloutLabel);

            Add(footer);
        }

        private void BindViewModel()
        {
            _viewModel.Nodes.Subscribe(RenderNodes);
            _viewModel.StatusSummary.Subscribe(status =>
            {
                if (_cadStatusLabel != null) _cadStatusLabel.text = status;
            });
            _viewModel.SelectedNode.Subscribe(node =>
            {
                HighlightSelectedNode(node);
                UpdateFooterCallout(node);
            });
            _viewModel.HoveredNode.Subscribe(node =>
            {
                if (node != null) UpdateFooterCallout(node);
                else UpdateFooterCallout(_viewModel.SelectedNode.Value);
            });

            // Initial render
            RenderNodes(_viewModel.Nodes.Value);
        }

        public void RenderNodes(IReadOnlyList<CrossSectionNode> nodes)
        {
            _stackContainer.Clear();
            _partElements.Clear();

            if (nodes == null || nodes.Count == 0)
            {
                var emptyLabel = new Label("Montaj için parça verisi bulunamadı.");
                emptyLabel.style.color = new Color(0.45f, 0.55f, 0.68f);
                emptyLabel.style.marginTop = 40;
                _stackContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var element = CreatePartVisualElement(node, i == 0, i == nodes.Count - 1);
                _partElements[node.Id] = element;
                _stackContainer.Add(element);

                // Add snap connecting joint line between nodes
                if (i < nodes.Count - 1)
                {
                    var snapConnector = CreateSnapConnector();
                    _stackContainer.Add(snapConnector);
                }
            }

            HighlightSelectedNode(_viewModel.SelectedNode.Value);
        }

        private VisualElement CreatePartVisualElement(CrossSectionNode node, bool isFirst, bool isLast)
        {
            var part = new VisualElement();
            part.AddToClassList("apogee-cad-part");

            // Apply relative width scaled to viewport — daha geniş ve daha okunaklı
            float basePixelWidth = 440f;
            float width = Math.Max(300f, (float)(node.RelativeWidth * basePixelWidth));
            part.style.width = width;
            part.style.minWidth = 300f;
            part.style.minHeight = (float)(node.RelativeHeight * 54f);  // daha yüksek parçalar
            part.style.flexDirection = FlexDirection.Row;
            part.style.alignItems = Align.Center;
            part.style.position = Position.Relative;
            SetBorderWidth(part.style, 1);
            part.style.paddingLeft = 14;
            part.style.paddingRight = 14;
            part.style.paddingTop = 8;
            part.style.paddingBottom = 8;
            part.style.marginBottom = 1;

            // Geometry styling per part type
            switch (node.PartType)
            {
                case CrossSectionPartType.FairingNose:
                    part.style.borderTopLeftRadius = 32;
                    part.style.borderTopRightRadius = 32;
                    part.style.borderBottomLeftRadius = 6;
                    part.style.borderBottomRightRadius = 6;
                    part.style.backgroundColor = new Color(0.07f, 0.13f, 0.22f);
                    SetBorderColor(part.style, new Color(0.18f, 0.42f, 0.72f));
                    break;

                case CrossSectionPartType.StageEngine:
                    part.style.borderBottomLeftRadius = 20;
                    part.style.borderBottomRightRadius = 20;
                    part.style.borderTopLeftRadius = 4;
                    part.style.borderTopRightRadius = 4;
                    part.style.backgroundColor = new Color(0.06f, 0.10f, 0.17f);
                    SetBorderColor(part.style, new Color(0.18f, 0.34f, 0.54f));
                    break;

                case CrossSectionPartType.InterstageDecoupler:
                    part.style.minHeight = 20;
                    part.style.borderTopLeftRadius = 2;
                    part.style.borderTopRightRadius = 2;
                    part.style.borderBottomLeftRadius = 2;
                    part.style.borderBottomRightRadius = 2;
                    part.style.backgroundColor = new Color(0.10f, 0.15f, 0.22f);
                    SetBorderColor(part.style, new Color(0.28f, 0.42f, 0.60f));
                    break;

                case CrossSectionPartType.AeroFins:
                    part.style.borderBottomLeftRadius = 12;
                    part.style.borderBottomRightRadius = 12;
                    part.style.borderTopLeftRadius = 4;
                    part.style.borderTopRightRadius = 4;
                    part.style.backgroundColor = new Color(0.05f, 0.09f, 0.15f);
                    SetBorderColor(part.style, new Color(0.22f, 0.36f, 0.56f));
                    break;

                default:
                    part.style.borderTopLeftRadius = 6;
                    part.style.borderTopRightRadius = 6;
                    part.style.borderBottomLeftRadius = 6;
                    part.style.borderBottomRightRadius = 6;
                    part.style.backgroundColor = new Color(0.055f, 0.095f, 0.155f);
                    SetBorderColor(part.style, new Color(0.14f, 0.26f, 0.42f));
                    break;
            }

            // Empty Slot style
            if (node.IsEmptySlot)
            {
                part.AddToClassList("apogee-cad-part--empty");
                part.style.backgroundColor = new Color(0.04f, 0.07f, 0.11f, 0.55f);
                SetBorderColor(part.style, new Color(0.36f, 0.46f, 0.58f, 0.5f));
            }

            // Supplier color stripe (GDD §15.1) — daha kalın, daha belirgin
            var stripe = new VisualElement();
            stripe.AddToClassList("apogee-cad-stripe");
            stripe.style.width = 5;
            stripe.style.height = Length.Percent(75);
            stripe.style.marginRight = 10;
            stripe.style.borderTopLeftRadius = 3;
            stripe.style.borderBottomLeftRadius = 3;
            if (ColorUtility.TryParseHtmlString(node.SupplierColorHex, out var stripeColor))
            {
                stripe.style.backgroundColor = stripeColor;
            }
            else
            {
                stripe.style.backgroundColor = new Color(0.22f, 0.74f, 0.97f);
            }
            part.Add(stripe);

            // Text Info Box
            var textBox = new VisualElement();
            textBox.style.flexGrow = 1;
            textBox.style.justifyContent = Justify.Center;

            var titleLabel = new Label(node.Title);
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = node.IsEmptySlot ? new Color(0.55f, 0.62f, 0.70f) : new Color(0.92f, 0.96f, 1.0f);
            textBox.Add(titleLabel);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subLabel = new Label(node.Subtitle);
                subLabel.style.fontSize = 10;
                subLabel.style.color = new Color(0.38f, 0.52f, 0.68f);
                subLabel.style.marginTop = 2;
                textBox.Add(subLabel);
            }
            part.Add(textBox);

            // Right Tag / Badge — daha büyük ve okunaklı
            var tagBadge = new Label(node.IsEmptySlot ? "[ BOŞ ]" : GetCategoryBadgeText(node.PartType));
            tagBadge.style.fontSize = 9;
            tagBadge.style.color = node.IsEmptySlot ? new Color(0.98f, 0.65f, 0.12f) : new Color(0.20f, 0.76f, 1.0f);
            tagBadge.style.backgroundColor = node.IsEmptySlot
                ? new Color(0.18f, 0.12f, 0.04f)
                : new Color(0.06f, 0.14f, 0.26f);
            tagBadge.style.paddingLeft = 7;
            tagBadge.style.paddingRight = 7;
            tagBadge.style.paddingTop = 3;
            tagBadge.style.paddingBottom = 3;
            tagBadge.style.borderTopLeftRadius = 4;
            tagBadge.style.borderTopRightRadius = 4;
            tagBadge.style.borderBottomLeftRadius = 4;
            tagBadge.style.borderBottomRightRadius = 4;
            tagBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            part.Add(tagBadge);

            // Click & Hover Events
            part.RegisterCallback<ClickEvent>(evt =>
            {
                _viewModel.RequestSelectPart(node);
                evt.StopPropagation();
            });

            part.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _viewModel.SetHoveredNode(node);
                SetBorderWidth(part.style, 2);
                if (!IsSelectedNode(node))
                {
                    SetBorderColor(part.style, new Color(0.28f, 0.68f, 1.0f));
                }
            });

            part.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _viewModel.SetHoveredNode(null);
                if (!IsSelectedNode(node))
                {
                    SetBorderWidth(part.style, 1);
                    SetBorderColor(part.style, node.IsEmptySlot
                        ? new Color(0.36f, 0.46f, 0.58f, 0.5f)
                        : new Color(0.14f, 0.26f, 0.42f));
                }
            });

            return part;
        }

        private VisualElement CreateSnapConnector()
        {
            // Daha belirgin snap konnektör — uzay aracı bağlantı noktası estetiği
            var joint = new VisualElement();
            joint.style.width = 60;
            joint.style.height = 14;
            joint.style.alignItems = Align.Center;
            joint.style.justifyContent = Justify.Center;
            joint.style.flexDirection = FlexDirection.Row;

            // Sol yatay çizgi
            var lineLeft = new VisualElement();
            lineLeft.style.width = 20;
            lineLeft.style.height = 1;
            lineLeft.style.backgroundColor = new Color(0.15f, 0.35f, 0.58f, 0.6f);
            joint.Add(lineLeft);

            // Merkez pin noktası
            var pin = new VisualElement();
            pin.style.width = 8;
            pin.style.height = 8;
            pin.style.borderTopLeftRadius = 4;
            pin.style.borderTopRightRadius = 4;
            pin.style.borderBottomLeftRadius = 4;
            pin.style.borderBottomRightRadius = 4;
            pin.style.backgroundColor = new Color(0.22f, 0.74f, 0.97f, 0.85f);
            pin.style.marginLeft = 4;
            pin.style.marginRight = 4;
            joint.Add(pin);

            // Sağ yatay çizgi
            var lineRight = new VisualElement();
            lineRight.style.width = 20;
            lineRight.style.height = 1;
            lineRight.style.backgroundColor = new Color(0.15f, 0.35f, 0.58f, 0.6f);
            joint.Add(lineRight);

            return joint;
        }

        private void HighlightSelectedNode(CrossSectionNode selectedNode)
        {
            foreach (var kvp in _partElements)
            {
                var element = kvp.Value;
                bool isSelected = selectedNode != null && kvp.Key == selectedNode.Id;

                if (isSelected)
                {
                    SetBorderColor(element.style, new Color(0.18f, 0.88f, 1.0f));
                    SetBorderWidth(element.style, 2);
                    element.style.backgroundColor = new Color(0.06f, 0.16f, 0.32f);
                }
                else
                {
                    SetBorderWidth(element.style, 1);
                    SetBorderColor(element.style, new Color(0.14f, 0.26f, 0.42f));
                    element.style.backgroundColor = new Color(0.055f, 0.095f, 0.155f);
                }
            }
        }

        private bool IsSelectedNode(CrossSectionNode node)
        {
            var current = _viewModel.SelectedNode.Value;
            return current != null && current.Id == node.Id;
        }

        private void UpdateFooterCallout(CrossSectionNode node)
        {
            if (_footerCalloutLabel == null) return;

            if (node == null)
            {
                _footerCalloutLabel.text = "Bileşen detaylarını görmek veya düzenlemek için bir parçaya tıklayın.";
                return;
            }

            string slotStatus = node.SlotId.HasValue ? $"[Slot: {node.SlotId.Value}]" : "[Montaj Gövdesi]";
            string supplier = node.Supplier != null ? $" • Tedarikçi: {node.Supplier.Name}" : "";
            string emptyNotice = node.IsEmptySlot ? " • [DİKKAT: Henüz bileşen seçilmedi]" : "";

            _footerCalloutLabel.text = $"{node.Title} {slotStatus}{supplier}{emptyNotice}";
        }

        private static string GetCategoryBadgeText(CrossSectionPartType partType)
        {
            return partType switch
            {
                CrossSectionPartType.FairingNose => "KAPORTA",
                CrossSectionPartType.PayloadInstrument => "ALET",
                CrossSectionPartType.PayloadBus => "BUS",
                CrossSectionPartType.PayloadSubsystem => "SİSTEM",
                CrossSectionPartType.PayloadEdl => "EDL",
                CrossSectionPartType.InterstageDecoupler => "DEKUPLÖR",
                CrossSectionPartType.StageTank => "TANK",
                CrossSectionPartType.StageEngine => "MOTOR",
                CrossSectionPartType.AeroFins => "KANAT",
                _ => "PARÇA"
            };
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
    }
}
