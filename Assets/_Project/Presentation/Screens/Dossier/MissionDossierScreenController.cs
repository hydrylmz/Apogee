using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.Tooltips;
using Apogee.UI.Screens.Composer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Dossier
{
    /// <summary>
    /// Screen controller for the Mission Dossier Screen (GDD §5.4, §5.5, §14.1 item 6, §21 M3-05).
    /// Presents the full engineering and operational requirements derived by the Mission Composer:
    ///   - Header: Mission title, destination & type badge, claim level (reward/risk).
    ///   - Left Column: Required Delta-V, reliability threshold, launch pad tier, data & comms specs, windows & costs.
    ///   - Center Column: Mandatory Modules & Instruments checklist (EDL, life-support, etc.).
    ///   - Right Column: Required Research (with locked/unlocked state and "Araştırmaya Ekle" queue action)
    ///                   and Measurable Success Criteria checklist (GDD §5.5).
    ///   - Footer: Navigation actions to Workshop or Back to Composer.
    /// </summary>
    public class MissionDossierScreenController : ScreenControllerBase<MissionDossierViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MissionBrief;

        // Header Elements
        private Label _missionTitleLabel;
        private Label _destinationBadge;
        private Label _missionTypeBadge;
        private Label _claimBadge;
        private Label _rewardRiskLabel;

        // Telemetry & Engineering Specs (Left)
        private Label _deltaVValueLabel;
        private Label _reliabilityValueLabel;
        private Label _padLevelValueLabel;
        private Label _downlinkValueLabel;
        private Label _storageValueLabel;
        private Label _commDelayValueLabel;
        private Label _transitDurationValueLabel;
        private Label _windowInfoValueLabel;
        private Label _costEstimateValueLabel;
        private Label _buildTimeValueLabel;
        private Label _budgetCeilingValueLabel;

        // Lists (Center & Right)
        private ScrollView _requirementsListContainer;
        private ScrollView _researchListContainer;
        private ScrollView _criteriaListContainer;

        private Label _lockedResearchSummaryLabel;
        private Button _queueAllResearchButton;

        // Footer Actions
        private Button _backToComposerButton;
        private Button _goToWorkshopButton;

        public MissionDossierScreenController(MissionDossierViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.AddToClassList("dossier-screen");
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f);

            // ── 1. Header ────────────────────────────────────────────────────────
            var header = BuildHeader();
            root.Add(header);

            // ── 2. Main Three-Column Layout ───────────────────────────────────────
            var mainContent = new VisualElement();
            mainContent.AddToClassList("dossier-main-content");
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;
            mainContent.style.marginTop = 10;
            mainContent.style.marginBottom = 10;

            // Col 1: Telemetry & Specs
            var colTelemetry = BuildTelemetryColumn();
            mainContent.Add(colTelemetry);

            // Col 2: Mandatory Modules & Instruments
            var colRequirements = BuildRequirementsColumn();
            mainContent.Add(colRequirements);

            // Col 3: Research & Success Criteria
            var colResearchAndCriteria = BuildResearchAndCriteriaColumn();
            mainContent.Add(colResearchAndCriteria);

            root.Add(mainContent);

            // ── 3. Footer ────────────────────────────────────────────────────────
            var footer = BuildFooter();
            root.Add(footer);

            return root;
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.AddToClassList("apogee-panel-header");
            header.AddToClassList("dossier-header");
            header.style.paddingLeft = 14;
            header.style.paddingRight = 14;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            // Left: Title & Badges
            var leftCol = new VisualElement();

            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;

            _missionTitleLabel = new Label("MİSYON DOSYASI (MISSION DOSSIER)");
            _missionTitleLabel.AddToClassList("apogee-panel-title");
            _missionTitleLabel.style.fontSize = 16;
            _missionTitleLabel.style.marginRight = 10;
            topRow.Add(_missionTitleLabel);

            _destinationBadge = new Label("AY (LUNA)");
            _destinationBadge.AddToClassList("apogee-badge");
            _destinationBadge.AddToClassList("apogee-badge--cyan");
            _destinationBadge.style.marginRight = 6;
            topRow.Add(_destinationBadge);

            _missionTypeBadge = new Label("ROVER");
            _missionTypeBadge.AddToClassList("apogee-badge");
            _missionTypeBadge.style.marginRight = 6;
            topRow.Add(_missionTypeBadge);

            _claimBadge = new Label("İDDİA: ORTA");
            _claimBadge.AddToClassList("claim-badge");
            _claimBadge.AddToClassList("claim-badge--medium");
            topRow.Add(_claimBadge);

            leftCol.Add(topRow);

            _rewardRiskLabel = new Label("GDD §5.4 Mühendislik Gereksinimleri ve Başarı Kriterleri");
            _rewardRiskLabel.style.fontSize = 11;
            _rewardRiskLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
            _rewardRiskLabel.style.marginTop = 4;
            leftCol.Add(_rewardRiskLabel);

            header.Add(leftCol);

            // Right: Quick Actions
            var rightCol = new VisualElement();
            rightCol.style.flexDirection = FlexDirection.Row;
            rightCol.style.alignItems = Align.Center;

            var quickWorkshopBtn = new Button(() => ViewModel.GoToWorkshopCommand.Execute());
            quickWorkshopBtn.text = "🚀 Atölyede Tasarla";
            quickWorkshopBtn.AddToClassList("apogee-button");
            quickWorkshopBtn.AddToClassList("apogee-button--primary");
            rightCol.Add(quickWorkshopBtn);

            header.Add(rightCol);

            return header;
        }

        private VisualElement BuildTelemetryColumn()
        {
            var col = new VisualElement();
            col.AddToClassList("apogee-panel");
            col.AddToClassList("apogee-panel--surface");
            col.style.width = 280;
            col.style.marginRight = 10;
            col.style.paddingTop = 10;
            col.style.paddingBottom = 10;
            col.style.paddingLeft = 12;
            col.style.paddingRight = 12;

            var title = new Label("MÜHENDİSLİK HEDEFLERİ");
            title.AddToClassList("composer-section-title");
            title.style.marginBottom = 10;
            col.Add(title);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            // 1. Delta-V
            var dvBox = CreateMetricCard("GEREKLİ MENZİL (Δv)", out _deltaVValueLabel, "13,600 m/s", "Fırlatma kayıpları + transfer + iniş");
            scroll.Add(dvBox);

            // 2. Reliability
            var relBox = CreateMetricCard("GÜVENİLİRLİK EŞİĞİ", out _reliabilityValueLabel, "%85", "Risk toleransı ve minimum sistem kalitesi");
            scroll.Add(relBox);

            // 3. Launch Pad
            var padBox = CreateMetricCard("RAMPA KAPASİTESİ", out _padLevelValueLabel, "Seviye 2 (Orta Sınıf)", "Kalkış kütlesi için gerekli üs rampası");
            scroll.Add(padBox);

            // 4. Data storage & Downlink
            var dataBox = new VisualElement();
            dataBox.AddToClassList("dossier-metric-card");
            dataBox.style.paddingBottom = 6;
            var dataTitle = new Label("TELEMETRİ & VERİ KANALI");
            dataTitle.AddToClassList("dossier-metric-title");
            dataBox.Add(dataTitle);

            _downlinkValueLabel = new Label("İndirme: ≥ 1.5 GB/gün");
            _downlinkValueLabel.style.fontSize = 11;
            _downlinkValueLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
            dataBox.Add(_downlinkValueLabel);

            _storageValueLabel = new Label("Depolama: ≥ 5.0 GB");
            _storageValueLabel.style.fontSize = 11;
            _storageValueLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
            _storageValueLabel.style.marginTop = 2;
            dataBox.Add(_storageValueLabel);
            scroll.Add(dataBox);

            // 5. Timeline & Windows
            var timeBox = new VisualElement();
            timeBox.AddToClassList("dossier-metric-card");
            var timeTitle = new Label("TAKVİM & SEYİR");
            timeTitle.AddToClassList("dossier-metric-title");
            timeBox.Add(timeTitle);

            _transitDurationValueLabel = new Label("Seyir: 3 gün");
            _transitDurationValueLabel.style.fontSize = 11;
            _transitDurationValueLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
            timeBox.Add(_transitDurationValueLabel);

            _commDelayValueLabel = new Label("Gecikme: Anlık (<0.05 sn)");
            _commDelayValueLabel.style.fontSize = 11;
            _commDelayValueLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
            _commDelayValueLabel.style.marginTop = 2;
            timeBox.Add(_commDelayValueLabel);

            _windowInfoValueLabel = new Label("Pencere: İlk Pencere");
            _windowInfoValueLabel.style.fontSize = 11;
            _windowInfoValueLabel.style.color = new Color(0.6f, 0.8f, 1.0f);
            _windowInfoValueLabel.style.marginTop = 2;
            timeBox.Add(_windowInfoValueLabel);
            scroll.Add(timeBox);

            // 6. Cost & Build Time
            var costBox = new VisualElement();
            costBox.AddToClassList("dossier-metric-card");
            var costTitle = new Label("TAHMİNİ BÜTÇE & SÜRE");
            costTitle.AddToClassList("dossier-metric-title");
            costBox.Add(costTitle);

            _costEstimateValueLabel = new Label("Tahmini Maliyet: 45.0M$ – 65.0M$");
            _costEstimateValueLabel.style.fontSize = 11;
            _costEstimateValueLabel.style.color = new Color(0.3f, 0.9f, 0.5f);
            costBox.Add(_costEstimateValueLabel);

            _buildTimeValueLabel = new Label("Üretim Süresi: 60 – 90 gün");
            _buildTimeValueLabel.style.fontSize = 11;
            _buildTimeValueLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
            _buildTimeValueLabel.style.marginTop = 2;
            costBox.Add(_buildTimeValueLabel);

            _budgetCeilingValueLabel = new Label("Bütçe Tavanı: Sınırsız");
            _budgetCeilingValueLabel.style.fontSize = 10;
            _budgetCeilingValueLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
            _budgetCeilingValueLabel.style.marginTop = 2;
            costBox.Add(_budgetCeilingValueLabel);
            scroll.Add(costBox);

            col.Add(scroll);
            return col;
        }

        private VisualElement BuildRequirementsColumn()
        {
            var col = new VisualElement();
            col.AddToClassList("apogee-panel");
            col.AddToClassList("apogee-panel--surface");
            col.style.flexGrow = 1;
            col.style.marginRight = 10;
            col.style.paddingTop = 10;
            col.style.paddingBottom = 10;
            col.style.paddingLeft = 12;
            col.style.paddingRight = 12;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 10;

            var title = new Label("ZORUNLU MODÜLLER & ENSTRÜMANLAR");
            title.AddToClassList("composer-section-title");
            titleRow.Add(title);

            var note = new Label("GDD §5.4 — Tasarımla karşılanmalı");
            note.style.fontSize = 10;
            note.style.color = new Color(0.5f, 0.6f, 0.7f);
            titleRow.Add(note);
            col.Add(titleRow);

            _requirementsListContainer = new ScrollView();
            _requirementsListContainer.style.flexGrow = 1;
            col.Add(_requirementsListContainer);

            return col;
        }

        private VisualElement BuildResearchAndCriteriaColumn()
        {
            var col = new VisualElement();
            col.AddToClassList("apogee-panel");
            col.AddToClassList("apogee-panel--surface");
            col.style.width = 320;
            col.style.paddingTop = 10;
            col.style.paddingBottom = 10;
            col.style.paddingLeft = 12;
            col.style.paddingRight = 12;
            col.style.flexDirection = FlexDirection.Column;

            // ── Section 1: Research Prerequisites ──
            var resHeader = new VisualElement();
            resHeader.style.flexDirection = FlexDirection.Row;
            resHeader.style.justifyContent = Justify.SpaceBetween;
            resHeader.style.alignItems = Align.Center;
            resHeader.style.marginBottom = 6;

            var resTitle = new Label("GEREKLİ ARAŞTIRMALAR");
            resTitle.AddToClassList("composer-section-title");
            resHeader.Add(resTitle);

            _lockedResearchSummaryLabel = new Label("0 Kilitli");
            _lockedResearchSummaryLabel.AddToClassList("apogee-badge");
            _lockedResearchSummaryLabel.AddToClassList("apogee-badge--mini");
            resHeader.Add(_lockedResearchSummaryLabel);
            col.Add(resHeader);

            _researchListContainer = new ScrollView();
            _researchListContainer.style.height = 200;
            _researchListContainer.style.marginBottom = 6;
            col.Add(_researchListContainer);

            _queueAllResearchButton = new Button(() => ViewModel.QueueAllLockedResearchCommand.Execute());
            _queueAllResearchButton.text = "⚡ Tüm Kilitlileri Araştırmaya Ekle";
            _queueAllResearchButton.AddToClassList("apogee-button");
            _queueAllResearchButton.style.fontSize = 11;
            _queueAllResearchButton.style.marginBottom = 12;
            col.Add(_queueAllResearchButton);

            // ── Section 2: Measurable Success Criteria ──
            var critHeader = new VisualElement();
            critHeader.style.flexDirection = FlexDirection.Row;
            critHeader.style.justifyContent = Justify.SpaceBetween;
            critHeader.style.alignItems = Align.Center;
            critHeader.style.marginBottom = 6;

            var critTitle = new Label("BAŞARI KRİTERLERİ (GDD §5.5)");
            critTitle.AddToClassList("composer-section-title");
            critHeader.Add(critTitle);

            var critSubtitle = new Label("Ölçülebilir Hedefler");
            critSubtitle.style.fontSize = 10;
            critSubtitle.style.color = new Color(0.5f, 0.6f, 0.7f);
            critHeader.Add(critSubtitle);
            col.Add(critHeader);

            _criteriaListContainer = new ScrollView();
            _criteriaListContainer.style.flexGrow = 1;
            col.Add(_criteriaListContainer);

            return col;
        }

        private VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.AddToClassList("composer-footer-bar");
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;

            _backToComposerButton = new Button(() => ViewModel.BackToComposerCommand.Execute()) { text = "← Besteleyiciye Dön" };
            _backToComposerButton.AddToClassList("apogee-button");
            footer.Add(_backToComposerButton);

            var rightActions = new VisualElement();
            rightActions.style.flexDirection = FlexDirection.Row;
            rightActions.style.alignItems = Align.Center;

            var noteLabel = new Label("Misyon profili atölyeye aktarılacaktır.");
            noteLabel.style.fontSize = 11;
            noteLabel.style.color = new Color(0.6f, 0.7f, 0.8f);
            noteLabel.style.marginRight = 10;
            rightActions.Add(noteLabel);

            _goToWorkshopButton = new Button(() => ViewModel.GoToWorkshopCommand.Execute()) { text = "Tasarım Atölyesine Başla →" };
            _goToWorkshopButton.AddToClassList("apogee-button");
            _goToWorkshopButton.AddToClassList("apogee-button--accent-amber");
            rightActions.Add(_goToWorkshopButton);

            footer.Add(rightActions);
            return footer;
        }

        private VisualElement CreateMetricCard(string title, out Label valueLabel, string defaultValue, string description)
        {
            var card = new VisualElement();
            card.AddToClassList("dossier-metric-card");

            var t = new Label(title);
            t.AddToClassList("dossier-metric-title");
            card.Add(t);

            valueLabel = new Label(defaultValue);
            valueLabel.AddToClassList("dossier-metric-value");
            card.Add(valueLabel);

            var desc = new Label(description);
            desc.AddToClassList("dossier-metric-desc");
            card.Add(desc);

            return card;
        }

        // ── Lifecycle & Screen Navigation ─────────────────────────────────────────

        public override void OnEnter(object context = null)
        {
            base.OnEnter(context);

            if (context is MissionProfile profile)
            {
                ViewModel.LoadProfile(profile);
            }
            else if (context is MissionCompositionResultArgs args)
            {
                ViewModel.LoadProfile(args.Profile, args);
            }
        }

        protected override void OnInitialized()
        {
            // Observables subscription
            ViewModel.MissionName.Subscribe(name => _missionTitleLabel.text = name?.ToUpperInvariant() ?? "MİSYON DOSYASI");
            ViewModel.DestinationLabel.Subscribe(dest => _destinationBadge.text = dest);
            ViewModel.MissionTypeLabel.Subscribe(type => _missionTypeBadge.text = type);

            ViewModel.ClaimLevelBadge.Subscribe(badge => _claimBadge.text = $"İDDİA: {badge}");
            ViewModel.ClaimLevelCssClass.Subscribe(css =>
            {
                _claimBadge.RemoveFromClassList("claim-badge--low");
                _claimBadge.RemoveFromClassList("claim-badge--medium");
                _claimBadge.RemoveFromClassList("claim-badge--high");
                _claimBadge.RemoveFromClassList("claim-badge--extreme");
                _claimBadge.AddToClassList(css);
            });

            ViewModel.RewardRiskSummary.Subscribe(summary => _rewardRiskLabel.text = summary);

            // Telemetry
            ViewModel.RequiredDeltaVText.Subscribe(val => _deltaVValueLabel.text = val);
            ViewModel.ReliabilityThresholdText.Subscribe(val => _reliabilityValueLabel.text = val);
            ViewModel.LaunchPadLevelText.Subscribe(val => _padLevelValueLabel.text = val);

            ViewModel.MinDownlinkRate.Subscribe(val => _downlinkValueLabel.text = $"İndirme: ≥ {val:F1} GB/gün");
            ViewModel.MinDataStorage.Subscribe(val => _storageValueLabel.text = $"Depolama: ≥ {val:F0} GB");

            ViewModel.TransitDurationText.Subscribe(val => _transitDurationValueLabel.text = $"Seyir: {val}");
            ViewModel.CommDelayText.Subscribe(val => _commDelayValueLabel.text = $"Gecikme: {val}");
            ViewModel.LaunchWindowInfoText.Subscribe(val => _windowInfoValueLabel.text = $"Pencere: {val}");

            ViewModel.EstimatedCostText.Subscribe(val => _costEstimateValueLabel.text = $"Tahmini Maliyet: {val}");
            ViewModel.EstimatedBuildTimeText.Subscribe(val => _buildTimeValueLabel.text = $"Üretim Süresi: {val}");
            ViewModel.BudgetCeilingText.Subscribe(val => _budgetCeilingValueLabel.text = $"Bütçe Tavanı: {val}");

            // Lists
            ViewModel.MandatoryRequirements.Subscribe(PopulateRequirements);
            ViewModel.ResearchRequirements.Subscribe(PopulateResearch);
            ViewModel.SuccessCriteria.Subscribe(PopulateSuccessCriteria);

            ViewModel.LockedResearchCount.Subscribe(count =>
            {
                if (count > 0)
                {
                    _lockedResearchSummaryLabel.text = $"{count} Kilitli";
                    _lockedResearchSummaryLabel.RemoveFromClassList("apogee-badge--green");
                    _lockedResearchSummaryLabel.AddToClassList("apogee-badge--amber");
                    _queueAllResearchButton.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _lockedResearchSummaryLabel.text = "Tümü Açık ✓";
                    _lockedResearchSummaryLabel.RemoveFromClassList("apogee-badge--amber");
                    _lockedResearchSummaryLabel.AddToClassList("apogee-badge--green");
                    _queueAllResearchButton.style.display = DisplayStyle.None;
                }
            });
        }

        private void PopulateRequirements(IReadOnlyList<DossierRequirementItem> items)
        {
            if (_requirementsListContainer == null) return;
            _requirementsListContainer.Clear();

            if (items == null || items.Count == 0)
            {
                var empty = new Label("Özel zorunlu donanım veya alet tanımlanmadı.");
                empty.style.color = new Color(0.5f, 0.6f, 0.7f);
                empty.style.marginTop = 10;
                _requirementsListContainer.Add(empty);
                return;
            }

            foreach (var req in items)
            {
                var card = new VisualElement();
                card.AddToClassList("dossier-req-card");

                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.justifyContent = Justify.SpaceBetween;
                topRow.style.alignItems = Align.Center;

                var title = new Label(req.Title);
                title.AddToClassList("dossier-req-title");
                topRow.Add(title);

                var badge = new Label(req.StatusBadge);
                badge.AddToClassList("apogee-badge");
                if (req.StatusBadge == "KRİTİK") badge.AddToClassList("apogee-badge--red");
                else if (req.StatusBadge == "ZORUNLU") badge.AddToClassList("apogee-badge--amber");
                else badge.AddToClassList("apogee-badge--cyan");
                topRow.Add(badge);

                card.Add(topRow);

                var category = new Label($"Kategori: {req.Category}");
                category.style.fontSize = 10;
                category.style.color = new Color(0.4f, 0.7f, 0.9f);
                category.style.marginTop = 2;
                card.Add(category);

                var desc = new Label(req.Description);
                desc.AddToClassList("dossier-req-desc");
                card.Add(desc);

                _requirementsListContainer.Add(card);
            }
        }

        private void PopulateResearch(IReadOnlyList<DossierResearchItem> items)
        {
            if (_researchListContainer == null) return;
            _researchListContainer.Clear();

            if (items == null || items.Count == 0)
            {
                var empty = new Label("Önkoşul araştırma bulunmamaktadır. Tüm teknolojiler hazır.");
                empty.style.color = new Color(0.4f, 0.8f, 0.5f);
                empty.style.fontSize = 11;
                empty.style.marginTop = 6;
                _researchListContainer.Add(empty);
                return;
            }

            foreach (var item in items)
            {
                var row = new VisualElement();
                row.AddToClassList("dossier-research-item");
                if (!item.IsUnlocked)
                {
                    row.AddToClassList("dossier-research-item--locked");
                }

                var titleRow = new VisualElement();
                titleRow.style.flexDirection = FlexDirection.Row;
                titleRow.style.justifyContent = Justify.SpaceBetween;
                titleRow.style.alignItems = Align.Center;

                var name = new Label(item.Title);
                name.AddToClassList("dossier-research-title");
                titleRow.Add(name);

                var statusBadge = new Label(item.IsUnlocked ? "AÇIK ✓" : (item.IsInQueue ? "KUYRUKTA ⏳" : "KİLİTLİ 🔒"));
                statusBadge.AddToClassList("apogee-badge");
                statusBadge.AddToClassList("apogee-badge--mini");
                if (item.IsUnlocked) statusBadge.AddToClassList("apogee-badge--green");
                else if (item.IsInQueue) statusBadge.AddToClassList("apogee-badge--cyan");
                else statusBadge.AddToClassList("apogee-badge--red");
                titleRow.Add(statusBadge);

                row.Add(titleRow);

                var desc = new Label(item.Description);
                desc.AddToClassList("dossier-research-desc");
                row.Add(desc);

                if (!item.IsUnlocked && !item.IsInQueue)
                {
                    var queueBtn = new Button(() => ViewModel.QueueResearch(item.ResearchId));
                    queueBtn.text = "+ Araştırmaya Ekle";
                    queueBtn.AddToClassList("apogee-button");
                    queueBtn.style.fontSize = 10;
                    queueBtn.style.marginTop = 4;
                    queueBtn.style.alignSelf = Align.FlexEnd;
                    row.Add(queueBtn);
                }

                _researchListContainer.Add(row);
            }
        }

        private void PopulateSuccessCriteria(IReadOnlyList<MissionSuccessCriterion> criteria)
        {
            if (_criteriaListContainer == null) return;
            _criteriaListContainer.Clear();

            if (criteria == null || criteria.Count == 0)
            {
                var empty = new Label("Özel başarı kriteri belirlenmedi.");
                empty.style.color = new Color(0.5f, 0.6f, 0.7f);
                empty.style.fontSize = 11;
                empty.style.marginTop = 6;
                _criteriaListContainer.Add(empty);
                return;
            }

            for (int i = 0; i < criteria.Count; i++)
            {
                var crit = criteria[i];
                var card = new VisualElement();
                card.AddToClassList("dossier-criterion-card");

                var topRow = new VisualElement();
                topRow.style.flexDirection = FlexDirection.Row;
                topRow.style.justifyContent = Justify.SpaceBetween;
                topRow.style.alignItems = Align.Center;

                var desc = new Label($"[ ] {crit.Description}");
                desc.AddToClassList("dossier-criterion-text");
                topRow.Add(desc);

                var typeLabel = new Label(crit.CriterionType.ToUpperInvariant());
                typeLabel.AddToClassList("apogee-badge");
                typeLabel.AddToClassList("apogee-badge--mini");
                typeLabel.AddToClassList("apogee-badge--cyan");
                topRow.Add(typeLabel);

                card.Add(topRow);

                if (crit.Threshold > 0.0)
                {
                    var targetLabel = new Label($"Hedef Eşik: ≥ {crit.Threshold:F1} ({crit.CriterionType})");
                    targetLabel.style.fontSize = 9;
                    targetLabel.style.color = new Color(0.5f, 0.7f, 0.9f);
                    targetLabel.style.marginTop = 2;
                    card.Add(targetLabel);
                }

                _criteriaListContainer.Add(card);
            }
        }
    }
}
