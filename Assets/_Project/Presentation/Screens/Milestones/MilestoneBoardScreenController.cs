using System.Collections.Specialized;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;
#pragma warning disable CS0649

namespace Apogee.UI.Milestones
{
    /// <summary>
    /// UI Toolkit screen controller for the Agency Milestone / Quest Board (ScreenId.MilestoneBoard = 21).
    /// Displays all 8 campaign milestones, progress, rewards, and one-click claim actions.
    ///
    /// GDD §4.4, §14.1, §21 M6-01, M9-01.
    ///
    /// Usage: construct with null ViewModel; call BindCampaign(state) once CampaignState is available
    /// (done by CampaignBootstrapper in M9-02).
    /// </summary>
    public sealed class MilestoneBoardScreenController : ScreenControllerBase<MilestoneBoardViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MilestoneBoard;

        // ── cached UI refs ────────────────────────────────────────────────────
        private Label          _completedCountLabel;
        private VisualElement  _progressBarFill;
        private Label          _activeGoalTitle;
        private Label          _activeGoalDesc;
        private VisualElement  _notificationBanner;
        private Label          _notificationText;
        private ScrollView     _milestoneList;
        private Button         _backButton;

        private readonly IScreenNavigator _navigator;

        // ── construction ─────────────────────────────────────────────────────
        public MilestoneBoardScreenController(IScreenNavigator navigator)
            : base(new MilestoneBoardViewModel(null))
        {
            _navigator = navigator;
        }

        public MilestoneBoardScreenController(MilestoneBoardViewModel viewModel, IScreenNavigator navigator)
            : base(viewModel ?? new MilestoneBoardViewModel(null))
        {
            _navigator = navigator;
        }

        /// <summary>Called by CampaignBootstrapper (M9-02) or on screen navigation once CampaignState is ready.</summary>
        public void BindCampaign(CampaignState campaign)
        {
            ViewModel?.BindCampaign(campaign);
        }

        // ── view creation ─────────────────────────────────────────────────────
        protected override VisualElement CreateView()
        {
            var root = new VisualElement { name = "milestone-board-screen" };
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-milestone-board");
            root.style.flexDirection = FlexDirection.Column;

            // ── Header ─────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.flexDirection    = FlexDirection.Row;
            header.style.justifyContent   = Justify.SpaceBetween;
            header.style.alignItems       = Align.Center;
            header.style.paddingTop       = 16;
            header.style.paddingBottom    = 16;
            header.style.paddingLeft      = 24;
            header.style.paddingRight     = 24;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.8f);

            var titleLabel = new Label("AJANS GÖREV TAHTASI");
            titleLabel.style.fontSize               = 20;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color                  = new Color(0.9f, 0.95f, 1f);

            _backButton = new Button(() => _navigator?.GoBack()) { text = "← Geri Dön" };
            _backButton.AddToClassList("apogee-btn");
            _backButton.AddToClassList("apogee-btn--secondary");

            header.Add(titleLabel);
            header.Add(_backButton);
            root.Add(header);

            // ── Progress Summary Bar ──────────────────────────────────────────
            var summaryRow = new VisualElement();
            summaryRow.style.flexDirection   = FlexDirection.Row;
            summaryRow.style.alignItems      = Align.Center;
            summaryRow.style.paddingLeft     = 24;
            summaryRow.style.paddingRight    = 24;
            summaryRow.style.paddingTop      = 12;
            summaryRow.style.paddingBottom   = 12;
            summaryRow.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.9f);

            _completedCountLabel = new Label("0 / 8");
            _completedCountLabel.style.color  = new Color(0.6f, 0.85f, 1f);
            _completedCountLabel.style.fontSize = 15;
            _completedCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _completedCountLabel.style.marginRight = 16;

            var progressTrack = new VisualElement();
            progressTrack.style.flexGrow        = 1;
            progressTrack.style.height          = 10;
            progressTrack.style.backgroundColor = new Color(0.15f, 0.2f, 0.3f, 1f);
            progressTrack.style.borderTopLeftRadius     = 5;
            progressTrack.style.borderTopRightRadius    = 5;
            progressTrack.style.borderBottomLeftRadius  = 5;
            progressTrack.style.borderBottomRightRadius = 5;

            _progressBarFill = new VisualElement();
            _progressBarFill.AddToClassList("telemetry-bar-fill--animated");
            _progressBarFill.style.height          = 10;
            _progressBarFill.style.width           = Length.Percent(0);
            _progressBarFill.style.backgroundColor = new Color(0.2f, 0.75f, 0.5f, 1f);
            _progressBarFill.style.borderTopLeftRadius     = 5;
            _progressBarFill.style.borderTopRightRadius    = 5;
            _progressBarFill.style.borderBottomLeftRadius  = 5;
            _progressBarFill.style.borderBottomRightRadius = 5;
            progressTrack.Add(_progressBarFill);

            summaryRow.Add(_completedCountLabel);
            summaryRow.Add(progressTrack);
            root.Add(summaryRow);

            // ── Notification Banner (hidden by default) ───────────────────────
            _notificationBanner = new VisualElement();
            _notificationBanner.style.flexDirection    = FlexDirection.Row;
            _notificationBanner.style.justifyContent   = Justify.SpaceBetween;
            _notificationBanner.style.alignItems       = Align.Center;
            _notificationBanner.style.paddingLeft      = 24;
            _notificationBanner.style.paddingRight     = 24;
            _notificationBanner.style.paddingTop       = 10;
            _notificationBanner.style.paddingBottom    = 10;
            _notificationBanner.style.backgroundColor  = new Color(0.1f, 0.3f, 0.15f, 0.95f);
            _notificationBanner.style.display          = DisplayStyle.None;

            _notificationText = new Label();
            _notificationText.style.color     = new Color(0.4f, 1f, 0.6f);
            _notificationText.style.whiteSpace = WhiteSpace.Normal;

            var dismissBtn = new Button(() => ViewModel?.DismissNotificationCommand?.Execute()) { text = "✕" };
            dismissBtn.AddToClassList("apogee-btn");
            dismissBtn.AddToClassList("apogee-btn--secondary");

            _notificationBanner.Add(_notificationText);
            _notificationBanner.Add(dismissBtn);
            root.Add(_notificationBanner);

            // ── Main 2-Column Split ───────────────────────────────────────────
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.flexGrow      = 1;
            mainRow.style.paddingLeft   = 24;
            mainRow.style.paddingRight  = 24;
            mainRow.style.paddingTop    = 16;
            mainRow.style.paddingBottom = 16;

            // Left: Scrollable milestone card list
            var leftPanel = new VisualElement();
            leftPanel.style.width           = 480;
            leftPanel.style.marginRight     = 16;
            leftPanel.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 0.95f);
            ApplyBorder(leftPanel, new Color(0.2f, 0.25f, 0.35f, 0.5f));
            leftPanel.style.paddingTop    = 8;
            leftPanel.style.paddingBottom = 8;
            leftPanel.style.paddingLeft   = 8;
            leftPanel.style.paddingRight  = 8;

            _milestoneList = new ScrollView(ScrollViewMode.Vertical);
            _milestoneList.style.flexGrow = 1;
            leftPanel.Add(_milestoneList);

            // Right: Active goal detail
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow       = 1;
            rightPanel.style.backgroundColor = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            ApplyBorder(rightPanel, new Color(0.2f, 0.25f, 0.35f, 0.5f));
            rightPanel.style.paddingTop    = 20;
            rightPanel.style.paddingBottom = 20;
            rightPanel.style.paddingLeft   = 24;
            rightPanel.style.paddingRight  = 24;

            var nextHeader = new Label("SONRAKİ HEDEF");
            nextHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            nextHeader.style.color      = new Color(0.5f, 0.7f, 0.9f);
            nextHeader.style.marginBottom = 8;

            _activeGoalTitle = new Label("-");
            _activeGoalTitle.style.fontSize               = 18;
            _activeGoalTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _activeGoalTitle.style.color                  = new Color(1f, 0.8f, 0.2f);
            _activeGoalTitle.style.marginBottom           = 10;
            _activeGoalTitle.style.whiteSpace             = WhiteSpace.Normal;

            _activeGoalDesc = new Label("-");
            _activeGoalDesc.style.fontSize  = 14;
            _activeGoalDesc.style.color     = new Color(0.8f, 0.85f, 0.9f);
            _activeGoalDesc.style.whiteSpace = WhiteSpace.Normal;

            rightPanel.Add(nextHeader);
            rightPanel.Add(_activeGoalTitle);
            rightPanel.Add(_activeGoalDesc);

            mainRow.Add(leftPanel);
            mainRow.Add(rightPanel);
            root.Add(mainRow);

            return root;
        }

        // ── lifecycle ─────────────────────────────────────────────────────────
        protected override void OnInitialized()
        {
            if (ViewModel != null) BindObservables();
        }

        protected override void OnShown(object context)
        {
            if (context is CampaignState campaign)
            {
                BindCampaign(campaign);
            }
            ViewModel?.Refresh();
        }

        // ── data binding ──────────────────────────────────────────────────────
        private void BindObservables()
        {
            if (ViewModel == null) return;

            ViewModel.CompletedCountText.Subscribe(v => _completedCountLabel.text = v);
            ViewModel.ProgressFraction.Subscribe(v =>
                _progressBarFill.style.width = Length.Percent(v * 100f));

            ViewModel.ActiveGoalTitleText.Subscribe(v => _activeGoalTitle.text = v);
            ViewModel.ActiveGoalDescriptionText.Subscribe(v => _activeGoalDesc.text = v);

            ViewModel.HasNotification.Subscribe(has =>
                _notificationBanner.style.display = has ? DisplayStyle.Flex : DisplayStyle.None);
            ViewModel.NotificationText.Subscribe(v => _notificationText.text = v);

            ViewModel.MilestoneItems.CollectionChanged += OnMilestoneItemsChanged;
            RebuildMilestoneCards();
        }

        private void OnMilestoneItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
            => RebuildMilestoneCards();

        private void RebuildMilestoneCards()
        {
            if (_milestoneList == null || ViewModel == null) return;
            _milestoneList.Clear();

            foreach (var item in ViewModel.MilestoneItems)
            {
                var card = BuildMilestoneCard(item);
                _milestoneList.Add(card);
            }
        }

        private VisualElement BuildMilestoneCard(MilestoneItemViewModel item)
        {
            var card = new VisualElement();
            card.style.marginBottom      = 8;
            card.style.paddingTop        = 12;
            card.style.paddingBottom     = 12;
            card.style.paddingLeft       = 14;
            card.style.paddingRight      = 14;
            card.style.backgroundColor   = new Color(0.14f, 0.17f, 0.24f, 0.9f);
            ApplyBorder(card, new Color(0.25f, 0.3f, 0.4f, 0.4f), 4);

            // Title row
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems    = Align.Center;
            titleRow.style.marginBottom  = 4;

            var titleLbl = new Label(item.Title);
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color  = new Color(0.9f, 0.95f, 1f);
            titleLbl.style.flexGrow = 1;
            titleLbl.style.whiteSpace = WhiteSpace.Normal;

            var statusBadge = new Label();
            item.StatusBadgeText.Subscribe(v => statusBadge.text = v);
            statusBadge.style.fontSize       = 11;
            statusBadge.style.color          = new Color(0.6f, 0.85f, 1f);
            statusBadge.style.paddingLeft    = 6;
            statusBadge.style.paddingRight   = 6;
            statusBadge.style.paddingTop     = 2;
            statusBadge.style.paddingBottom  = 2;
            statusBadge.style.backgroundColor = new Color(0.15f, 0.25f, 0.4f, 0.7f);
            ApplyBorder(statusBadge, new Color(0.2f, 0.35f, 0.55f, 0.5f), 3);

            titleRow.Add(titleLbl);
            titleRow.Add(statusBadge);
            card.Add(titleRow);

            // Description
            var descLbl = new Label(item.Description);
            descLbl.style.fontSize   = 12;
            descLbl.style.color      = new Color(0.65f, 0.75f, 0.85f);
            descLbl.style.whiteSpace = WhiteSpace.Normal;
            descLbl.style.marginBottom = 8;
            card.Add(descLbl);

            // Rewards row
            var rewardsRow = new VisualElement();
            rewardsRow.style.flexDirection = FlexDirection.Row;
            rewardsRow.style.marginBottom  = 8;

            AddRewardChip(rewardsRow, item.RewardBudgetText,     new Color(0.3f, 0.9f, 0.5f));
            AddRewardChip(rewardsRow, item.RewardReputationText, new Color(0.9f, 0.75f, 0.3f));
            AddRewardChip(rewardsRow, item.RewardKnowledgeText,  new Color(0.4f, 0.7f, 1.0f));
            card.Add(rewardsRow);

            // Claim button (only when claimable)
            var claimBtn = new Button(() => ViewModel?.ClaimMilestone(item.Id));
            claimBtn.text = "🏆 Ödülü Al";
            claimBtn.AddToClassList("apogee-btn");
            claimBtn.AddToClassList("apogee-btn--primary");
            claimBtn.style.display = DisplayStyle.None;

            item.CanClaim.Subscribe(can =>
                claimBtn.style.display = can ? DisplayStyle.Flex : DisplayStyle.None);

            card.Add(claimBtn);
            return card;
        }

        // ── helpers ───────────────────────────────────────────────────────────
        private static void AddRewardChip(VisualElement parent, string text, Color color)
        {
            var chip = new Label(text);
            chip.style.fontSize       = 11;
            chip.style.color          = color;
            chip.style.paddingLeft    = 6;
            chip.style.paddingRight   = 6;
            chip.style.paddingTop     = 2;
            chip.style.paddingBottom  = 2;
            chip.style.marginRight    = 4;
            chip.style.backgroundColor = new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 0.6f);
            ApplyBorder(chip, new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.5f), 3);
            parent.Add(chip);
        }

        private static void ApplyBorder(VisualElement el, Color color, int radius = 6)
        {
            el.style.borderTopWidth    = 1;
            el.style.borderBottomWidth = 1;
            el.style.borderLeftWidth   = 1;
            el.style.borderRightWidth  = 1;
            el.style.borderTopColor    = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor   = color;
            el.style.borderRightColor  = color;
            el.style.borderTopLeftRadius     = radius;
            el.style.borderTopRightRadius    = radius;
            el.style.borderBottomLeftRadius  = radius;
            el.style.borderBottomRightRadius = radius;
        }
    }
}
