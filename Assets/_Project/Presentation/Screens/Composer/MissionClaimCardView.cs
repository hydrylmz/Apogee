using System;
using System.Collections.Generic;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Core.ViewModels;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// UI Toolkit component that displays the Mission Claim Level badge, numerical difficulty score,
    /// reward multiplier breakdown, and operational risk summary (GDD §5.1, §5.3, §14.5).
    /// Binds reactively to <see cref="MissionClaimViewModel"/>.
    /// </summary>
    public sealed class MissionClaimCardView : VisualElement, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private MissionClaimViewModel _viewModel;

        // UI Elements
        private readonly Label _titleLabel;
        private readonly Label _badgeLabel;
        private readonly Label _scoreLabel;
        private readonly Label _rewardSummaryLabel;
        private readonly Label _rewardMultiplierPill;
        private readonly Label _riskSummaryLabel;
        private readonly Label _riskMultiplierPill;
        private string _currentBadgeClass = "claim-badge--low";

        public MissionClaimViewModel ViewModel => _viewModel;

        public MissionClaimCardView()
        {
            AddToClassList("mission-claim-card");

            // ── Header Row ─────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("mission-claim-header");

            _titleLabel = new Label("İDDİA SEVİYESİ");
            _titleLabel.AddToClassList("mission-claim-title");
            header.Add(_titleLabel);

            _badgeLabel = new Label("Düşük");
            _badgeLabel.AddToClassList("claim-badge");
            _badgeLabel.AddToClassList(_currentBadgeClass);
            header.Add(_badgeLabel);

            _scoreLabel = new Label("Zorluk: 1.0");
            _scoreLabel.AddToClassList("claim-score-pill");
            header.Add(_scoreLabel);

            Add(header);

            // ── Summary Row / Boxes ────────────────────────────────────────────────
            var body = new VisualElement();
            body.AddToClassList("mission-claim-body");

            // Reward Box
            var rewardBox = new VisualElement();
            rewardBox.AddToClassList("claim-summary-box");
            rewardBox.AddToClassList("claim-summary-box--reward");

            var rewardHeader = new VisualElement();
            rewardHeader.AddToClassList("claim-summary-header");

            var rewardTitle = new Label("BEKLENEN ÖDÜLLER");
            rewardTitle.AddToClassList("claim-summary-title");
            rewardHeader.Add(rewardTitle);

            _rewardMultiplierPill = new Label("1.00x");
            _rewardMultiplierPill.AddToClassList("claim-metric-pill");
            _rewardMultiplierPill.AddToClassList("claim-metric-pill--reward");
            rewardHeader.Add(_rewardMultiplierPill);

            rewardBox.Add(rewardHeader);

            _rewardSummaryLabel = new Label("Standart Bilim ve İtibar Kazanımı");
            _rewardSummaryLabel.AddToClassList("claim-summary-text");
            rewardBox.Add(_rewardSummaryLabel);

            body.Add(rewardBox);

            // Risk Box
            var riskBox = new VisualElement();
            riskBox.AddToClassList("claim-summary-box");
            riskBox.AddToClassList("claim-summary-box--risk");

            var riskHeader = new VisualElement();
            riskHeader.AddToClassList("claim-summary-header");

            var riskTitle = new Label("OPERASYONEL RİSK");
            riskTitle.AddToClassList("claim-summary-title");
            riskHeader.Add(riskTitle);

            _riskMultiplierPill = new Label("1.00x");
            _riskMultiplierPill.AddToClassList("claim-metric-pill");
            _riskMultiplierPill.AddToClassList("claim-metric-pill--risk");
            riskHeader.Add(_riskMultiplierPill);

            riskBox.Add(riskHeader);

            _riskSummaryLabel = new Label("Düşük riskli yörünge görevi");
            _riskSummaryLabel.AddToClassList("claim-summary-text");
            riskBox.Add(_riskSummaryLabel);

            body.Add(riskBox);

            Add(body);
        }

        /// <summary>
        /// Binds to a new ViewModel, unsubscribing from any previous ViewModel.
        /// </summary>
        public void Bind(MissionClaimViewModel viewModel)
        {
            Unbind();
            _viewModel = viewModel;

            if (_viewModel == null) return;

            _subscriptions.Add(_viewModel.ClaimLevel.Subscribe(OnClaimLevelChanged));
            _subscriptions.Add(_viewModel.DifficultyScore.Subscribe(OnDifficultyScoreChanged));
            _subscriptions.Add(_viewModel.BadgeCssClass.Subscribe(OnBadgeClassChanged));
            _subscriptions.Add(_viewModel.RewardMultiplier.Subscribe(OnRewardMultiplierChanged));
            _subscriptions.Add(_viewModel.RiskMultiplier.Subscribe(OnRiskMultiplierChanged));
            _subscriptions.Add(_viewModel.RewardSummary.Subscribe(OnRewardSummaryChanged));
            _subscriptions.Add(_viewModel.RiskSummary.Subscribe(OnRiskSummaryChanged));
        }

        public void Unbind()
        {
            foreach (var sub in _subscriptions)
                sub?.Dispose();

            _subscriptions.Clear();
            _viewModel = null;
        }

        public void Dispose()
        {
            Unbind();
        }

        private void OnClaimLevelChanged(string label)
        {
            _badgeLabel.text = label ?? string.Empty;
        }

        private void OnDifficultyScoreChanged(double score)
        {
            _scoreLabel.text = $"Zorluk: {score:0.0}";
        }

        private void OnBadgeClassChanged(string cssClass)
        {
            if (!string.IsNullOrEmpty(_currentBadgeClass))
                _badgeLabel.RemoveFromClassList(_currentBadgeClass);

            _currentBadgeClass = cssClass ?? "claim-badge--low";
            _badgeLabel.AddToClassList(_currentBadgeClass);
        }

        private void OnRewardMultiplierChanged(double mult)
        {
            _rewardMultiplierPill.text = $"{mult:0.00}x";
        }

        private void OnRiskMultiplierChanged(double mult)
        {
            _riskMultiplierPill.text = $"{mult:0.00}x";
        }

        private void OnRewardSummaryChanged(string summary)
        {
            _rewardSummaryLabel.text = summary ?? string.Empty;
        }

        private void OnRiskSummaryChanged(string summary)
        {
            _riskSummaryLabel.text = summary ?? string.Empty;
        }
    }
}
