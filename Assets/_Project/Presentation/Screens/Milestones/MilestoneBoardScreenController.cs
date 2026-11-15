using System.Collections.Specialized;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Milestones
{
    /// <summary>
    /// UI Toolkit screen controller for the Agency Milestone / Quest Board (ScreenId.MilestoneBoard = 21).
    /// Visual identity: "Madalya Panosu" — an oak wall with a hung deep-green felt patch board;
    /// milestones are ribboned medal cards carrying NASA public-domain photos with Turkish credits,
    /// and completing one "pins the medal" on the felt. A brass plaque on the right names the
    /// next recommended goal. Decorative Painter2D artwork (felt board + compass rose + medal
    /// patch) ignores picking so every interaction stays clickable.
    ///
    /// GDD §4.4, §14.1, §21 M6-01, M9-01.
    ///
    /// Usage: construct with null ViewModel; call BindCampaign(state) once CampaignState is available
    /// (done by CampaignBootstrapper in M9-02).
    /// </summary>
    public sealed class MilestoneBoardScreenController : ScreenControllerBase<MilestoneBoardViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MilestoneBoard;

        // Photo texture cache (ADR-054/063/066/068 pattern — Resources.Load + dictionary).
        private static readonly Dictionary<string, Texture2D> PhotoCache = new();

        // ── cached UI refs ────────────────────────────────────────────────────
        private Label _completedCountLabel;
        private VisualElement _progressBarFill;
        private Label _activeGoalTitle;
        private Label _activeGoalDesc;
        private VisualElement _notificationBanner;
        private Label _notificationText;
        private ScrollView _milestoneList;
        private VisualElement _plaquePatchSlot;
        private readonly IScreenNavigator _navigator;
        private ScreenNavBar _navBar;

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
            root.AddToClassList("theme-medal-board"); // Madalya Panosu identity
            root.style.flexDirection = FlexDirection.Column;

            // Decorative felt patch board (oak wall + stitches + compass rose).
            root.Add(new MedalBoardBackdrop());

            // ── Header ─────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("medal-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.paddingLeft = 24;
            header.style.paddingRight = 24;

            var titleBox = new VisualElement();
            var titleLabel = new Label("AJANS MADALYA PANOSU");
            titleLabel.AddToClassList("medal-title");
            titleLabel.style.fontSize = 20;
            titleBox.Add(titleLabel);

            var subtitleLabel = new Label("Kampanya kilometre taşları · madalyalar kurdeleleriyle sergilenir");
            subtitleLabel.AddToClassList("medal-subtitle");
            subtitleLabel.style.fontSize = 11;
            titleBox.Add(subtitleLabel);

            header.Add(titleBox);

            // Medal patch badge on the header (ceremonial crest)
            var crest = new MedalPatchBadge();
            crest.style.width = 44;
            crest.style.height = 48;
            header.Add(crest);

            root.Add(header);

            // ── Progress Summary Bar ──────────────────────────────────────────
            var summaryRow = new VisualElement();
            summaryRow.AddToClassList("medal-summary");
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.paddingLeft = 24;
            summaryRow.style.paddingRight = 24;
            summaryRow.style.paddingTop = 10;
            summaryRow.style.paddingBottom = 10;

            _completedCountLabel = new Label("0 / 8");
            _completedCountLabel.AddToClassList("medal-count");
            _completedCountLabel.style.fontSize = 15;
            _completedCountLabel.style.marginRight = 16;
            summaryRow.Add(_completedCountLabel);

            var progressTrack = new VisualElement();
            progressTrack.AddToClassList("medal-track");
            progressTrack.style.flexGrow = 1;
            progressTrack.style.height = 8;

            _progressBarFill = new VisualElement();
            _progressBarFill.AddToClassList("medal-track-fill");
            _progressBarFill.AddToClassList("telemetry-bar-fill--animated");
            _progressBarFill.style.height = 8;
            _progressBarFill.style.width = Length.Percent(0);
            progressTrack.Add(_progressBarFill);

            summaryRow.Add(progressTrack);
            root.Add(summaryRow);

            // ── Ceremony Banner (hidden by default) ───────────────────────────
            _notificationBanner = new VisualElement();
            _notificationBanner.AddToClassList("medal-banner");
            _notificationBanner.style.flexDirection = FlexDirection.Row;
            _notificationBanner.style.justifyContent = Justify.SpaceBetween;
            _notificationBanner.style.alignItems = Align.Center;
            _notificationBanner.style.paddingLeft = 24;
            _notificationBanner.style.paddingRight = 24;
            _notificationBanner.style.paddingTop = 10;
            _notificationBanner.style.paddingBottom = 10;
            _notificationBanner.style.display = DisplayStyle.None;

            _notificationText = new Label();
            _notificationText.AddToClassList("medal-banner-text");

            var dismissBtn = new Button(() => ViewModel?.DismissNotificationCommand?.Execute()) { text = "X" };
            dismissBtn.AddToClassList("apogee-btn");
            dismissBtn.AddToClassList("apogee-btn--secondary");

            _notificationBanner.Add(_notificationText);
            _notificationBanner.Add(dismissBtn);
            root.Add(_notificationBanner);

            // ── Main 2-Column Split ───────────────────────────────────────────
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.flexGrow = 1;
            mainRow.style.paddingLeft = 24;
            mainRow.style.paddingRight = 24;
            mainRow.style.paddingTop = 16;
            mainRow.style.paddingBottom = 16;

            // Left: the felt patch board with the medal cards
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("medal-board");
            leftPanel.style.width = 520;
            leftPanel.style.marginRight = 16;
            leftPanel.style.paddingTop = 8;
            leftPanel.style.paddingBottom = 8;
            leftPanel.style.paddingLeft = 8;
            leftPanel.style.paddingRight = 8;

            _milestoneList = new ScrollView(ScrollViewMode.Vertical);
            _milestoneList.AddToClassList("medal-board-scroll");
            _milestoneList.style.flexGrow = 1;
            leftPanel.Add(_milestoneList);

            // Right: brass plaque naming the next goal
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("medal-detail");
            rightPanel.style.flexGrow = 1;
            rightPanel.style.paddingTop = 20;
            rightPanel.style.paddingBottom = 20;
            rightPanel.style.paddingLeft = 24;
            rightPanel.style.paddingRight = 24;

            var plaque = new VisualElement();
            plaque.AddToClassList("medal-detail-plaque");
            plaque.style.paddingTop = 16;
            plaque.style.paddingBottom = 16;
            plaque.style.paddingLeft = 18;
            plaque.style.paddingRight = 18;

            var patchSlot = new VisualElement();
            patchSlot.style.height = 84;
            patchSlot.style.alignItems = Align.Center;
            _plaquePatchSlot = new VisualElement();
            _plaquePatchSlot.style.width = 72;
            _plaquePatchSlot.style.height = 84;
            patchSlot.Add(_plaquePatchSlot);
            plaque.Add(patchSlot);

            var nextHeader = new Label("SONRAKİ HEDEF");
            nextHeader.AddToClassList("medal-section-label");
            nextHeader.style.marginTop = 10;
            nextHeader.style.marginBottom = 8;
            plaque.Add(nextHeader);

            _activeGoalTitle = new Label("-");
            _activeGoalTitle.AddToClassList("medal-goal-title");
            _activeGoalTitle.style.fontSize = 18;
            _activeGoalTitle.style.marginBottom = 10;
            plaque.Add(_activeGoalTitle);

            _activeGoalDesc = new Label("-");
            _activeGoalDesc.AddToClassList("medal-goal-desc");
            _activeGoalDesc.style.fontSize = 14;
            plaque.Add(_activeGoalDesc);

            rightPanel.Add(plaque);
            mainRow.Add(leftPanel);
            mainRow.Add(rightPanel);
            root.Add(mainRow);

            // ── Footer: shared nav bar (geri + 8 sekme + status slot) ─────────
            var footer = new VisualElement();
            footer.AddToClassList("medal-footer");
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;

            _navBar = new ScreenNavBar(_navigator, ScreenId.MilestoneBoard);
            _navBar.style.flexGrow = 1;
            footer.Add(_navBar);

            root.Add(footer);

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
            RebuildMilestoneCards();
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
            card.AddToClassList("medal-card");
            card.style.marginBottom = 8;
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;

            void ApplyStatusCardStyle(Apogee.Strategy.Milestones.MilestoneStatus status)
            {
                card.RemoveFromClassList("medal-card--claimed");
                card.RemoveFromClassList("medal-card--pending");
                if (status == Apogee.Strategy.Milestones.MilestoneStatus.Claimed)
                {
                    card.AddToClassList("medal-card--claimed");
                }
                else if (status == Apogee.Strategy.Milestones.MilestoneStatus.CompletedPendingClaim)
                {
                    card.AddToClassList("medal-card--pending");
                }
            }

            item.Status.Subscribe(ApplyStatusCardStyle);
            ApplyStatusCardStyle(item.Status.Value);

            // ── NASA photo slot (kamu malı madalya fotoğrafı + atıf) ────────
            var photoSlot = new VisualElement();
            photoSlot.AddToClassList("medal-card-photo");
            photoSlot.style.height = 86;
            photoSlot.style.marginBottom = 8;
            photoSlot.style.overflow = Overflow.Hidden;
            BuildPhotoSlot(photoSlot, item);
            card.Add(photoSlot);

            // Title row
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 4;

            var titleLbl = new Label((item.Status.Value == Apogee.Strategy.Milestones.MilestoneStatus.Claimed ? "★ " : "") + item.Title);
            titleLbl.AddToClassList("medal-card-title");
            titleLbl.style.fontSize = 13;
            titleLbl.style.flexGrow = 1;
            titleRow.Add(titleLbl);

            var statusBadge = new Label();
            statusBadge.AddToClassList("medal-badge");
            statusBadge.style.fontSize = 10;
            statusBadge.style.paddingLeft = 8;
            statusBadge.style.paddingRight = 8;
            statusBadge.style.paddingTop = 3;
            statusBadge.style.paddingBottom = 3;

            void UpdateStatusBadgeStyle(Apogee.Strategy.Milestones.MilestoneStatus status)
            {
                statusBadge.RemoveFromClassList("medal-badge--claimed");
                statusBadge.RemoveFromClassList("medal-badge--pending");
                statusBadge.RemoveFromClassList("medal-badge--available");
                statusBadge.RemoveFromClassList("medal-badge--locked");

                switch (status)
                {
                    case Apogee.Strategy.Milestones.MilestoneStatus.Claimed:
                        statusBadge.text = "★ MADALYA TAKILDI";
                        statusBadge.AddToClassList("medal-badge--claimed");
                        break;
                    case Apogee.Strategy.Milestones.MilestoneStatus.CompletedPendingClaim:
                        statusBadge.text = "TÖREN BEKLİYOR";
                        statusBadge.AddToClassList("medal-badge--pending");
                        break;
                    case Apogee.Strategy.Milestones.MilestoneStatus.Available:
                        statusBadge.text = "ÖNERİLEN HEDEF";
                        statusBadge.AddToClassList("medal-badge--available");
                        break;
                    default:
                        statusBadge.text = "KİLİTLİ";
                        statusBadge.AddToClassList("medal-badge--locked");
                        break;
                }
            }

            item.Status.Subscribe(UpdateStatusBadgeStyle);
            UpdateStatusBadgeStyle(item.Status.Value);

            titleRow.Add(titleLbl);
            titleRow.Add(statusBadge);
            card.Add(titleRow);

            // Description
            var descLbl = new Label(item.Description);
            descLbl.AddToClassList("medal-card-desc");
            descLbl.style.fontSize = 12;
            descLbl.style.marginBottom = 8;
            card.Add(descLbl);

            // Rewards row
            var rewardsRow = new VisualElement();
            rewardsRow.style.flexDirection = FlexDirection.Row;
            rewardsRow.style.marginBottom = 8;

            AddRewardChip(rewardsRow, item.RewardBudgetText, new Color(0.831f, 0.690f, 0.369f));  // gold
            AddRewardChip(rewardsRow, item.RewardReputationText, new Color(0.937f, 0.512f, 0.512f)); // ribbon red-light
            AddRewardChip(rewardsRow, item.RewardKnowledgeText, new Color(0.612f, 0.827f, 0.776f)); // patina
            card.Add(rewardsRow);

            // Claim button (only when claimable)
            var claimBtn = new Button(() => ViewModel?.ClaimMilestone(item.Id)) { text = "Madalyayı Tak" };
            claimBtn.AddToClassList("apogee-btn");
            claimBtn.AddToClassList("apogee-btn--primary");
            claimBtn.AddToClassList("medal-claim");
            claimBtn.style.display = DisplayStyle.None;

            item.CanClaim.Subscribe(can =>
                claimBtn.style.display = can ? DisplayStyle.Flex : DisplayStyle.None);

            card.Add(claimBtn);
            return card;
        }

        /// <summary>
        /// NASA kamu malı fotoğrafı + tek satır Türkçe atıf; asset yoksa
        /// "FOTO BEKLENİYOR" placeholder'ı (test ortamı asset'e bağımlı değil).
        /// </summary>
        private void BuildPhotoSlot(VisualElement photoSlot, MilestoneItemViewModel item)
        {
            photoSlot.Clear();

            var record = item.Progress?.Record;
            var tex = record == null ? null : GetPhotoTexture(record.Art);

            if (tex != null)
            {
                var photo = new VisualElement();
                photo.style.position = Position.Absolute;
                photo.style.left = 0;
                photo.style.top = 0;
                photo.style.right = 0;
                photo.style.bottom = 0;
                photo.style.backgroundImage = new StyleBackground(tex);
                photo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                photoSlot.Add(photo);

                if (!string.IsNullOrEmpty(record.ArtCredit))
                {
                    var caption = new Label("Kaynak: NASA — " + record.ArtCredit);
                    caption.AddToClassList("medal-card-photo__caption");
                    caption.style.position = Position.Absolute;
                    caption.style.left = 0;
                    caption.style.right = 0;
                    caption.style.bottom = 0;
                    caption.style.fontSize = 8;
                    caption.style.paddingLeft = 4;
                    caption.style.paddingRight = 4;
                    caption.style.paddingTop = 1;
                    caption.style.paddingBottom = 1;
                    photoSlot.Add(caption);
                }
            }
            else
            {
                var fallback = new Label("FOTO BEKLENİYOR");
                fallback.AddToClassList("medal-card-photo__fallback");
                fallback.style.position = Position.Absolute;
                fallback.style.left = 0;
                fallback.style.right = 0;
                fallback.style.top = 0;
                fallback.style.bottom = 0;
                fallback.style.fontSize = 10;
                photoSlot.Add(fallback);
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────
        private static void AddRewardChip(VisualElement parent, string text, Color color)
        {
            var chip = new Label(text);
            chip.AddToClassList("medal-chip");
            chip.style.fontSize = 11;
            chip.style.color = color;
            chip.style.paddingLeft = 6;
            chip.style.paddingRight = 6;
            chip.style.paddingTop = 2;
            chip.style.paddingBottom = 2;
            chip.style.marginRight = 4;
            chip.style.backgroundColor = new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 0.6f);
            chip.style.borderTopWidth = 1;
            chip.style.borderBottomWidth = 1;
            chip.style.borderLeftWidth = 1;
            chip.style.borderRightWidth = 1;
            chip.style.borderTopColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.5f);
            chip.style.borderBottomColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.5f);
            chip.style.borderLeftColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.5f);
            chip.style.borderRightColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.5f);
            chip.style.borderTopLeftRadius = 3;
            chip.style.borderTopRightRadius = 3;
            chip.style.borderBottomLeftRadius = 3;
            chip.style.borderBottomRightRadius = 3;
            parent.Add(chip);
        }

        // ── photo loading (ADR-054/063/066/068 pattern) ───────────────────────

        private static Texture2D GetPhotoTexture(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
            {
                return null;
            }

            if (PhotoCache.TryGetValue(resourcesPath, out var cached))
            {
                return cached;
            }

            Texture2D tex = null;
            try
            {
                tex = Resources.Load<Texture2D>(resourcesPath);
            }
            catch
            {
                tex = null; // non-Unity harness / missing asset — cards render without photo
            }

            if (tex != null)
            {
                PhotoCache[resourcesPath] = tex;
            }

            return tex;
        }
    }
}
