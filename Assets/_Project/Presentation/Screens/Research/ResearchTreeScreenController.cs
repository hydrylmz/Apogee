using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Research
{
    /// <summary>
    /// UI Toolkit screen controller for the Strategic Research Tree (ScreenId.ResearchTree = 10).
    /// Complies with GDD §9.2, §14.1 (#9 Araştırma Ağacı), and M9-05.
    /// Provides technology branch filtering, node inspection, and StartResearch command dispatching.
    /// </summary>
    public sealed class ResearchTreeScreenController : ScreenControllerBase<ResearchTreeViewModel>
    {
        public override ScreenId ScreenId => ScreenId.ResearchTree;

        // Top HUD
        private Label _titleLabel;
        private Label _rpLabel;
        private Label _kpLabel;
        private Label _budgetLabel;
        private Button _backBtn;

        // Active Banner
        private Label _activeTitleLabel;
        private Label _activeProgressLabel;
        private VisualElement _activeProgressBar;
        private Button _cancelActiveBtn;

        // Branch Bar
        private VisualElement _branchContainer;
        private readonly Dictionary<string, Button> _branchButtons = new(StringComparer.OrdinalIgnoreCase);

        // Nodes List
        private ScrollView _nodesList;

        // Inspector
        private Label _inspectorTitle;
        private Label _inspectorBranchTier;
        private Label _inspectorStatus;
        private Label _inspectorCost;
        private Label _inspectorDuration;
        private Label _inspectorPrereqs;
        private Label _inspectorUnlocks;
        private Label _inspectorDesc;
        private Button _startResearchBtn;

        // Status Footer
        private Label _statusLabel;

        public ResearchTreeScreenController(ResearchTreeViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-research-tree");
            root.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // ── TOP HEADER ──────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var leftHeader = new VisualElement();
            leftHeader.style.flexDirection = FlexDirection.Row;
            leftHeader.style.alignItems = Align.Center;

            _backBtn = new Button(() => ViewModel.BackCommand.Execute()) { text = "<- Geri (Harita)"};
            _backBtn.style.marginRight = 16;
            _backBtn.style.paddingLeft = 12;
            _backBtn.style.paddingRight = 12;
            _backBtn.style.paddingTop = 6;
            _backBtn.style.paddingBottom = 6;
            _backBtn.style.backgroundColor = new Color(0.15f, 0.20f, 0.30f);
            _backBtn.style.color = Color.white;
            leftHeader.Add(_backBtn);

            _titleLabel = new Label("AR-GE VE TEKNOLOJİ AĞACI");
            _titleLabel.style.fontSize = 16;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new Color(0.9f, 0.95f, 1f);
            leftHeader.Add(_titleLabel);
            header.Add(leftHeader);

            var rightHeader = new VisualElement();
            rightHeader.style.flexDirection = FlexDirection.Row;
            rightHeader.style.alignItems = Align.Center;

            _rpLabel = CreatePill("0 RP", new Color(0.35f, 0.85f, 1f));
            _kpLabel = CreatePill("0 KP", new Color(0.8f, 0.6f, 1f));
            _budgetLabel = CreatePill("0 M$", new Color(0.4f, 0.9f, 0.5f));
            rightHeader.Add(_rpLabel);
            rightHeader.Add(_kpLabel);
            rightHeader.Add(_budgetLabel);
            header.Add(rightHeader);

            root.Add(header);

            // ── ACTIVE RESEARCH STRIP ───────────────────────────────────────
            var activeStrip = new VisualElement();
            activeStrip.style.flexDirection = FlexDirection.Row;
            activeStrip.style.justifyContent = Justify.SpaceBetween;
            activeStrip.style.alignItems = Align.Center;
            activeStrip.style.flexShrink = 0;
            activeStrip.style.paddingTop = 6;
            activeStrip.style.paddingBottom = 6;
            activeStrip.style.paddingLeft = 16;
            activeStrip.style.paddingRight = 16;
            activeStrip.style.backgroundColor = new Color(0.05f, 0.08f, 0.14f, 0.95f);
            activeStrip.style.borderBottomWidth = 1;
            activeStrip.style.borderBottomColor = new Color(0.15f, 0.20f, 0.30f, 0.8f);

            var activeLeft = new VisualElement();
            activeLeft.style.flexDirection = FlexDirection.Row;
            activeLeft.style.alignItems = Align.Center;
            activeLeft.style.flexGrow = 1;

            _activeTitleLabel = new Label("Aktif Araştırma: Yok");
            _activeTitleLabel.style.fontSize = 12;
            _activeTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _activeTitleLabel.style.color = new Color(0.4f, 0.85f, 1f);
            _activeTitleLabel.style.marginRight = 16;
            activeLeft.Add(_activeTitleLabel);

            var barTrack = new VisualElement();
            barTrack.style.width = 160;
            barTrack.style.height = 4;
            // --color-panel-border: #2D3D58
            barTrack.style.backgroundColor = new Color(0.176f, 0.239f, 0.345f, 1f);
            barTrack.style.borderTopLeftRadius = 9999;
            barTrack.style.borderTopRightRadius = 9999;
            barTrack.style.borderBottomLeftRadius = 9999;
            barTrack.style.borderBottomRightRadius = 9999;
            barTrack.style.marginRight = 12;

            _activeProgressBar = new VisualElement();
            _activeProgressBar.style.width = Length.Percent(0);
            _activeProgressBar.style.height = Length.Percent(100);
            // --color-accent-cyan: #06B6D4
            _activeProgressBar.style.backgroundColor = new Color(0.024f, 0.714f, 0.831f, 1f);
            barTrack.Add(_activeProgressBar);
            activeLeft.Add(barTrack);

            _activeProgressLabel = new Label("—");
            _activeProgressLabel.style.fontSize = 11;
            _activeProgressLabel.style.color = new Color(0.7f, 0.85f, 1f);
            activeLeft.Add(_activeProgressLabel);
            activeStrip.Add(activeLeft);

            _cancelActiveBtn = new Button(() => ViewModel.CancelActiveResearchCommand.Execute()) { text = "İptal Et" };
            _cancelActiveBtn.style.backgroundColor = new Color(0.4f, 0.12f, 0.12f);
            _cancelActiveBtn.style.color = new Color(1f, 0.7f, 0.7f);
            activeStrip.Add(_cancelActiveBtn);

            root.Add(activeStrip);

            // ── BRANCH FILTER BAR ───────────────────────────────────────────
            _branchContainer = new VisualElement();
            _branchContainer.style.flexDirection = FlexDirection.Row;
            _branchContainer.style.flexWrap = Wrap.Wrap;
            _branchContainer.style.flexShrink = 0;
            _branchContainer.style.paddingTop = 6;
            _branchContainer.style.paddingBottom = 6;
            _branchContainer.style.paddingLeft = 16;
            _branchContainer.style.backgroundColor = new Color(0.08f, 0.11f, 0.18f);
            _branchContainer.style.borderBottomWidth = 1;
            _branchContainer.style.borderBottomColor = new Color(0.15f, 0.20f, 0.30f);
            root.Add(_branchContainer);

            // ── MAIN BODY: 2-COLUMN SPLIT ───────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;

            // Left: Nodes ScrollView
            _nodesList = new ScrollView();
            // flexBasis=0 şart: ScrollView'ın doğal yüksekliği tüm liste kadar olur,
            // bu da üstteki barların flex-shrink ile ezilip iç içe geçmesine yol açar.
            _nodesList.style.flexGrow = 1;
            _nodesList.style.flexShrink = 1;
            _nodesList.style.flexBasis = 0;
            _nodesList.style.paddingLeft = 16;
            _nodesList.style.paddingRight = 16;
            _nodesList.style.paddingTop = 12;
            _nodesList.style.paddingBottom = 12;
            body.Add(_nodesList);

            // Right: Detail Inspector
            var inspector = new VisualElement();
            inspector.style.width = 320;
            inspector.style.backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.98f);
            inspector.style.borderLeftWidth = 1;
            inspector.style.borderLeftColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            inspector.style.paddingLeft = 16;
            inspector.style.paddingRight = 16;
            inspector.style.paddingTop = 16;
            inspector.style.paddingBottom = 16;
            inspector.style.justifyContent = Justify.SpaceBetween;

            var inspectorTop = new VisualElement();

            var inspectorHeader = new Label("DÜĞÜM DETAYI");
            inspectorHeader.style.fontSize = 11;
            inspectorHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            inspectorHeader.style.color = new Color(0.4f, 0.8f, 1f);
            inspectorTop.Add(inspectorHeader);

            _inspectorTitle = new Label("Düğüm Seçiniz");
            _inspectorTitle.style.fontSize = 18;
            _inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorTitle.style.color = new Color(0.95f, 0.98f, 1f);
            _inspectorTitle.style.marginTop = 4;
            inspectorTop.Add(_inspectorTitle);

            _inspectorBranchTier = new Label("Dal: — · Aşama: —");
            _inspectorBranchTier.style.fontSize = 11;
            _inspectorBranchTier.style.color = new Color(0.6f, 0.75f, 0.9f);
            _inspectorBranchTier.style.marginBottom = 8;
            inspectorTop.Add(_inspectorBranchTier);

            _inspectorStatus = new Label("Durum: —");
            _inspectorStatus.style.fontSize = 12;
            _inspectorStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorStatus.style.color = Color.white;
            _inspectorStatus.style.marginBottom = 12;
            inspectorTop.Add(_inspectorStatus);

            _inspectorDesc = new Label(string.Empty);
            _inspectorDesc.style.fontSize = 11;
            _inspectorDesc.style.color = new Color(0.8f, 0.85f, 0.95f);
            _inspectorDesc.style.whiteSpace = WhiteSpace.Normal;
            _inspectorDesc.style.marginBottom = 12;
            inspectorTop.Add(_inspectorDesc);

            _inspectorCost = CreateDetailRow(inspectorTop, "Maliyet:", "0 RP / 0 M$");
            _inspectorDuration = CreateDetailRow(inspectorTop, "Süre:", "0 gün");
            _inspectorPrereqs = CreateDetailRow(inspectorTop, "Ön Koşul:", "Yok");
            _inspectorUnlocks = CreateDetailRow(inspectorTop, "Açılan Modüller:", "Yok");

            inspector.Add(inspectorTop);

            var inspectorBottom = new VisualElement();
            _startResearchBtn = new Button(() => ViewModel.StartSelectedResearchCommand.Execute()) { text = "Araştırmayı Başlat"};
            _startResearchBtn.style.height = 36;
            _startResearchBtn.style.backgroundColor = new Color(0.15f, 0.45f, 0.85f);
            _startResearchBtn.style.color = Color.white;
            _startResearchBtn.style.fontSize = 13;
            _startResearchBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            inspectorBottom.Add(_startResearchBtn);
            inspector.Add(inspectorBottom);

            body.Add(inspector);
            root.Add(body);

            // ── FOOTER ──────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.flexShrink = 0;
            footer.style.paddingTop = 6;
            footer.style.paddingBottom = 6;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.15f, 0.20f, 0.30f);

            _statusLabel = new Label("Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.4f, 0.8f, 1f);
            footer.Add(_statusLabel);
            root.Add(footer);

            return root;
        }

        protected override void OnInitialized()
        {
            RegisterObservables();
        }

        protected override void OnShown(object context)
        {
            if (context is CampaignState campaign)
            {
                ViewModel?.BindCampaign(campaign);
            }
            else
            {
                ViewModel?.Refresh();
            }

            RenderBranches();
            RenderNodes();
            UpdateInspector();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.ResearchPoints.Subscribe(rp =>
            {
                if (_rpLabel != null) _rpLabel.text = $"{rp:F0} RP";
            });

            ViewModel.KnowledgePoints.Subscribe(kp =>
            {
                if (_kpLabel != null) _kpLabel.text = $"{kp:F0} KP";
            });

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
            });

            ViewModel.ActiveResearchTitle.Subscribe(t =>
            {
                if (_activeTitleLabel != null) _activeTitleLabel.text = t;
            });

            ViewModel.ActiveResearchProgress.Subscribe(p =>
            {
                if (_activeProgressLabel != null) _activeProgressLabel.text = p;
            });

            ViewModel.ActiveResearchProgressFraction.Subscribe(f =>
            {
                if (_activeProgressBar != null) _activeProgressBar.style.width = Length.Percent(f * 100f);
            });

            ViewModel.VisibleNodes.Subscribe(_ => RenderNodes());
            ViewModel.Branches.Subscribe(_ => RenderBranches());
            ViewModel.SelectedNode.Subscribe(_ => UpdateInspector());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg ?? string.Empty;
            });
        }

        private void RenderBranches()
        {
            if (_branchContainer == null || ViewModel == null) return;

            _branchContainer.Clear();
            _branchButtons.Clear();

            var branches = ViewModel.Branches.Value;
            if (branches == null) return;

            foreach (var branch in branches)
            {
                string bName = branch;
                var btn = new Button(() =>
                {
                    ViewModel.SelectBranch(bName);
                    HighlightActiveBranch();
                }) { text = GetBranchLabel(bName) };

                btn.style.marginRight = 6;
                btn.style.marginBottom = 4;
                btn.style.paddingLeft = 10;
                btn.style.paddingRight = 10;
                btn.style.paddingTop = 4;
                btn.style.paddingBottom = 4;
                btn.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f);
                btn.style.color = new Color(0.85f, 0.9f, 1f);

                _branchButtons[bName] = btn;
                _branchContainer.Add(btn);
            }

            HighlightActiveBranch();
        }

        private static string GetBranchLabel(string branch)
        {
            if (string.Equals(branch, "Tümü", StringComparison.OrdinalIgnoreCase))
                return ResearchNodeItemViewModel.LocalizeOr("research.branch.all", "Tümü");
            return ResearchNodeItemViewModel.GetBranchDisplayName(branch);
        }

        private void HighlightActiveBranch()
        {
            string sel = ViewModel?.SelectedBranch.Value ?? "Tümü";
            foreach (var kvp in _branchButtons)
            {
                bool isSelected = string.Equals(kvp.Key, sel, StringComparison.OrdinalIgnoreCase);
                kvp.Value.style.backgroundColor = isSelected ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.12f, 0.16f, 0.24f);
                kvp.Value.style.unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void RenderNodes()
        {
            if (_nodesList == null || ViewModel == null) return;
            _nodesList.Clear();

            var nodes = ViewModel.VisibleNodes.Value;
            if (nodes == null || nodes.Count == 0)
            {
                var emptyLabel = new Label("Bu kategoride araştırma düğümü bulunamadı.");
                emptyLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
                _nodesList.Add(emptyLabel);
                return;
            }

            // "Tümü" görünümünde kartları dala göre grupla, aralarına dal başlığı koy.
            bool showAll = string.Equals(ViewModel.SelectedBranch.Value, "Tümü", StringComparison.OrdinalIgnoreCase);
            string lastBranchHeader = null;

            foreach (var node in nodes)
            {
                if (showAll)
                {
                    string header = node.BranchDisplayName;
                    if (!string.Equals(lastBranchHeader, header, StringComparison.Ordinal))
                    {
                        lastBranchHeader = header;
                        _nodesList.Add(CreateBranchHeader(header));
                    }
                }

                var card = CreateNodeCard(node);
                _nodesList.Add(card);
            }
        }

        private static VisualElement CreateBranchHeader(string branchDisplayName)
        {
            var header = new Label(branchDisplayName.ToUpperInvariant());
            header.style.fontSize = 11;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.4f, 0.8f, 1f);
            header.style.marginTop = 12;
            header.style.marginBottom = 4;
            header.style.paddingBottom = 4;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            header.style.flexShrink = 0;
            return header;
        }

        private VisualElement CreateNodeCard(ResearchNodeItemViewModel node)
        {
            var card = new Button(() => ViewModel.SelectNode(node.Id));
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            // Kartların ScrollView içinde sıkışmasını/üst üste binmesini engelle:
            // içerik görüntüleyiciden uzun olduğunda flex-shrink varsayılanı (1)
            // kartları dikey olarak ezer. "Tümü" listesindeki çakışmanın asıl nedeni budur.
            card.style.flexShrink = 0;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 6;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;

            Color statusColor;
            string statusBadge;

            switch (node.Status)
            {
                case ResearchStatus.Completed:
                    card.style.backgroundColor = new Color(0.08f, 0.16f, 0.12f, 0.95f);
                    statusColor = new Color(0.35f, 0.95f, 0.55f);
                    statusBadge = "OK Tamamlandı";
                    break;
                case ResearchStatus.InProgress:
                    card.style.backgroundColor = new Color(0.10f, 0.18f, 0.28f, 0.95f);
                    statusColor = new Color(0.35f, 0.85f, 1f);
                    statusBadge = $"Araştırılıyor ({node.ProgressDays}/{node.DurationDays}g)";
                    break;
                case ResearchStatus.Available:
                    card.style.backgroundColor = new Color(0.10f, 0.14f, 0.22f, 0.95f);
                    statusColor = new Color(0.95f, 0.85f, 0.4f);
                    statusBadge = "Açık";
                    break;
                default:
                    card.style.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 0.8f);
                    statusColor = new Color(0.5f, 0.55f, 0.65f);
                    statusBadge = "Kilitli";
                    break;
            }

            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            left.style.marginRight = 8;

            var nameLabel = new Label(node.Name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            nameLabel.style.color = node.IsCompleted ? new Color(0.7f, 1f, 0.8f) : new Color(0.95f, 0.98f, 1f);
            left.Add(nameLabel);

            var metaLabel = new Label($"{node.BranchDisplayName} · Aşama {node.Tier} · {node.ResearchPointCost:F0} RP · {node.DurationDays} gün");
            metaLabel.style.fontSize = 10;
            metaLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            metaLabel.style.color = new Color(0.6f, 0.7f, 0.85f);
            left.Add(metaLabel);
            card.Add(left);

            var right = new Label(statusBadge);
            right.style.fontSize = 11;
            right.style.unityFontStyleAndWeight = FontStyle.Bold;
            right.style.unityTextAlign = TextAnchor.MiddleRight;
            right.style.flexShrink = 0;
            right.style.color = statusColor;
            card.Add(right);

            return card;
        }

        private void UpdateInspector()
        {
            var node = ViewModel?.SelectedNode.Value;
            if (node == null)
            {
                if (_inspectorTitle != null) _inspectorTitle.text = "Düğüm Seçiniz";
                if (_inspectorBranchTier != null) _inspectorBranchTier.text = "—";
                if (_inspectorStatus != null) _inspectorStatus.text = "Durum: —";
                if (_inspectorDesc != null) _inspectorDesc.text = string.Empty;
                if (_startResearchBtn != null) _startResearchBtn.SetEnabled(false);
                return;
            }

            if (_inspectorTitle != null) _inspectorTitle.text = node.Name;
            if (_inspectorBranchTier != null) _inspectorBranchTier.text = $"Dal: {node.BranchDisplayName} · Aşama: {node.Tier}";

            if (_inspectorStatus != null)
            {
                switch (node.Status)
                {
                    case ResearchStatus.Completed:
                        _inspectorStatus.text = "Durum: OK Tamamlandı";
                        _inspectorStatus.style.color = new Color(0.35f, 0.95f, 0.55f);
                        break;
                    case ResearchStatus.InProgress:
                        _inspectorStatus.text = $"Durum: Araştırılıyor ({node.ProgressDays}/{node.DurationDays} gün)";
                        _inspectorStatus.style.color = new Color(0.35f, 0.85f, 1f);
                        break;
                    case ResearchStatus.Available:
                        _inspectorStatus.text = "Durum: Araştırmaya Hazır";
                        _inspectorStatus.style.color = new Color(0.95f, 0.85f, 0.4f);
                        break;
                    default:
                        _inspectorStatus.text = "Durum: Kilitli (Ön koşulları tamamlayın)";
                        _inspectorStatus.style.color = new Color(0.85f, 0.45f, 0.45f);
                        break;
                }
            }

            if (_inspectorDesc != null) _inspectorDesc.text = node.Description;
            if (_inspectorCost != null) _inspectorCost.text = $"{node.ResearchPointCost:F0} RP  |  Başlangıç: {node.StartCostMUSD:F1} M$";
            if (_inspectorDuration != null) _inspectorDuration.text = $"{node.DurationDays} gün";
            if (_inspectorPrereqs != null)
            {
                _inspectorPrereqs.text = node.Prerequisites.Count > 0
                    ? string.Join(", ", node.Prerequisites.Select(id => ViewModel.GetNodeName(id)))
                    : "Yok (Başlangıç Düğümü)";
            }

            if (_inspectorUnlocks != null)
            {
                _inspectorUnlocks.text = node.UnlockedModules.Count > 0
                    ? string.Join(", ", node.UnlockedModules.Select(ResearchNodeItemViewModel.PrettifyId))
                    : "Yok";
            }

            if (_startResearchBtn != null)
            {
                _startResearchBtn.SetEnabled(node.CanStart);
                _startResearchBtn.text = node.IsCompleted ? "Tamamlandı" :
                                         node.IsInProgress ? "Araştırılıyor..." :
                                         node.IsLocked ? "Kilitli": "Araştırmayı Başlat";
            }
        }

        private static Label CreatePill(string text, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            label.style.marginLeft = 10;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            label.style.borderTopLeftRadius = 10;
            label.style.borderTopRightRadius = 10;
            label.style.borderBottomLeftRadius = 10;
            label.style.borderBottomRightRadius = 10;
            return label;
        }

        private static Label CreateDetailRow(VisualElement parent, string title, string val)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 6;

            var t = new Label(title);
            t.style.fontSize = 11;
            t.style.flexShrink = 0;
            t.style.marginRight = 8;
            t.style.color = new Color(0.6f, 0.75f, 0.9f);
            row.Add(t);

            var v = new Label(val);
            v.style.fontSize = 11;
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            v.style.unityTextAlign = TextAnchor.UpperRight;
            v.style.whiteSpace = WhiteSpace.Normal;
            v.style.flexGrow = 1;
            v.style.flexShrink = 1;
            v.style.color = Color.white;
            row.Add(v);

            parent.Add(row);
            return v;
        }
    }
}
