using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Audio;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Controls;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Bases
{
    /// <summary>
    /// UI Toolkit screen controller for Strategic Base Management (ScreenId.BaseManagement = 11),
    /// restyled as a green-phosphor "Radar Console" (GDD §6.1, §14.1, UI redesign ADR).
    /// Layout: radar scope with real-world region blips + region detail card (photo, NASA-sourced
    /// Turkish description with citation) on the left; facility telemetry ledger + construction
    /// "order sheet" on the right; shared icon nav bar at the bottom.
    /// </summary>
    public sealed class BaseManagementScreenController : ScreenControllerBase<BaseManagementViewModel>
    {
        public override ScreenId ScreenId => ScreenId.BaseManagement;

        // Palette mirrors the .theme-radar USS tokens (kept as C# constants for inline styles).
        private static readonly Color CanvasColor = new(0.012f, 0.043f, 0.027f, 1f);
        private static readonly Color PanelColor = new(0.020f, 0.078f, 0.035f, 0.98f);
        private static readonly Color Phosphor = new(0.208f, 0.878f, 0.549f, 1f);
        private static readonly Color PhosphorBright = new(0.435f, 1f, 0.69f, 1f);
        private static readonly Color TextPrimary = new(0.851f, 1f, 0.914f, 1f);
        private static readonly Color TextSecondary = new(0.612f, 0.812f, 0.698f, 1f);
        private static readonly Color TextMuted = new(0.369f, 0.541f, 0.447f, 1f);
        private static readonly Color BorderColor = new(0.208f, 0.878f, 0.549f, 0.40f);
        private static readonly Color BorderSubtle = new(0.208f, 0.878f, 0.549f, 0.14f);
        private static readonly Color Amber = new(1f, 0.706f, 0.329f, 1f);
        private static readonly Color RowColor = new(0.031f, 0.125f, 0.075f, 0.85f);
        private static readonly Color RowSelectedColor = new(0.071f, 0.227f, 0.137f, 0.95f);

        // Header pills
        private Label _budgetLabel;
        private Label _upkeepLabel;
        private Label _countLabel;

        // Radar + detail card
        private RadarScopeView _radar;
        private VisualElement _detailPhoto;
        private Label _detailRegionName;
        private Label _detailSiteName;
        private Label _detailDescription;
        private Label _detailSource;
        private VisualElement _detailFacilities;

        // Telemetry ledger (right, inside the unified right-column scroll)
        private VisualElement _facilitiesList;

        // Construction order sheet (right)
        private VisualElement _regionOptionsContainer;
        private VisualElement _typeOptionsContainer;
        private Label _costPreviewLabel;
        private ActionButton _buildButton;
        private readonly Dictionary<string, VisualElement> _regionButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BaseType, ActionButton> _typeButtons = new();

        private ScreenNavBar _navBar;

        // SFX: her başarılı satın alımda (tesis inşası / yükseltme) para kasası sesi.
        private readonly IAudioService _audioService;

        // Region content catalogs (loaded lazily, test-harness safe)
        private readonly Dictionary<string, RegionRecord> _regionsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RegionDescriptionRecord> _descriptionsByRegion = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<RegionMediaRecord>> _mediaByRegion = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _photoTexCache = new();
        private bool _contentLoaded;

        private static readonly BaseType[] AllBaseTypes =
        {
            BaseType.LaunchPad, BaseType.Factory, BaseType.ResearchCenter, BaseType.GroundStation, BaseType.TestFacility
        };

        public BaseManagementScreenController(BaseManagementViewModel viewModel, IAudioService audioService = null)
            : base(viewModel)
        {
            _audioService = audioService
                ?? (UnityEngine.Object.FindObjectOfType<Apogee.UI.Audio.UnityAudioService>() as IAudioService)
                ?? new NullAudioService();
        }

        // ───────────────────────────────────────────────────────────── layout

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-base-management");
            root.AddToClassList("theme-radar"); // Radar console identity: green phosphor tokens
            root.style.backgroundColor = CanvasColor;
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildHeader());
            root.Add(BuildBody());

            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.BaseManagement);
            root.Add(_navBar);

            return root;
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("radar-console-header");
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

            var back = new VisualElement();
            back.style.flexDirection = FlexDirection.Row;
            back.style.alignItems = Align.Center;
            back.style.paddingLeft = 8;
            back.style.paddingRight = 10;
            back.style.paddingTop = 4;
            back.style.paddingBottom = 4;
            back.style.marginRight = 14;
            back.style.borderTopLeftRadius = 4;
            back.style.borderTopRightRadius = 4;
            back.style.borderBottomLeftRadius = 4;
            back.style.borderBottomRightRadius = 4;
            back.style.backgroundColor = RowColor;
            back.style.borderLeftWidth = 2;
            back.style.borderLeftColor = Phosphor;
            back.Add(IconElement.Create(IconType.Back, 14f));
            var backLabel = new Label("Harita");
            backLabel.style.fontSize = 11;
            backLabel.style.color = TextSecondary;
            backLabel.style.marginLeft = 6;
            back.Add(backLabel);
            back.AddManipulator(new TooltipManipulator(
                new TooltipData("Kampanya Haritası'na dön", "Radar konsolunu kapatıp strateji haritasına geri döner.")));
            back.RegisterCallback<ClickEvent>(_ => ViewModel?.BackCommand.Execute());
            left.Add(back);

            var icon = IconElement.Create(IconType.Bases, 18f);
            icon.style.color = PhosphorBright;
            left.Add(icon);

            var title = new Label("ÜS TAKİBİ · RADAR KONSOLU");
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            title.style.marginLeft = 8;
            left.Add(title);

            var subtitle = new Label("Dünya tesis ağı canlı tarama");
            subtitle.style.fontSize = 10;
            subtitle.style.color = TextMuted;
            subtitle.style.marginLeft = 10;
            left.Add(subtitle);
            header.Add(left);

            var right = new VisualElement();
            right.style.flexDirection = FlexDirection.Row;
            right.style.alignItems = Align.Center;

            var pillBudget = CreatePill(IconType.Budget, "0 M$", PhosphorBright, out _budgetLabel);
            var pillUpkeep = CreatePill(IconType.Time, "Bakım: 0 M$/Ay", Amber, out _upkeepLabel);
            var pillCount = CreatePill(IconType.Bases, "0 tesis", TextSecondary, out _countLabel);
            pillBudget.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                "Bütçe (M$)",
                "Ajansın kasası — milyon dolar cinsinden. Üs inşaatı ve seviye yükseltmeleri bu kasadan ödenir; yıllık tahsisat itibarınızla orantılıdır.")));
            pillUpkeep.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                "Aylık Bakım Maliyeti",
                "Tüm tesislerinizin toplam aylık işletme gideri. Bu tutar her ay kasadan düşülür; yeni tesisler bu yükü artırır.")));
            pillCount.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                "Tesis Sayısı",
                "Ajansınızın işletmedeki üs ve tesis toplamı: ramplar, fabrikalar, araştırma merkezleri ve izleme istasyonları.")));
            right.Add(pillBudget);
            right.Add(pillUpkeep);
            right.Add(pillCount);
            header.Add(right);

            return header;
        }

        private VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("screen-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.flexBasis = 0;
            body.style.minHeight = 0;

            // ── Left: radar scope + region detail card ──
            var leftCol = new VisualElement();
            leftCol.style.flexGrow = 1;
            leftCol.style.paddingLeft = 14;
            leftCol.style.paddingRight = 8;
            leftCol.style.paddingTop = 12;
            leftCol.style.paddingBottom = 10;
            leftCol.style.minWidth = 0;

            var scopeFrame = new VisualElement();
            scopeFrame.style.flexGrow = 1;
            scopeFrame.style.borderTopLeftRadius = 8;
            scopeFrame.style.borderTopRightRadius = 8;
            scopeFrame.style.borderBottomLeftRadius = 8;
            scopeFrame.style.borderBottomRightRadius = 8;
            scopeFrame.style.borderBottomWidth = 2;
            scopeFrame.style.borderBottomColor = BorderSubtle;
            scopeFrame.style.borderTopWidth = 2;
            scopeFrame.style.borderTopColor = BorderSubtle;
            scopeFrame.style.overflow = Overflow.Hidden;

            var scopeHeader = new VisualElement();
            scopeHeader.style.flexDirection = FlexDirection.Row;
            scopeHeader.style.justifyContent = Justify.SpaceBetween;
            scopeHeader.style.paddingLeft = 12;
            scopeHeader.style.paddingRight = 12;
            scopeHeader.style.paddingTop = 6;
            var scopeTitle = new Label("DÜNYA TARANMASI");
            scopeTitle.style.fontSize = 10;
            scopeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            scopeTitle.style.color = Phosphor;
            scopeHeader.Add(scopeTitle);
            var scopeHint = new Label("Blip üzerine gel · detay için tıkla");
            scopeHint.style.fontSize = 10;
            scopeHint.style.color = TextMuted;
            scopeHeader.Add(scopeHint);
            scopeFrame.Add(scopeHeader);

            _radar = new RadarScopeView();
            _radar.style.flexGrow = 1;
            _radar.RegionClicked += OnRegionBlipClicked;
            scopeFrame.Add(_radar);
            leftCol.Add(scopeFrame);

            leftCol.Add(BuildDetailCard());
            body.Add(leftCol);

            // ── Right: telemetry ledger + construction order sheet ──
            var rightCol = new VisualElement();
            rightCol.style.width = 372;
            rightCol.style.flexShrink = 0;
            rightCol.style.backgroundColor = PanelColor;
            rightCol.style.borderLeftWidth = 1;
            rightCol.style.borderLeftColor = BorderColor;
            rightCol.style.paddingLeft = 14;
            rightCol.style.paddingRight = 14;
            rightCol.style.paddingTop = 12;
            rightCol.style.paddingBottom = 10;
            rightCol.style.minHeight = 0;

            var ledgerHeader = new VisualElement();
            ledgerHeader.style.flexDirection = FlexDirection.Row;
            ledgerHeader.style.alignItems = Align.Center;
            ledgerHeader.style.marginBottom = 8;
            var ledgerIcon = IconElement.Create(IconType.Contracts, 14f);
            ledgerIcon.style.color = PhosphorBright;
            ledgerHeader.Add(ledgerIcon);
            var ledgerTitle = new Label("TELEMETRİ DEFTERİ");
            ledgerTitle.style.fontSize = 11;
            ledgerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            ledgerTitle.style.color = PhosphorBright;
            ledgerTitle.style.marginLeft = 6;
            ledgerHeader.Add(ledgerTitle);

            // ── Sağ panel tek ScrollVİew içinde: defter + sipariş fişi tek belge
            //    gibi kayar. Pencere alanı yetmediğinde fiş içeriği (bölge düğmeleri,
            //    tesis türleri, maliyet önizlemesi, İnşa Et) eski haliyle sıkışı
            //    üst üste binmiyordu; artık dikey kaydırma devreye girer.
            var rightScroll = new ScrollView(ScrollViewMode.Vertical);
            rightScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rightScroll.style.flexGrow = 1;
            rightScroll.style.flexShrink = 1;
            rightScroll.style.minHeight = 0;
            rightCol.Add(rightScroll);

            rightScroll.Add(ledgerHeader);

            _facilitiesList = new VisualElement();
            rightScroll.Add(_facilitiesList);

            rightScroll.Add(BuildOrderSheet());
            body.Add(rightCol);

            return body;
        }

        private VisualElement BuildDetailCard()
        {
            var card = new VisualElement();
            card.AddToClassList("radar-detail-card");
            card.style.flexDirection = FlexDirection.Row;
            // Sabit yükseklik yerine min/max aralık: içerik (bölge ansiklopedisi +
            // tesis satırları) 236px'e sığmadığında satırlar birbirinin üstüne
            // basıyordu. Kart artık içeriğe göre büyür, tesis listesi kendi içinde
            // dikey kayar; taşma durumunda kart kenarında güvenli kırpma uygulanır.
            card.style.minHeight = 236;
            card.style.maxHeight = 560;
            card.style.overflow = Overflow.Hidden;
            card.style.flexShrink = 0;
            card.style.marginTop = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;

            // Photo panel
            _detailPhoto = new VisualElement();
            _detailPhoto.style.width = 148;
            _detailPhoto.style.borderTopLeftRadius = 4;
            _detailPhoto.style.borderTopRightRadius = 4;
            _detailPhoto.style.borderBottomLeftRadius = 4;
            _detailPhoto.style.borderBottomRightRadius = 4;
            _detailPhoto.style.overflow = Overflow.Hidden;
            _detailPhoto.style.backgroundColor = new Color(0.008f, 0.047f, 0.027f, 1f);
            _detailPhoto.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            _detailPhoto.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _detailPhoto.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _detailPhoto.style.flexShrink = 0;
            card.Add(_detailPhoto);

            // Info column
            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.style.marginLeft = 12;
            info.style.minWidth = 0;

            _detailRegionName = new Label("BÖLGE SEÇİLMEDİ");
            _detailRegionName.style.fontSize = 14;
            _detailRegionName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailRegionName.style.color = PhosphorBright;
            _detailRegionName.style.flexShrink = 0;
            info.Add(_detailRegionName);

            _detailSiteName = new Label("Radar kapsülünden bir blip seçin.");
            _detailSiteName.style.fontSize = 11;
            _detailSiteName.style.color = TextSecondary;
            _detailSiteName.style.marginBottom = 4;
            _detailSiteName.style.flexShrink = 0;
            info.Add(_detailSiteName);

            _detailDescription = new Label(string.Empty);
            _detailDescription.style.fontSize = 11;
            _detailDescription.style.color = TextPrimary;
            _detailDescription.style.whiteSpace = WhiteSpace.Normal;
            _detailDescription.style.flexShrink = 0;
            info.Add(_detailDescription);

            _detailSource = new Label(string.Empty);
            _detailSource.style.fontSize = 9;
            _detailSource.style.color = TextMuted;
            _detailSource.style.marginTop = 4;
            _detailSource.style.flexShrink = 0;
            info.Add(_detailSource);

            // Tesis satırları kendi içinde kayar: bölgedeki tesis sayısı artsa bile
            // satırlar kartın dışına taşıp alttaki yazıların üstüne binmez.
            var facilitiesScroll = new ScrollView(ScrollViewMode.Vertical);
            facilitiesScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            facilitiesScroll.style.flexGrow = 1;
            facilitiesScroll.style.flexShrink = 1;
            facilitiesScroll.style.minHeight = 48;
            facilitiesScroll.style.marginTop = 6;
            _detailFacilities = facilitiesScroll;
            info.Add(_detailFacilities);

            card.Add(info);
            return card;
        }

        private VisualElement BuildOrderSheet()
        {
            var sheet = new VisualElement();
            sheet.AddToClassList("radar-order-sheet");
            sheet.style.flexShrink = 0;
            sheet.style.marginTop = 10;
            sheet.style.paddingTop = 10;
            sheet.style.paddingLeft = 2;
            sheet.style.paddingRight = 2;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;
            var icon = IconElement.Create(IconType.Plus, 14f);
            icon.style.color = Amber;
            titleRow.Add(icon);
            var title = new Label("İNŞAAT SİPARİŞ FİŞİ");
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Amber;
            title.style.marginLeft = 6;
            titleRow.Add(title);
            sheet.Add(titleRow);

            var regionLabel = new Label("Bölge:");
            regionLabel.style.fontSize = 10;
            regionLabel.style.color = TextSecondary;
            regionLabel.style.marginBottom = 4;
            sheet.Add(regionLabel);

            _regionOptionsContainer = new VisualElement();
            _regionOptionsContainer.style.flexDirection = FlexDirection.Row;
            _regionOptionsContainer.style.flexWrap = Wrap.Wrap;
            _regionOptionsContainer.style.marginBottom = 8;
            sheet.Add(_regionOptionsContainer);

            var typeLabel = new Label("Tesis türü:");
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = TextSecondary;
            typeLabel.style.marginBottom = 4;
            sheet.Add(typeLabel);

            _typeOptionsContainer = new VisualElement();
            _typeOptionsContainer.style.marginBottom = 8;
            sheet.Add(_typeOptionsContainer);

            _costPreviewLabel = new Label("Maliyet önizleme: 0 M$");
            _costPreviewLabel.style.fontSize = 11;
            _costPreviewLabel.style.color = PhosphorBright;
            _costPreviewLabel.style.marginBottom = 8;
            sheet.Add(_costPreviewLabel);

            _buildButton = new ActionButton(
                IconType.Plus,
                "İnşa Et",
                () => ShowBuildConfirmation(),
                resultDescription: "Seçili bölgede seçilen tesis türünü inşa eder. Kampanya bütçesinden düşülür.")
            {
                style = { flexGrow = 1, height = 34 }
            };
            _buildButton.SetVariant("green");
            sheet.Add(_buildButton);

            return sheet;
        }

        // ───────────────────────────────────────────────────────────── lifecycle

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

            EnsureContentLoaded();
            RenderRegionButtons();
            RenderTypeButtons();
            RenderFacilities();
            RenderRadar();
            RenderDetailCard();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
                RefreshAffordanceStates();
            });

            ViewModel.TotalMonthlyMaintenance.Subscribe(up =>
            {
                if (_upkeepLabel != null) _upkeepLabel.text = $"Bakım: {up:F1} M$/Ay";
            });

            ViewModel.TotalBasesCount.Subscribe(c =>
            {
                if (_countLabel != null) _countLabel.text = $"{c} tesis";
                RenderRadar();
            });

            ViewModel.Facilities.Subscribe(_ =>
            {
                RenderFacilities();
                RenderDetailCard();
                RenderRadar();
                UpdateTypeButtonCounts();
                RefreshAffordanceStates();
            });

            ViewModel.SelectedBuildCost.Subscribe(cost =>
            {
                if (_costPreviewLabel != null) _costPreviewLabel.text = $"Maliyet önizleme: {cost:F1} M$";
                RefreshAffordanceStates();
            });

            ViewModel.AvailableRegions.Subscribe(_ =>
            {
                RenderRegionButtons();
                RenderRadar();
            });

            ViewModel.SelectedRegionId.Subscribe(_ =>
            {
                HighlightRegionButtons();
                RenderDetailCard();
                if (_radar != null) _radar.SetSelectedRegion(ViewModel.SelectedRegionId.Value);
                UpdateTypeButtonCounts();
                UpdateTypeButtonPermissions();
            });

            ViewModel.SelectedBaseType.Subscribe(_ => HighlightTypeButtons());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                _navBar?.SetStatus(msg ?? string.Empty);
            });
        }

        // ───────────────────────────────────────────────────────────── content catalogs

        /// <summary>
        /// Loads regions.json (radar coordinates — same pin source as the Campaign Map),
        /// region_descriptions.json (NASA-sourced Turkish encyclopedia entries) and
        /// region_media.json (detail card photos). All loaders are test-harness safe.
        /// </summary>
        private void EnsureContentLoaded()
        {
            if (_contentLoaded)
            {
                return;
            }

            _contentLoaded = true;

            var regionsJson = ReadCatalogJson("regions.json");
            if (!string.IsNullOrEmpty(regionsJson))
            {
                var catalog = CatalogLoader.Load<RegionRecord>(regionsJson);
                if (catalog != null && catalog.Validation.IsValid)
                {
                    foreach (var rec in catalog.All)
                    {
                        if (rec != null && !string.IsNullOrEmpty(rec.Id))
                        {
                            _regionsById[rec.Id] = rec;
                        }
                    }
                }
            }

            var descriptionsJson = ReadCatalogJson("region_descriptions.json");
            if (!string.IsNullOrEmpty(descriptionsJson))
            {
                var catalog = CatalogLoader.Load<RegionDescriptionRecord>(descriptionsJson);
                if (catalog != null && catalog.Validation.IsValid)
                {
                    foreach (var rec in catalog.All)
                    {
                        if (rec != null && !string.IsNullOrEmpty(rec.RegionId))
                        {
                            _descriptionsByRegion[rec.RegionId] = rec;
                        }
                    }
                }
            }

            var mediaJson = ReadCatalogJson("region_media.json");
            if (!string.IsNullOrEmpty(mediaJson))
            {
                var catalog = CatalogLoader.Load<RegionMediaRecord>(mediaJson);
                if (catalog != null && catalog.Validation.IsValid)
                {
                    foreach (var rec in catalog.All)
                    {
                        if (rec == null || string.IsNullOrEmpty(rec.RegionId))
                        {
                            continue;
                        }

                        if (!_mediaByRegion.TryGetValue(rec.RegionId, out var list))
                        {
                            list = new List<RegionMediaRecord>();
                            _mediaByRegion[rec.RegionId] = list;
                        }

                        list.Add(rec);
                    }

                    foreach (var list in _mediaByRegion.Values)
                    {
                        list.Sort((a, b) => a.Order.CompareTo(b.Order));
                    }
                }
            }
        }

        /// <summary>
        /// Mirrors CampaignMapViewModel's catalog read pattern: Resources first,
        /// then the on-disk catalog directory (non-Unity harness fallback).
        /// </summary>
        private static string ReadCatalogJson(string fileName)
        {
            string resourcesName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            try
            {
                var asset = Resources.Load<TextAsset>("Catalog/Json/" + resourcesName);
                if (asset != null && !string.IsNullOrEmpty(asset.text))
                {
                    return asset.text;
                }
            }
            catch
            {
                // Resources.Load may not be available in non-Unity test harness.
            }

            try
            {
                var path = System.IO.Path.Combine(Application.dataPath, "_Project", "Data", "Catalog", "Json", fileName);
                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllText(path);
                }
            }
            catch
            {
                // Filesystem unavailable in some harnesses; content simply stays empty.
            }

            return null;
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
                tex = null; // non-Unity harness / missing asset — detail card renders without photo
            }

            if (tex != null)
            {
                _photoTexCache[resourcesPath] = tex;
            }

            return tex;
        }

        // ───────────────────────────────────────────────────────────── radar

        private void RenderRadar()
        {
            if (_radar == null || ViewModel == null)
            {
                return;
            }

            var blips = new List<RadarScopeView.Blip>();
            var facilities = ViewModel.Facilities.Value ?? Array.Empty<BaseFacilityItemViewModel>();
            var countsByRegion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var facility in facilities)
            {
                if (string.IsNullOrEmpty(facility.RegionId))
                {
                    continue;
                }

                countsByRegion.TryGetValue(facility.RegionId, out int count);
                countsByRegion[facility.RegionId] = count + 1;
            }

            var regionIds = ViewModel.AvailableRegions.Value ?? Array.Empty<string>();
            foreach (var regionId in regionIds)
            {
                var blip = new RadarScopeView.Blip
                {
                    RegionId = regionId,
                    Label = FormatRegionName(regionId),
                    NormalizedX = 50f,
                    NormalizedY = 50f,
                    HasFacility = countsByRegion.TryGetValue(regionId, out int facilityCount) && facilityCount > 0,
                    FacilityCount = countsByRegion.TryGetValue(regionId, out int fc) ? fc : 0,
                    IsSelected = string.Equals(regionId, ViewModel.SelectedRegionId.Value, StringComparison.OrdinalIgnoreCase)
                };

                if (_regionsById.TryGetValue(regionId, out var region))
                {
                    blip.NormalizedX = (float)((region.Longitude + 180.0) / 360.0 * 100.0);
                    blip.NormalizedY = (float)((90.0 - region.Latitude) / 180.0 * 100.0);
                }

                if (_descriptionsByRegion.TryGetValue(regionId, out var description))
                {
                    blip.SiteName = description.SiteName;
                    blip.ShortDescription = description.ShortDescription;
                }

                blips.Add(blip);
            }

            _radar.SetBlips(blips);
            _radar.SetSelectedRegion(ViewModel.SelectedRegionId.Value);
        }

        private void OnRegionBlipClicked(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return;
            }

            ViewModel?.SetRegionForConstruction(regionId);
        }

        // ───────────────────────────────────────────────────────────── detail card

        private void RenderDetailCard()
        {
            if (_detailRegionName == null || ViewModel == null)
            {
                return;
            }

            string regionId = ViewModel.SelectedRegionId.Value;
            if (string.IsNullOrEmpty(regionId))
            {
                _detailRegionName.text = "BÖLGE SEÇİLMEDİ";
                _detailSiteName.text = "Radar kapsülünden bir blip seçin.";
                _detailDescription.text = string.Empty;
                _detailSource.text = string.Empty;
                _detailPhoto.style.backgroundImage = null;
                _detailFacilities.Clear();
                return;
            }

            string regionName = FormatRegionName(regionId);
            _detailRegionName.text = regionName.ToUpperInvariant();

            string coords = string.Empty;
            if (_regionsById.TryGetValue(regionId, out var region))
            {
                coords = $" · {region.Latitude:F1}°, {region.Longitude:F1}°";
            }

            if (_descriptionsByRegion.TryGetValue(regionId, out var desc))
            {
                _detailSiteName.text = desc.SiteName + coords;
                _detailDescription.text = desc.FullDescription;
                _detailSource.text = string.IsNullOrEmpty(desc.Source)
                    ? string.Empty
                    : $"Kaynak: {desc.Source}";
            }
            else
            {
                _detailSiteName.text = "Stratejik bölge" + coords;
                _detailDescription.text = "Bu bölge için ansiklopedi kaydı bulunmuyor.";
                _detailSource.text = string.Empty;
            }

            // Region photo (region_media.json → cached Resources texture).
            var tex = GetRegionPhotoTexture(regionId);
            if (tex != null)
            {
                _detailPhoto.style.backgroundImage = new StyleBackground(tex);
            }
            else
            {
                _detailPhoto.style.backgroundImage = null;
            }

            // Facility rows with level/capacity/maintenance bars + upgrade actions.
            _detailFacilities.Clear();
            var regionFacilities = (ViewModel.Facilities.Value ?? Array.Empty<BaseFacilityItemViewModel>())
                .Where(f => string.Equals(f.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (regionFacilities.Count == 0)
            {
                var empty = new Label("Bu bölgede tesis yok — sipariş fişinden inşa edin.");
                empty.style.fontSize = 10;
                empty.style.color = TextMuted;
                _detailFacilities.Add(empty);
                return;
            }

            foreach (var facility in regionFacilities)
            {
                _detailFacilities.Add(CreateDetailFacilityRow(facility));
            }
        }

        private Texture2D GetRegionPhotoTexture(string regionId)
        {
            if (!_mediaByRegion.TryGetValue(regionId, out var records) || records.Count == 0)
            {
                return null;
            }

            // Prefer the representative photo when marked, else the first record.
            var chosen = records.FirstOrDefault(r => r.IsRepresentative) ?? records[0];
            return GetPhotoTexture(chosen.Path);
        }

        private VisualElement CreateDetailFacilityRow(BaseFacilityItemViewModel facility)
        {
            var row = new VisualElement();
            row.AddToClassList("radar-ledger-row");
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.marginBottom = 4;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;

            var topLine = new VisualElement();
            topLine.style.flexDirection = FlexDirection.Row;
            topLine.style.justifyContent = Justify.SpaceBetween;
            topLine.style.alignItems = Align.Center;

            var name = new Label($"{facility.TypeName} · Sv {facility.Level}/{facility.MaxLevel}");
            name.style.fontSize = 11;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = TextPrimary;
            topLine.Add(name);

            var upkeep = new Label($"{facility.MonthlyMaintenanceCost:F1} M$/ay");
            upkeep.style.fontSize = 10;
            upkeep.style.color = Amber;
            topLine.Add(upkeep);
            row.Add(topLine);

            // Level bar (level progress against max level).
            row.Add(CreateBarRow("Seviye", facility.Level / (float)Math.Max(1, facility.MaxLevel), Phosphor));
            row.Add(CreateBarRow("Kapasite", Mathf.Clamp01((float)(facility.CapacityRating / 100.0)), PhosphorBright));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 2;

            if (facility.CanUpgrade)
            {
                bool affordable = ViewModel.Budget.Value >= facility.NextUpgradeCost;
                var upgrade = new ActionButton(
                    IconType.Approve,
                    "Yükselt",
                    () => ShowUpgradeConfirmation(facility),
                    resultDescription: $"{facility.TypeName} seviyesi {facility.Level + 1}. seviyeye çıkar. Kapasite ve bakım maliyeti artar.")
                {
                    style = { height = 22 }
                };
                upgrade.SetPrice($"{facility.NextUpgradeCost:F1} M$");
                upgrade.SetAvailable(affordable, affordable
                    ? null
                    : $"Bütçe yetersiz — {facility.NextUpgradeCost:F1} M$ gerekli, kasa {ViewModel.Budget.Value:F1} M$.");
                actions.Add(upgrade);
            }
            else
            {
                var max = new Label("MAKS SEVİYE");
                max.style.fontSize = 9;
                max.style.unityFontStyleAndWeight = FontStyle.Bold;
                max.style.color = Phosphor;
                actions.Add(max);
            }

            row.Add(actions);
            return row;
        }

        private VisualElement CreateBarRow(string label, float fill01, Color fillColor)
        {
            var barRow = new VisualElement();
            barRow.style.flexDirection = FlexDirection.Row;
            barRow.style.alignItems = Align.Center;
            barRow.style.marginTop = 2;

            var labelElement = new Label(label);
            labelElement.style.fontSize = 9;
            labelElement.style.color = TextMuted;
            labelElement.style.width = 52;
            labelElement.style.flexShrink = 0;
            barRow.Add(labelElement);

            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 5;
            track.style.backgroundColor = BorderSubtle;
            track.style.borderTopLeftRadius = 3;
            track.style.borderTopRightRadius = 3;
            track.style.borderBottomLeftRadius = 3;
            track.style.borderBottomRightRadius = 3;
            barRow.Add(track);

            var fill = new VisualElement();
            fill.style.height = 5;
            fill.style.width = Length.Percent(Mathf.Clamp01(fill01) * 100f);
            fill.style.backgroundColor = fillColor;
            fill.style.borderTopLeftRadius = 3;
            fill.style.borderTopRightRadius = 3;
            fill.style.borderBottomLeftRadius = 3;
            fill.style.borderBottomRightRadius = 3;
            track.Add(fill);

            return barRow;
        }

        // ───────────────────────────────────────────────────────────── telemetry ledger

        private void RenderFacilities()
        {
            if (_facilitiesList == null || ViewModel == null)
            {
                return;
            }

            _facilitiesList.Clear();

            var facilities = ViewModel.Facilities.Value;
            if (facilities == null || facilities.Count == 0)
            {
                var empty = new Label("Radar temiz — henüz kurulmuş bir tesis yok.");
                empty.style.fontSize = 11;
                empty.style.color = TextMuted;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _facilitiesList.Add(empty);
                return;
            }

            double maxUpkeep = facilities.Max(f => f.MonthlyMaintenanceCost);
            if (maxUpkeep <= 0.001)
            {
                maxUpkeep = 1.0;
            }

            foreach (var facility in facilities)
            {
                _facilitiesList.Add(CreateLedgerRow(facility, maxUpkeep));
            }
        }

        private VisualElement CreateLedgerRow(BaseFacilityItemViewModel facility, double maxUpkeep)
        {
            bool selected = ViewModel.SelectedFacility.Value != null &&
                            string.Equals(ViewModel.SelectedFacility.Value.Id, facility.Id, StringComparison.OrdinalIgnoreCase);

            var row = new VisualElement();
            row.AddToClassList(selected ? "radar-ledger-row--selected" : "radar-ledger-row");
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.marginBottom = 5;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;

            var title = new Label($"{facility.TypeName} · {FormatRegionName(facility.RegionId)}");
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            row.Add(title);

            var meta = new Label($"Sv {facility.Level}/{facility.MaxLevel} · Kapasite {facility.CapacityRating:F0}");
            meta.style.fontSize = 10;
            meta.style.color = TextSecondary;
            row.Add(meta);

            row.Add(CreateBarRow("Kapasite", Mathf.Clamp01((float)(facility.CapacityRating / 100.0)), PhosphorBright));
            row.Add(CreateBarRow("Bakım", Mathf.Clamp01((float)(facility.MonthlyMaintenanceCost / maxUpkeep)), Amber));

            var upkeep = new Label($"Bakım yükü: {facility.MonthlyMaintenanceCost:F1} M$/ay");
            upkeep.style.fontSize = 9;
            upkeep.style.color = Amber;
            upkeep.style.marginTop = 2;
            row.Add(upkeep);

            row.AddManipulator(new TooltipManipulator(
                new TooltipData(
                    $"{facility.TypeName} ({FormatRegionName(facility.RegionId)})",
                    $"Seviye {facility.Level}/{facility.MaxLevel} · Kapasite {facility.CapacityRating:F0} · Bakım {facility.MonthlyMaintenanceCost:F1} M$/ay",
                    "Tıklayınca radar bu bölgeye kilitlenir ve inşa fişi o bölgeye ayarlanır.")));

            row.RegisterCallback<ClickEvent>(_ =>
            {
                ViewModel.SelectFacility(facility.Id);
                ViewModel.SetRegionForConstruction(facility.RegionId);
                HighlightLedgerSelection();
            });

            return row;
        }

        private void HighlightLedgerSelection()
        {
            if (_facilitiesList == null || ViewModel == null)
            {
                return;
            }

            string selectedId = ViewModel.SelectedFacility.Value?.Id;
            int index = 0;
            var facilities = ViewModel.Facilities.Value ?? Array.Empty<BaseFacilityItemViewModel>();
            foreach (var child in _facilitiesList.Children())
            {
                bool isSelected = index < facilities.Count &&
                                  string.Equals(facilities[index].Id, selectedId, StringComparison.OrdinalIgnoreCase);
                child.AddToClassList(isSelected ? "radar-ledger-row--selected" : "radar-ledger-row");
                child.RemoveFromClassList(isSelected ? "radar-ledger-row" : "radar-ledger-row--selected");
                index++;
            }
        }

        // ───────────────────────────────────────────────────────────── order sheet

        private void RenderRegionButtons()
        {
            if (_regionOptionsContainer == null || ViewModel == null)
            {
                return;
            }

            _regionOptionsContainer.Clear();
            _regionButtons.Clear();

            var regions = ViewModel.AvailableRegions.Value;
            if (regions == null)
            {
                return;
            }

            foreach (var reg in regions)
            {
                string regionId = reg;
                var btn = new VisualElement();
                btn.style.flexDirection = FlexDirection.Row;
                btn.style.alignItems = Align.Center;
                // Sabit genişlik: seçili bölge Kalın (bold) yazıya dönünce etiket
                // genişler ve wrap ızgarası yeniden akarak düğmeler farklı
                // sıralardaymış gibi görünüyordu. Sabit genişlik + kırpma ile
                // düğme boyutları (ve dolayısıyla grid sıralaması) hiç değişmez.
                btn.style.width = 108;
                btn.style.paddingLeft = 6;
                btn.style.paddingRight = 6;
                btn.style.paddingTop = 3;
                btn.style.paddingBottom = 3;
                btn.style.marginRight = 4;
                btn.style.marginBottom = 4;
                btn.style.borderTopLeftRadius = 3;
                btn.style.borderTopRightRadius = 3;
                btn.style.borderBottomLeftRadius = 3;
                btn.style.borderBottomRightRadius = 3;
                btn.style.backgroundColor = RowColor;
                btn.style.borderLeftWidth = 2;
                btn.style.borderLeftColor = BorderSubtle;
                btn.style.overflow = Overflow.Hidden;

                var label = new Label(FormatRegionName(regionId));
                label.style.fontSize = 10;
                label.style.color = TextSecondary;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                btn.Add(label);

                string tooltipSite = _descriptionsByRegion.TryGetValue(regionId, out var d) ? d.SiteName : "Bilinmeyen bölge";
                btn.AddManipulator(new TooltipManipulator(
                    new TooltipData(
                        FormatRegionName(regionId),
                        $"İnşa hedefi: {tooltipSite}.",
                        "Tıklayınca sipariş fişi bu bölgeye ayarlanır.")));

                btn.RegisterCallback<ClickEvent>(_ =>
                {
                    ViewModel.SetRegionForConstruction(regionId);
                });

                _regionButtons[regionId] = btn;
                _regionOptionsContainer.Add(btn);
            }

            HighlightRegionButtons();
        }

        private void HighlightRegionButtons()
        {
            if (ViewModel == null)
            {
                return;
            }

            string selected = ViewModel.SelectedRegionId.Value ?? string.Empty;
            foreach (var kvp in _regionButtons)
            {
                bool isSelected = string.Equals(kvp.Key, selected, StringComparison.OrdinalIgnoreCase);
                kvp.Value.style.backgroundColor = isSelected ? RowSelectedColor : RowColor;
                kvp.Value.style.borderLeftColor = isSelected ? Amber : BorderSubtle;
                var label = kvp.Value.Q<Label>();
                if (label != null)
                {
                    label.style.color = isSelected ? TextPrimary : TextSecondary;
                    label.style.unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        private void RenderTypeButtons()
        {
            if (_typeOptionsContainer == null || ViewModel == null)
            {
                return;
            }

            _typeOptionsContainer.Clear();
            _typeButtons.Clear();

            foreach (var type in AllBaseTypes)
            {
                var btn = new ActionButton(
                    MapBaseTypeIcon(type),
                    BaseFacilityItemViewModel.GetBaseTypeName(type),
                    () => ViewModel.SetBaseTypeForConstruction(type),
                    resultDescription: "Bu tesis türünü sipariş fişine yazar; maliyet önizlemesi güncellenir.")
                {
                    style = { height = 24, marginBottom = 4 }
                };
                btn.style.flexDirection = FlexDirection.Row;
                btn.style.justifyContent = Justify.FlexStart;
                _typeButtons[type] = btn;
                _typeOptionsContainer.Add(btn);
            }

            UpdateTypeButtonCounts();
            UpdateTypeButtonPermissions();
            HighlightTypeButtons();
        }

        private void UpdateTypeButtonCounts()
        {
            if (ViewModel == null)
            {
                return;
            }

            string region = ViewModel.SelectedRegionId.Value;
            foreach (var kvp in _typeButtons)
            {
                int count = ViewModel.GetFacilityCount(region, kvp.Key);
                kvp.Value.SetLabel($"{BaseFacilityItemViewModel.GetBaseTypeName(kvp.Key)} ({count})");
            }
        }

        private void UpdateTypeButtonPermissions()
        {
            if (ViewModel == null)
            {
                return;
            }

            string region = ViewModel.SelectedRegionId.Value;
            var allowed = GetAllowedBaseTypes(region);
            foreach (var kvp in _typeButtons)
            {
                bool allowedHere = allowed == null || allowed.Contains(MapBaseTypeJsonName(kvp.Key));
                kvp.Value.SetAvailable(allowedHere, allowedHere ? null : "Bu tesis türü seçili bölgede izinli değil.");
            }
        }

        private void HighlightTypeButtons()
        {
            if (ViewModel == null)
            {
                return;
            }

            BaseType selected = ViewModel.SelectedBaseType.Value;
            foreach (var kvp in _typeButtons)
            {
                bool isSelected = kvp.Key == selected;
                kvp.Value.SetVariant(isSelected ? "primary" : null);
            }
        }

        /// <summary>allowedBaseTypes from regions.json for the given region; null = unknown/unrestricted.</summary>
        private List<string> GetAllowedBaseTypes(string regionId)
        {
            if (!string.IsNullOrEmpty(regionId) && _regionsById.TryGetValue(regionId, out var region))
            {
                return region.AllowedBaseTypes;
            }

            return null;
        }

        private static string MapBaseTypeJsonName(BaseType type) => type switch
        {
            BaseType.LaunchPad => "launch_pad",
            BaseType.Factory => "factory",
            BaseType.ResearchCenter => "research_center",
            BaseType.GroundStation => "ground_station",
            BaseType.TestFacility => "test_facility",
            _ => type.ToString().ToLowerInvariant()
        };

        private static IconType MapBaseTypeIcon(BaseType type) => type switch
        {
            BaseType.LaunchPad => IconType.Bases,
            BaseType.Factory => IconType.Production,
            BaseType.ResearchCenter => IconType.Research,
            BaseType.GroundStation => IconType.SolarSystem,
            BaseType.TestFacility => IconType.Risk,
            _ => IconType.Bases
        };

        private void RefreshAffordanceStates()
        {
            if (ViewModel == null || _buildButton == null)
            {
                return;
            }

            double cost = ViewModel.SelectedBuildCost.Value;
            double budget = ViewModel.Budget.Value;
            _buildButton.SetPrice($"{cost:F1} M$");
            _buildButton.SetAvailable(cost <= budget, cost <= budget
                ? null
                : $"Bütçe yetersiz — {cost:F1} M$ gerekli, kasa {budget:F1} M$.");

            UpdateTypeButtonPermissions();
        }

        // ───────────────────────────────────────────────────────────── confirmations

        /// <summary>
        /// Irreversible spend → explicit ConfirmDialog with the outcome (interaction contract #2).
        /// </summary>
        private void ShowBuildConfirmation()
        {
            if (ViewModel == null)
            {
                return;
            }

            string regionName = FormatRegionName(ViewModel.SelectedRegionId.Value);
            string typeName = BaseFacilityItemViewModel.GetBaseTypeName(ViewModel.SelectedBaseType.Value);
            double cost = ViewModel.SelectedBuildCost.Value;

            ConfirmDialog.Show(
                RootElement,
                "Tesis inşa et",
                $"{regionName} bölgesinde bir {typeName} inşa edilecek. Kasadan {cost:F1} M$ düşülecek; bu işlem geri alınamaz.",
                $"İnşa Et · {cost:F1} M$",
                () =>
                {
                    if (ViewModel.BuildFacility(ViewModel.SelectedRegionId.Value, ViewModel.SelectedBaseType.Value))
                    {
                        _audioService?.PlaySfx(AudioEvent.PurchaseCashRegister);
                    }
                });
        }

        private void ShowUpgradeConfirmation(BaseFacilityItemViewModel facility)
        {
            if (ViewModel == null || facility == null)
            {
                return;
            }

            ConfirmDialog.Show(
                RootElement,
                "Tesis yükseltme",
                $"{FormatRegionName(facility.RegionId)} — {facility.TypeName}, {facility.Level + 1}. seviyeye yükseltilecek. " +
                $"Kasadan {facility.NextUpgradeCost:F1} M$ düşülecek ve aylık bakım artacak; bu işlem geri alınamaz.",
                $"Yükselt · {facility.NextUpgradeCost:F1} M$",
                () =>
                {
                    if (ViewModel.UpgradeFacility(facility.Id))
                    {
                        _audioService?.PlaySfx(AudioEvent.PurchaseCashRegister);
                    }
                });
        }

        // ───────────────────────────────────────────────────────────── helpers

        /// <summary>
        /// Region display names come from the single shared source of truth used by the
        /// Campaign Map screen (RegionMapItemViewModel), keyed by regions.json ids.
        /// </summary>
        private static string FormatRegionName(string regionId) =>
            CampaignMap.RegionMapItemViewModel.FormatRegionName(regionId);

        /// <summary>Header status pill: icon + tinted text. The text target is returned via <paramref name="textTarget"/>.</summary>
        private static VisualElement CreatePill(IconType icon, string text, Color color, out Label textTarget)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.marginLeft = 10;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 10;
            pill.style.paddingTop = 4;
            pill.style.paddingBottom = 4;
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            pill.style.backgroundColor = new Color(0.008f, 0.047f, 0.027f, 0.85f);
            pill.style.borderLeftWidth = 1;
            pill.style.borderLeftColor = BorderSubtle;

            var iconElement = IconElement.Create(icon, 13f);
            iconElement.style.color = color;
            pill.Add(iconElement);

            textTarget = new Label(text);
            textTarget.style.fontSize = 11;
            textTarget.style.unityFontStyleAndWeight = FontStyle.Bold;
            textTarget.style.color = color;
            textTarget.style.marginLeft = 6;
            pill.Add(textTarget);

            return pill;
        }
    }
}
