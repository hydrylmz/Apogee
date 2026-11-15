using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Strategy;
using Apogee.UI.Core.Controls;
using Apogee.UI.Core.Dialogs;
using Apogee.UI.Core.Icons;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Core.Vehicle;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Production
{
    /// <summary>
    /// "Montaj Salonu" — Hangar Floor identity for the Strategic Production and Testing
    /// Queue (ScreenId.ProductionQueue = 12; GDD §9.3, §14.1, ADR-064).
    /// Concrete-and-steel world identity via ".theme-hangar-floor":
    /// - Assembly stand (left): NASA work-log photo card, vehicle silhouette on a
    ///   Painter2D scaffold/crane backdrop (AssemblyStandView), work order details
    ///   with a status stamp and an industrial progress gauge.
    /// - Work order ledger (right): all jobs as stamped factory cards + quick dispatch
    ///   ActionButtons with icon + cost + duration.
    /// - Conveyor band (bottom): queued jobs as pallets with mini vehicle silhouettes.
    /// - Andon lamps in the header for factory / test facility occupancy.
    /// Irreversible cancels go through ConfirmDialog (interaction contract #2).
    /// All previous interactions are preserved: quick build/test, cancel, back
    /// (now via the shared ScreenNavBar), live budget/capacity/status bindings.
    /// </summary>
    public sealed class ProductionQueueScreenController : ScreenControllerBase<ProductionQueueViewModel>
    {
        public override ScreenId ScreenId => ScreenId.ProductionQueue;

        // NASA work-log photos (drop-in: Assets/Resources/Production/Photos/…jpg).
        // Missing files fall back to a placeholder.
        private const string PhotoResourceMain = "Production/Photos/vab_assembly_01";
        private const string PhotoResourceSecond = "Production/Photos/vab_assembly_02";

        private static readonly Dictionary<string, Texture2D> PhotoCache = new();

        private static readonly Color CanvasColor = new(0.090f, 0.098f, 0.118f);      // --color-bg-canvas
        private static readonly Color GaugeTrackColor = new(0.078f, 0.086f, 0.102f);  // --color-bg-canvas-dark
        private static readonly Color GaugeFillColor = new(0.961f, 0.722f, 0.290f);   // --floor-safety

        // Header
        private ScreenNavBar _navBar;
        private Label _budgetLabel;
        private Label _factoryLampText;
        private Label _testLampText;
        private VisualElement _factoryLampRow;
        private VisualElement _testLampRow;

        // Assembly stand (left column)
        private VisualElement _standEmptyLabel;
        private VisualElement _standContent;
        private VisualElement _photoFrame;
        private VisualElement _photoPreview;
        private Label _photoPlaceholder;
        private Label _photoCaptionLabel;
        private VisualElement _thumbMain;
        private VisualElement _thumbSecond;
        private string _activePhotoResource;
        private AssemblyStandView _standCanvas;
        private VehicleSilhouetteView _standSilhouette;
        private Label _workOrderCodeLabel;
        private Label _workOrderNameLabel;
        private Label _workOrderMetaLabel;
        private VisualElement _workOrderStamp;
        private VisualElement _gaugeFill;
        private ActionButton _cancelButton;

        // Work order ledger (right column)
        private ScrollView _ledgerScroll;

        // Conveyor band (bottom)
        private VisualElement _conveyorPallets;

        // Quick dispatch
        private ActionButton _quickBuildButton;
        private ActionButton _quickTestButton;

        // Footer
        private Label _statusLabel;

        private VisualElement _root;
        private string _selectedJobId;

        public ProductionQueueScreenController(ProductionQueueViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            _root = new VisualElement();
            _root.AddToClassList("screen-root");
            _root.AddToClassList("theme-hangar-floor");
            _root.style.backgroundColor = CanvasColor;
            _root.style.flexDirection = FlexDirection.Column;

            // ── 1. Header: title + budget + andon lamps ──────────────────────────
            _root.Add(BuildHeader());

            // ── 2. Body: assembly stand (left) + work order ledger (right) ───────
            var body = new VisualElement();
            body.AddToClassList("screen-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.flexShrink = 1;
            body.style.flexBasis = 0;
            body.style.minHeight = 0;

            body.Add(BuildAssemblyStand());
            body.Add(BuildLedgerColumn());

            _root.Add(body);

            // ── 3. Conveyor band (bottom strip) ──────────────────────────────────
            _root.Add(BuildConveyor());

            // ── 4. Footer: line status ───────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.paddingTop = 5;
            footer.style.paddingBottom = 5;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.239f, 0.267f, 0.302f, 0.7f);

            _statusLabel = new Label("HAT DURUMU: Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.561f, 0.722f, 0.847f);
            footer.Add(_statusLabel);
            _root.Add(footer);

            // ── 5. Shared icon nav bar — docked to the bottom like every panel ───
            _navBar = new ScreenNavBar(ViewModel.Navigator, ScreenId.ProductionQueue);
            _root.Add(_navBar);

            return _root;
        }

        // ── Builders ────────────────────────────────────────────────────────────

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.239f, 0.267f, 0.302f, 0.8f);

            var titleBox = new VisualElement();
            var title = new Label("MONTAJ SALONU");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.949f, 0.957f, 0.969f);
            titleBox.Add(title);

            var subtitle = new Label("Araç Montajı & Kalifikasyon Hattı · Üretim Dairesi");
            subtitle.style.fontSize = 9;
            subtitle.style.color = new Color(0.463f, 0.494f, 0.541f);
            titleBox.Add(subtitle);
            header.Add(titleBox);

            // Factory +on lamps
            header.Add(BuildAndonGroup("FABRİKA HATTI", out _factoryLampRow, out _factoryLampText));
            header.Add(BuildAndonGroup("TEST TESİSİ", out _testLampRow, out _testLampText));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            var budgetBadge = new VisualElement();
            budgetBadge.style.flexDirection = FlexDirection.Row;
            budgetBadge.style.alignItems = Align.Center;
            budgetBadge.AddManipulator(new TooltipManipulator(() => TooltipData.CreateSimple(
                "Bütçe (M$)",
                "Ajansın kasası — milyon dolar cinsinden. Üretim iş emirleri ve test programları bu kasadan ödenir; yıllık tahsisat itibarınızla orantılıdır.")));

            var budgetIcon = IconElement.Create(IconType.Budget, 14f);
            budgetIcon.style.color = new Color(0.20f, 0.75f, 0.95f); // --color-accent-cyan
            budgetIcon.style.marginRight = 6;
            budgetBadge.Add(budgetIcon);

            _budgetLabel = new Label("0.0 M$");
            _budgetLabel.AddToClassList("apogee-badge");
            _budgetLabel.AddToClassList("apogee-badge--cyan");
            _budgetLabel.style.fontSize = 12;
            _budgetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            budgetBadge.Add(_budgetLabel);
            header.Add(budgetBadge);

            return header;
        }

        private VisualElement BuildAndonGroup(string title, out VisualElement lampRow, out Label lampText)
        {
            var group = new VisualElement();
            group.style.flexDirection = FlexDirection.Row;
            group.style.alignItems = Align.Center;
            group.style.marginLeft = 22;

            var groupLabel = new Label(title);
            groupLabel.style.fontSize = 9;
            groupLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
            group.Add(groupLabel);

            lampRow = new VisualElement();
            lampRow.style.flexDirection = FlexDirection.Row;
            lampRow.style.alignItems = Align.Center;
            lampRow.style.marginLeft = 8;
            group.Add(lampRow);

            lampText = new Label("0 / 0");
            lampText.style.fontSize = 11;
            lampText.style.unityFontStyleAndWeight = FontStyle.Bold;
            lampText.style.color = new Color(0.706f, 0.738f, 0.780f);
            lampText.style.marginLeft = 8;
            group.Add(lampText);

            return group;
        }

        private VisualElement BuildAssemblyStand()
        {
            var column = new VisualElement();
            column.AddToClassList("floor-stand");
            column.style.width = Length.Percent(50);
            column.style.flexShrink = 0;
            column.style.paddingTop = 12;
            column.style.paddingBottom = 12;
            column.style.paddingLeft = 14;
            column.style.paddingRight = 14;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;

            var standTitle = new Label("MONTAJ STANDI");
            standTitle.style.fontSize = 12;
            standTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            standTitle.style.color = new Color(0.961f, 0.722f, 0.290f);
            headerRow.Add(standTitle);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            _workOrderCodeLabel = new Label("İŞ EMRİ #—");
            _workOrderCodeLabel.style.fontSize = 10;
            _workOrderCodeLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
            headerRow.Add(_workOrderCodeLabel);
            column.Add(headerRow);

            // Empty state (no job selected)
            _standEmptyLabel = new Label("HAT BOŞ — montaj standında araç yok.\nSağdaki defterden bir iş emri seçin veya hızlı emir verin.");
            _standEmptyLabel.style.fontSize = 11;
            _standEmptyLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
            _standEmptyLabel.style.whiteSpace = WhiteSpace.Normal;
            column.Add(_standEmptyLabel);

            // Selected job presentation. ScrollView kullanılır: foto kartı, iskele
            // tuvali ve detay kutusu doğal yüksekliklerini korur (flexShrink = 0);
            // pencere alanı yetmezse içerik sıkıştırılıp başlıkla yazılar üst üste
            // binmez, dikey kaydırma devreye girer.
            var standScroll = new ScrollView(ScrollViewMode.Vertical);
            standScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _standContent = standScroll;
            _standContent.style.flexGrow = 1;
            _standContent.style.display = DisplayStyle.None;
            column.Add(_standContent);

            // ── NASA work-log photo card (VehicleAssembly jobs only) ────────────
            _photoFrame = new VisualElement();
            _photoFrame.AddToClassList("floor-photo-frame");
            _photoFrame.style.flexShrink = 0;
            _photoFrame.style.marginBottom = 8;

            _photoPreview = new VisualElement();
            _photoPreview.style.height = 230;
            _photoPreview.style.backgroundColor = new Color(0.078f, 0.086f, 0.102f);
            _photoPreview.style.overflow = Overflow.Hidden;
            _photoPreview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _photoPreview.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _photoPreview.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _photoFrame.Add(_photoPreview);

            _photoPlaceholder = new Label("NASA FOTOĞRAFI BEKLENİYOR\n(Resources/Production/Photos/vab_assembly_01.jpg)");
            _photoPlaceholder.style.fontSize = 9;
            _photoPlaceholder.style.color = new Color(0.463f, 0.494f, 0.541f);
            _photoPlaceholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            _photoPlaceholder.style.position = Position.Absolute;
            _photoPlaceholder.style.left = 0;
            _photoPlaceholder.style.right = 0;
            _photoPlaceholder.style.top = 0;
            _photoPlaceholder.style.bottom = 0;
            _photoPlaceholder.style.whiteSpace = WhiteSpace.Normal;
            _photoFrame.Add(_photoPlaceholder);
            _photoFrame.style.position = Position.Relative;

            _photoCaptionLabel = new Label("VAB'de araç montajı — NASA teknisyenleri çekirdek aşama üzerinde çalışıyor.");
            _photoCaptionLabel.style.fontSize = 9;
            _photoCaptionLabel.style.color = new Color(0.706f, 0.738f, 0.780f);
            _photoCaptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _photoCaptionLabel.style.marginTop = 6;
            _photoFrame.Add(_photoCaptionLabel);

            var thumbRow = new VisualElement();
            thumbRow.style.flexDirection = FlexDirection.Row;
            thumbRow.style.alignItems = Align.Center;
            thumbRow.style.marginTop = 6;
            _thumbMain = BuildPhotoThumb(PhotoResourceMain);
            _thumbSecond = BuildPhotoThumb(PhotoResourceSecond);
            thumbRow.Add(_thumbMain);
            thumbRow.Add(_thumbSecond);
            _photoFrame.Add(thumbRow);

            _standContent.Add(_photoFrame);

            // ── Vehicle on the stand: scaffold backdrop + silhouette ─────────────
            var standCanvasHost = new VisualElement();
            standCanvasHost.style.height = 210;
            standCanvasHost.style.flexShrink = 0;
            standCanvasHost.style.position = Position.Relative;
            standCanvasHost.style.backgroundColor = new Color(0.090f, 0.098f, 0.118f);
            standCanvasHost.style.marginBottom = 8;

            _standCanvas = new AssemblyStandView();
            standCanvasHost.Add(_standCanvas);

            var silhouetteHost = new VisualElement();
            silhouetteHost.style.position = Position.Absolute;
            silhouetteHost.style.left = Length.Percent(30);
            silhouetteHost.style.right = Length.Percent(30);
            silhouetteHost.style.top = 14;
            silhouetteHost.style.bottom = Length.Percent(8);
            _standSilhouette = new VehicleSilhouetteView();
            _standSilhouette.SetLevel(VehicleSilhouetteView.SilhouetteLevel.Launch);
            _standSilhouette.SetAccentColor(new Color(0.961f, 0.722f, 0.290f));
            silhouetteHost.Add(_standSilhouette);
            standCanvasHost.Add(silhouetteHost);

            _standContent.Add(standCanvasHost);

            // ── Work order details ──────────────────────────────────────────────
            var detailBox = new VisualElement();
            detailBox.style.flexShrink = 0;
            detailBox.style.paddingTop = 8;
            detailBox.style.paddingLeft = 10;
            detailBox.style.paddingRight = 10;
            detailBox.style.paddingBottom = 8;
            detailBox.style.backgroundColor = new Color(0.078f, 0.086f, 0.102f, 0.85f);
            detailBox.style.borderTopLeftRadius = 3;
            detailBox.style.borderTopRightRadius = 3;
            detailBox.style.borderBottomLeftRadius = 3;
            detailBox.style.borderBottomRightRadius = 3;

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            _workOrderNameLabel = new Label("—");
            _workOrderNameLabel.style.fontSize = 14;
            _workOrderNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _workOrderNameLabel.style.color = new Color(0.949f, 0.957f, 0.969f);
            nameRow.Add(_workOrderNameLabel);

            var stampSpacer = new VisualElement();
            stampSpacer.style.flexGrow = 1;
            nameRow.Add(stampSpacer);

            _workOrderStamp = new Label("—");
            _workOrderStamp.AddToClassList("floor-stamp");
            _workOrderStamp.style.fontSize = 10;
            _workOrderStamp.style.paddingLeft = 8;
            _workOrderStamp.style.paddingRight = 8;
            _workOrderStamp.style.paddingTop = 3;
            _workOrderStamp.style.paddingBottom = 3;
            _workOrderStamp.style.rotate = new Rotate(new Angle(-4f, AngleUnit.Degree));
            nameRow.Add(_workOrderStamp);
            detailBox.Add(nameRow);

            _workOrderMetaLabel = new Label("—");
            _workOrderMetaLabel.style.fontSize = 10;
            _workOrderMetaLabel.style.color = new Color(0.706f, 0.738f, 0.780f);
            _workOrderMetaLabel.style.marginTop = 4;
            _workOrderMetaLabel.style.whiteSpace = WhiteSpace.Normal;
            detailBox.Add(_workOrderMetaLabel);

            var gaugeTrack = new VisualElement();
            gaugeTrack.style.height = 8;
            gaugeTrack.style.backgroundColor = GaugeTrackColor;
            gaugeTrack.style.borderTopLeftRadius = 2;
            gaugeTrack.style.borderTopRightRadius = 2;
            gaugeTrack.style.borderBottomLeftRadius = 2;
            gaugeTrack.style.borderBottomRightRadius = 2;
            gaugeTrack.style.marginTop = 8;
            gaugeTrack.style.overflow = Overflow.Hidden;

            _gaugeFill = new VisualElement();
            _gaugeFill.style.height = Length.Percent(100);
            _gaugeFill.style.width = Length.Percent(0);
            _gaugeFill.style.backgroundColor = GaugeFillColor;
            gaugeTrack.Add(_gaugeFill);
            detailBox.Add(gaugeTrack);

            var gaugeCaption = new Label("MONTAJ İLERLEMESİ");
            gaugeCaption.style.fontSize = 8;
            gaugeCaption.style.color = new Color(0.463f, 0.494f, 0.541f);
            gaugeCaption.style.marginTop = 3;
            detailBox.Add(gaugeCaption);

            _cancelButton = new ActionButton(
                IconType.Cancel,
                "İş Emrini İptal Et",
                () => RequestCancelJob(_selectedJobId),
                "İş emrini kuyruktan/hatttan çıkarır; harcanan bütçe geri dönmez.");
            _cancelButton.SetVariant("danger");
            _cancelButton.style.marginTop = 10;
            detailBox.Add(_cancelButton);

            _standContent.Add(detailBox);

            return column;
        }

        private VisualElement BuildPhotoThumb(string resourcePath)
        {
            var thumb = new VisualElement();
            thumb.AddToClassList("floor-thumb");
            thumb.style.width = 76;
            thumb.style.height = 54;
            thumb.style.marginRight = 8;
            thumb.style.backgroundColor = new Color(0.078f, 0.086f, 0.102f);
            thumb.style.overflow = Overflow.Hidden;
            thumb.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            thumb.style.unityTextAlign = TextAnchor.MiddleCenter;

            var tex = LoadPhoto(resourcePath);
            if (tex != null)
            {
                thumb.style.backgroundImage = new StyleBackground(tex);
            }
            else
            {
                var thumbFallback = new Label("□");
                thumbFallback.style.fontSize = 12;
                thumbFallback.style.color = new Color(0.463f, 0.494f, 0.541f);
                thumb.Add(thumbFallback);
            }

            thumb.RegisterCallback<ClickEvent>(_ => SetActivePhoto(resourcePath));
            return thumb;
        }

        private VisualElement BuildLedgerColumn()
        {
            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.paddingTop = 12;
            column.style.paddingBottom = 12;
            column.style.paddingLeft = 16;
            column.style.paddingRight = 16;

            var ledgerTitle = new Label("İŞ EMRİ DEFTERİ");
            ledgerTitle.style.fontSize = 12;
            ledgerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            ledgerTitle.style.color = new Color(0.561f, 0.722f, 0.847f);
            ledgerTitle.style.flexShrink = 0;
            ledgerTitle.style.marginBottom = 8;
            column.Add(ledgerTitle);

            _ledgerScroll = new ScrollView();
            _ledgerScroll.style.flexGrow = 1;
            column.Add(_ledgerScroll);

            // ── Quick dispatch panel ─────────────────────────────────────────────
            var quickBox = new VisualElement();
            quickBox.style.flexShrink = 0;
            quickBox.style.marginTop = 10;
            quickBox.style.paddingTop = 10;
            quickBox.style.paddingLeft = 12;
            quickBox.style.paddingRight = 12;
            quickBox.style.paddingBottom = 10;
            quickBox.style.backgroundColor = new Color(0.078f, 0.086f, 0.102f, 0.85f);
            quickBox.style.borderTopLeftRadius = 3;
            quickBox.style.borderTopRightRadius = 3;
            quickBox.style.borderBottomLeftRadius = 3;
            quickBox.style.borderBottomRightRadius = 3;
            quickBox.style.borderLeftWidth = 3;
            quickBox.style.borderLeftColor = new Color(0.961f, 0.722f, 0.290f, 0.55f);

            var quickTitle = new Label("HIZLI İŞ EMRİ");
            quickTitle.style.fontSize = 11;
            quickTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            quickTitle.style.color = new Color(0.961f, 0.722f, 0.290f);
            quickTitle.style.marginBottom = 8;
            quickBox.Add(quickTitle);

            _quickBuildButton = new ActionButton(
                IconType.Production,
                "Araç Montajı Başlat",
                () => ViewModel.QuickBuildRocketCommand.Execute(),
                "Temel bir roket modelinin montajını fabrika hattına alır.");
            _quickBuildButton.SetPrice("40 M$");
            _quickBuildButton.SetDuration("20 gün");
            _quickBuildButton.SetVariant("primary");
            _quickBuildButton.style.marginBottom = 8;
            quickBox.Add(_quickBuildButton);

            _quickTestButton = new ActionButton(
                IconType.Approve,
                "Sistem Testi Başlat",
                () => ViewModel.QuickStartTestCommand.Execute(),
                "Güvenilirlik kalifikasyonunu test tesisinde başlatır.");
            _quickTestButton.SetPrice("10 M$");
            _quickTestButton.SetDuration("25 gün");
            quickBox.Add(_quickTestButton);

            column.Add(quickBox);
            return column;
        }

        private VisualElement BuildConveyor()
        {
            var band = new VisualElement();
            band.AddToClassList("floor-conveyor");
            band.style.paddingTop = 8;
            band.style.paddingBottom = 8;
            band.style.paddingLeft = 12;
            band.style.paddingRight = 12;

            var bandLabel = new Label("KONVEYÖR HATTI — KUYRUK");
            bandLabel.style.fontSize = 9;
            bandLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
            bandLabel.style.marginBottom = 6;
            band.Add(bandLabel);

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.height = 78;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            _conveyorPallets = new VisualElement();
            _conveyorPallets.style.flexDirection = FlexDirection.Row;
            scroll.Add(_conveyorPallets);
            band.Add(scroll);

            return band;
        }

        // ── Bindings ────────────────────────────────────────────────────────────

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

            RenderJobs();
        }

        private void RegisterObservables()
        {
            if (ViewModel == null) return;

            ViewModel.Budget.Subscribe(b =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{b:F1} M$";
            });

            ViewModel.FactoryCapacityText.Subscribe(_ => { }); // text driven below with fraction
            ViewModel.FactoryCapacityFraction.Subscribe(f =>
            {
                RenderAndon(_factoryLampRow, _factoryLampText, ViewModel.FactoryCapacityText.Value, f);
            });

            ViewModel.TestCapacityFraction.Subscribe(f =>
            {
                RenderAndon(_testLampRow, _testLampText, ViewModel.TestCapacityText.Value, f);
            });

            ViewModel.Jobs.Subscribe(_ => RenderJobs());

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = $"HAT DURUMU: {msg ?? string.Empty}";
                if (_navBar != null && _navBar.StatusLabel != null) _navBar.StatusLabel.text = msg ?? string.Empty;
            });
        }

        // ── Rendering ───────────────────────────────────────────────────────────

        private void RenderJobs()
        {
            if (_ledgerScroll == null || ViewModel == null) return;

            var jobs = ViewModel.Jobs.Value ?? Array.Empty<ProductionJobItemViewModel>();

            // Keep / recover a sensible selection for the assembly stand.
            if (_selectedJobId != null && jobs.All(j => j.Id != _selectedJobId))
            {
                _selectedJobId = null;
            }
            if (_selectedJobId == null && jobs.Count > 0)
            {
                var first = jobs.FirstOrDefault(j => j.Status == ProductionJobStatus.InProgress) ?? jobs[0];
                _selectedJobId = first.Id;
            }

            RenderLedger(jobs);
            RenderConveyor(jobs);
            UpdateStand(jobs);
        }

        private void RenderLedger(IReadOnlyList<ProductionJobItemViewModel> jobs)
        {
            _ledgerScroll.Clear();

            if (jobs.Count == 0)
            {
                var emptyLabel = new Label("Üretim veya test kuyruğunda iş emri yok.\nFabrika katı sessiz — hızlı emir vererek hattı çalıştırabilirsin.");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                _ledgerScroll.Add(emptyLabel);
                return;
            }

            foreach (var job in jobs)
            {
                _ledgerScroll.Add(CreateWorkOrderCard(job));
            }
        }

        private VisualElement CreateWorkOrderCard(ProductionJobItemViewModel job)
        {
            var card = new VisualElement();
            card.AddToClassList("floor-workorder");
            if (job.Id == _selectedJobId)
            {
                card.AddToClassList("floor-workorder--selected");
            }
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 6;

            var capturedId = job.Id;
            card.RegisterCallback<ClickEvent>(_ => SelectJobById(capturedId));

            // Row 1: code + name + status stamp
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;

            var codeLabel = new Label($"#{job.Id}");
            codeLabel.style.fontSize = 9;
            codeLabel.style.color = new Color(0.463f, 0.494f, 0.541f);
            codeLabel.style.marginRight = 8;
            row1.Add(codeLabel);

            var nameLabel = new Label(job.Name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0.949f, 0.957f, 0.969f);
            row1.Add(nameLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row1.Add(spacer);

            row1.Add(CreateStamp(job.Status));
            card.Add(row1);

            // Row 2: type + meta
            var meta = new Label($"{job.JobTypeName}  ·  {job.WorkedDays}/{job.TotalWorkDays} gün  ·  Kalan: {job.RemainingDays}g  ·  Kapasite: {job.RequiredCapacity:F0}  ·  {job.CostMUSD:F1} M$");
            meta.style.fontSize = 10;
            meta.style.color = new Color(0.706f, 0.738f, 0.780f);
            meta.style.marginTop = 3;
            meta.style.marginBottom = 5;
            card.Add(meta);

            // Row 3: progress bar
            var track = new VisualElement();
            track.style.height = 6;
            track.style.backgroundColor = GaugeTrackColor;
            track.style.borderTopLeftRadius = 2;
            track.style.borderTopRightRadius = 2;
            track.style.borderBottomLeftRadius = 2;
            track.style.borderBottomRightRadius = 2;
            track.style.overflow = Overflow.Hidden;

            var fill = new VisualElement();
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(job.ProgressFraction * 100f);
            fill.style.backgroundColor = job.Status == ProductionJobStatus.Completed
                ? new Color(0.341f, 0.851f, 0.560f)
                : job.Status == ProductionJobStatus.InProgress
                    ? new Color(0.961f, 0.722f, 0.290f)
                    : new Color(0.541f, 0.576f, 0.620f);
            track.Add(fill);
            card.Add(track);

            // Row 4: actions (ActionButton stops ClickEvent propagation, card select stays safe)
            if (job.CanCancel)
            {
                var cancelBtn = new ActionButton(
                    IconType.Cancel,
                    "İptal",
                    () => RequestCancelJob(job.Id),
                    "İş emrini iptal eder; geri alınamaz.");
                cancelBtn.SetVariant("danger");
                cancelBtn.style.marginTop = 6;
                card.Add(cancelBtn);
            }

            return card;
        }

        private void RenderConveyor(IReadOnlyList<ProductionJobItemViewModel> jobs)
        {
            _conveyorPallets.Clear();

            var queueJobs = jobs
                .Where(j => j.Status == ProductionJobStatus.Queued || j.Status == ProductionJobStatus.InProgress)
                .ToList();

            if (queueJobs.Count == 0)
            {
                var empty = new Label("Bant boş — kuyrukta aktif iş yok.");
                empty.style.fontSize = 10;
                empty.style.color = new Color(0.463f, 0.494f, 0.541f);
                _conveyorPallets.Add(empty);
                return;
            }

            foreach (var job in queueJobs)
            {
                _conveyorPallets.Add(CreatePallet(job));
            }
        }

        private VisualElement CreatePallet(ProductionJobItemViewModel job)
        {
            var pallet = new VisualElement();
            pallet.AddToClassList("floor-pallet");
            if (job.Id == _selectedJobId)
            {
                pallet.AddToClassList("floor-pallet--selected");
            }
            pallet.style.width = 108;
            pallet.style.marginRight = 8;
            pallet.style.paddingTop = 5;
            pallet.style.paddingBottom = 5;
            pallet.style.paddingLeft = 6;
            pallet.style.paddingRight = 6;

            var capturedId = job.Id;
            pallet.RegisterCallback<ClickEvent>(_ => SelectJobById(capturedId));

            var silhouetteHost = new VisualElement();
            silhouetteHost.style.height = 38;
            silhouetteHost.style.width = Length.Percent(100);
            var silhouette = new VehicleSilhouetteView();
            silhouette.SetLevel(VehicleSilhouetteView.SilhouetteLevel.Launch);
            silhouette.SetAccentColor(new Color(0.961f, 0.722f, 0.290f));
            silhouetteHost.Add(silhouette);
            pallet.Add(silhouetteHost);

            var code = new Label($"#{job.Id}");
            code.style.fontSize = 8;
            code.style.color = new Color(0.463f, 0.494f, 0.541f);
            code.style.marginTop = 3;
            pallet.Add(code);

            var name = new Label(job.Name);
            name.style.fontSize = 9;
            name.style.color = new Color(0.949f, 0.957f, 0.969f);
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            pallet.Add(name);

            return pallet;
        }

        private void UpdateStand(IReadOnlyList<ProductionJobItemViewModel> jobs)
        {
            var job = jobs.FirstOrDefault(j => j.Id == _selectedJobId);

            bool hasJob = job != null;
            _standEmptyLabel.style.display = hasJob ? DisplayStyle.None : DisplayStyle.Flex;
            _standContent.style.display = hasJob ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasJob)
            {
                _standCanvas.SetWorking(false);
                return;
            }

            _workOrderCodeLabel.text = $"İŞ EMRİ #{job.Id}";
            _workOrderNameLabel.text = job.Name;
            _workOrderMetaLabel.text = $"{job.JobTypeName}  ·  {job.WorkedDays}/{job.TotalWorkDays} gün  ·  Kalan: {job.RemainingDays} gün  ·  Kapasite: {job.RequiredCapacity:F0}  ·  Maliyet: {job.CostMUSD:F1} M$";

            ApplyStamp(_workOrderStamp, job.Status);
            _gaugeFill.style.width = Length.Percent(job.ProgressFraction * 100f);

            bool working = job.Status == ProductionJobStatus.InProgress;
            _standCanvas.SetWorking(working);
            _cancelButton.SetAvailable(job.CanCancel, job.CanCancel ? null : "Tamamlanan ya da iptal edilmiş iş emri iptal edilemez.");
            _cancelButton.style.display = job.CanCancel ? DisplayStyle.Flex : DisplayStyle.None;

            // NASA work-log photo only rides on vehicle assembly jobs.
            bool showPhoto = job.JobType == ProductionJobType.VehicleAssembly;
            _photoFrame.style.display = showPhoto ? DisplayStyle.Flex : DisplayStyle.None;
            if (showPhoto)
            {
                SetActivePhoto(string.IsNullOrEmpty(_activePhotoResource) ? PhotoResourceMain : _activePhotoResource);
            }
        }

        private void SetActivePhoto(string resourcePath)
        {
            _activePhotoResource = resourcePath;

            var tex = LoadPhoto(resourcePath);
            if (tex != null)
            {
                _photoPreview.style.backgroundImage = new StyleBackground(tex);
                _photoPlaceholder.style.display = DisplayStyle.None;
            }
            else
            {
                _photoPreview.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                _photoPlaceholder.style.display = DisplayStyle.Flex;
            }

            bool mainActive = resourcePath == PhotoResourceMain;
            ApplyThumbActive(_thumbMain, mainActive);
            ApplyThumbActive(_thumbSecond, !mainActive);
        }

        private void RenderAndon(VisualElement lampRow, Label lampText, string capacityText, float fraction)
        {
            if (lampRow == null || lampText == null) return;

            lampText.text = capacityText;
            lampRow.Clear();

            const int lampCount = 8;
            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * lampCount), 0, lampCount);
            bool full = fraction >= 0.999f;

            for (int i = 0; i < lampCount; i++)
            {
                var lamp = new VisualElement();
                lamp.AddToClassList("floor-andon-lamp");
                lamp.AddToClassList(i < filled
                    ? (full ? "floor-andon-lamp--full" : "floor-andon-lamp--busy")
                    : "floor-andon-lamp--free");
                lampRow.Add(lamp);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static VisualElement CreateStamp(ProductionJobStatus status)
        {
            var stamp = new Label(GetStampText(status));
            stamp.AddToClassList("floor-stamp");
            stamp.AddToClassList(GetStampClass(status));
            stamp.style.fontSize = 9;
            stamp.style.paddingLeft = 8;
            stamp.style.paddingRight = 8;
            stamp.style.paddingTop = 2;
            stamp.style.paddingBottom = 2;
            stamp.style.rotate = new Rotate(new Angle(-4f, AngleUnit.Degree));
            return stamp;
        }

        private static void ApplyStamp(VisualElement stamp, ProductionJobStatus status)
        {
            stamp.RemoveFromClassList("floor-stamp--queued");
            stamp.RemoveFromClassList("floor-stamp--inprogress");
            stamp.RemoveFromClassList("floor-stamp--paused");
            stamp.RemoveFromClassList("floor-stamp--completed");
            stamp.RemoveFromClassList("floor-stamp--cancelled");
            stamp.AddToClassList(GetStampClass(status));
            stamp.tooltip = GetStampText(status);
        }

        private static string GetStampText(ProductionJobStatus status) => status switch
        {
            ProductionJobStatus.Queued => "KUYRUKTA",
            ProductionJobStatus.InProgress => "ÜRETİMDE",
            ProductionJobStatus.Paused => "DURAKLATILDI",
            ProductionJobStatus.Completed => "TAMAMLANDI",
            ProductionJobStatus.Cancelled => "İPTAL EDİLDİ",
            _ => status.ToString().ToUpperInvariant()
        };

        private static string GetStampClass(ProductionJobStatus status) => status switch
        {
            ProductionJobStatus.Queued => "floor-stamp--queued",
            ProductionJobStatus.InProgress => "floor-stamp--inprogress",
            ProductionJobStatus.Paused => "floor-stamp--paused",
            ProductionJobStatus.Completed => "floor-stamp--completed",
            ProductionJobStatus.Cancelled => "floor-stamp--cancelled",
            _ => "floor-stamp--paused"
        };

        private static void ApplyThumbActive(VisualElement thumb, bool active)
        {
            if (active)
            {
                thumb.AddToClassList("floor-thumb--active");
            }
            else
            {
                thumb.RemoveFromClassList("floor-thumb--active");
            }
        }

        private static Texture2D LoadPhoto(string resourcePath)
        {
            if (PhotoCache.TryGetValue(resourcePath, out var cached)) return cached;

            Texture2D tex = null;
            try
            {
                tex = Resources.Load<Texture2D>(resourcePath);
            }
            catch
            {
                tex = null; // test harness / missing file safe
            }

            PhotoCache[resourcePath] = tex;
            return tex;
        }

        // ── Public interaction entry points (also used by tests) ────────────────

        /// <summary>Highlights the given job on the assembly stand, ledger and conveyor.</summary>
        public void SelectJobById(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            _selectedJobId = jobId;
            RenderJobs();
        }

        /// <summary>Opens the irreversible-cancel confirmation for a job (contract #2).</summary>
        public void RequestCancelJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return;

            var job = (ViewModel.Jobs.Value ?? Array.Empty<ProductionJobItemViewModel>())
                .FirstOrDefault(j => j.Id == jobId);
            string jobName = job?.Name ?? jobId;

            ConfirmDialog.Show(
                _root,
                "İş emrini iptal et",
                $"#{jobId} ({jobName}) kuyruktan çıkarılır; harcanan bütçe geri dönmez. Bu işlem geri alınamaz.",
                "İptal Et",
                () => ViewModel.CancelJob(jobId),
                destructive: true);
        }
    }
}
