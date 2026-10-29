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
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingBottom = 8;
            topBar.style.borderBottomWidth = 1;
            topBar.style.borderBottomColor = new Color(0.12f, 0.22f, 0.35f);

            var titleBox = new VisualElement();
            var title = new Label("MODÜLER KESİT MONTAJI");
            title.AddToClassList("apogee-panel-title");
            title.style.fontSize = 13;
            title.style.color = new Color(0.22f, 0.74f, 0.97f);
            titleBox.Add(title);

            _cadStatusLabel = new Label("3 Aşamalı Fırlatıcı + Yük");
            _cadStatusLabel.style.fontSize = 10;
            _cadStatusLabel.style.color = new Color(0.58f, 0.65f, 0.75f);
            titleBox.Add(_cadStatusLabel);
            topBar.Add(titleBox);

            _headerBadge = new Label("Snap: Kenetli [✓]");
            _headerBadge.AddToClassList("apogee-badge");
            _headerBadge.AddToClassList("apogee-badge--cyan");
            _headerBadge.style.fontSize = 10;
            topBar.Add(_headerBadge);

            Add(topBar);

            // ── Main Blueprint CAD Viewport with Grid & Stack ───────────────────────
            var viewportContainer = new VisualElement();
            viewportContainer.AddToClassList("apogee-cad-viewport");
            viewportContainer.style.flexGrow = 1;
            viewportContainer.style.flexDirection = FlexDirection.Row;
            viewportContainer.style.marginTop = 8;
            viewportContainer.style.position = Position.Relative;
            viewportContainer.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f);
            SetBorderWidth(viewportContainer.style, 1);
            SetBorderColor(viewportContainer.style, new Color(0.12f, 0.24f, 0.38f));
            viewportContainer.style.borderTopLeftRadius = 6;
            viewportContainer.style.borderTopRightRadius = 6;
            viewportContainer.style.borderBottomLeftRadius = 6;
            viewportContainer.style.borderBottomRightRadius = 6;

            // Height Ruler (Left side)
            var ruler = new VisualElement();
            ruler.AddToClassList("apogee-cad-ruler");
            ruler.style.width = 36;
            ruler.style.borderRightWidth = 1;
            ruler.style.borderRightColor = new Color(0.12f, 0.22f, 0.35f);
            ruler.style.justifyContent = Justify.SpaceBetween;
            ruler.style.paddingTop = 12;
            ruler.style.paddingBottom = 12;
            ruler.style.paddingLeft = 4;
            ruler.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f);

            var rLabels = new[] { "65m", "50m", "35m", "20m", "5m", "0m" };
            foreach (var r in rLabels)
            {
                var lbl = new Label(r);
                lbl.style.fontSize = 9;
                lbl.style.color = new Color(0.35f, 0.45f, 0.58f);
                ruler.Add(lbl);
            }
            viewportContainer.Add(ruler);

            // Scrollable Assembly Area
            _scrollView = new ScrollView();
            _scrollView.style.flexGrow = 1;
            _scrollView.style.paddingTop = 16;
            _scrollView.style.paddingBottom = 16;
            _scrollView.style.paddingLeft = 20;
            _scrollView.style.paddingRight = 20;

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
            footer.style.paddingTop = 6;
            footer.style.marginTop = 4;

            _footerCalloutLabel = new Label("Bileşen detaylarını görmek veya düzenlemek için bir parçaya tıklayın.");
            _footerCalloutLabel.style.fontSize = 11;
            _footerCalloutLabel.style.color = new Color(0.58f, 0.65f, 0.75f);
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

            // Apply relative width scaled to viewport
            float basePixelWidth = 260f;
            float width = (float)(node.RelativeWidth * basePixelWidth);
            part.style.width = width;
            part.style.minHeight = (float)(node.RelativeHeight * 36f);
            part.style.flexDirection = FlexDirection.Row;
            part.style.alignItems = Align.Center;
            part.style.position = Position.Relative;
            SetBorderWidth(part.style, 1);
            part.style.paddingLeft = 8;
            part.style.paddingRight = 8;
            part.style.paddingTop = 4;
            part.style.paddingBottom = 4;

            // Geometry styling per part type
            switch (node.PartType)
            {
                case CrossSectionPartType.FairingNose:
                    part.style.borderTopLeftRadius = 24;
                    part.style.borderTopRightRadius = 24;
                    part.style.backgroundColor = new Color(0.08f, 0.14f, 0.22f);
                    SetBorderColor(part.style, new Color(0.22f, 0.44f, 0.68f));
                    break;

                case CrossSectionPartType.StageEngine:
                    part.style.borderBottomLeftRadius = 14;
                    part.style.borderBottomRightRadius = 14;
                    part.style.backgroundColor = new Color(0.07f, 0.11f, 0.17f);
                    SetBorderColor(part.style, new Color(0.20f, 0.35f, 0.52f));
                    break;

                case CrossSectionPartType.InterstageDecoupler:
                    part.style.minHeight = 14;
                    part.style.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
                    SetBorderColor(part.style, new Color(0.35f, 0.45f, 0.58f));
                    break;

                case CrossSectionPartType.AeroFins:
                    part.style.borderBottomLeftRadius = 8;
                    part.style.borderBottomRightRadius = 8;
                    part.style.backgroundColor = new Color(0.06f, 0.10f, 0.15f);
                    SetBorderColor(part.style, new Color(0.25f, 0.38f, 0.55f));
                    break;

                default:
                    part.style.borderTopLeftRadius = 4;
                    part.style.borderTopRightRadius = 4;
                    part.style.borderBottomLeftRadius = 4;
                    part.style.borderBottomRightRadius = 4;
                    part.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f);
                    SetBorderColor(part.style, new Color(0.16f, 0.28f, 0.42f));
                    break;
            }

            // Empty Slot style
            if (node.IsEmptySlot)
            {
                part.AddToClassList("apogee-cad-part--empty");
                part.style.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.6f);
                SetBorderColor(part.style, new Color(0.40f, 0.48f, 0.58f, 0.6f));
            }

            // Supplier color stripe (GDD §15.1)
            var stripe = new VisualElement();
            stripe.AddToClassList("apogee-cad-stripe");
            stripe.style.width = 4;
            stripe.style.height = Length.Percent(80);
            stripe.style.marginRight = 6;
            stripe.style.borderTopLeftRadius = 2;
            stripe.style.borderBottomLeftRadius = 2;
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
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = node.IsEmptySlot ? new Color(0.6f, 0.65f, 0.72f) : Color.white;
            textBox.Add(titleLabel);

            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subLabel = new Label(node.Subtitle);
                subLabel.style.fontSize = 9;
                subLabel.style.color = new Color(0.45f, 0.55f, 0.68f);
                textBox.Add(subLabel);
            }
            part.Add(textBox);

            // Right Tag / Badge
            var tagBadge = new Label(node.IsEmptySlot ? "[BOŞ]" : GetCategoryBadgeText(node.PartType));
            tagBadge.style.fontSize = 8;
            tagBadge.style.color = node.IsEmptySlot ? new Color(0.95f, 0.60f, 0.15f) : new Color(0.22f, 0.74f, 0.97f);
            tagBadge.style.backgroundColor = new Color(0.08f, 0.15f, 0.25f);
            tagBadge.style.paddingLeft = 4;
            tagBadge.style.paddingRight = 4;
            tagBadge.style.paddingTop = 2;
            tagBadge.style.paddingBottom = 2;
            tagBadge.style.borderTopLeftRadius = 3;
            tagBadge.style.borderTopRightRadius = 3;
            tagBadge.style.borderBottomLeftRadius = 3;
            tagBadge.style.borderBottomRightRadius = 3;
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
                    SetBorderColor(part.style, new Color(0.35f, 0.65f, 0.95f));
                }
            });

            part.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _viewModel.SetHoveredNode(null);
                if (!IsSelectedNode(node))
                {
                    SetBorderWidth(part.style, 1);
                    SetBorderColor(part.style, node.IsEmptySlot
                        ? new Color(0.40f, 0.48f, 0.58f, 0.6f)
                        : new Color(0.16f, 0.28f, 0.42f));
                }
            });

            return part;
        }

        private VisualElement CreateSnapConnector()
        {
            var joint = new VisualElement();
            joint.style.width = 12;
            joint.style.height = 6;
            joint.style.alignItems = Align.Center;
            joint.style.justifyContent = Justify.Center;

            var pin = new VisualElement();
            pin.style.width = 4;
            pin.style.height = 6;
            pin.style.backgroundColor = new Color(0.22f, 0.74f, 0.97f, 0.7f);
            joint.Add(pin);

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
                    SetBorderColor(element.style, new Color(0.22f, 0.85f, 1.0f));
                    SetBorderWidth(element.style, 2);
                    element.style.backgroundColor = new Color(0.08f, 0.16f, 0.28f);
                }
                else
                {
                    SetBorderWidth(element.style, 1);
                    SetBorderColor(element.style, new Color(0.16f, 0.28f, 0.42f));
                    element.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f);
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
