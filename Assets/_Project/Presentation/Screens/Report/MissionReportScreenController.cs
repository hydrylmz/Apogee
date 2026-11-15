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

        // Header visuals
        private VisualElement _destinationPhoto;
        private VisualElement _gradeCircle;
        private Label _gradeCircleLetter;

        // Science telemetry
        private Label _scienceDeliveryRatioLabel;
        private Label _scienceBottleneckLabel;
        private VisualElement _scienceBarTrack;
        private VisualElement _scienceBarFill;
        private Label _scienceBarPctLabel;

        // Dynamic lists
        private VisualElement _criteriaContainer;
        private VisualElement _causeChainContainer;
        private VisualElement _lessonsContainer;

        // ── Scoring & Medals (M5-08) ─────────────────────────────────────────
        private Label _gradeBadge;
        private Label _scoreLabel;
        private VisualElement _scorePillarsContainer;
        private Label _difficultyLabel;
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
            header.style.paddingTop = 14;
            header.style.paddingBottom = 14;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.marginBottom = 12;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            // Left: destination photo + mission titles
            var headerLeft = new VisualElement();
            headerLeft.style.flexDirection = FlexDirection.Row;
            headerLeft.style.alignItems = Align.Center;
            headerLeft.style.flexShrink = 1;

            _destinationPhoto = new VisualElement
            {
                style =
                {
                    width = 84,
                    height = 84,
                    marginRight = 14,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    backgroundSize = new BackgroundSize(BackgroundSizeType.Cover),
                    overflow = Overflow.Hidden,
                    borderTopWidth = 1,
                    borderRightWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderTopColor = new Color(0.25f, 0.45f, 0.7f, 0.8f),
                    borderRightColor = new Color(0.25f, 0.45f, 0.7f, 0.8f),
                    borderBottomColor = new Color(0.25f, 0.45f, 0.7f, 0.8f),
                    borderLeftColor = new Color(0.25f, 0.45f, 0.7f, 0.8f)
                }
            };
            headerLeft.Add(_destinationPhoto);

            var titleBox = new VisualElement();
            _missionTitleLabel = new Label("MİSYON SONUÇ RAPORU");
            _missionTitleLabel.style.fontSize = 22;
            _missionTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _missionTitleLabel.style.color = new Color(0.92f, 0.96f, 1.0f);

            _destinationLabel = new Label("HEDEF: -");
            _destinationLabel.style.fontSize = 12;
            _destinationLabel.style.color = new Color(0.55f, 0.75f, 0.95f);
            _destinationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _destinationLabel.style.marginTop = 4;
            _destinationLabel.style.letterSpacing = 1;

            titleBox.Add(_missionTitleLabel);
            titleBox.Add(_destinationLabel);
            headerLeft.Add(titleBox);

            // Right: big score + grade circle + outcome badge
            var rightBox = new VisualElement();
            rightBox.style.flexDirection = FlexDirection.Row;
            rightBox.style.alignItems = Align.Center;

            var scoreBox = new VisualElement();
            scoreBox.style.alignItems = Align.FlexEnd;
            scoreBox.style.marginRight = 14;

            var scoreCaption = new Label("SKOR");
            scoreCaption.style.fontSize = 10;
            scoreCaption.style.color = new Color(0.6f, 0.65f, 0.75f);
            scoreCaption.style.unityTextAlign = TextAnchor.MiddleRight;

            _scoreLabel = new Label("0");
            _scoreLabel.style.fontSize = 30;
            _scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreLabel.style.color = new Color(0.95f, 0.8f, 0.2f);
            _scoreLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            _gradeBadge = new Label("DERECELENDİRME: -");
            _gradeBadge.style.fontSize = 11;
            _gradeBadge.style.color = new Color(0.7f, 0.75f, 0.85f);
            _gradeBadge.style.unityTextAlign = TextAnchor.MiddleRight;

            scoreBox.Add(scoreCaption);
            scoreBox.Add(_scoreLabel);
            scoreBox.Add(_gradeBadge);

            // Grade letter in a colored ring (S/A/B/C/D/F)
            _gradeCircle = new VisualElement
            {
                style =
                {
                    width = 52,
                    height = 52,
                    marginRight = 14,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    borderTopLeftRadius = 26,
                    borderTopRightRadius = 26,
                    borderBottomLeftRadius = 26,
                    borderBottomRightRadius = 26,
                    backgroundColor = new Color(0.55f, 0.15f, 0.15f, 0.35f),
                    borderTopWidth = 3,
                    borderRightWidth = 3,
                    borderBottomWidth = 3,
                    borderLeftWidth = 3,
                    borderTopColor = new Color(0.95f, 0.3f, 0.3f),
                    borderRightColor = new Color(0.95f, 0.3f, 0.3f),
                    borderBottomColor = new Color(0.95f, 0.3f, 0.3f),
                    borderLeftColor = new Color(0.95f, 0.3f, 0.3f)
                }
            };
            _gradeCircleLetter = new Label("-")
            {
                style =
                {
                    fontSize = 24,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.95f, 0.4f, 0.4f)
                }
            };
            _gradeCircle.Add(_gradeCircleLetter);

            _outcomeBadge = new Label("DEĞERLENDİRİLİYOR");
            _outcomeBadge.style.fontSize = 14;
            _outcomeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _outcomeBadge.style.paddingLeft = 14;
            _outcomeBadge.style.paddingRight = 14;
            _outcomeBadge.style.paddingTop = 8;
            _outcomeBadge.style.paddingBottom = 8;
            _outcomeBadge.style.borderTopLeftRadius = 6;
            _outcomeBadge.style.borderTopRightRadius = 6;
            _outcomeBadge.style.borderBottomLeftRadius = 6;
            _outcomeBadge.style.borderBottomRightRadius = 6;

            rightBox.Add(scoreBox);
            rightBox.Add(_gradeCircle);
            rightBox.Add(_outcomeBadge);

            header.Add(headerLeft);
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
            colLeft.style.paddingTop = 14;
            colLeft.style.paddingBottom = 14;
            colLeft.style.paddingLeft = 16;
            colLeft.style.paddingRight = 16;

            colLeft.Add(CreateSectionTitle("1. MİSYON BAŞARI KRİTERLERİ"));

            _criteriaContainer = new ScrollView();
            _criteriaContainer.style.maxHeight = 220;
            _criteriaContainer.style.marginBottom = 14;
            colLeft.Add(_criteriaContainer);

            colLeft.Add(CreateSectionTitle("2. BİLİM VE VERİ TESLİMİ"));

            // Delivery ratio progress bar (downlinked / generated)
            _scienceBarTrack = new VisualElement
            {
                style =
                {
                    height = 12,
                    flexGrow = 1,
                    marginBottom = 4,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    backgroundColor = new Color(0.08f, 0.12f, 0.20f, 0.9f),
                    overflow = Overflow.Hidden
                }
            };
            _scienceBarFill = new VisualElement
            {
                style =
                {
                    height = 12,
                    width = new Length(0, LengthUnit.Percent),
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    backgroundColor = new Color(0.2f, 0.85f, 0.95f)
                }
            };
            _scienceBarTrack.Add(_scienceBarFill);

            _scienceBarPctLabel = new Label("%0");
            _scienceBarPctLabel.style.fontSize = 11;
            _scienceBarPctLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scienceBarPctLabel.style.color = new Color(0.2f, 0.85f, 0.95f);

            var scienceBarRow = new VisualElement();
            scienceBarRow.style.flexDirection = FlexDirection.Row;
            scienceBarRow.style.alignItems = Align.Center;
            scienceBarRow.style.marginBottom = 4;
            scienceBarRow.Add(_scienceBarTrack);
            scienceBarRow.Add(_scienceBarPctLabel);
            colLeft.Add(scienceBarRow);

            _scienceDeliveryRatioLabel = new Label("0 GB / 0 GB");
            _scienceDeliveryRatioLabel.style.fontSize = 13;
            _scienceDeliveryRatioLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scienceDeliveryRatioLabel.style.color = new Color(0.2f, 0.85f, 0.95f);
            _scienceDeliveryRatioLabel.style.marginBottom = 6;
            colLeft.Add(_scienceDeliveryRatioLabel);

            _scienceBottleneckLabel = new Label();
            _scienceBottleneckLabel.style.fontSize = 11;
            _scienceBottleneckLabel.style.whiteSpace = WhiteSpace.Normal;
            _scienceBottleneckLabel.style.color = new Color(0.78f, 0.82f, 0.88f);
            colLeft.Add(_scienceBottleneckLabel);

            body.Add(colLeft);

            // Column 2: Cause-and-Effect Chain (Center, 38%)
            var colCenter = new VisualElement();
            colCenter.style.flexBasis = new Length(38, LengthUnit.Percent);
            colCenter.style.flexGrow = 1;
            colCenter.style.marginRight = 8;
            colCenter.AddToClassList("apogee-panel");
            colCenter.style.paddingTop = 14;
            colCenter.style.paddingBottom = 14;
            colCenter.style.paddingLeft = 16;
            colCenter.style.paddingRight = 16;

            colCenter.Add(CreateSectionTitle("3. NEDEN - SONUÇ ZİNCİRİ"));

            _causeChainContainer = new ScrollView();
            _causeChainContainer.style.flexGrow = 1;
            colCenter.Add(_causeChainContainer);

            body.Add(colCenter);

            // Column 3: Lessons Learned & Campaign Impact (Right, 27%)
            var colRight = new VisualElement();
            colRight.style.flexBasis = new Length(27, LengthUnit.Percent);
            colRight.style.flexGrow = 1;
            colRight.AddToClassList("apogee-panel");
            colRight.style.paddingTop = 14;
            colRight.style.paddingBottom = 14;
            colRight.style.paddingLeft = 16;
            colRight.style.paddingRight = 16;

            colRight.Add(CreateSectionTitle("4. ÖĞRENİLEN DERSLER"));

            _lessonsContainer = new ScrollView();
            _lessonsContainer.style.maxHeight = 170;
            _lessonsContainer.style.marginBottom = 12;
            colRight.Add(_lessonsContainer);

            colRight.Add(CreateSectionTitle("5. KAMPANYA ETKİSİ"));

            _campaignImpactLabel = new Label();
            _campaignImpactLabel.style.fontSize = 12;
            _campaignImpactLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _campaignImpactLabel.style.color = new Color(0.95f, 0.75f, 0.2f);
            _campaignImpactLabel.style.whiteSpace = WhiteSpace.Normal;
            _campaignImpactLabel.style.marginBottom = 12;
            colRight.Add(_campaignImpactLabel);

            colRight.Add(CreateSectionTitle("6. KAZANILAN MADALYALAR"));

            _medalsContainer = new ScrollView();
            _medalsContainer.style.maxHeight = 120;
            _medalsContainer.style.marginBottom = 8;
            colRight.Add(_medalsContainer);

            // Score pillar mini bar chart (science/objective/resource/decision/planning)
            _scorePillarsContainer = new VisualElement();
            colRight.Add(_scorePillarsContainer);

            _difficultyLabel = new Label();
            _difficultyLabel.style.fontSize = 10;
            _difficultyLabel.style.color = new Color(0.6f, 0.65f, 0.75f);
            _difficultyLabel.style.marginTop = 4;
            colRight.Add(_difficultyLabel);

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
            _finalNarrativeLabel.style.paddingLeft = 10;
            _finalNarrativeLabel.style.borderLeftWidth = 3;
            _finalNarrativeLabel.style.borderLeftColor = new Color(0.95f, 0.8f, 0.2f, 0.7f);

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

            // Destination photo (Resources/Destinations/{id}.jpg — graceful fallback hides the element)
            if (_destinationPhoto != null)
            {
                var tex = LoadDestinationTexture(ViewModel.DestinationText.Value);
                if (tex != null)
                {
                    _destinationPhoto.style.display = DisplayStyle.Flex;
                    _destinationPhoto.style.backgroundImage = new StyleBackground(tex);
                }
                else
                {
                    _destinationPhoto.style.display = DisplayStyle.None;
                }
            }

            // Grade ring color + letter
            if (_gradeCircle != null && _gradeCircleLetter != null)
            {
                string grade = ViewModel.GradeText.Value;
                _gradeCircleLetter.text = string.IsNullOrEmpty(grade) ? "-" : grade;
                Color ring = GetGradeColor(grade, out Color fill);
                _gradeCircle.style.borderTopColor = ring;
                _gradeCircle.style.borderRightColor = ring;
                _gradeCircle.style.borderBottomColor = ring;
                _gradeCircle.style.borderLeftColor = ring;
                _gradeCircle.style.backgroundColor = fill;
                _gradeCircleLetter.style.color = ring;
            }

            // Science delivery bar
            if (_scienceBarFill != null && _scienceBarPctLabel != null)
            {
                double ratio = ViewModel.ScienceDeliveryRatioValue.Value;
                _scienceBarFill.style.width = new Length((float)(ratio * 100.0), LengthUnit.Percent);
                _scienceBarPctLabel.text = $"%{ratio * 100.0:F0}";
            }

            if (_scienceDeliveryRatioLabel != null)
                _scienceDeliveryRatioLabel.text = ViewModel.ScienceDeliveryRatioText.Value;

            if (_scienceBottleneckLabel != null)
                _scienceBottleneckLabel.text = ViewModel.ScienceBottleneckNarrative.Value;

            if (_campaignImpactLabel != null)
                _campaignImpactLabel.text = ViewModel.CampaignImpactText.Value;

            if (_scoreLabel != null)
                _scoreLabel.text = ViewModel.FinalScoreText.Value;

            if (_gradeBadge != null)
            {
                _gradeBadge.text = $"DERECELENDİRME: {ViewModel.GradeText.Value}";
                _gradeBadge.style.color = GetGradeColor(ViewModel.GradeText.Value, out _);
            }

            RenderScorePillars();

            // Render Criteria List (circle badges: filled dot = met, X = missed)
            if (_criteriaContainer != null)
            {
                _criteriaContainer.Clear();
                foreach (var item in ViewModel.CriteriaList)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 6;
                    row.style.paddingBottom = 6;
                    row.style.borderBottomWidth = 1;
                    row.style.borderBottomColor = new Color(0.2f, 0.25f, 0.35f, 0.4f);

                    // Badge: green ring with filled dot when met; red disc with X when missed
                    VisualElement badge = new VisualElement
                    {
                        style =
                        {
                            width = 18,
                            height = 18,
                            marginRight = 10,
                            justifyContent = Justify.Center,
                            alignItems = Align.Center,
                            borderTopLeftRadius = 9,
                            borderTopRightRadius = 9,
                            borderBottomLeftRadius = 9,
                            borderBottomRightRadius = 9
                        }
                    };
                    if (item.IsMet)
                    {
                        badge.style.backgroundColor = new StyleColor(new Color(0.12f, 0.35f, 0.18f, 0.9f));
                        badge.style.borderTopWidth = 2;
                        badge.style.borderRightWidth = 2;
                        badge.style.borderBottomWidth = 2;
                        badge.style.borderLeftWidth = 2;
                        badge.style.borderTopColor = new StyleColor(new Color(0.3f, 0.9f, 0.4f));
                        badge.style.borderRightColor = new StyleColor(new Color(0.3f, 0.9f, 0.4f));
                        badge.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.9f, 0.4f));
                        badge.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.9f, 0.4f));
                        var dot = new VisualElement
                        {
                            style =
                            {
                                width = 8,
                                height = 8,
                                borderTopLeftRadius = 4,
                                borderTopRightRadius = 4,
                                borderBottomLeftRadius = 4,
                                borderBottomRightRadius = 4,
                                backgroundColor = new StyleColor(new Color(0.3f, 0.95f, 0.45f))
                            }
                        };
                        badge.Add(dot);
                    }
                    else
                    {
                        badge.style.backgroundColor = new StyleColor(new Color(0.55f, 0.15f, 0.15f, 0.9f));
                        var markX = new Label("X")
                        {
                            style =
                            {
                                fontSize = 10,
                                unityFontStyleAndWeight = FontStyle.Bold,
                                color = new StyleColor(new Color(1.0f, 0.6f, 0.6f))
                            }
                        };
                        badge.Add(markX);
                    }

                    var textBox = new VisualElement();
                    textBox.style.flexGrow = 1;

                    var desc = new Label(item.Criterion.Description);
                    desc.style.fontSize = 11;
                    desc.style.whiteSpace = WhiteSpace.Normal;
                    desc.style.color = item.IsMet ? new Color(0.85f, 0.9f, 0.92f) : new Color(0.75f, 0.78f, 0.85f);

                    var sub = new Label(item.SummaryText);
                    sub.style.fontSize = 10;
                    sub.style.color = item.IsMet ? new Color(0.45f, 0.85f, 0.55f) : new Color(0.95f, 0.5f, 0.5f);
                    sub.style.marginTop = 2;

                    textBox.Add(desc);
                    textBox.Add(sub);

                    row.Add(badge);
                    row.Add(textBox);
                    _criteriaContainer.Add(row);
                }
            }

            // Render Cause-and-Effect Chain (vertical timeline with numbered nodes + phase chips)
            if (_causeChainContainer != null)
            {
                _causeChainContainer.Clear();
                var nodes = ViewModel.CauseAndEffectNodes;
                int idx = 1;
                int total = nodes.Count;

                foreach (var node in nodes)
                {
                    bool isFault = node.IsNegativeOrFault;
                    Color accent = isFault
                        ? new Color(0.9f, 0.3f, 0.3f, 0.95f)
                        : new Color(0.2f, 0.7f, 0.9f, 0.9f);

                    var item = new VisualElement();
                    item.style.flexDirection = FlexDirection.Row;
                    item.style.marginBottom = 2;

                    // ── Timeline column: number disc + connecting line ──
                    var timelineCol = new VisualElement();
                    timelineCol.style.width = 26;
                    timelineCol.style.alignItems = Align.Center;

                    if (idx > 1)
                    {
                        var lineTop = new VisualElement
                        {
                            style =
                            {
                                width = 2,
                                flexGrow = 1,
                                backgroundColor = new StyleColor(new Color(0.3f, 0.38f, 0.5f, 0.5f))
                            }
                        };
                        timelineCol.Add(lineTop);
                    }
                    else
                    {
                        var gapTop = new VisualElement { style = { height = 6, flexGrow = 0 } };
                        timelineCol.Add(gapTop);
                    }

                    var numDisc = new VisualElement
                    {
                        style =
                        {
                            width = 22,
                            height = 22,
                            justifyContent = Justify.Center,
                            alignItems = Align.Center,
                            borderTopLeftRadius = 11,
                            borderTopRightRadius = 11,
                            borderBottomLeftRadius = 11,
                            borderBottomRightRadius = 11,
                            backgroundColor = new StyleColor(new Color(0.07f, 0.1f, 0.16f, 0.95f)),
                            borderTopWidth = 2,
                            borderRightWidth = 2,
                            borderBottomWidth = 2,
                            borderLeftWidth = 2,
                            borderTopColor = new StyleColor(accent),
                            borderRightColor = new StyleColor(accent),
                            borderBottomColor = new StyleColor(accent),
                            borderLeftColor = new StyleColor(accent)
                        }
                    };
                    var numLabel = new Label(idx.ToString())
                    {
                        style =
                        {
                            fontSize = 11,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            color = new StyleColor(accent)
                        }
                    };
                    numDisc.Add(numLabel);
                    timelineCol.Add(numDisc);

                    if (idx < total)
                    {
                        var lineBottom = new VisualElement
                        {
                            style =
                            {
                                width = 2,
                                flexGrow = 1,
                                minHeight = 14,
                                backgroundColor = new StyleColor(new Color(0.3f, 0.38f, 0.5f, 0.5f))
                            }
                        };
                        timelineCol.Add(lineBottom);
                    }

                    item.Add(timelineCol);

                    // ── Content column: phase chip + title + explanation ──
                    var content = new VisualElement();
                    content.style.flexGrow = 1;
                    content.style.marginBottom = 10;
                    content.style.paddingLeft = 8;

                    var headerRow = new VisualElement();
                    headerRow.style.flexDirection = FlexDirection.Row;
                    headerRow.style.alignItems = Align.Center;
                    headerRow.style.marginBottom = 3;

                    var phaseChip = new Label(node.Phase.ToUpperInvariant())
                    {
                        style =
                        {
                            fontSize = 9,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            letterSpacing = 1,
                            paddingLeft = 6,
                            paddingRight = 6,
                            paddingTop = 2,
                            paddingBottom = 2,
                            marginRight = 8,
                            borderTopLeftRadius = 3,
                            borderTopRightRadius = 3,
                            borderBottomLeftRadius = 3,
                            borderBottomRightRadius = 3,
                            color = new StyleColor(Color.white),
                            backgroundColor = new StyleColor(GetPhaseColor(node.Phase, isFault))
                        }
                    };

                    var title = new Label(node.Title);
                    title.style.fontSize = 12;
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                    title.style.color = isFault ? new Color(1.0f, 0.5f, 0.5f) : new Color(0.8f, 0.9f, 1.0f);

                    headerRow.Add(phaseChip);
                    headerRow.Add(title);
                    content.Add(headerRow);

                    var exp = new Label(node.Explanation);
                    exp.style.fontSize = 10;
                    exp.style.whiteSpace = WhiteSpace.Normal;
                    exp.style.color = new Color(0.72f, 0.78f, 0.85f);
                    content.Add(exp);

                    item.Add(content);
                    _causeChainContainer.Add(item);
                    idx++;
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

            // Render Medals (M5-08) — gold badge chips with bonus score
            if (_medalsContainer != null)
            {
                _medalsContainer.Clear();
                if (ViewModel.AwardedMedalsList.Count == 0)
                {
                    var none = new Label("Bu görevde özel madalya kazanılamadı.");
                    none.style.fontSize = 10;
                    none.style.color = new Color(0.6f, 0.65f, 0.7f);
                    none.style.whiteSpace = WhiteSpace.Normal;
                    _medalsContainer.Add(none);
                }
                else
                {
                    foreach (var medal in ViewModel.AwardedMedalsList)
                    {
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 6;

                        // Gold disc with bonus points (font-safe, no emoji glyphs)
                        var disc = new VisualElement
                        {
                            style =
                            {
                                width = 26,
                                height = 26,
                                marginRight = 8,
                                justifyContent = Justify.Center,
                                alignItems = Align.Center,
                                borderTopLeftRadius = 13,
                                borderTopRightRadius = 13,
                                borderBottomLeftRadius = 13,
                                borderBottomRightRadius = 13,
                                backgroundColor = new StyleColor(new Color(0.42f, 0.30f, 0.05f, 0.9f)),
                                borderTopWidth = 2,
                                borderRightWidth = 2,
                                borderBottomWidth = 2,
                                borderLeftWidth = 2,
                                borderTopColor = new StyleColor(new Color(1.0f, 0.85f, 0.3f)),
                                borderRightColor = new StyleColor(new Color(1.0f, 0.85f, 0.3f)),
                                borderBottomColor = new StyleColor(new Color(1.0f, 0.85f, 0.3f)),
                                borderLeftColor = new StyleColor(new Color(1.0f, 0.85f, 0.3f))
                            }
                        };
                        var bonusText = new Label($"+{medal.Definition.BonusScore:F0}")
                        {
                            style =
                            {
                                fontSize = 8,
                                unityFontStyleAndWeight = FontStyle.Bold,
                                color = new StyleColor(new Color(1.0f, 0.9f, 0.5f))
                            }
                        };
                        disc.Add(bonusText);

                        var loc = LocalizationManager.Instance;
                        var rawTitle = medal.Definition.TitleKey;
                        var titleText = loc.Get(rawTitle);
                        if (string.IsNullOrEmpty(titleText) || titleText == $"?{rawTitle}")
                        {
                            titleText = rawTitle;
                        }

                        var textBox = new VisualElement();
                        textBox.style.flexGrow = 1;

                        var mTitle = new Label(titleText);
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

                        textBox.Add(mTitle);
                        textBox.Add(mDesc);

                        row.Add(disc);
                        row.Add(textBox);
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

        // ── Visual helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Section header with a golden accent bar on the left edge.
        /// </summary>
        private static VisualElement CreateSectionTitle(string text)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var accent = new VisualElement
            {
                style =
                {
                    width = 4,
                    height = 14,
                    marginRight = 8,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    backgroundColor = new StyleColor(new Color(0.95f, 0.75f, 0.2f))
                }
            };

            var label = new Label(text)
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 1,
                    color = new StyleColor(new Color(0.82f, 0.87f, 0.92f))
                }
            };

            row.Add(accent);
            row.Add(label);
            return row;
        }

        /// <summary>
        /// Renders the five score pillars as a labeled horizontal mini bar chart
        /// (replaces the flat "Bilim: %x | Amaç: %y ..." text line).
        /// </summary>
        private void RenderScorePillars()
        {
            if (_scorePillarsContainer == null) return;
            _scorePillarsContainer.Clear();

            AddPillarBar(_scorePillarsContainer, "BİLİM",    ViewModel.PillarScienceValue.Value);
            AddPillarBar(_scorePillarsContainer, "AMAÇ",     ViewModel.PillarObjectiveValue.Value);
            AddPillarBar(_scorePillarsContainer, "KAYNAK",   ViewModel.PillarResourceValue.Value);
            AddPillarBar(_scorePillarsContainer, "KARAR",    ViewModel.PillarDecisionValue.Value);
            AddPillarBar(_scorePillarsContainer, "PLAN",     ViewModel.PillarPlanningValue.Value);

            if (_difficultyLabel != null)
                _difficultyLabel.text = $"Zorluk Çarpanı: {ViewModel.DifficultyMultiplierValue.Value:F1}x";
        }

        /// <summary>
        /// One labeled progress bar row: caption, track with colored fill, percent value.
        /// Fill color encodes performance: red (&lt;35%), amber (&lt;70%), green (≥70%).
        /// </summary>
        private static void AddPillarBar(VisualElement container, string caption, double value)
        {
            value = Math.Clamp(value, 0.0, 1.0);
            Color fill = value < 0.35
                ? new Color(0.9f, 0.32f, 0.32f)
                : value < 0.70
                    ? new Color(0.95f, 0.78f, 0.25f)
                    : new Color(0.35f, 0.9f, 0.45f);

            var row = new VisualElement();
            row.style.marginBottom = 5;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.marginBottom = 2;

            var cap = new Label(caption)
            {
                style =
                {
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 1,
                    color = new StyleColor(new Color(0.62f, 0.68f, 0.76f))
                }
            };
            var pct = new Label($"%{value * 100.0:F0}")
            {
                style =
                {
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(fill)
                }
            };
            headerRow.Add(cap);
            headerRow.Add(pct);

            var track = new VisualElement
            {
                style =
                {
                    height = 7,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    backgroundColor = new StyleColor(new Color(0.08f, 0.12f, 0.20f, 0.9f)),
                    overflow = Overflow.Hidden
                }
            };
            var barFill = new VisualElement
            {
                style =
                {
                    height = 7,
                    width = new Length((float)(value * 100.0), LengthUnit.Percent),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    backgroundColor = new StyleColor(fill)
                }
            };
            track.Add(barFill);

            row.Add(headerRow);
            row.Add(track);
            container.Add(row);
        }

        /// <summary>
        /// Grade color scale: S gold, A green, B/C amber, D orange, F red.
        /// Returns the ring/letter color; writes the soft disc fill color.
        /// </summary>
        private static Color GetGradeColor(string grade, out Color fill)
        {
            switch (grade)
            {
                case "S":
                    fill = new Color(0.45f, 0.33f, 0.05f, 0.4f);
                    return new Color(1.0f, 0.85f, 0.3f);
                case "A":
                    fill = new Color(0.1f, 0.4f, 0.18f, 0.4f);
                    return new Color(0.35f, 0.95f, 0.45f);
                case "B":
                case "C":
                    fill = new Color(0.45f, 0.35f, 0.05f, 0.4f);
                    return new Color(0.95f, 0.8f, 0.25f);
                case "D":
                    fill = new Color(0.5f, 0.28f, 0.05f, 0.4f);
                    return new Color(0.98f, 0.6f, 0.2f);
                default:
                    fill = new Color(0.55f, 0.15f, 0.15f, 0.35f);
                    return new Color(0.95f, 0.35f, 0.35f);
            }
        }

        /// <summary>
        /// Phase chip color for the cause-and-effect timeline. Fault nodes always render red.
        /// </summary>
        private static Color GetPhaseColor(string phase, bool isFault)
        {
            if (isFault) return new Color(0.62f, 0.16f, 0.16f, 0.95f);
            if (string.IsNullOrEmpty(phase)) return new Color(0.22f, 0.3f, 0.42f, 0.95f);

            string p = phase.ToLowerInvariant();
            if (p.Contains("tasarım") || p.Contains("tasarim") || p.Contains("design"))
                return new Color(0.16f, 0.42f, 0.72f, 0.95f);   // blue — design choices
            if (p.Contains("fırlatma") || p.Contains("firlatma") || p.Contains("launch"))
                return new Color(0.75f, 0.42f, 0.12f, 0.95f);   // orange — launch phase
            if (p.Contains("seyir") || p.Contains("cruise"))
                return new Color(0.55f, 0.38f, 0.72f, 0.95f);   // violet — cruise phase
            if (p.Contains("edl") || p.Contains("iniş") || p.Contains("inis") || p.Contains("landing"))
                return new Color(0.58f, 0.28f, 0.55f, 0.95f);   // magenta — EDL
            if (p.Contains("bilim") || p.Contains("science"))
                return new Color(0.1f, 0.55f, 0.6f, 0.95f);     // teal — science phase
            if (p.Contains("sonuç") || p.Contains("sonuc") || p.Contains("outcome"))
                return new Color(0.4f, 0.45f, 0.5f, 0.95f);     // neutral gray — outcome

            return new Color(0.22f, 0.3f, 0.42f, 0.95f);
        }

        /// <summary>
        /// Destination photo loader (Resources/Destinations/{id}.jpg).
        /// Unknown id or non-Unity harness returns null — the header renders without a photo.
        /// </summary>
        private static Texture2D LoadDestinationTexture(string destinationText)
        {
            if (string.IsNullOrWhiteSpace(destinationText)) return null;

            string id = destinationText.Trim().ToLowerInvariant();
            if (id == "-" || id.Length > 24) return null;

            try
            {
                return Resources.Load<Texture2D>($"Destinations/{id}");
            }
            catch
            {
                return null;
            }
        }
    }
}
