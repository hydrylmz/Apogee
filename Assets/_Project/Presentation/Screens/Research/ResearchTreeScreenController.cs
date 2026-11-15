using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy;
using Apogee.UI.Core.Controls;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Research
{
    /// <summary>
    /// UI Toolkit screen controller for the Strategic Research Tree (ScreenId.ResearchTree = 10),
    /// restyled as a dark "circuit board" tech-tree graph (GDD §9.2, §14.1, UI redesign ADR).
    /// HOI4-style visual tree: branch swim-lanes with caption strips, prerequisite connector
    /// traces, NASA-photo node cards that stay dark while locked and light up as researched.
    /// Left = tree canvas (zoomable), right = node dossier, bottom = shared icon nav bar.
    /// </summary>
    public sealed class ResearchTreeScreenController : ScreenControllerBase<ResearchTreeViewModel>
    {
        public override ScreenId ScreenId => ScreenId.ResearchTree;

        // Palette mirrors the .theme-circuit USS tokens (kept as C# constants for inline styles).
        private static readonly Color CanvasColor = new(0.024f, 0.035f, 0.059f, 1f);          // #06090F
        private static readonly Color HeaderColor = new(0.031f, 0.051f, 0.082f, 0.98f);        // #080D15
        private static readonly Color PanelColor = new(0.039f, 0.063f, 0.102f, 0.98f);         // #0A101A
        private static readonly Color TextPrimary = new(0.945f, 0.965f, 0.988f, 1f);           // #F1F6FC
        private static readonly Color TextSecondary = new(0.718f, 0.776f, 0.851f, 1f);         // #B7C6D9
        private static readonly Color TextMuted = new(0.478f, 0.549f, 0.639f, 1f);             // #7A8CA3
        private static readonly Color Cyan = new(0.024f, 0.714f, 0.831f, 1f);                  // #06B6D4
        private static readonly Color CyanLight = new(0.403f, 0.909f, 0.976f, 1f);             // #67E8F9
        private static readonly Color Amber = new(0.909f, 0.639f, 0.239f, 1f);                 // #E8A33D
        private static readonly Color AmberLight = new(0.969f, 0.808f, 0.518f, 1f);            // #F7CE84
        private static readonly Color GreenLight = new(0.431f, 0.905f, 0.717f, 1f);            // #6EE7B7
        private static readonly Color RedLight = new(0.988f, 0.647f, 0.647f, 1f);              // #FCA5A5
        private static readonly Color BorderColor = new(0.133f, 0.208f, 0.310f, 0.9f);         // #22354F
        private static readonly Color PillBg = new(0.016f, 0.031f, 0.055f, 0.85f);

        // Top HUD
        private VisualElement _rpPill;
        private VisualElement _kpPill;
        private VisualElement _budgetPill;

        // Active banner
        private Label _activeTitleLabel;
        private Label _activeProgressLabel;
        private VisualElement _activeProgressBar;
        private Button _cancelActiveBtn;

        // Branch chips
        private VisualElement _branchContainer;
        private readonly Dictionary<string, VisualElement> _branchChips = new(StringComparer.OrdinalIgnoreCase);

        // Destination rail (LEO / Moon / Mars / Venus / Europa)
        private VisualElement _destBarRow;
        private VisualElement _destTabsContainer;
        private readonly Dictionary<string, VisualElement> _destTabs = new(StringComparer.OrdinalIgnoreCase);
        private VisualElement _destDetails;
        private VisualElement _destPhoto;
        private Label _destCredit;
        private Label _destName;
        private Label _destStatus;
        private Label _destDesc;
        private Label _destStats;
        private Label _destHazards;
        private VisualElement _destReqList;

        // Graph + toolbar
        private ResearchTreeGraphView _graph;

        // Inspector
        private VisualElement _inspectorPhoto;
        private Label _inspectorCredit;
        private Label _inspectorTitle;
        private Label _inspectorBranchTier;
        private Label _inspectorStatus;
        private Label _inspectorDesc;
        private Label _inspectorCost;
        private Label _inspectorDuration;
        private Label _inspectorPrereqs;
        private Label _inspectorUnlocks;
        private ActionButton _startButton;

        private ScreenNavBar _navBar;
        private readonly Dictionary<string, Texture2D> _photoTexCache = new(StringComparer.OrdinalIgnoreCase);

        public ResearchTreeScreenController(ResearchTreeViewModel viewModel) : base(viewModel) { }

        // ───────────────────────────────────────────────────────────── layout

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-research-tree");
            root.AddToClassList("theme-circuit"); // Circuit board identity
            root.style.backgroundColor = CanvasColor;
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildHeader());
            root.Add(BuildActiveStrip());
            root.Add(BuildDestinationBar());
            root.Add(BuildBranchBar());

            var body = new VisualElement();
            body.AddToClassList("screen-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.flexBasis = 0;
            body.style.minHeight = 0;

            body.Add(BuildGraphColumn());
            body.Add(BuildInspector());
            root.Add(body);

            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.ResearchTree);
            root.Add(_navBar);

            return root;
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("circuit-console-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 14;
            header.style.paddingRight = 14;
            header.style.flexShrink = 0;

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;

            var titleIcon = IconElement.Create(IconType.Research, 18f);
            titleIcon.style.color = CyanLight;
            titleIcon.style.marginRight = 8;
            left.Add(titleIcon);

            var title = new Label("AR-GE VE TEKNOLOJİ AĞACI");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1f;
            title.style.color = TextPrimary;
            left.Add(title);
            header.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            _rpPill = CreatePill(IconType.ResearchPoints, "0 RP", CyanLight);
            _kpPill = CreatePill(IconType.KnowledgePoints, "0 KP", AmberLight);
            _budgetPill = CreatePill(IconType.Budget, "0 M$", GreenLight);
            AttachResourceTooltip(_rpPill, "Araştırma Puanı (RP)",
                "Teknoloji ağacındaki düğümleri ilerletmek için harcanır. Zaman içinde aylık olarak üretilir; bilimsel misyonlar ekstra RP kazandırır.");
            AttachResourceTooltip(_kpPill, "Bilgi Puanı (KP)",
                "Eğitim içeriklerinden ve misyon sonrası çıkarımlardan kazanılır; kilometre taşı ödülleriyle de artar. Kampanya sonundaki skorunuza katkı sağlar.");
            AttachResourceTooltip(_budgetPill, "Bütçe (M$)",
                "Ajansın kasası — milyon dolar cinsinden. Araştırma başlangıç maliyetleri bu kasadan düşülür; yıllık tahsisat itibarınızla orantılıdır.");
            right.Add(_rpPill);
            right.Add(_kpPill);
            right.Add(_budgetPill);
            header.Add(right);

            return header;
        }

        private VisualElement BuildActiveStrip()
        {
            var strip = new VisualElement();
            strip.AddToClassList("circuit-active-strip");
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.justifyContent = Justify.SpaceBetween;
            strip.style.alignItems = Align.Center;
            strip.style.paddingTop = 5;
            strip.style.paddingBottom = 5;
            strip.style.paddingLeft = 14;
            strip.style.paddingRight = 14;
            strip.style.flexShrink = 0;

            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.Center;
            left.style.flexGrow = 1;
            left.style.marginRight = 12;

            var activeIcon = IconElement.Create(IconType.Time, 15f);
            activeIcon.style.color = CyanLight;
            activeIcon.style.marginRight = 8;
            left.Add(activeIcon);

            _activeTitleLabel = new Label("Aktif Araştırma: Yok");
            _activeTitleLabel.style.fontSize = 12;
            _activeTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _activeTitleLabel.style.color = CyanLight;
            _activeTitleLabel.style.marginRight = 14;
            left.Add(_activeTitleLabel);

            var barTrack = new VisualElement();
            barTrack.style.width = 170;
            barTrack.style.height = 4;
            barTrack.style.backgroundColor = BorderColor;
            barTrack.style.borderTopLeftRadius = 9999;
            barTrack.style.borderTopRightRadius = 9999;
            barTrack.style.borderBottomLeftRadius = 9999;
            barTrack.style.borderBottomRightRadius = 9999;
            barTrack.style.marginRight = 10;

            _activeProgressBar = new VisualElement();
            _activeProgressBar.style.width = Length.Percent(0);
            _activeProgressBar.style.height = Length.Percent(100);
            _activeProgressBar.style.backgroundColor = Cyan;
            _activeProgressBar.style.borderTopLeftRadius = 9999;
            _activeProgressBar.style.borderTopRightRadius = 9999;
            _activeProgressBar.style.borderBottomLeftRadius = 9999;
            _activeProgressBar.style.borderBottomRightRadius = 9999;
            barTrack.Add(_activeProgressBar);
            left.Add(barTrack);

            _activeProgressLabel = new Label("—");
            _activeProgressLabel.style.fontSize = 11;
            _activeProgressLabel.style.color = TextSecondary;
            left.Add(_activeProgressLabel);

            strip.Add(left);

            _cancelActiveBtn = new Button(() => ShowCancelConfirmation()) { text = "İptal Et" };
            _cancelActiveBtn.style.paddingLeft = 12;
            _cancelActiveBtn.style.paddingRight = 12;
            _cancelActiveBtn.style.paddingTop = 3;
            _cancelActiveBtn.style.paddingBottom = 3;
            _cancelActiveBtn.style.backgroundColor = new Color(0.28f, 0.09f, 0.09f, 0.9f);
            _cancelActiveBtn.style.color = RedLight;
            _cancelActiveBtn.style.fontSize = 11;
            _cancelActiveBtn.style.borderTopLeftRadius = 4;
            _cancelActiveBtn.style.borderTopRightRadius = 4;
            _cancelActiveBtn.style.borderBottomLeftRadius = 4;
            _cancelActiveBtn.style.borderBottomRightRadius = 4;
            _cancelActiveBtn.AddManipulator(new TooltipManipulator(new TooltipData(
                "Araştırmayı iptal et",
                "Kuyruğun başındaki araştırmayı iptal eder; harcanan başlangıç maliyeti geri ödenir.",
                reason: "Onay penceresi açılır — işlem geri alınamaz.",
                severity: TooltipSeverity.Warning)));
            strip.Add(_cancelActiveBtn);

            return strip;
        }

        // ───────────────────────────────────────────────────────────── destination rail

        /// <summary>
        /// Mission destination rail: one tab per destination (LEO, Moon, Mars, Venus,
        /// Europa) with its NASA photo coin. Locked destinations render dark (HOI4
        /// locked-node feel) until their required research is completed. Selecting a
        /// destination expands a details card and highlights the required nodes on the
        /// tree — the "why" behind every branch.
        /// </summary>
        private VisualElement BuildDestinationBar()
        {
            var column = new VisualElement();
            column.style.flexShrink = 0;

            _destBarRow = new VisualElement();
            _destBarRow.AddToClassList("circuit-dest-bar");
            _destBarRow.style.flexDirection = FlexDirection.Row;
            _destBarRow.style.alignItems = Align.Center;
            _destBarRow.style.paddingTop = 6;
            _destBarRow.style.paddingBottom = 2;
            _destBarRow.style.paddingLeft = 14;
            _destBarRow.style.paddingRight = 14;

            var captionIcon = IconElement.Create(IconType.SolarSystem, 14f);
            captionIcon.style.color = AmberLight;
            captionIcon.style.marginRight = 6;

            var caption = new Label("GÖREV HEDEFLERİ");
            caption.style.fontSize = 10;
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.letterSpacing = 1.5f;
            caption.style.color = AmberLight;
            caption.style.marginRight = 12;

            _destBarRow.Add(captionIcon);
            _destBarRow.Add(caption);

            _destTabsContainer = new VisualElement();
            _destTabsContainer.style.flexDirection = FlexDirection.Row;
            _destTabsContainer.style.alignItems = Align.Center;
            _destTabsContainer.style.flexWrap = Wrap.Wrap;
            _destTabsContainer.style.flexGrow = 1;
            _destBarRow.Add(_destTabsContainer);

            var hint = new Label("Bir hedef seçin → ağaçta o görev için gereken teknolojiler vurgulanır");
            hint.style.fontSize = 10;
            hint.style.color = TextMuted;
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            _destBarRow.Add(hint);

            column.Add(_destBarRow);

            _destDetails = BuildDestinationDetails();
            _destDetails.style.display = DisplayStyle.None;
            column.Add(_destDetails);

            return column;
        }

        private VisualElement BuildDestinationDetails()
        {
            var panel = new VisualElement();
            panel.AddToClassList("circuit-dest-details");
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.FlexStart;
            panel.style.marginLeft = 8;
            panel.style.marginRight = 8;
            panel.style.marginBottom = 4;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;
            panel.style.paddingLeft = 10;
            panel.style.paddingRight = 10;
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.backgroundColor = new Color(0.039f, 0.063f, 0.102f, 0.85f);
            panel.style.borderTopWidth = 1;
            panel.style.borderRightWidth = 1;
            panel.style.borderBottomWidth = 1;
            panel.style.borderLeftWidth = 1;
            panel.style.borderTopColor = BorderColor;
            panel.style.borderRightColor = BorderColor;
            panel.style.borderBottomColor = BorderColor;
            panel.style.borderLeftColor = BorderColor;

            // ── Left: photo + dossier ──
            var left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.FlexStart;
            left.style.flexGrow = 1;
            left.style.marginRight = 12;

            _destPhoto = new VisualElement();
            _destPhoto.style.width = 64;
            _destPhoto.style.height = 64;
            _destPhoto.style.flexShrink = 0;
            _destPhoto.style.borderTopLeftRadius = 6;
            _destPhoto.style.borderTopRightRadius = 6;
            _destPhoto.style.borderBottomLeftRadius = 6;
            _destPhoto.style.borderBottomRightRadius = 6;
            _destPhoto.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
            _destPhoto.AddManipulator(new TooltipManipulator(() =>
            {
                var dest = ViewModel?.SelectedDestination.Value;
                return new TooltipData(
                    dest?.DisplayName ?? "Görev hedefi",
                    string.IsNullOrEmpty(dest?.ArtCredit)
                        ? "NASA kamuya açık görsel arşivinden hedef fotoğrafı."
                        : dest.ArtCredit,
                    reason: "Kaynak: NASA (kamu malı)");
            }));
            left.Add(_destPhoto);

            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.style.flexShrink = 1;
            info.style.marginLeft = 10;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            _destName = new Label("—");
            _destName.style.fontSize = 14;
            _destName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _destName.style.color = TextPrimary;
            titleRow.Add(_destName);

            _destStatus = new Label("—");
            _destStatus.style.fontSize = 10;
            _destStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _destStatus.style.letterSpacing = 1f;
            _destStatus.style.marginLeft = 8;
            _destStatus.style.paddingLeft = 6;
            _destStatus.style.paddingRight = 6;
            _destStatus.style.paddingTop = 1;
            _destStatus.style.paddingBottom = 1;
            _destStatus.style.borderTopLeftRadius = 8;
            _destStatus.style.borderTopRightRadius = 8;
            _destStatus.style.borderBottomLeftRadius = 8;
            _destStatus.style.borderBottomRightRadius = 8;
            titleRow.Add(_destStatus);

            info.Add(titleRow);

            _destCredit = new Label("Kaynak: NASA");
            _destCredit.style.fontSize = 9;
            _destCredit.style.color = TextMuted;
            _destCredit.style.whiteSpace = WhiteSpace.Normal;
            _destCredit.style.marginTop = 1;
            _destCredit.style.marginBottom = 3;
            info.Add(_destCredit);

            _destDesc = new Label(string.Empty);
            _destDesc.style.fontSize = 11;
            _destDesc.style.color = TextSecondary;
            _destDesc.style.whiteSpace = WhiteSpace.Normal;
            _destDesc.style.marginBottom = 5;
            info.Add(_destDesc);

            _destStats = new Label(string.Empty);
            _destStats.style.fontSize = 11;
            _destStats.style.color = CyanLight;
            _destStats.style.whiteSpace = WhiteSpace.Normal;
            _destStats.style.marginBottom = 2;
            info.Add(_destStats);

            _destHazards = new Label(string.Empty);
            _destHazards.style.fontSize = 10;
            _destHazards.style.color = TextMuted;
            _destHazards.style.whiteSpace = WhiteSpace.Normal;
            info.Add(_destHazards);

            left.Add(info);
            panel.Add(left);

            // ── Right: requirement checklist ──
            var right = new VisualElement();
            right.style.width = 250;
            right.style.flexShrink = 0;

            var reqCaption = new Label("KİLİT KOŞULLARI");
            reqCaption.style.fontSize = 9;
            reqCaption.style.unityFontStyleAndWeight = FontStyle.Bold;
            reqCaption.style.letterSpacing = 1.5f;
            reqCaption.style.color = CyanLight;
            reqCaption.style.marginBottom = 5;
            right.Add(reqCaption);

            _destReqList = new VisualElement();
            right.Add(_destReqList);

            panel.Add(right);
            return panel;
        }

        private void RenderDestinationTabs()
        {
            if (_destTabsContainer == null || ViewModel == null)
            {
                return;
            }

            _destTabsContainer.Clear();
            _destTabs.Clear();

            var destinations = ViewModel.Destinations.Value;
            bool hasDestinations = destinations != null && destinations.Count > 0;
            _destBarRow.style.display = hasDestinations ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasDestinations)
            {
                return;
            }

            foreach (var dest in destinations)
            {
                var tab = new VisualElement();
                tab.AddToClassList("circuit-dest-tab");
                if (!dest.IsUnlocked)
                {
                    tab.AddToClassList("circuit-dest-tab--locked");
                }
                tab.style.flexDirection = FlexDirection.Row;
                tab.style.alignItems = Align.Center;
                tab.style.paddingTop = 3;
                tab.style.paddingBottom = 3;
                tab.style.paddingLeft = 4;
                tab.style.paddingRight = 10;
                tab.style.marginRight = 6;
                tab.style.marginBottom = 2;
                tab.pickingMode = PickingMode.Position;

                // Planet photo coin (24px circle) with a dark veil while locked.
                var coinWrap = new VisualElement();
                coinWrap.style.width = 22;
                coinWrap.style.height = 22;
                coinWrap.style.flexShrink = 0;
                coinWrap.style.marginRight = 6;
                coinWrap.style.borderTopLeftRadius = 11;
                coinWrap.style.borderTopRightRadius = 11;
                coinWrap.style.borderBottomLeftRadius = 11;
                coinWrap.style.borderBottomRightRadius = 11;
                coinWrap.style.overflow = Overflow.Hidden;
                coinWrap.AddToClassList("circuit-dest-coin");

                var coinTex = GetPhotoTexture(dest.Art);
                if (coinTex != null)
                {
                    coinWrap.style.backgroundImage = new StyleBackground(coinTex);
                    coinWrap.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                }
                else
                {
                    coinWrap.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
                }

                var coinVeil = new VisualElement();
                coinVeil.AddToClassList("circuit-dest-coin-veil");
                coinVeil.style.position = Position.Absolute;
                coinVeil.style.left = 0;
                coinVeil.style.top = 0;
                coinVeil.style.right = 0;
                coinVeil.style.bottom = 0;
                coinVeil.pickingMode = PickingMode.Ignore;
                coinWrap.Add(coinVeil);

                tab.Add(coinWrap);

                var nameLabel = new Label(dest.DisplayName);
                nameLabel.style.fontSize = 11;
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                tab.Add(nameLabel);

                string tooltipReason = dest.IsUnlocked
                    ? "Tıkla: bu görev için gereken teknolojiler ağaçta vurgulanır."
                    : $"Kilitli — {dest.BuildLockReason(id => ViewModel.GetNodeName(id))}";
                tab.AddManipulator(new TooltipManipulator(new TooltipData(
                    dest.DisplayName,
                    $"{dest.Description}\n{dest.BuildTravelSummary()}",
                    reason: tooltipReason,
                    severity: dest.IsUnlocked ? TooltipSeverity.Success : TooltipSeverity.Warning)));

                tab.RegisterCallback<ClickEvent>(_ => ViewModel.SelectDestination(dest.Id));

                _destTabs[dest.Id] = tab;
                _destTabsContainer.Add(tab);
            }

            HighlightActiveDestination();
        }

        private void HighlightActiveDestination()
        {
            if (ViewModel == null)
            {
                return;
            }

            string selectedId = ViewModel.SelectedDestination.Value?.Id;
            foreach (var kvp in _destTabs)
            {
                bool isActive = !string.IsNullOrEmpty(selectedId)
                    && string.Equals(kvp.Key, selectedId, StringComparison.OrdinalIgnoreCase);
                if (isActive)
                {
                    kvp.Value.AddToClassList("circuit-dest-tab--active");
                }
                else
                {
                    kvp.Value.RemoveFromClassList("circuit-dest-tab--active");
                }
            }
        }

        private void UpdateDestinationDetails()
        {
            if (_destDetails == null || ViewModel == null)
            {
                return;
            }

            var dest = ViewModel.SelectedDestination.Value;
            if (dest == null)
            {
                _destDetails.style.display = DisplayStyle.None;
                return;
            }

            _destDetails.style.display = DisplayStyle.Flex;

            if (_destPhoto != null)
            {
                var tex = GetPhotoTexture(dest.Art);
                if (tex != null)
                {
                    _destPhoto.style.backgroundImage = new StyleBackground(tex);
                    _destPhoto.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                }
                else
                {
                    _destPhoto.style.backgroundImage = null;
                    _destPhoto.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
                }
            }

            if (_destCredit != null)
            {
                _destCredit.text = string.IsNullOrEmpty(dest.ArtCredit)
                    ? "Kaynak: NASA (kamu malı)"
                    : $"Kaynak: NASA — {dest.ArtCredit}";
            }

            if (_destName != null) _destName.text = dest.DisplayName;

            if (_destStatus != null)
            {
                _destStatus.text = dest.IsUnlocked ? "AÇIK" : "KİLİTLİ";
                _destStatus.style.color = dest.IsUnlocked ? GreenLight : RedLight;
                _destStatus.style.backgroundColor = dest.IsUnlocked
                    ? new Color(0.03f, 0.16f, 0.11f, 0.9f)
                    : new Color(0.18f, 0.05f, 0.05f, 0.9f);
            }

            if (_destDesc != null) _destDesc.text = dest.Description;
            if (_destStats != null) _destStats.text = dest.BuildTravelSummary();

            if (_destHazards != null)
            {
                string hazards = dest.BuildHazardSummary();
                _destHazards.text = string.IsNullOrEmpty(hazards) ? "Ortam tehlikesi: yok" : $"Ortam tehlikeleri: {hazards}";
            }

            RenderDestinationRequirements(dest);
        }

        private void RenderDestinationRequirements(DestinationItemViewModel dest)
        {
            if (_destReqList == null)
            {
                return;
            }

            _destReqList.Clear();

            if (dest.UnlockResearchIds.Count == 0 && dest.UnlockReputationMin <= 0.0)
            {
                var openRow = new Label("Bu hedef kampanya başından itibaren açıktır.");
                openRow.style.fontSize = 11;
                openRow.style.color = GreenLight;
                openRow.style.whiteSpace = WhiteSpace.Normal;
                _destReqList.Add(openRow);
                return;
            }

            foreach (var researchId in dest.UnlockResearchIds)
            {
                bool missing = dest.IsResearchMissing(researchId);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 3;

                var icon = IconElement.Create(missing ? IconType.Lock : IconType.Approve, 12f);
                icon.style.color = missing ? Amber : GreenLight;
                icon.style.marginRight = 5;
                row.Add(icon);

                var label = new Label(ViewModel.GetNodeName(researchId));
                label.style.fontSize = 11;
                label.style.color = missing ? AmberLight : GreenLight;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexShrink = 1;
                row.Add(label);

                if (missing)
                {
                    row.AddManipulator(new TooltipManipulator(new TooltipData(
                        ViewModel.GetNodeName(researchId),
                        "Bu teknoloji hedefin kilidini açar; kartına tıklayıp araştırmayı başlatabilirsin.",
                        reason: "Tıkla: düğüm detayını aç.",
                        severity: TooltipSeverity.Warning)));
                    row.RegisterCallback<ClickEvent>(_ => ViewModel.SelectNode(researchId));
                }

                _destReqList.Add(row);
            }

            if (dest.UnlockReputationMin > 0.0)
            {
                bool repOk = dest.CurrentReputation >= dest.UnlockReputationMin;
                var repRow = new VisualElement();
                repRow.style.flexDirection = FlexDirection.Row;
                repRow.style.alignItems = Align.Center;
                repRow.style.marginBottom = 3;

                var repIcon = IconElement.Create(repOk ? IconType.Approve : IconType.Budget, 12f);
                repIcon.style.color = repOk ? GreenLight : Amber;
                repIcon.style.marginRight = 5;
                repRow.Add(repIcon);

                var repLabel = new Label(
                    $"İtibar ≥ {dest.UnlockReputationMin:F0} (mevcut {dest.CurrentReputation:F0})");
                repLabel.style.fontSize = 11;
                repLabel.style.color = repOk ? GreenLight : AmberLight;
                repRow.Add(repLabel);

                _destReqList.Add(repRow);
            }
        }

        private VisualElement BuildBranchBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("circuit-branch-bar");
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.paddingTop = 5;
            bar.style.paddingBottom = 5;
            bar.style.paddingLeft = 14;
            bar.style.paddingRight = 14;
            bar.style.flexShrink = 0;
            _branchContainer = bar;
            return bar;
        }

        private VisualElement BuildGraphColumn()
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;
            column.style.flexBasis = 0;

            // Toolbar: status legend (left) + zoom (right)
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.SpaceBetween;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingTop = 6;
            toolbar.style.paddingBottom = 6;
            toolbar.style.paddingLeft = 14;
            toolbar.style.paddingRight = 14;
            toolbar.style.flexShrink = 0;

            var legend = new VisualElement();
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.alignItems = Align.Center;
            AddLegendItem(legend, new Color(0.07f, 0.10f, 0.16f), "Kilitli");
            AddLegendItem(legend, Amber, "Açık");
            AddLegendItem(legend, CyanLight, "Araştırılıyor");
            AddLegendItem(legend, GreenLight, "Tamamlandı");
            toolbar.Add(legend);

            var zoomRow = new VisualElement();
            zoomRow.style.flexDirection = FlexDirection.Row;
            zoomRow.style.alignItems = Align.Center;

            var zoomOut = new Button(() => _graph?.ZoomOut()) { text = "−" };
            StyleZoomButton(zoomOut);
            var zoomIn = new Button(() => _graph?.ZoomIn()) { text = "+" };
            StyleZoomButton(zoomIn);
            var zoomReset = new Button(() => _graph?.ZoomReset()) { text = "%100" };
            StyleZoomButton(zoomReset);

            zoomOut.AddManipulator(new TooltipManipulator(new TooltipData("Uzaklaştır", "Ağacı küçültür; daha fazla dal aynı anda görünür kalır.")));
            zoomIn.AddManipulator(new TooltipManipulator(new TooltipData("Yakınlaştır", "Ağacı büyütür; kart okunurluğu artar.")));
            zoomReset.AddManipulator(new TooltipManipulator(new TooltipData("Yakınlaşmayı sıfırla", "Yakınlaşmayı varsayılan seviyeye getirir.")));

            zoomRow.Add(zoomOut);
            zoomRow.Add(zoomIn);
            zoomRow.Add(zoomReset);
            toolbar.Add(zoomRow);

            column.Add(toolbar);

            _graph = new ResearchTreeGraphView();
            _graph.NodeClicked += id =>
            {
                ViewModel?.SelectNode(id);
            };
            column.Add(_graph);

            return column;
        }

        private VisualElement BuildInspector()
        {
            var inspector = new VisualElement();
            inspector.AddToClassList("circuit-inspector");
            inspector.style.width = 320;
            inspector.style.flexShrink = 0;
            inspector.style.paddingTop = 12;
            inspector.style.paddingBottom = 12;
            inspector.style.paddingLeft = 14;
            inspector.style.paddingRight = 14;
            inspector.style.flexDirection = FlexDirection.Column;
            inspector.style.justifyContent = Justify.SpaceBetween;

            var top = new VisualElement();

            var caption = new Label("DÜĞÜM DETAYI");
            caption.style.fontSize = 10;
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.letterSpacing = 1.5f;
            caption.style.color = CyanLight;
            caption.style.marginBottom = 8;
            top.Add(caption);

            _inspectorPhoto = new VisualElement();
            _inspectorPhoto.style.height = 150;
            _inspectorPhoto.style.flexShrink = 0;
            _inspectorPhoto.style.marginBottom = 4;
            _inspectorPhoto.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
            _inspectorPhoto.style.borderTopLeftRadius = 4;
            _inspectorPhoto.style.borderTopRightRadius = 4;
            _inspectorPhoto.style.borderBottomLeftRadius = 4;
            _inspectorPhoto.style.borderBottomRightRadius = 4;
            _inspectorPhoto.AddManipulator(new TooltipManipulator(() =>
            {
                var node = ViewModel?.SelectedNode.Value;
                return new TooltipData(
                    node?.Name ?? "Ar-Ge arşivi",
                    string.IsNullOrEmpty(node?.ArtCredit)
                        ? "NASA kamuya açık görsel arşivinden teknoloji kartı fotoğrafı."
                        : node.ArtCredit,
                    reason: "Kaynak: NASA (kamu malı)");
            }));
            top.Add(_inspectorPhoto);

            _inspectorCredit = new Label("Kaynak: NASA");
            _inspectorCredit.AddToClassList("circuit-credit");
            _inspectorCredit.style.fontSize = 9;
            _inspectorCredit.style.color = TextMuted;
            _inspectorCredit.style.whiteSpace = WhiteSpace.Normal;
            _inspectorCredit.style.marginBottom = 8;
            top.Add(_inspectorCredit);

            _inspectorTitle = new Label("Düğüm Seçiniz");
            _inspectorTitle.style.fontSize = 17;
            _inspectorTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorTitle.style.color = TextPrimary;
            _inspectorTitle.style.whiteSpace = WhiteSpace.Normal;
            _inspectorTitle.style.marginBottom = 4;
            top.Add(_inspectorTitle);

            _inspectorBranchTier = new Label("Dal: — · Aşama: —");
            _inspectorBranchTier.style.fontSize = 11;
            _inspectorBranchTier.style.color = TextSecondary;
            _inspectorBranchTier.style.marginBottom = 6;
            top.Add(_inspectorBranchTier);

            _inspectorStatus = new Label("Durum: —");
            _inspectorStatus.style.fontSize = 12;
            _inspectorStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorStatus.style.color = TextPrimary;
            _inspectorStatus.style.marginBottom = 8;
            top.Add(_inspectorStatus);

            _inspectorDesc = new Label(string.Empty);
            _inspectorDesc.style.fontSize = 11;
            _inspectorDesc.style.color = TextSecondary;
            _inspectorDesc.style.whiteSpace = WhiteSpace.Normal;
            _inspectorDesc.style.marginBottom = 10;
            top.Add(_inspectorDesc);

            _inspectorCost = CreateDetailRow(top, "Maliyet:", "0 RP / 0 M$");
            _inspectorDuration = CreateDetailRow(top, "Süre:", "0 gün");
            _inspectorPrereqs = CreateDetailRow(top, "Ön Koşul:", "Yok");
            _inspectorUnlocks = CreateDetailRow(top, "Açılan Modüller:", "Yok");

            inspector.Add(top);

            var bottom = new VisualElement();
            _startButton = new ActionButton(
                IconType.Approve,
                "Araştırmayı Başlat",
                () => ViewModel?.StartSelectedResearchCommand.Execute(),
                "Seçilen teknolojiyi araştırma kuyruğuna ekler; başlangıç maliyeti kasadan düşülür.")
            {
                style = { height = 38, marginBottom = 0 }
            };
            _startButton.SetVariant("primary");
            bottom.Add(_startButton);
            inspector.Add(bottom);

            return inspector;
        }

        // ───────────────────────────────────────────────────────────── lifecycle

        protected override void OnInitialized()
        {
            RegisterObservables();

            // Destination catalog (destinations.json) — failure leaves the rail hidden.
            ViewModel?.SetDestinations(ResearchTreeViewModel.LoadDefaultDestinations());
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
            RenderDestinationTabs();
            _graph?.SetNodes(ViewModel?.AllNodes.Value);
            UpdateInspector();
            UpdateGraphSelection();
            UpdateDestinationDetails();
            ApplyDestinationHighlight();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.ResearchPoints.Subscribe(rp =>
            {
                SetPillText(_rpPill, $"{rp:F0} RP");
            });

            ViewModel.KnowledgePoints.Subscribe(kp =>
            {
                SetPillText(_kpPill, $"{kp:F0} KP");
            });

            ViewModel.Budget.Subscribe(b =>
            {
                SetPillText(_budgetPill, $"{b:F1} M$");
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

            ViewModel.Branches.Subscribe(_ => RenderBranches());
            ViewModel.AllNodes.Subscribe(nodes =>
            {
                _graph?.SetNodes(nodes);
                UpdateGraphSelection();
                UpdateInspectorPhoto();
            });

            ViewModel.SelectedNode.Subscribe(_ =>
            {
                UpdateInspector();
                UpdateGraphSelection();
            });

            ViewModel.Destinations.Subscribe(_ => RenderDestinationTabs());

            ViewModel.SelectedDestination.Subscribe(_ =>
            {
                HighlightActiveDestination();
                UpdateDestinationDetails();
                ApplyDestinationHighlight();
            });

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                _navBar?.SetStatus(msg ?? string.Empty);
            });
        }

        // ───────────────────────────────────────────────────────────── branch chips

        private void RenderBranches()
        {
            if (_branchContainer == null || ViewModel == null)
            {
                return;
            }

            _branchContainer.Clear();
            _branchChips.Clear();

            var branches = ViewModel.Branches.Value;
            if (branches == null)
            {
                return;
            }

            foreach (var branch in branches)
            {
                string bName = branch;
                var chip = new Label(GetBranchLabel(bName));
                chip.AddToClassList("circuit-chip");
                chip.style.marginRight = 5;
                chip.style.marginBottom = 2;
                chip.pickingMode = PickingMode.Position;

                chip.AddManipulator(new TooltipManipulator(new TooltipData(
                    GetBranchLabel(bName),
                    bName == "Tümü"
                        ? "Tüm dalları aynı parlaklıkta gösterir."
                        : $"{GetBranchLabel(bName)} dalı vurgulanır; diğer dallar soluklaştırılır.")));

                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    ViewModel.SelectBranch(bName);
                    HighlightActiveBranch();
                });

                _branchChips[bName] = chip;
                _branchContainer.Add(chip);
            }

            HighlightActiveBranch();
        }

        private static string GetBranchLabel(string branch)
        {
            if (string.Equals(branch, "Tümü", StringComparison.OrdinalIgnoreCase))
            {
                return ResearchNodeItemViewModel.LocalizeOr("research.branch.all", "Tümü");
            }

            return ResearchNodeItemViewModel.GetBranchDisplayName(branch);
        }

        private void HighlightActiveBranch()
        {
            if (ViewModel == null || _graph == null)
            {
                return;
            }

            string sel = ViewModel.SelectedBranch.Value ?? "Tümü";
            foreach (var kvp in _branchChips)
            {
                bool isSelected = string.Equals(kvp.Key, sel, StringComparison.OrdinalIgnoreCase);
                if (kvp.Value == null)
                {
                    continue;
                }

                if (isSelected)
                {
                    kvp.Value.AddToClassList("circuit-chip--active");
                }
                else
                {
                    kvp.Value.RemoveFromClassList("circuit-chip--active");
                }
            }

            _graph.SetHighlightedBranch(sel);
        }

        // ───────────────────────────────────────────────────────────── inspector

        private void UpdateGraphSelection()
        {
            _graph?.SetSelected(ViewModel?.SelectedNode.Value?.Id);
        }

        /// <summary>Feeds the selected destination's required research ids to the graph.</summary>
        private void ApplyDestinationHighlight()
        {
            var dest = ViewModel?.SelectedDestination.Value;
            _graph?.SetHighlightedNodes(dest?.UnlockResearchIds, dest?.DisplayName);
        }

        private void UpdateInspector()
        {
            var node = ViewModel?.SelectedNode.Value;
            if (node == null)
            {
                if (_inspectorTitle != null) _inspectorTitle.text = "Düğüm Seçiniz";
                if (_inspectorBranchTier != null) _inspectorBranchTier.text = "Ağaçtan bir düğüm seçin";
                if (_inspectorStatus != null) _inspectorStatus.text = "Durum: —";
                if (_inspectorDesc != null) _inspectorDesc.text = string.Empty;
                if (_inspectorPhoto != null) SetPhoto(null);
                if (_inspectorCredit != null) _inspectorCredit.text = "Kaynak: NASA";
                if (_startButton != null) _startButton.SetAvailable(false, "Önce ağaçtan bir araştırma düğümü seçin.");
                return;
            }

            if (_inspectorTitle != null) _inspectorTitle.text = node.Name;
            if (_inspectorBranchTier != null) _inspectorBranchTier.text = $"Dal: {node.BranchDisplayName} · Aşama: {node.Tier}";

            if (_inspectorStatus != null)
            {
                switch (node.Status)
                {
                    case ResearchStatus.Completed:
                        _inspectorStatus.text = "Durum: Tamamlandı";
                        _inspectorStatus.style.color = GreenLight;
                        break;
                    case ResearchStatus.InProgress:
                        _inspectorStatus.text = $"Durum: Araştırılıyor ({node.ProgressDays}/{node.DurationDays} gün)";
                        _inspectorStatus.style.color = CyanLight;
                        break;
                    case ResearchStatus.Available:
                        _inspectorStatus.text = "Durum: Araştırmaya Hazır";
                        _inspectorStatus.style.color = Amber;
                        break;
                    default:
                        _inspectorStatus.text = "Durum: Kilitli";
                        _inspectorStatus.style.color = RedLight;
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

            UpdateInspectorPhoto();

            if (_startButton != null)
            {
                switch (node.Status)
                {
                    case ResearchStatus.Available when node.CanStart:
                        _startButton.SetLabel("Araştırmayı Başlat");
                        _startButton.SetPrice($"{node.StartCostMUSD:F0} M$");
                        _startButton.SetDuration($"{node.DurationDays} gün");
                        _startButton.SetAvailable(true);
                        _startButton.SetResultDescription(
                            $"{node.Name} araştırması kuyruğa eklenir. " +
                            $"Maliyet: {node.ResearchPointCost:F0} RP (aylık üretilir) + {node.StartCostMUSD:F1} M$ başlangıç kasası. Süre: ~{node.DurationDays} gün.");
                        break;
                    case ResearchStatus.Available:
                        _startButton.SetLabel("Araştırmayı Başlat");
                        _startButton.SetPrice($"{node.StartCostMUSD:F0} M$");
                        _startButton.SetDuration($"{node.DurationDays} gün");
                        _startButton.SetAvailable(false, $"Yetersiz bütçe — başlangıç için {node.StartCostMUSD:F1} M$ gerekli.");
                        break;
                    case ResearchStatus.InProgress:
                        _startButton.SetLabel("Araştırılıyor");
                        _startButton.SetPrice(null);
                        _startButton.SetDuration($"{node.DurationDays} gün");
                        _startButton.SetAvailable(false, "Bu teknoloji şu anda araştırılıyor.");
                        break;
                    case ResearchStatus.Completed:
                        _startButton.SetLabel("Tamamlandı");
                        _startButton.SetPrice(null);
                        _startButton.SetDuration(null);
                        _startButton.SetAvailable(false, "Bu teknoloji zaten tamamlandı.");
                        break;
                    default:
                        _startButton.SetLabel("Kilitli");
                        _startButton.SetPrice(null);
                        _startButton.SetDuration(null);
                        _startButton.SetAvailable(false,
                            node.Prerequisites.Count > 0
                                ? $"Ön koşullar tamamlanmalı: {string.Join(", ", node.Prerequisites.Select(id => ViewModel.GetNodeName(id)))}"
                                : "Ön koşullar tamamlanmalı.");
                        break;
                }
            }
        }

        private void UpdateInspectorPhoto()
        {
            var node = ViewModel?.SelectedNode.Value;
            SetPhoto(node == null ? null : GetPhotoTexture(node.Art));
            if (_inspectorCredit != null)
            {
                _inspectorCredit.text = string.IsNullOrEmpty(node?.ArtCredit)
                    ? "Kaynak: NASA (kamu malı)"
                    : $"Kaynak: NASA — {node.ArtCredit}";
            }
        }

        private void SetPhoto(Texture2D tex)
        {
            if (_inspectorPhoto == null)
            {
                return;
            }

            if (tex != null)
            {
                _inspectorPhoto.style.backgroundImage = new StyleBackground(tex);
                _inspectorPhoto.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _inspectorPhoto.style.color = Color.white;
            }
            else
            {
                _inspectorPhoto.style.backgroundImage = null;
                _inspectorPhoto.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                _inspectorPhoto.style.backgroundColor = new Color(0.05f, 0.08f, 0.13f, 1f);
            }
        }

        private Texture2D GetPhotoTexture(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
            {
                return null;
            }

            if (_photoTexCache.TryGetValue(resourcesPath, out var cached))
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
                tex = null; // non-Unity harness / missing asset — inspector renders without photo
            }

            if (tex != null)
            {
                _photoTexCache[resourcesPath] = tex;
            }

            return tex;
        }

        // ───────────────────────────────────────────────────────────── dialogs

        private void ShowCancelConfirmation()
        {
            if (ViewModel == null)
            {
                return;
            }

            ConfirmDialog.Show(
                RootElement,
                "Araştırmayı iptal et",
                "Kuyruğun başındaki araştırma iptal edilecek; harcanan başlangıç maliyeti geri ödenir ve ilerleme sıfırlanır. Bu işlem geri alınamaz.",
                "İptal Et",
                () => ViewModel.CancelActiveResearchCommand.Execute());
        }

        // ───────────────────────────────────────────────────────────── helpers

        /// <summary>
        /// Shared hover tooltip explaining a campaign resource pill (same copy used
        /// on every screen so each resource always reads the same way).
        /// </summary>
        private static void AttachResourceTooltip(VisualElement pill, string title, string content)
        {
            if (pill == null) return;
            pill.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(title, content)));
        }

        private static VisualElement CreatePill(IconType icon, string text, Color color)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.marginLeft = 10;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 8;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.backgroundColor = PillBg;
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            pill.style.borderTopWidth = 1;
            pill.style.borderRightWidth = 1;
            pill.style.borderBottomWidth = 1;
            pill.style.borderLeftWidth = 1;
            pill.style.borderTopColor = BorderColor;
            pill.style.borderRightColor = BorderColor;
            pill.style.borderBottomColor = BorderColor;
            pill.style.borderLeftColor = BorderColor;

            var iconElement = IconElement.Create(icon, 13f);
            iconElement.style.color = color;
            iconElement.style.marginRight = 5;
            pill.Add(iconElement);

            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = color;
            pill.Add(label);

            return pill;
        }

        /// <summary>Updates the text label inside a HUD pill (first Label descendant).</summary>
        private static void SetPillText(VisualElement pill, string text)
        {
            var label = pill?.Q<Label>();
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void AddLegendItem(VisualElement parent, Color color, string text)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 14;

            var dot = new VisualElement();
            dot.style.width = 8;
            dot.style.height = 8;
            dot.style.backgroundColor = color;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight = 5;
            item.Add(dot);

            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = TextMuted;
            item.Add(label);

            parent.Add(item);
        }

        private static void StyleZoomButton(Button button)
        {
            button.AddToClassList("circuit-zoom");
            button.style.marginLeft = 4;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.paddingTop = 2;
            button.style.paddingBottom = 2;
            button.style.fontSize = 11;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
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
            t.style.color = TextMuted;
            row.Add(t);

            var v = new Label(val);
            v.style.fontSize = 11;
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            v.style.unityTextAlign = TextAnchor.UpperRight;
            v.style.whiteSpace = WhiteSpace.Normal;
            v.style.flexGrow = 1;
            v.style.flexShrink = 1;
            v.style.color = TextPrimary;
            row.Add(v);

            parent.Add(row);
            return v;
        }
    }
}
