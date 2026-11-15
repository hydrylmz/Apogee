using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Research
{
    /// <summary>
    /// Fixed-position layout solver for the HOI4-style tech-tree graph.
    /// Branches become horizontal swim-lanes (stacked vertically), prerequisite
    /// depth becomes columns (left → right). Node positions are computed so that
    /// cards never overlap inside a lane; cross-branch edges simply span lanes.
    /// </summary>
    public static class ResearchTreeLayout
    {
        // ── Geometry constants (pixels at zoom = 1) ──────────────────────────
        public const float LeftMargin = 150f;   // room for lane labels
        public const float TopMargin = 16f;
        public const float RightPadding = 36f;
        public const float BottomPadding = 20f;
        public const float NodeWidth = 168f;
        public const float NodeHeight = 140f;
        public const float NodeHGap = 56f;
        public const float NodeVGap = 14f;
        public const float LaneLabelStrip = 32f; // branch caption strip inside each lane

        /// <summary>User-facing lane order (Yönelim → ... → Yapı).</summary>
        public static readonly string[] LaneOrder =
        {
            "ADCS", "Comms", "EDL", "LifeSupport", "Power", "Propulsion", "Reliability", "Structure"
        };

        /// <summary>A computed rectangle for one node on the canvas.</summary>
        public readonly struct NodeSlot
        {
            public readonly string Id;
            public readonly Rect Rect;
            public NodeSlot(string id, Rect rect) { Id = id; Rect = rect; }
        }

        /// <summary>Result of a layout pass.</summary>
        public sealed class Result
        {
            public Dictionary<string, NodeSlot> Slots { get; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string[]> Edges { get; } = new();      // (sourceId, targetId)
            public List<(string branch, string label, Rect band, Rect labelRect)> Lanes { get; } = new();
            public float CanvasWidth { get; internal set; }
            public float CanvasHeight { get; internal set; }
            public int MaxDepth { get; internal set; }
        }

        /// <summary>
        /// Solves positions for the given nodes. Deterministic: same input → same output.
        /// </summary>
        public static Result Solve(IReadOnlyList<ResearchNodeItemViewModel> nodes)
        {
            var result = new Result();
            if (nodes == null || nodes.Count == 0)
            {
                result.CanvasWidth = 400f;
                result.CanvasHeight = 200f;
                return result;
            }

            var byId = new Dictionary<string, ResearchNodeItemViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                byId[n.Id] = n;
            }

            // 1) Depth = longest prerequisite chain (longest-path layering).
            var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                ComputeDepth(n.Id, byId, depth);
            }

            // 2) Lane assignment in fixed order; unknown branches appended.
            var laneIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in LaneOrder)
            {
                laneIndex[b] = laneIndex.Count;
            }
            foreach (var n in nodes)
            {
                if (!laneIndex.ContainsKey(n.Branch))
                {
                    laneIndex[n.Branch] = laneIndex.Count;
                }
            }

            int laneCount = laneIndex.Count;
            int maxDepth = depth.Values.Count > 0 ? depth.Values.Max() : 0;
            result.MaxDepth = maxDepth;
            result.CanvasWidth = LeftMargin + (maxDepth + 1) * (NodeWidth + NodeHGap) - NodeHGap + RightPadding;

            // 3) Row packing: nodes sharing (lane, depth) stack vertically.
            var rowById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rowCursor = new Dictionary<(int lane, int depth), int>();
            var laneRowHeights = new float[laneCount]; // rows occupied per lane

            foreach (var n in nodes.OrderBy(n => laneIndex[n.Branch]).ThenBy(n => depth[n.Id]).ThenBy(n => n.Id, StringComparer.Ordinal))
            {
                int lane = laneIndex[n.Branch];
                int d = depth[n.Id];
                var key = (lane, d);
                rowCursor.TryGetValue(key, out int row);
                rowCursor[key] = row + 1;
                rowById[n.Id] = row;
                laneRowHeights[lane] = Mathf.Max(laneRowHeights[lane], row + 1);
            }

            // 4) Lane bands with per-lane heights (lanes with more rows get taller bands).
            float y = TopMargin;
            foreach (var pair in laneIndex.OrderBy(p => p.Value))
            {
                string branch = pair.Key;
                int index = pair.Value;
                float rows = Mathf.Max(1f, laneRowHeights[index]);
                float bandHeight = LaneLabelStrip + rows * NodeHeight + Mathf.Max(0f, rows - 1f) * NodeVGap + 12f;
                var band = new Rect(0f, y, result.CanvasWidth, bandHeight);

                // Place the nodes of this lane inside the band.
                foreach (var n in nodes.Where(n => string.Equals(n.Branch, branch, StringComparison.OrdinalIgnoreCase)))
                {
                    int d = depth[n.Id];
                    int row = rowById.TryGetValue(n.Id, out int r) ? r : 0;

                    float nx = LeftMargin + d * (NodeWidth + NodeHGap);
                    float ny = y + LaneLabelStrip + row * (NodeHeight + NodeVGap);
                    result.Slots[n.Id] = new NodeSlot(n.Id, new Rect(nx, ny, NodeWidth, NodeHeight));
                }

                var labelRect = new Rect(12f, y + 8f, LeftMargin - 24f, LaneLabelStrip - 8f);
                result.Lanes.Add((branch, ResearchNodeItemViewModel.GetBranchDisplayName(branch), band, labelRect));
                y += bandHeight;
            }

            result.CanvasHeight = y + BottomPadding;

            // 5) Edges: one per prerequisite pair (only when both ends are known).
            foreach (var n in nodes)
            {
                foreach (var pre in n.Prerequisites)
                {
                    if (byId.ContainsKey(pre) && result.Slots.ContainsKey(pre))
                    {
                        result.Edges.Add(new[] { pre, n.Id });
                    }
                }
            }

            return result;
        }

        private static int ComputeDepth(string id, Dictionary<string, ResearchNodeItemViewModel> byId, Dictionary<string, int> depth)
        {
            if (depth.TryGetValue(id, out int cached))
            {
                return cached;
            }

            depth[id] = 0; // cycle guard (catalog is a DAG, but be defensive)
            var node = byId[id];
            int max = 0;
            foreach (var pre in node.Prerequisites)
            {
                if (!byId.ContainsKey(pre))
                {
                    continue;
                }

                max = Mathf.Max(max, ComputeDepth(pre, byId, depth) + 1);
            }

            depth[id] = max;
            return max;
        }
    }

    /// <summary>
    /// One tech-tree card: NASA art photo on top, name + cost strip below, status
    /// badges and progress overlay. Locked cards render dark (HOI4 silhouette feel).
    /// </summary>
    public sealed class ResearchTreeNodeCard : VisualElement
    {
        public const string UssClassName = "circuit-node";

        public string NodeId { get; }

        private readonly VisualElement _artBox;
        private readonly VisualElement _artVeil;
        private readonly Label _nameLabel;
        private readonly Label _metaLabel;
        private readonly VisualElement _badgeSlot;
        private readonly VisualElement _progressTrack;
        private readonly VisualElement _progressFill;
        private readonly Action<string> _onClick;

        public ResearchTreeNodeCard(ResearchNodeItemViewModel item, Texture2D art, Action<string> onClick)
        {
            NodeId = item?.Id ?? throw new ArgumentNullException(nameof(item));
            _onClick = onClick ?? throw new ArgumentNullException(nameof(onClick));

            AddToClassList(UssClassName);
            pickingMode = PickingMode.Position;

            // ── Art panel ──
            _artBox = new VisualElement();
            _artBox.AddToClassList("circuit-node__art");
            _artBox.style.height = 82f;
            _artBox.style.flexShrink = 0;
            if (art != null)
            {
                _artBox.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 1f);
                _artBox.style.backgroundImage = new StyleBackground(art);
                _artBox.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }
            else
            {
                // No texture available (e.g. test harness): vector fallback.
                _artBox.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
                var fallbackIcon = IconElement.Create(IconType.Research, 34f);
                fallbackIcon.style.color = new Color(0.42f, 0.55f, 0.72f, 0.8f);
                _artBox.style.alignItems = Align.Center;
                _artBox.style.justifyContent = Justify.Center;
                _artBox.Add(fallbackIcon);
            }

            _artVeil = new VisualElement();
            _artVeil.AddToClassList("circuit-node__art-veil");
            _artVeil.style.position = Position.Absolute;
            _artVeil.style.left = 0;
            _artVeil.style.top = 0;
            _artVeil.style.right = 0;
            _artVeil.style.bottom = 0;
            _artVeil.pickingMode = PickingMode.Ignore;
            _artBox.Add(_artVeil);

            // Tier badge (top-left)
            var tierBadge = new Label($"T{item.Tier}");
            tierBadge.AddToClassList("circuit-node__tier");
            tierBadge.style.position = Position.Absolute;
            tierBadge.style.left = 4;
            tierBadge.style.top = 4;
            tierBadge.style.fontSize = 10;
            tierBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            tierBadge.style.paddingLeft = 4;
            tierBadge.style.paddingRight = 4;
            tierBadge.style.paddingTop = 1;
            tierBadge.style.paddingBottom = 1;
            tierBadge.style.borderTopLeftRadius = 3;
            tierBadge.style.borderTopRightRadius = 3;
            tierBadge.style.borderBottomLeftRadius = 3;
            tierBadge.style.borderBottomRightRadius = 3;
            tierBadge.style.backgroundColor = new Color(0.03f, 0.06f, 0.11f, 0.85f);
            tierBadge.style.color = new Color(0.65f, 0.78f, 0.92f, 1f);
            tierBadge.pickingMode = PickingMode.Ignore;
            _artBox.Add(tierBadge);

            // Status badge (top-right)
            _badgeSlot = new VisualElement();
            _badgeSlot.AddToClassList("circuit-node__badge");
            _badgeSlot.style.position = Position.Absolute;
            _badgeSlot.style.right = 4;
            _badgeSlot.style.top = 4;
            _badgeSlot.pickingMode = PickingMode.Ignore;
            _artBox.Add(_badgeSlot);

            Add(_artBox);

            // ── Text strip ──
            var text = new VisualElement();
            text.AddToClassList("circuit-node__text");
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            text.style.paddingTop = 4;
            text.style.paddingBottom = 4;
            text.style.paddingLeft = 6;
            text.style.paddingRight = 6;

            _nameLabel = new Label(item.Name);
            _nameLabel.AddToClassList("circuit-node__name");
            _nameLabel.style.fontSize = 11;
            _nameLabel.style.whiteSpace = WhiteSpace.Normal;
            _nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _nameLabel.style.flexShrink = 1;
            _nameLabel.pickingMode = PickingMode.Ignore;
            text.Add(_nameLabel);

            _metaLabel = new Label(BuildMetaText(item));
            _metaLabel.AddToClassList("circuit-node__meta");
            _metaLabel.style.fontSize = 9;
            _metaLabel.style.whiteSpace = WhiteSpace.Normal;
            _metaLabel.style.marginTop = 2;
            _metaLabel.pickingMode = PickingMode.Ignore;
            text.Add(_metaLabel);

            Add(text);

            // ── Progress overlay (in-progress only) ──
            _progressTrack = new VisualElement();
            _progressTrack.AddToClassList("circuit-node__progress");
            _progressTrack.style.position = Position.Absolute;
            _progressTrack.style.left = 0;
            _progressTrack.style.right = 0;
            _progressTrack.style.bottom = 0;
            _progressTrack.style.height = 4;
            _progressTrack.style.backgroundColor = new Color(0.09f, 0.14f, 0.22f, 1f);
            _progressTrack.pickingMode = PickingMode.Ignore;

            _progressFill = new VisualElement();
            _progressFill.style.height = Length.Percent(100f);
            _progressFill.style.width = Length.Percent(0f);
            _progressFill.style.backgroundColor = new Color(0.024f, 0.714f, 0.831f, 1f);
            _progressTrack.Add(_progressFill);
            Add(_progressTrack);

            // ── Interaction ──
            this.AddManipulator(new TooltipManipulator(() => BuildTooltip(item)));
            RegisterCallback<ClickEvent>(_ => _onClick(NodeId));

            // Apply the initial state class, badge and progress visibility.
            UpdateState(item);
        }

        /// <summary>Refreshes visual state classes/badges for a possibly-new view model.</summary>
        public void UpdateState(ResearchNodeItemViewModel item)
        {
            RemoveFromClassList(UssClassName + "--locked");
            RemoveFromClassList(UssClassName + "--available");
            RemoveFromClassList(UssClassName + "--inprogress");
            RemoveFromClassList(UssClassName + "--completed");

            switch (item.Status)
            {
                case ResearchStatus.Completed:
                    AddToClassList(UssClassName + "--completed");
                    break;
                case ResearchStatus.InProgress:
                    AddToClassList(UssClassName + "--inprogress");
                    break;
                case ResearchStatus.Available:
                    AddToClassList(UssClassName + "--available");
                    break;
                default:
                    AddToClassList(UssClassName + "--locked");
                    break;
            }

            _nameLabel.text = item.Name;
            _metaLabel.text = BuildMetaText(item);
            RenderBadge(item.Status);
            _progressTrack.style.display = item.IsInProgress ? DisplayStyle.Flex : DisplayStyle.None;
            if (item.IsInProgress)
            {
                _progressFill.style.width = Length.Percent(Mathf.Clamp(item.ProgressFraction, 0f, 1f) * 100f);
            }
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                AddToClassList(UssClassName + "--selected");
            }
            else
            {
                RemoveFromClassList(UssClassName + "--selected");
            }
        }

        public void SetDimmed(bool dimmed)
        {
            style.opacity = dimmed ? 0.28f : 1f;
        }

        private void RenderBadge(ResearchStatus status)
        {
            _badgeSlot.Clear();
            IconElement icon = null;
            Color tint = Color.white;

            switch (status)
            {
                case ResearchStatus.Completed:
                    icon = IconElement.Create(IconType.Approve, 14f);
                    tint = new Color(0.43f, 0.905f, 0.717f); // #6EE7B7
                    break;
                case ResearchStatus.InProgress:
                    icon = IconElement.Create(IconType.Time, 14f);
                    tint = new Color(0.403f, 0.909f, 0.976f); // #67E8F9
                    break;
                case ResearchStatus.Available:
                    icon = IconElement.Create(IconType.Plus, 14f);
                    tint = new Color(0.909f, 0.639f, 0.239f); // #E8A33D
                    break;
                default:
                    icon = IconElement.Create(IconType.Lock, 13f);
                    tint = new Color(0.35f, 0.42f, 0.52f);
                    break;
            }

            if (icon != null)
            {
                icon.style.color = tint;
                var chip = new VisualElement();
                chip.style.backgroundColor = new Color(0.02f, 0.05f, 0.09f, 0.85f);
                chip.style.borderTopLeftRadius = 3;
                chip.style.borderTopRightRadius = 3;
                chip.style.borderBottomLeftRadius = 3;
                chip.style.borderBottomRightRadius = 3;
                chip.style.paddingTop = 2;
                chip.style.paddingBottom = 2;
                chip.style.paddingLeft = 2;
                chip.style.paddingRight = 2;
                chip.pickingMode = PickingMode.Ignore;
                chip.Add(icon);
                _badgeSlot.Add(chip);
            }
        }

        private static string BuildMetaText(ResearchNodeItemViewModel item)
        {
            string cost = $"{item.ResearchPointCost:F0} RP";
            if (item.StartCostMUSD > 0)
            {
                cost += $" · {item.StartCostMUSD:F0} M$";
            }

            return $"{cost} · {item.DurationDays}g";
        }

        private TooltipData BuildTooltip(ResearchNodeItemViewModel item)
        {
            string reason = null;
            TooltipSeverity severity = TooltipSeverity.Info;

            switch (item.Status)
            {
                case global::Apogee.Strategy.ResearchStatus.Locked:
                    reason = item.Prerequisites.Count > 0
                        ? $"Kilitli — ön koşul: {string.Join(", ", item.Prerequisites.Select(ResearchNodeItemViewModel.PrettifyId))}"
                        : "Kilitli";
                    severity = TooltipSeverity.Warning;
                    break;
                case global::Apogee.Strategy.ResearchStatus.Available when !item.CanStart:
                    reason = $"Bütçe yetersiz — başlangıç maliyeti {item.StartCostMUSD:F1} M$";
                    severity = TooltipSeverity.Warning;
                    break;
                case global::Apogee.Strategy.ResearchStatus.Available:
                    reason = "Tıklayarak seç, sağ panelden başlat.";
                    severity = TooltipSeverity.Success;
                    break;
                case global::Apogee.Strategy.ResearchStatus.InProgress:
                    reason = $"Araştırılıyor — {item.ProgressDays}/{item.DurationDays} gün";
                    break;
                case global::Apogee.Strategy.ResearchStatus.Completed:
                    reason = "Tamamlandı";
                    severity = TooltipSeverity.Success;
                    break;
            }

            return new TooltipData(item.Name, item.Description, reason, severity: severity);
        }
    }

    /// <summary>
    /// Painter2D backdrop: PCB grid, swim-lane bands, and prerequisite connector
    /// traces with solder-pad endpoints. Trace brightness encodes tree progress
    /// (dim → amber "ready" → cyan "active" → green "done").
    /// </summary>
    public sealed class ResearchTreeConnectorLayer : VisualElement
    {
        // Palette mirrors ThemeCircuit.uss tokens.
        internal static readonly Color GridColor = new(0.024f, 0.714f, 0.831f, 0.05f);
        internal static readonly Color BandColor = new(0.059f, 0.102f, 0.165f, 0.55f);
        internal static readonly Color BandBorder = new(0.133f, 0.208f, 0.310f, 0.35f);
        internal static readonly Color TraceDim = new(0.137f, 0.208f, 0.310f, 0.65f);
        internal static readonly Color TraceReady = new(0.909f, 0.639f, 0.239f, 0.75f);
        internal static readonly Color TraceActive = new(0.403f, 0.909f, 0.976f, 0.85f);
        internal static readonly Color TraceDone = new(0.431f, 0.905f, 0.717f, 0.9f);

        private ResearchTreeLayout.Result _layout;
        private readonly Dictionary<string, ResearchNodeItemViewModel> _nodes = new(StringComparer.OrdinalIgnoreCase);

        public ResearchTreeConnectorLayer()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void Refresh(ResearchTreeLayout.Result layout, IReadOnlyList<ResearchNodeItemViewModel> nodes)
        {
            _layout = layout;
            _nodes.Clear();
            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    _nodes[n.Id] = n;
                }
            }

            style.width = layout?.CanvasWidth ?? 400f;
            style.height = layout?.CanvasHeight ?? 200f;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_layout == null)
            {
                return;
            }

            var p = ctx.painter2D;
            float w = _layout.CanvasWidth;
            float h = _layout.CanvasHeight;

            DrawGrid(p, w, h);
            DrawLaneBands(p, _layout);
            DrawEdges(p, _layout);
        }

        private static void DrawGrid(Painter2D p, float w, float h)
        {
            const float step = 36f;
            p.strokeColor = GridColor;
            p.lineWidth = 1f;
            p.BeginPath();
            for (float x = step; x < w; x += step)
            {
                p.MoveTo(new Vector2(x, 0f));
                p.LineTo(new Vector2(x, h));
            }

            for (float y = step; y < h; y += step)
            {
                p.MoveTo(new Vector2(0f, y));
                p.LineTo(new Vector2(w, y));
            }

            p.Stroke();
        }

        private static void DrawLaneBands(Painter2D p, ResearchTreeLayout.Result layout)
        {
            for (int i = 0; i < layout.Lanes.Count; i++)
            {
                var band = layout.Lanes[i].band;
                if (i % 2 == 1)
                {
                    p.fillColor = BandColor;
                    p.BeginPath();
                    p.MoveTo(new Vector2(band.x, band.y));
                    p.LineTo(new Vector2(band.x + band.width, band.y));
                    p.LineTo(new Vector2(band.x + band.width, band.y + band.height));
                    p.LineTo(new Vector2(band.x, band.y + band.height));
                    p.ClosePath();
                    p.Fill();
                }

                p.strokeColor = BandBorder;
                p.lineWidth = 1f;
                p.BeginPath();
                p.MoveTo(new Vector2(0f, band.y));
                p.LineTo(new Vector2(band.width, band.y));
                p.Stroke();
            }

            // Closing rule at the bottom.
            p.BeginPath();
            p.MoveTo(new Vector2(0f, layout.CanvasHeight));
            p.LineTo(new Vector2(layout.CanvasWidth, layout.CanvasHeight));
            p.Stroke();
        }

        private void DrawEdges(Painter2D p, ResearchTreeLayout.Result layout)
        {
            foreach (var edge in layout.Edges)
            {
                string sourceId = edge[0];
                string targetId = edge[1];
                if (!layout.Slots.TryGetValue(sourceId, out var src) || !layout.Slots.TryGetValue(targetId, out var dst))
                {
                    continue;
                }

                _nodes.TryGetValue(sourceId, out var srcNode);
                _nodes.TryGetValue(targetId, out var dstNode);
                Color trace = ResolveTraceColor(srcNode, dstNode);

                Vector2 from = new(src.Rect.x + src.Rect.width, src.Rect.y + src.Rect.height * 0.5f);
                Vector2 to = new(dst.Rect.x, dst.Rect.y + dst.Rect.height * 0.5f);

                p.strokeColor = trace;
                p.lineWidth = 2f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(from);
                if (Mathf.Abs(from.y - to.y) < 2f)
                {
                    p.LineTo(to);
                }
                else
                {
                    float midX = (from.x + to.x) * 0.5f;
                    p.LineTo(new Vector2(midX, from.y));
                    p.LineTo(new Vector2(midX, to.y));
                    p.LineTo(to);
                }

                p.Stroke();

                // Solder pads at both endpoints.
                p.fillColor = trace;
                p.BeginPath();
                p.Arc(from, 2.6f, 0f, 360f);
                p.Fill();
                p.BeginPath();
                p.Arc(to, 2.6f, 0f, 360f);
                p.Fill();
            }
        }

        private static Color ResolveTraceColor(ResearchNodeItemViewModel source, ResearchNodeItemViewModel target)
        {
            if (source != null && source.IsCompleted && target != null && target.IsCompleted)
            {
                return TraceDone;
            }

            if (source != null && source.IsCompleted)
            {
                if (target == null)
                {
                    return TraceDim;
                }

                return target.IsInProgress ? TraceActive : TraceReady;
            }

            return TraceDim;
        }
    }

    /// <summary>
    /// Scrollable tech-tree canvas: absolutely-positioned node cards over the
    /// connector layer, with branch lane labels and a simple zoom control
    /// (transform scale on the canvas wrapper).
    /// </summary>
    public sealed class ResearchTreeGraphView : VisualElement
    {
        public const string UssClassName = "circuit-graph";

        public event Action<string> NodeClicked;

        private readonly ScrollView _scroller;
        private readonly VisualElement _canvas;
        private readonly ResearchTreeConnectorLayer _connectorLayer;
        private readonly List<Label> _laneLabels = new();
        private readonly Dictionary<string, ResearchTreeNodeCard> _cards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _artCache = new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<ResearchNodeItemViewModel> _nodes = Array.Empty<ResearchNodeItemViewModel>();
        private ResearchTreeLayout.Result _layout;
        private string _selectedId;
        private string _highlightedBranch;

        private float _zoom = 1f;
        private const float ZoomStep = 0.15f;
        private const float ZoomMin = 0.55f;
        private const float ZoomMax = 1.3f;

        public float Zoom => _zoom;

        public ResearchTreeGraphView()
        {
            AddToClassList(UssClassName);
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.flexBasis = 0;

            _scroller = new ScrollView(ScrollViewMode.Vertical);
            _scroller.style.flexGrow = 1;
            Add(_scroller);

            _canvas = new VisualElement();
            _canvas.style.position = Position.Relative;
            _scroller.Add(_canvas);

            _connectorLayer = new ResearchTreeConnectorLayer();
            _connectorLayer.style.position = Position.Absolute;
            _connectorLayer.style.left = 0;
            _connectorLayer.style.top = 0;
            _canvas.Add(_connectorLayer);

            // Node clicks arrive here from the cards.
            // (cards call HandleNodeClicked directly via ctor callback)
        }

        /// <summary>Rebuilds or refreshes the tree. Preserves scroll + cards when only statuses change.</summary>
        public void SetNodes(IReadOnlyList<ResearchNodeItemViewModel> nodes)
        {
            _nodes = nodes ?? Array.Empty<ResearchNodeItemViewModel>();
            _layout = ResearchTreeLayout.Solve(_nodes);

            bool structuralChange =
                Math.Abs(_cards.Count - _nodes.Count) > 0 ||
                _nodes.Any(n => !_cards.ContainsKey(n.Id));

            if (structuralChange)
            {
                RebuildAll();
            }
            else
            {
                foreach (var n in _nodes)
                {
                    if (_cards.TryGetValue(n.Id, out var card))
                    {
                        card.UpdateState(n);
                    }
                }

                _connectorLayer.Refresh(_layout, _nodes);
            }

            ApplySelection();
            ApplyHighlight();
            ApplyZoom();
        }

        public void SetSelected(string nodeId)
        {
            _selectedId = nodeId;
            ApplySelection();
        }

        /// <summary>Dims cards of other branches. Null or "Tümü" clears the dim.</summary>
        public void SetHighlightedBranch(string branch)
        {
            _highlightedBranch = branch;
            ApplyHighlight();
        }

        public void ZoomIn() => SetZoom(_zoom + ZoomStep);
        public void ZoomOut() => SetZoom(_zoom - ZoomStep);
        public void ZoomReset() => SetZoom(1f);

        private void SetZoom(float value)
        {
            _zoom = Mathf.Clamp(value, ZoomMin, ZoomMax);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (_layout == null)
            {
                return;
            }

            _canvas.style.width = _layout.CanvasWidth * _zoom;
            _canvas.style.height = _layout.CanvasHeight * _zoom;
            _canvas.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(0f));
            _canvas.style.scale = new Scale(new Vector3(_zoom, _zoom, 1f));
        }

        private void RebuildAll()
        {
            _canvas.Clear();
            _canvas.Add(_connectorLayer);
            _laneLabels.Clear();
            _cards.Clear();

            if (_layout == null || _nodes.Count == 0)
            {
                var empty = new Label("Araştırma verisi yüklenemedi.");
                empty.style.color = new Color(0.48f, 0.55f, 0.64f);
                empty.style.marginTop = 24;
                empty.style.marginLeft = 24;
                _canvas.Add(empty);
                _connectorLayer.Refresh(null, null);
                return;
            }

            // Lane captions (branch identity like the HOI4 LIGHT/HEAVY headers).
            foreach (var lane in _layout.Lanes)
            {
                var label = new Label(lane.label.ToUpperInvariant());
                label.AddToClassList("circuit-lane-label");
                label.style.position = Position.Absolute;
                label.style.left = lane.labelRect.x;
                label.style.top = lane.labelRect.y;
                label.style.width = lane.labelRect.width;
                label.style.fontSize = 10;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.letterSpacing = 2f;
                label.style.color = new Color(0.48f, 0.55f, 0.64f, 0.95f);
                label.pickingMode = PickingMode.Ignore;
                _canvas.Add(label);
                _laneLabels.Add(label);
            }

            // Node cards.
            foreach (var n in _nodes)
            {
                var slot = _layout.Slots[n.Id];
                var card = new ResearchTreeNodeCard(n, GetArt(n), HandleNodeClicked);
                card.style.position = Position.Absolute;
                card.style.left = slot.Rect.x;
                card.style.top = slot.Rect.y;
                card.style.width = slot.Rect.width;
                card.style.height = slot.Rect.height;
                _canvas.Add(card);
                _cards[n.Id] = card;
            }

            _connectorLayer.Refresh(_layout, _nodes);
        }

        private void HandleNodeClicked(string nodeId)
        {
            NodeClicked?.Invoke(nodeId);
        }

        private void ApplySelection()
        {
            foreach (var kvp in _cards)
            {
                kvp.Value.SetSelected(string.Equals(kvp.Key, _selectedId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ApplyHighlight()
        {
            bool filterActive = !string.IsNullOrEmpty(_highlightedBranch)
                && !string.Equals(_highlightedBranch, "Tümü", StringComparison.OrdinalIgnoreCase);

            foreach (var n in _nodes)
            {
                if (_cards.TryGetValue(n.Id, out var card))
                {
                    card.SetDimmed(filterActive && !string.Equals(n.Branch, _highlightedBranch, StringComparison.OrdinalIgnoreCase));
                }
            }

            for (int i = 0; i < _laneLabels.Count; i++)
            {
                if (_layout != null && i < _layout.Lanes.Count)
                {
                    string branch = _layout.Lanes[i].branch;
                    bool dim = filterActive && !string.Equals(branch, _highlightedBranch, StringComparison.OrdinalIgnoreCase);
                    _laneLabels[i].style.opacity = dim ? 0.35f : 1f;
                }
            }
        }

        private Texture2D GetArt(ResearchNodeItemViewModel node)
        {
            if (string.IsNullOrEmpty(node.Art))
            {
                return null;
            }

            if (_artCache.TryGetValue(node.Art, out var cached))
            {
                return cached;
            }

            Texture2D tex = null;
            try
            {
                tex = Resources.Load<Texture2D>(node.Art);
            }
            catch
            {
                tex = null; // non-Unity harness — cards fall back to vector icon
            }

            if (tex != null)
            {
                _artCache[node.Art] = tex;
            }

            return tex;
        }
    }
}
