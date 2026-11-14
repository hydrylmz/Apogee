using System;
using System.Linq;
using Apogee.Core.Localization;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Report
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Mission Report Screen (ScreenId.MissionReport).
    /// Presents a clean, informative HOI4-inspired after-action report:
    ///   - Mission Outcome Badge & Narrative (GDD §10.5)
    ///   - Measurable Success Criteria Checklist (GDD §5.5)
    ///   - Science Downlink vs Generated Data Bottleneck Analysis (GDD §10.5, §13.2)
    ///   - Cause-and-Effect Chain (Design -> Launch -> Cruise -> EDL -> Science)
    ///   - Lessons Learned & Knowledge Points (GDD §4.3, §10.5)
    ///   - Campaign Economic & Reputational Impact Summary
    /// </summary>
    public sealed class MissionReportScreenController : ScreenControllerBase<MissionReportViewModel>
    {
        public override ScreenId ScreenId => ScreenId.MissionReport;

        private Label _missionTitleLabel;
        private Label _destinationLabel;
        private Label _outcomeBadge;
        private Label _finalNarrativeLabel;
        private Label _campaignImpactLabel;

        // Science telemetry
        private Label _scienceDeliveryRatioLabel;
        private Label _scienceBottleneckLabel;

        // Dynamic lists
        private VisualElement _criteriaContainer;
        private VisualElement _causeChainContainer;
        private VisualElement _lessonsContainer;

        // ── Scoring & Medals (M5-08) ─────────────────────────────────────────
        private Label _gradeBadge;
        private Label _scoreLabel;
        private Label _scorePillarsLabel;
        private VisualElement _medalsContainer;

        // Buttons
        private Button _workshopButton;
        private Button _campaignMapButton;

        // ── What-If panel (M5-07) ────────────────────────────────────────────
        private VisualElement _whatIfPanel;
        private VisualElement _whatIfEventList;
        private VisualElement _whatIfOptionList;
        private Label         _whatIfOutcomeBadge;
        private Label         _whatIfDeltaLabel;
        private Button        _whatIfClearButton;

        public MissionReportScreenController(MissionReportViewModel viewModel)
            : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("apogee-root");
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;
            root.style.flexDirection = FlexDirection.Column;

            // ── HEADER SECTION ──────────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("apogee-panel");
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.marginBottom = 12;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var titleBox = new VisualElement();
            _missionTitleLabel = new Label("MİSYON SONUÇ RAPORU");
            _missionTitleLabel.style.fontSize = 20;
            _missionTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _missionTitleLabel.style.color = new Color(0.9f, 0.95f, 1.0f);

            _destinationLabel = new Label("HEDEF: -");
            _destinationLabel.style.fontSize = 12;
            _destinationLabel.style.color = new Color(0.6f, 0.7f, 0.85f);
            _destinationLabel.style.marginTop = 2;

            titleBox.Add(_missionTitleLabel);
            titleBox.Add(_destinationLabel);

            var rightBox = new VisualElement();
            rightBox.style.flexDirection = FlexDirection.Row;
            rightBox.style.alignItems = Align.Center;

            var scoreBox = new VisualElement();
            scoreBox.style.alignItems = Align.FlexEnd;
            scoreBox.style.marginRight = 14;

            _scoreLabel = new Label("SKOR: 0");
            _scoreLabel.style.fontSize = 15;
            _scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreLabel.style.color = new Color(0.95f, 0.8f, 0.2f);

            _gradeBadge = new Label("DERECELENDİRME: -");
            _gradeBadge.style.fontSize = 11;
            _gradeBadge.style.color = new Color(0.7f, 0.75f, 0.85f);

            scoreBox.Add(_scoreLabel);
            scoreBox.Add(_gradeBadge);

            _outcomeBadge = new Label("DEĞERLENDİRİLİYOR");
            _outcomeBadge.style.fontSize = 14;
            _outcomeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _outcomeBadge.style.paddingLeft = 14;
            _outcomeBadge.style.paddingRight = 14;
            _outcomeBadge.style.paddingTop = 6;
            _outcomeBadge.style.paddingBottom = 6;
            _outcomeBadge.style.borderTopLeftRadius = 4;
            _outcomeBadge.style.borderTopRightRadius = 4;
            _outcomeBadge.style.borderBottomLeftRadius = 4;
            _outcomeBadge.style.borderBottomRightRadius = 4;

            rightBox.Add(scoreBox);
            rightBox.Add(_outcomeBadge);

            header.Add(titleBox);
            header.Add(rightBox);
            root.Add(header);

            // ── MAIN BODY (3 Columns) ──────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.marginBottom = 12;

            // Column 1: Criteria & Science Delivery (Left, 35%)
            var colLeft = new VisualElement();
            colLeft.style.flexBasis = new Length(35, LengthUnit.Percent);
            colLeft.style.flexGrow = 1;
            colLeft.style.marginRight = 8;
            colLeft.AddToClassList("apogee-panel");
            colLeft.style.paddingTop = 12;
            colLeft.style.paddingBottom = 12;
            colLeft.style.paddingLeft = 14;
            colLeft.style.paddingRight = 14;

            var critTitle = new Label("1. MİSYON BAŞARI KRİTERLERİ");
            critTitle.style.fontSize = 13;
            critTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            critTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            critTitle.style.marginBottom = 8;
            colLeft.Add(critTitle);

            _criteriaContainer = new ScrollView();
            _criteriaContainer.style.maxHeight = 220;
            _criteriaContainer.style.marginBottom = 12;
            colLeft.Add(_criteriaContainer);

            var scienceTitle = new Label("2. BİLİM VE VERİ TESLİMİ");
            scienceTitle.style.fontSize = 13;
            scienceTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            scienceTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            scienceTitle.style.marginBottom = 4;
            colLeft.Add(scienceTitle);

            _scienceDeliveryRatioLabel = new Label("0 GB / 0 GB");
            _scienceDeliveryRatioLabel.style.fontSize = 14;
            _scienceDeliveryRatioLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scienceDeliveryRatioLabel.style.color = new Color(0.2f, 0.85f, 0.95f);
            _scienceDeliveryRatioLabel.style.marginBottom = 4;
            colLeft.Add(_scienceDeliveryRatioLabel);

            _scienceBottleneckLabel = new Label();
            _scienceBottleneckLabel.style.fontSize = 11;
            _scienceBottleneckLabel.style.whiteSpace = WhiteSpace.Normal;
            _scienceBottleneckLabel.style.color = new Color(0.8f, 0.8f, 0.85f);
            colLeft.Add(_scienceBottleneckLabel);

            body.Add(colLeft);

            // Column 2: Cause-and-Effect Chain (Center, 38%)
            var colCenter = new VisualElement();
            colCenter.style.flexBasis = new Length(38, LengthUnit.Percent);
            colCenter.style.flexGrow = 1;
            colCenter.style.marginRight = 8;
            colCenter.AddToClassList("apogee-panel");
            colCenter.style.paddingTop = 12;
            colCenter.style.paddingBottom = 12;
            colCenter.style.paddingLeft = 14;
            colCenter.style.paddingRight = 14;

            var causeTitle = new Label("3. NEDEN - SONUÇ ZİNCİRİ");
            causeTitle.style.fontSize = 13;
            causeTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            causeTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            causeTitle.style.marginBottom = 8;
            colCenter.Add(causeTitle);

            _causeChainContainer = new ScrollView();
            _causeChainContainer.style.flexGrow = 1;
            colCenter.Add(_causeChainContainer);

            body.Add(colCenter);

            // Column 3: Lessons Learned & Campaign Impact (Right, 27%)
            var colRight = new VisualElement();
            colRight.style.flexBasis = new Length(27, LengthUnit.Percent);
            colRight.style.flexGrow = 1;
            colRight.AddToClassList("apogee-panel");
            colRight.style.paddingTop = 12;
            colRight.style.paddingBottom = 12;
            colRight.style.paddingLeft = 14;
            colRight.style.paddingRight = 14;

            var lessonsTitle = new Label("4. ÖĞRENİLEN DERSLER");
            lessonsTitle.style.fontSize = 13;
            lessonsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lessonsTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            lessonsTitle.style.marginBottom = 8;
            colRight.Add(lessonsTitle);

            _lessonsContainer = new ScrollView();
            _lessonsContainer.style.maxHeight = 180;
            _lessonsContainer.style.marginBottom = 12;
            colRight.Add(_lessonsContainer);

            var impactTitle = new Label("5. KAMPANYA ETKİSİ");
            impactTitle.style.fontSize = 13;
            impactTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            impactTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            impactTitle.style.marginBottom = 4;
            colRight.Add(impactTitle);

            _campaignImpactLabel = new Label();
            _campaignImpactLabel.style.fontSize = 12;
            _campaignImpactLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _campaignImpactLabel.style.color = new Color(0.95f, 0.75f, 0.2f);
            _campaignImpactLabel.style.whiteSpace = WhiteSpace.Normal;
            _campaignImpactLabel.style.marginBottom = 12;
            colRight.Add(_campaignImpactLabel);

            var medalsTitle = new Label("6. KAZANILAN MADALYALAR");
            medalsTitle.style.fontSize = 13;
            medalsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            medalsTitle.style.color = new Color(0.8f, 0.85f, 0.9f);
            medalsTitle.style.marginBottom = 6;
            colRight.Add(medalsTitle);

            _medalsContainer = new ScrollView();
            _medalsContainer.style.maxHeight = 120;
            _medalsContainer.style.marginBottom = 8;
            colRight.Add(_medalsContainer);

            _scorePillarsLabel = new Label();
            _scorePillarsLabel.style.fontSize = 9;
            _scorePillarsLabel.style.color = new Color(0.6f, 0.65f, 0.75f);
            _scorePillarsLabel.style.whiteSpace = WhiteSpace.Normal;
            colRight.Add(_scorePillarsLabel);

            body.Add(colRight);
            root.Add(body);

            // ── WHAT-IF PANEL (M5-07) ──────────────────────────────────────────
            _whatIfPanel = new VisualElement();
            _whatIfPanel.AddToClassList("apogee-panel");
            _whatIfPanel.style.paddingTop    = 10;
            _whatIfPanel.style.paddingBottom = 10;
            _whatIfPanel.style.paddingLeft   = 16;
            _whatIfPanel.style.paddingRight  = 16;
            _whatIfPanel.style.marginBottom  = 8;

            // Header row
            var wiHeaderRow = new VisualElement();
            wiHeaderRow.style.flexDirection = FlexDirection.Row;
            wiHeaderRow.style.alignItems    = Align.Center;
            wiHeaderRow.style.marginBottom  = 8;

            var wiTitle = new Label("\"YA ŞÖYLE OLSAYDI?\"ANALİZİ");
            wiTitle.style.fontSize                    = 13;
            wiTitle.style.unityFontStyleAndWeight     = FontStyle.Bold;
            wiTitle.style.color                       = new Color(0.95f, 0.80f, 0.3f);
            wiTitle.style.flexGrow                    = 1;

            _whatIfClearButton = new Button(OnWhatIfClear) { text = "Temizle" };
            _whatIfClearButton.AddToClassList("apogee-button");
            _whatIfClearButton.style.paddingLeft  = 10;
            _whatIfClearButton.style.paddingRight = 10;
            _whatIfClearButton.style.paddingTop   = 4;
            _whatIfClearButton.style.paddingBottom = 4;

            wiHeaderRow.Add(wiTitle);
            wiHeaderRow.Add(_whatIfClearButton);
            _whatIfPanel.Add(wiHeaderRow);

            var wiHint = new Label("Bir operasyon olayı seçin ve farklı bir karar verin — sonuç anında yeniden hesaplanır.");
            wiHint.style.fontSize    = 10;
            wiHint.style.color       = new Color(0.65f, 0.7f, 0.75f);
            wiHint.style.marginBottom = 8;
            wiHint.style.whiteSpace  = WhiteSpace.Normal;
            _whatIfPanel.Add(wiHint);

            // Event list (left side) + option list (right) in a row
            var wiBody = new VisualElement();
            wiBody.style.flexDirection = FlexDirection.Row;

            _whatIfEventList = new ScrollView();
            _whatIfEventList.style.flexBasis  = new Length(45, LengthUnit.Percent);
            _whatIfEventList.style.maxHeight  = 110;
            _whatIfEventList.style.marginRight = 8;

            _whatIfOptionList = new ScrollView();
            _whatIfOptionList.style.flexBasis = new Length(55, LengthUnit.Percent);
            _whatIfOptionList.style.maxHeight = 110;

            wiBody.Add(_whatIfEventList);
            wiBody.Add(_whatIfOptionList);
            _whatIfPanel.Add(wiBody);

            // Outcome + delta summary row
            var wiResultRow = new VisualElement();
            wiResultRow.style.flexDirection = FlexDirection.Row;
            wiResultRow.style.alignItems    = Align.Center;
            wiResultRow.style.marginTop     = 8;

            _whatIfOutcomeBadge = new Label();
            _whatIfOutcomeBadge.style.fontSize                = 12;
            _whatIfOutcomeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _whatIfOutcomeBadge.style.paddingLeft             = 10;
            _whatIfOutcomeBadge.style.paddingRight            = 10;
            _whatIfOutcomeBadge.style.paddingTop              = 4;
            _whatIfOutcomeBadge.style.paddingBottom           = 4;
            _whatIfOutcomeBadge.style.borderTopLeftRadius     = 4;
            _whatIfOutcomeBadge.style.borderTopRightRadius    = 4;
            _whatIfOutcomeBadge.style.borderBottomLeftRadius  = 4;
            _whatIfOutcomeBadge.style.borderBottomRightRadius = 4;
            _whatIfOutcomeBadge.style.marginRight             = 10;

            _whatIfDeltaLabel = new Label();
            _whatIfDeltaLabel.style.fontSize   = 11;
            _whatIfDeltaLabel.style.color      = new Color(0.9f, 0.9f, 0.75f);
            _whatIfDeltaLabel.style.flexGrow   = 1;
            _whatIfDeltaLabel.style.whiteSpace = WhiteSpace.Normal;

            wiResultRow.Add(_whatIfOutcomeBadge);
            wiResultRow.Add(_whatIfDeltaLabel);
            _whatIfPanel.Add(wiResultRow);

            root.Add(_whatIfPanel);

            // ── BOTTOM SUMMARY & ACTIONS BAR ──────────────────────────────────
            var footer = new VisualElement();
            footer.AddToClassList("apogee-panel");
            footer.style.paddingTop = 10;
            footer.style.paddingBottom = 10;
            footer.style.paddingLeft = 16;
            footer.style.paddingRight = 16;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;

            _finalNarrativeLabel = new Label("Rapor özeti yükleniyor...");
            _finalNarrativeLabel.style.fontSize = 12;
            _finalNarrativeLabel.style.color = new Color(0.85f, 0.85f, 0.9f);
            _finalNarrativeLabel.style.flexShrink = 1;
            _finalNarrativeLabel.style.maxWidth = new Length(65, LengthUnit.Percent);

            var buttonsBox = new VisualElement();
            buttonsBox.style.flexDirection = FlexDirection.Row;

            _workshopButton = new Button(OnWorkshopClicked) { text = "Tasarım Atölyesi" };
            _workshopButton.AddToClassList("apogee-button");
            _workshopButton.style.paddingLeft = 14;
            _workshopButton.style.paddingRight = 14;
            _workshopButton.style.paddingTop = 6;
            _workshopButton.style.paddingBottom = 6;
            _workshopButton.style.marginRight = 8;

            _campaignMapButton = new Button(OnCampaignMapClicked) { text = "Haritaya Dön" };
            _campaignMapButton.AddToClassList("apogee-button");
            _campaignMapButton.AddToClassList("apogee-button-primary");
            _campaignMapButton.style.paddingLeft = 16;
            _campaignMapButton.style.paddingRight = 16;
            _campaignMapButton.style.paddingTop = 6;
            _campaignMapButton.style.paddingBottom = 6;

            buttonsBox.Add(_workshopButton);
            buttonsBox.Add(_campaignMapButton);

            footer.Add(_finalNarrativeLabel);
            footer.Add(buttonsBox);
            root.Add(footer);

            return root;
        }

        protected override void OnShown(object context = null)
        {
            base.OnShown(context);

            if (ViewModel != null)
            {
                ViewModel.OnReportDataUpdated += RefreshView;
                ViewModel.WhatIf.OnWhatIfUpdated += RefreshWhatIf;

                if (context is MissionReportNavigationArgs args)
                {
                    ViewModel.BindReport(args.MissionId, args.Report);
                }
                else if (context is string missionId)
                {
                    ViewModel.BindReport(missionId);
                }
                else
                {
                    ViewModel.BindReport(null);
                }
            }

            RefreshView();
            RefreshWhatIf();
        }

        protected override void OnHidden()
        {
            if (ViewModel != null)
            {
                ViewModel.OnReportDataUpdated -= RefreshView;
                ViewModel.WhatIf.OnWhatIfUpdated -= RefreshWhatIf;
            }

            base.OnHidden();
        }

        private void RefreshView()
        {
            if (ViewModel == null) return;

            if (_missionTitleLabel != null)
                _missionTitleLabel.text = ViewModel.MissionName.Value;

            if (_destinationLabel != null)
                _destinationLabel.text = $"HEDEF: {ViewModel.DestinationText.Value}";

            if (_outcomeBadge != null)
            {
                _outcomeBadge.text = ViewModel.OverallResultBadgeText.Value;
                if (ViewModel.IsOverallSuccess.Value)
                {
                    _outcomeBadge.style.backgroundColor = new Color(0.1f, 0.45f, 0.2f, 0.9f);
                    _outcomeBadge.style.color = new Color(0.4f, 1.0f, 0.5f);
                }
                else
                {
                    _outcomeBadge.style.backgroundColor = new Color(0.55f, 0.15f, 0.15f, 0.9f);
                    _outcomeBadge.style.color = new Color(1.0f, 0.45f, 0.45f);
                }
            }

            if (_finalNarrativeLabel != null)
                _finalNarrativeLabel.text = ViewModel.FinalStatusNarrative.Value;

            if (_scienceDeliveryRatioLabel != null)
                _scienceDeliveryRatioLabel.text = ViewModel.ScienceDeliveryRatioText.Value;

            if (_scienceBottleneckLabel != null)
                _scienceBottleneckLabel.text = ViewModel.ScienceBottleneckNarrative.Value;

            if (_campaignImpactLabel != null)
                _campaignImpactLabel.text = ViewModel.CampaignImpactText.Value;

            if (_scoreLabel != null)
                _scoreLabel.text = $"SKOR: {ViewModel.FinalScoreText.Value}";

            if (_gradeBadge != null)
            {
                _gradeBadge.text = $"DERECELENDİRME: {ViewModel.GradeText.Value}";
                string grade = ViewModel.GradeText.Value;
                if (grade == "S" || grade == "A")
                {
                    _gradeBadge.style.color = new Color(0.3f, 0.95f, 0.4f);
                }
                else if (grade == "B" || grade == "C")
                {
                    _gradeBadge.style.color = new Color(0.95f, 0.8f, 0.2f);
                }
                else
                {
                    _gradeBadge.style.color = new Color(0.95f, 0.4f, 0.4f);
                }
            }

            if (_scorePillarsLabel != null)
                _scorePillarsLabel.text = ViewModel.ScorePillarsSummaryText.Value;

            // Render Criteria List
            if (_criteriaContainer != null)
            {
                _criteriaContainer.Clear();
                foreach (var item in ViewModel.CriteriaList)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 6;
                    row.style.paddingBottom = 4;
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.4f);

                    var mark = new Label(item.IsMet ? "OK": "X");
                    mark.style.fontSize = 14;
                    mark.style.unityFontStyleAndWeight = FontStyle.Bold;
                    mark.style.color = item.IsMet ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.3f, 0.3f);
                    mark.style.marginRight = 8;
                    mark.style.width = 16;

                    var textBox = new VisualElement();
                    textBox.style.flexGrow = 1;

                    var desc = new Label(item.Criterion.Description);
                    desc.style.fontSize = 11;
                    desc.style.whiteSpace = WhiteSpace.Normal;
                    desc.style.color = new Color(0.85f, 0.85f, 0.9f);

                    var sub = new Label(item.SummaryText);
                    sub.style.fontSize = 10;
                    sub.style.color = new Color(0.6f, 0.65f, 0.75f);

                    textBox.Add(desc);
                    textBox.Add(sub);

                    row.Add(mark);
                    row.Add(textBox);
                    _criteriaContainer.Add(row);
                }
            }

            // Render Cause-and-Effect Chain
            if (_causeChainContainer != null)
            {
                _causeChainContainer.Clear();
                int idx = 1;
                foreach (var node in ViewModel.CauseAndEffectNodes)
                {
                    var item = new VisualElement();
                    item.style.marginBottom = 8;
                    item.style.paddingLeft = 8;
                    item.style.borderLeftWidth = 3;
                    item.style.borderLeftColor = node.IsNegativeOrFault
                        ? new Color(0.9f, 0.3f, 0.3f, 0.9f)
                        : new Color(0.2f, 0.7f, 0.9f, 0.8f);

                    var headerRow = new VisualElement();
                    headerRow.style.flexDirection = FlexDirection.Row;
                    headerRow.style.justifyContent = Justify.SpaceBetween;

                    var title = new Label($"{idx++}. [{node.Phase}] {node.Title}");
                    title.style.fontSize = 11;
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                    title.style.color = node.IsNegativeOrFault ? new Color(1.0f, 0.5f, 0.5f) : new Color(0.8f, 0.9f, 1.0f);

                    headerRow.Add(title);
                    item.Add(headerRow);

                    var exp = new Label(node.Explanation);
                    exp.style.fontSize = 10;
                    exp.style.whiteSpace = WhiteSpace.Normal;
                    exp.style.color = new Color(0.75f, 0.8f, 0.85f);
                    exp.style.marginTop = 2;
                    item.Add(exp);

                    _causeChainContainer.Add(item);
                }
            }

            // Render Lessons Learned
            if (_lessonsContainer != null)
            {
                _lessonsContainer.Clear();
                foreach (var lesson in ViewModel.LessonsLearnedList)
                {
                    var item = new Label($"• {lesson}");
                    item.style.fontSize = 10;
                    item.style.whiteSpace = WhiteSpace.Normal;
                    item.style.color = new Color(0.85f, 0.85f, 0.75f);
                    item.style.marginBottom = 4;
                    _lessonsContainer.Add(item);
                }
            }

            // Render Medals (M5-08)
            if (_medalsContainer != null)
            {
                _medalsContainer.Clear();
                if (ViewModel.AwardedMedalsList.Count == 0)
                {
                    var none = new Label("Bu görevde özel madalya kazanılamadı.");
                    none.style.fontSize = 10;
                    none.style.color = new Color(0.6f, 0.65f, 0.7f);
                    _medalsContainer.Add(none);
                }
                else
                {
                    foreach (var medal in ViewModel.AwardedMedalsList)
                    {
                        var row = new VisualElement();
                        row.style.marginBottom = 4;
                        row.style.paddingLeft = 4;

                        var loc = LocalizationManager.Instance;
                        var rawTitle = medal.Definition.TitleKey;
                        var titleText = loc.Get(rawTitle);
                        if (string.IsNullOrEmpty(titleText) || titleText == $"?{rawTitle}")
                        {
                            titleText = rawTitle;
                        }

                        var mTitle = new Label($"{titleText} (+{medal.Definition.BonusScore:F0})");
                        mTitle.style.fontSize = 11;
                        mTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                        mTitle.style.color = new Color(1.0f, 0.85f, 0.3f);

                        var descText = medal.ReasonSummary;
                        if (string.IsNullOrEmpty(descText))
                        {
                            var rawDesc = medal.Definition.DescriptionKey;
                            descText = loc.Get(rawDesc);
                            if (string.IsNullOrEmpty(descText) || descText == $"?{rawDesc}")
                            {
                                descText = rawDesc;
                            }
                        }

                        var mDesc = new Label(descText);
                        mDesc.style.fontSize = 9;
                        mDesc.style.color = new Color(0.8f, 0.85f, 0.85f);
                        mDesc.style.whiteSpace = WhiteSpace.Normal;

                        row.Add(mTitle);
                        row.Add(mDesc);
                        _medalsContainer.Add(row);
                    }
                }
            }
        }

        private void OnWorkshopClicked()
        {
            ViewModel?.ReturnToWorkshopCommand?.Execute();
        }

        private void OnCampaignMapClicked()
        {
            ViewModel?.ReturnToMapCommand?.Execute();
        }

        private void OnWhatIfClear()
        {
            ViewModel?.WhatIf?.ClearWhatIf();
        }

        // ── What-If panel refresh ────────────────────────────────────────────
        private void RefreshWhatIf()
        {
            if (ViewModel?.WhatIf == null) return;
            var wi = ViewModel.WhatIf;

            // Show/hide panel based on eligible events
            if (_whatIfPanel != null)
                _whatIfPanel.style.display = wi.HasEligibleEvents.Value ? DisplayStyle.Flex : DisplayStyle.None;

            // Clear button state
            if (_whatIfClearButton != null)
                _whatIfClearButton.SetEnabled(wi.IsWhatIfActive.Value);

            // Render eligible event list
            if (_whatIfEventList != null)
            {
                _whatIfEventList.Clear();

                if (!wi.HasEligibleEvents.Value)
                {
                    var noEvt = new Label("Bu misyonda yeniden değerlendirilebilecek karar kartı yok.");
                    noEvt.style.fontSize   = 10;
                    noEvt.style.color      = new Color(0.55f, 0.6f, 0.65f);
                    noEvt.style.whiteSpace = WhiteSpace.Normal;
                    _whatIfEventList.Add(noEvt);
                }
                else
                {
                    var evtTitle = new Label("Karar Seçin:");
                    evtTitle.style.fontSize                = 10;
                    evtTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    evtTitle.style.color                   = new Color(0.7f, 0.75f, 0.8f);
                    evtTitle.style.marginBottom            = 4;
                    _whatIfEventList.Add(evtTitle);

                    foreach (var ev in wi.EligibleEvents)
                    {
                        var localEv = ev;
                        bool isSelected = wi.SelectedEvent?.InstanceId == ev.InstanceId;

                        var btn = new Button(() => { wi.SelectEvent(localEv); RefreshWhatIf(); });
                        btn.text                   = ev.Record?.TitleKey ?? ev.Record?.EventId ?? ev.InstanceId;
                        btn.style.fontSize         = 10;
                        btn.style.marginBottom     = 3;
                        btn.style.paddingTop       = 4;
                        btn.style.paddingBottom    = 4;
                        btn.style.unityTextAlign   = TextAnchor.MiddleLeft;
                        btn.style.backgroundColor  = isSelected
                            ? new Color(0.15f, 0.3f, 0.5f, 0.8f)
                            : new Color(0.08f, 0.1f, 0.15f, 0.6f);
                        if (isSelected)
                        {
                            btn.style.borderLeftWidth = 2;
                            btn.style.borderLeftColor = new Color(0.3f, 0.7f, 1.0f);
                        }
                        _whatIfEventList.Add(btn);
                    }
                }
            }

            // Render alternative options for the selected event
            if (_whatIfOptionList != null)
            {
                _whatIfOptionList.Clear();

                if (wi.SelectedEvent == null)
                {
                    var hint = new Label("<- Soldaki listeden bir karar seçin.");
                    hint.style.fontSize   = 10;
                    hint.style.color      = new Color(0.5f, 0.55f, 0.6f);
                    hint.style.whiteSpace = WhiteSpace.Normal;
                    _whatIfOptionList.Add(hint);
                }
                else
                {
                    var origOptId = wi.SelectedEvent.ChosenOptionId;
                    var origOpt   = wi.SelectedEvent.Record?.Options?
                        .FirstOrDefault(o => o.OptionId == origOptId);

                    var origRow = new VisualElement();
                    origRow.style.flexDirection = FlexDirection.Row;
                    origRow.style.marginBottom  = 4;

                    var origMark = new Label("OK Seçilen: ");
                    origMark.style.fontSize    = 10;
                    origMark.style.color       = new Color(0.5f, 0.75f, 0.5f);
                    origMark.style.marginRight = 4;

                    var origLabel = new Label(origOpt?.LabelKey ?? origOptId ?? "(Bilinmiyor)");
                    origLabel.style.fontSize = 10;
                    origLabel.style.color    = new Color(0.75f, 0.75f, 0.75f);

                    origRow.Add(origMark);
                    origRow.Add(origLabel);
                    _whatIfOptionList.Add(origRow);

                    var sep = new VisualElement();
                    sep.style.height          = 1;
                    sep.style.backgroundColor = new Color(0.2f, 0.25f, 0.3f, 0.5f);
                    sep.style.marginBottom    = 4;
                    _whatIfOptionList.Add(sep);

                    var altTitle = new Label("Alternatif seçenek:");
                    altTitle.style.fontSize     = 10;
                    altTitle.style.color        = new Color(0.7f, 0.75f, 0.8f);
                    altTitle.style.marginBottom = 4;
                    _whatIfOptionList.Add(altTitle);

                    foreach (var opt in wi.AlternativeOptions)
                    {
                        var localOpt = opt;
                        bool isChosen = wi.SelectedAlternativeOptionId == opt.OptionId;

                        var optBtn = new Button(() => { wi.RunWhatIf(localOpt.OptionId); });
                        optBtn.text                   = opt.LabelKey ?? opt.OptionId;
                        optBtn.style.fontSize         = 10;
                        optBtn.style.marginBottom     = 3;
                        optBtn.style.paddingTop       = 4;
                        optBtn.style.paddingBottom    = 4;
                        optBtn.style.unityTextAlign   = TextAnchor.MiddleLeft;
                        optBtn.style.backgroundColor  = isChosen
                            ? new Color(0.25f, 0.2f, 0.08f, 0.9f)
                            : new Color(0.08f, 0.1f, 0.15f, 0.6f);
                        if (isChosen)
                        {
                            optBtn.style.borderLeftWidth = 2;
                            optBtn.style.borderLeftColor = new Color(0.95f, 0.8f, 0.2f);
                        }

                        var conseq = new Label(opt.VisibleConsequenceKey ?? string.Empty);
                        conseq.style.fontSize   = 9;
                        conseq.style.color      = new Color(0.5f, 0.55f, 0.6f);
                        conseq.style.whiteSpace = WhiteSpace.Normal;
                        conseq.style.marginBottom = 2;

                        var optWrapper = new VisualElement();
                        optWrapper.Add(optBtn);
                        optWrapper.Add(conseq);
                        _whatIfOptionList.Add(optWrapper);
                    }
                }
            }

            // Render what-if result badges
            if (_whatIfOutcomeBadge != null)
            {
                bool showBadge = wi.IsWhatIfActive.Value && !string.IsNullOrEmpty(wi.WhatIfOutcomeBadge.Value);
                _whatIfOutcomeBadge.style.display = showBadge ? DisplayStyle.Flex : DisplayStyle.None;

                if (showBadge)
                {
                    _whatIfOutcomeBadge.text = wi.WhatIfOutcomeBadge.Value;
                    if (wi.WhatIfOutcomeChanged.Value)
                    {
                        _whatIfOutcomeBadge.style.backgroundColor = new Color(0.4f, 0.3f, 0.05f, 0.9f);
                        _whatIfOutcomeBadge.style.color           = new Color(1.0f, 0.85f, 0.2f);
                    }
                    else
                    {
                        _whatIfOutcomeBadge.style.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 0.7f);
                        _whatIfOutcomeBadge.style.color           = new Color(0.65f, 0.7f, 0.75f);
                    }
                }
            }

            if (_whatIfDeltaLabel != null)
            {
                _whatIfDeltaLabel.text = wi.IsWhatIfActive.Value
                    ? wi.WhatIfDeltaSummary.Value
                    : (wi.SelectedEvent != null ? "Alternatif seçeneklerden birini tıklayın." : string.Empty);
            }
        }
    }
}
