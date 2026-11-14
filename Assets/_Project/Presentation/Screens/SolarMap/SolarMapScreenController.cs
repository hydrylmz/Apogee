using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Solar System Orrery Map (ScreenId.SolarMap = 4).
    /// Complies with GDD §6.2, §14.1 (#4 Güneş Sistemi Haritası), and §21 M9-06.
    /// Visualizes dynamic planetary orbits around the Sun, real-time transfer window states,
    /// interplanetary flight telemetry, and in-flight spacecraft positions.
    /// </summary>
    public sealed class SolarMapScreenController : ScreenControllerBase<SolarMapViewModel>
    {
        public override ScreenId ScreenId => ScreenId.SolarMap;

        // HUD Elements
        private Label _agencyLabel;
        private Label _budgetLabel;
        private Label _reputationLabel;
        private Label _researchLabel;
        private Label _dateLabel;
        private Button _pauseBtn;
        private Button _speed1xBtn;
        private Button _speed2xBtn;
        private Button _speed3xBtn;
        private Button _stepDayBtn;

        // Solar Orrery Elements
        private VisualElement _orreryCanvas;
        private readonly Dictionary<string, VisualElement> _planetMarkers = new();
        private readonly List<VisualElement> _missionPins = new();

        // Briefing Panel Elements
        private Label _targetNameLabel;
        private Label _targetTypeLabel;
        private VisualElement _windowCard;
        private Label _windowBadge;
        private Label _windowDesc;
        private Label _deltaVLabel;
        private Label _transitTimeLabel;
        private Label _commDelayLabel;
        private VisualElement _hazardsContainer;
        private Button _designMissionBtn;

        // Active Missions Section
        private VisualElement _missionsContainer;
        private Label _statusLabel;

        public SolarMapScreenController(SolarMapViewModel viewModel) : base(viewModel) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-root");
            root.AddToClassList("screen-solar-map");
            root.style.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 1f);
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;

            // ── TOP: STRATEGIC HUD ───────────────────────────────────────────
            var hud = new VisualElement();
            hud.style.flexDirection = FlexDirection.Row;
            hud.style.justifyContent = Justify.SpaceBetween;
            hud.style.alignItems = Align.Center;
            hud.style.paddingTop = 8;
            hud.style.paddingBottom = 8;
            hud.style.paddingLeft = 16;
            hud.style.paddingRight = 16;
            hud.style.backgroundColor = new Color(0.05f, 0.08f, 0.14f, 0.98f);
            hud.style.borderBottomWidth = 1;
            hud.style.borderBottomColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            // Left: Agency Title
            _agencyLabel = new Label("Birleşik Yörünge Ajansı · Güneş Sistemi Haritası");
            _agencyLabel.style.fontSize = 15;
            _agencyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _agencyLabel.style.color = new Color(0.9f, 0.95f, 1f);
            hud.Add(_agencyLabel);

            // Center: Resources Pill
            var resPill = new VisualElement();
            resPill.style.flexDirection = FlexDirection.Row;
            resPill.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.85f);
            resPill.style.paddingTop = 4;
            resPill.style.paddingBottom = 4;
            resPill.style.paddingLeft = 14;
            resPill.style.paddingRight = 14;
            SetBorderRadius(resPill, 14);

            _budgetLabel = new Label("0.0 M$");
            _budgetLabel.style.color = new Color(0.3f, 0.85f, 0.5f);
            _budgetLabel.style.fontSize = 12;
            _budgetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _budgetLabel.style.marginRight = 14;
            resPill.Add(_budgetLabel);

            _reputationLabel = new Label("0.0");
            _reputationLabel.style.color = new Color(0.95f, 0.75f, 0.25f);
            _reputationLabel.style.fontSize = 12;
            _reputationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _reputationLabel.style.marginRight = 14;
            resPill.Add(_reputationLabel);

            _researchLabel = new Label("0.0 AP");
            _researchLabel.style.color = new Color(0.3f, 0.75f, 0.95f);
            _researchLabel.style.fontSize = 12;
            _researchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            resPill.Add(_researchLabel);

            hud.Add(resPill);

            // Right: Time Controls
            var timeBox = new VisualElement();
            timeBox.style.flexDirection = FlexDirection.Row;
            timeBox.style.alignItems = Align.Center;

            _dateLabel = new Label("2031-01-01 · Gün 0");
            _dateLabel.style.fontSize = 12;
            _dateLabel.style.color = new Color(0.85f, 0.9f, 0.95f);
            _dateLabel.style.marginRight = 12;
            timeBox.Add(_dateLabel);

            var speedGroup = new VisualElement();
            speedGroup.style.flexDirection = FlexDirection.Row;

            _pauseBtn = CreateSpeedButton("", () => ViewModel.PauseCommand.Execute());
            _speed1xBtn = CreateSpeedButton("1x", () => ViewModel.Speed1xCommand.Execute());
            _speed2xBtn = CreateSpeedButton("2x", () => ViewModel.Speed2xCommand.Execute());
            _speed3xBtn = CreateSpeedButton("3x", () => ViewModel.Speed3xCommand.Execute());
            _stepDayBtn = CreateSpeedButton("+1G", () => ViewModel.AdvanceDayCommand.Execute());

            speedGroup.Add(_pauseBtn);
            speedGroup.Add(_speed1xBtn);
            speedGroup.Add(_speed2xBtn);
            speedGroup.Add(_speed3xBtn);
            speedGroup.Add(_stepDayBtn);
            timeBox.Add(speedGroup);

            hud.Add(timeBox);
            root.Add(hud);

            // ── MAIN VIEW: ORRERY MAP (LEFT) + BRIEFING CARD (RIGHT) ─────────
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;

            // Orrery Canvas
            _orreryCanvas = new VisualElement();
            _orreryCanvas.style.flexGrow = 1;
            _orreryCanvas.style.position = Position.Relative;
            _orreryCanvas.style.overflow = Overflow.Hidden;
            _orreryCanvas.style.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);

            // Sun at center
            var sun = new VisualElement();
            sun.style.position = Position.Absolute;
            sun.style.left = Length.Percent(50);
            sun.style.top = Length.Percent(50);
            sun.style.width = 36;
            sun.style.height = 36;
            sun.style.marginLeft = -18;
            sun.style.marginTop = -18;
            SetBorderRadius(sun, 18);
            sun.style.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f);
            SetBorderWidth(sun, 2);
            SetBorderColor(sun, new Color(1f, 0.95f, 0.6f, 0.9f));
            _orreryCanvas.Add(sun);

            var sunLabel = new Label("GÜNEŞ");
            sunLabel.style.position = Position.Absolute;
            sunLabel.style.left = Length.Percent(50);
            sunLabel.style.top = Length.Percent(50);
            sunLabel.style.marginLeft = -16;
            sunLabel.style.marginTop = 20;
            sunLabel.style.fontSize = 9;
            sunLabel.style.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            _orreryCanvas.Add(sunLabel);

            // Add standard concentric orbit rings
            float[] orbitRadii = new[] { 18f, 32f, 48f, 68f };
            foreach (var r in orbitRadii)
            {
                var orbit = new VisualElement();
                orbit.AddToClassList("solar-orbit-line");
                orbit.style.position = Position.Absolute;
                orbit.style.left = Length.Percent(50);
                orbit.style.top = Length.Percent(50);
                orbit.style.width = Length.Percent(r * 2);
                orbit.style.height = Length.Percent(r * 2);
                orbit.style.marginLeft = Length.Percent(-r);
                orbit.style.marginTop = Length.Percent(-r);
                SetBorderRadius(orbit, 500); // Circular
                SetBorderWidth(orbit, 1);
                SetBorderColor(orbit, new Color(0.2f, 0.28f, 0.42f, 0.35f));
                _orreryCanvas.Add(orbit);
            }

            mainContainer.Add(_orreryCanvas);

            // ── RIGHT SIDE: DESTINATION BRIEFING PANEL ─────────────────────────
            var rightPanel = new VisualElement();
            rightPanel.style.width = 340;
            rightPanel.style.backgroundColor = new Color(0.06f, 0.09f, 0.15f, 0.98f);
            rightPanel.style.borderLeftWidth = 1;
            rightPanel.style.borderLeftColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);
            rightPanel.style.paddingLeft = 16;
            rightPanel.style.paddingRight = 16;
            rightPanel.style.paddingTop = 14;
            rightPanel.style.paddingBottom = 14;
            rightPanel.style.justifyContent = Justify.SpaceBetween;

            var panelScroll = new ScrollView();
            panelScroll.style.flexGrow = 1;

            // Target Header
            var targetHeader = new VisualElement();
            targetHeader.style.marginBottom = 12;

            _targetNameLabel = new Label("Mars");
            _targetNameLabel.style.fontSize = 20;
            _targetNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _targetNameLabel.style.color = new Color(0.95f, 0.98f, 1f);
            targetHeader.Add(_targetNameLabel);

            _targetTypeLabel = new Label("Karasal Gezegen · 1.52 AU");
            _targetTypeLabel.style.fontSize = 11;
            _targetTypeLabel.style.color = new Color(0.55f, 0.65f, 0.8f);
            targetHeader.Add(_targetTypeLabel);

            panelScroll.Add(targetHeader);

            // Transfer Window Card
            _windowCard = new VisualElement();
            _windowCard.style.backgroundColor = new Color(0.08f, 0.12f, 0.20f, 0.9f);
            SetBorderWidth(_windowCard, 1);
            SetBorderColor(_windowCard, new Color(0.2f, 0.3f, 0.45f));
            SetBorderRadius(_windowCard, 8);
            _windowCard.style.paddingLeft = 12;
            _windowCard.style.paddingRight = 12;
            _windowCard.style.paddingTop = 10;
            _windowCard.style.paddingBottom = 10;
            _windowCard.style.marginBottom = 14;

            var winTitleRow = new VisualElement();
            winTitleRow.style.flexDirection = FlexDirection.Row;
            winTitleRow.style.justifyContent = Justify.SpaceBetween;
            winTitleRow.style.alignItems = Align.Center;

            var winTitle = new Label("FIRLATMA PENCERESİ");
            winTitle.style.fontSize = 10;
            winTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            winTitle.style.color = new Color(0.6f, 0.7f, 0.85f);
            winTitleRow.Add(winTitle);

            _windowBadge = new Label("PENCERE AÇIK!");
            _windowBadge.style.fontSize = 10;
            _windowBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _windowBadge.style.paddingTop = 2;
            _windowBadge.style.paddingBottom = 2;
            _windowBadge.style.paddingLeft = 6;
            _windowBadge.style.paddingRight = 6;
            SetBorderRadius(_windowBadge, 4);
            winTitleRow.Add(_windowBadge);
            _windowCard.Add(winTitleRow);

            _windowDesc = new Label("Hohmann transfer yörüngesi için en uygun fırlatma tarihi.");
            _windowDesc.style.fontSize = 11;
            _windowDesc.style.color = new Color(0.8f, 0.88f, 0.95f);
            _windowDesc.style.marginTop = 6;
            _windowCard.Add(_windowDesc);

            panelScroll.Add(_windowCard);

            // Telemetry Grid
            var statsBox = new VisualElement();
            statsBox.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.8f);
            SetBorderRadius(statsBox, 6);
            statsBox.style.paddingLeft = 10;
            statsBox.style.paddingRight = 10;
            statsBox.style.paddingTop = 8;
            statsBox.style.paddingBottom = 8;
            statsBox.style.marginBottom = 14;

            _deltaVLabel = CreateStatRow(statsBox, "Gereken Toplam Δv", "18,900 m/s");
            _transitTimeLabel = CreateStatRow(statsBox, "Seyir Süresi", "180–270 gün");
            _commDelayLabel = CreateStatRow(statsBox, "Haberleşme Gecikmesi", "3.0–22.0 dk");

            panelScroll.Add(statsBox);

            // Environmental Hazards Section
            var hazTitle = new Label("ÇEVRESEL TEHLİKELER");
            hazTitle.style.fontSize = 10;
            hazTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            hazTitle.style.color = new Color(0.6f, 0.7f, 0.85f);
            hazTitle.style.marginBottom = 6;
            panelScroll.Add(hazTitle);

            _hazardsContainer = new VisualElement();
            _hazardsContainer.style.flexDirection = FlexDirection.Row;
            _hazardsContainer.style.flexWrap = Wrap.Wrap;
            _hazardsContainer.style.marginBottom = 16;
            panelScroll.Add(_hazardsContainer);

            // Active Interplanetary Missions Section
            var misTitle = new Label("AKTİF SEYİRDEKİ ARAÇLAR");
            misTitle.style.fontSize = 10;
            misTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            misTitle.style.color = new Color(0.6f, 0.7f, 0.85f);
            misTitle.style.marginBottom = 6;
            panelScroll.Add(misTitle);

            _missionsContainer = new VisualElement();
            _missionsContainer.style.marginBottom = 12;
            panelScroll.Add(_missionsContainer);

            rightPanel.Add(panelScroll);

            // Launch Mission Button
            _designMissionBtn = new Button(() => ViewModel.LaunchToSelectedDestinationCommand.Execute());
            _designMissionBtn.text = "BU HEDEFE GÖREV TASARLA";
            _designMissionBtn.AddToClassList("apogee-btn");
            _designMissionBtn.AddToClassList("apogee-btn--primary");
            _designMissionBtn.style.height = 40;
            _designMissionBtn.style.fontSize = 12;
            _designMissionBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _designMissionBtn.style.backgroundColor = new Color(0.08f, 0.6f, 0.75f);
            SetBorderWidth(_designMissionBtn, 0);
            SetBorderRadius(_designMissionBtn, 6);
            _designMissionBtn.style.color = Color.white;
            rightPanel.Add(_designMissionBtn);

            mainContainer.Add(rightPanel);
            root.Add(mainContainer);

            // ── BOTTOM: STRATEGIC TOOLBAR ────────────────────────────────────
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.justifyContent = Justify.SpaceBetween;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingTop = 6;
            toolbar.style.paddingBottom = 6;
            toolbar.style.paddingLeft = 16;
            toolbar.style.paddingRight = 16;
            toolbar.style.backgroundColor = new Color(0.04f, 0.06f, 0.11f, 0.98f);
            toolbar.style.borderTopWidth = 1;
            toolbar.style.borderTopColor = new Color(0.18f, 0.25f, 0.38f, 0.8f);

            var navLeft = new VisualElement();
            navLeft.style.flexDirection = FlexDirection.Row;

            navLeft.Add(CreateNavButton("Dünya Haritası", () => ViewModel.GoToCampaignMapCommand.Execute()));
            navLeft.Add(CreateNavButton("Misyon Bestecisi", () => ViewModel.GoToComposerCommand.Execute()));
            navLeft.Add(CreateNavButton("Tasarım Atölyesi", () => ViewModel.GoToWorkshopCommand.Execute()));

            toolbar.Add(navLeft);

            _statusLabel = new Label("Güneş Sistemi Hazır.");
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.55f, 0.65f, 0.78f);
            toolbar.Add(_statusLabel);

            var navRight = new VisualElement();
            navRight.style.flexDirection = FlexDirection.Row;
            navRight.Add(CreateNavButton("Ana Menü", () => ViewModel.GoToMainMenuCommand.Execute()));
            toolbar.Add(navRight);

            root.Add(toolbar);

            return root;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            BindViewModel();
        }

        private void BindViewModel()
        {
            if (ViewModel == null) return;

            ViewModel.AgencyName.Subscribe(name =>
            {
                if (_agencyLabel != null)
                    _agencyLabel.text = $"{name} · Güneş Sistemi Haritası";
            });

            ViewModel.DateDisplay.Subscribe(date =>
            {
                if (_dateLabel != null) _dateLabel.text = date;
            });

            ViewModel.Budget.Subscribe(val =>
            {
                if (_budgetLabel != null) _budgetLabel.text = $"{val:F1} M$";
            });

            ViewModel.Reputation.Subscribe(val =>
            {
                if (_reputationLabel != null) _reputationLabel.text = $"{val:F0}";
            });

            ViewModel.ResearchPoints.Subscribe(val =>
            {
                if (_researchLabel != null) _researchLabel.text = $"{val:F0} AP";
            });

            ViewModel.CurrentSpeed.Subscribe(speed =>
            {
                HighlightSpeedButton(speed);
            });

            ViewModel.Bodies.Subscribe(bodies =>
            {
                PopulatePlanetMarkers(bodies);
            });

            ViewModel.SelectedBody.Subscribe(body =>
            {
                UpdateSelectedBodyView(body);
            });

            ViewModel.BriefingCard.Subscribe(card =>
            {
                UpdateBriefingCard(card);
            });

            ViewModel.ActiveMissions.Subscribe(missions =>
            {
                PopulateActiveMissions(missions);
            });

            ViewModel.StatusMessage.Subscribe(msg =>
            {
                if (_statusLabel != null) _statusLabel.text = msg;
            });
        }

        protected override void OnShown(object context)
        {
            base.OnShown(context);

            if (context is string destinationId)
            {
                ViewModel?.SelectBody(destinationId);
            }

            ViewModel?.Refresh();
        }

        private void PopulatePlanetMarkers(IReadOnlyList<CelestialBodyItemViewModel> bodies)
        {
            if (_orreryCanvas == null || bodies == null) return;

            // Clear old planet markers
            foreach (var kvp in _planetMarkers)
            {
                if (kvp.Value?.parent != null)
                {
                    _orreryCanvas.Remove(kvp.Value);
                }
            }
            _planetMarkers.Clear();

            foreach (var body in bodies)
            {
                var marker = CreatePlanetElement(body);
                _planetMarkers[body.Id] = marker;
                _orreryCanvas.Add(marker);
            }
        }

        private VisualElement CreatePlanetElement(CelestialBodyItemViewModel body)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = Length.Percent(body.NormalizedX);
            container.style.top = Length.Percent(body.NormalizedY);
            container.style.marginLeft = -body.RadiusPx / 2;
            container.style.marginTop = -body.RadiusPx / 2;
            container.style.alignItems = Align.Center;

            // Glowing aura if window is open
            if (body.IsWindowOpen)
            {
                var aura = new VisualElement();
                aura.style.position = Position.Absolute;
                aura.style.width = body.RadiusPx + 16;
                aura.style.height = body.RadiusPx + 16;
                aura.style.marginLeft = -(body.RadiusPx + 16) / 2;
                aura.style.marginTop = -(body.RadiusPx + 16) / 2;
                SetBorderRadius(aura, 500);
                SetBorderWidth(aura, 2);
                SetBorderColor(aura, new Color(0.2f, 0.85f, 0.95f, 0.8f));
                aura.style.backgroundColor = new Color(0.2f, 0.85f, 0.95f, 0.15f);
                container.Add(aura);
            }

            // Planet circle
            var planet = new VisualElement();
            planet.AddToClassList("solar-planet-marker");
            planet.style.width = body.RadiusPx;
            planet.style.height = body.RadiusPx;
            SetBorderRadius(planet, 500);

            if (ColorUtility.TryParseHtmlString(body.ColorHex, out var col))
            {
                planet.style.backgroundColor = col;
            }
            else
            {
                planet.style.backgroundColor = Color.gray;
            }

            container.Add(planet);

            // Label tag
            var tag = new Label(body.Record.Id.ToUpperInvariant());
            tag.style.fontSize = 9;
            tag.style.unityFontStyleAndWeight = FontStyle.Bold;
            tag.style.color = new Color(0.85f, 0.9f, 0.98f);
            tag.style.marginTop = 4;
            container.Add(tag);

            container.RegisterCallback<ClickEvent>(evt =>
            {
                ViewModel?.SelectBody(body.Id);
                evt.StopPropagation();
            });

            return container;
        }

        private void UpdateSelectedBodyView(CelestialBodyItemViewModel body)
        {
            if (body == null) return;

            if (_targetNameLabel != null)
                _targetNameLabel.text = body.Name;

            if (_targetTypeLabel != null)
                _targetTypeLabel.text = $"{body.BodyType} · {body.SemiMajorAxisAu:F2} AU";

            // Update planet markers selection outline
            foreach (var kvp in _planetMarkers)
            {
                bool isSelected = string.Equals(kvp.Key, body.Id, StringComparison.OrdinalIgnoreCase);
                kvp.Value.style.scale = isSelected ? new Scale(new Vector3(1.25f, 1.25f, 1f)) : new Scale(Vector3.one);
            }
        }

        private void UpdateBriefingCard(DestinationBriefingCard card)
        {
            if (card == null) return;

            if (_windowBadge != null)
            {
                _windowBadge.text = card.WindowStatusText.ToUpperInvariant();
                if (card.IsWindowActive)
                {
                    _windowBadge.style.backgroundColor = new Color(0.08f, 0.6f, 0.75f, 0.3f);
                    _windowBadge.style.color = new Color(0.25f, 0.95f, 1f);
                    SetBorderWidth(_windowBadge, 1);
                    SetBorderColor(_windowBadge, new Color(0.25f, 0.95f, 1f));
                }
                else
                {
                    _windowBadge.style.backgroundColor = new Color(0.85f, 0.55f, 0.15f, 0.25f);
                    _windowBadge.style.color = new Color(1f, 0.8f, 0.3f);
                    SetBorderWidth(_windowBadge, 1);
                    SetBorderColor(_windowBadge, new Color(1f, 0.8f, 0.3f));
                }
            }

            if (_windowDesc != null)
            {
                _windowDesc.text = card.IsWindowActive
                    ? "OK Transfer penceresi açık! Fırlatma için ideal yakıt tasarrufu."
                    : $"Sonraki fırlatma penceresine {card.DaysUntilNextWindow} gün kaldı.";
            }

            if (_deltaVLabel != null)
                _deltaVLabel.text = $"{card.RequiredDeltaVMps:N0} m/s";

            if (_transitTimeLabel != null)
                _transitTimeLabel.text = card.TransitDurationText;

            if (_commDelayLabel != null)
                _commDelayLabel.text = card.CommDelayText;

            // Hazards
            if (_hazardsContainer != null)
            {
                _hazardsContainer.Clear();
                if (card.HazardTags != null && card.HazardTags.Count > 0)
                {
                    foreach (var h in card.HazardTags)
                    {
                        var badge = new Label(h);
                        badge.style.fontSize = 10;
                        badge.style.color = new Color(1f, 0.55f, 0.55f);
                        badge.style.backgroundColor = new Color(0.4f, 0.1f, 0.1f, 0.5f);
                        SetBorderWidth(badge, 1);
                        SetBorderColor(badge, new Color(0.8f, 0.2f, 0.2f));
                        SetBorderRadius(badge, 4);
                        badge.style.paddingLeft = 6;
                        badge.style.paddingRight = 6;
                        badge.style.paddingTop = 2;
                        badge.style.paddingBottom = 2;
                        badge.style.marginRight = 6;
                        badge.style.marginBottom = 6;
                        _hazardsContainer.Add(badge);
                    }
                }
                else
                {
                    var none = new Label("Önemli bir tehlike yok.");
                    none.style.fontSize = 11;
                    none.style.color = new Color(0.45f, 0.8f, 0.55f);
                    _hazardsContainer.Add(none);
                }
            }
        }

        private void PopulateActiveMissions(IReadOnlyList<ActiveMissionMarkerViewModel> missions)
        {
            if (_missionsContainer == null) return;

            // Clear old mission markers from map canvas
            foreach (var pin in _missionPins)
            {
                if (pin?.parent != null) pin.RemoveFromHierarchy();
            }
            _missionPins.Clear();

            _missionsContainer.Clear();

            if (missions == null || missions.Count == 0)
            {
                var empty = new Label("Aktif seyirde uzay aracı bulunmuyor.");
                empty.style.fontSize = 11;
                empty.style.color = new Color(0.5f, 0.6f, 0.72f);
                _missionsContainer.Add(empty);
                return;
            }

            foreach (var m in missions)
            {
                // List Item
                var item = new VisualElement();
                item.style.backgroundColor = new Color(0.04f, 0.08f, 0.15f, 0.9f);
                SetBorderRadius(item, 4);
                item.style.paddingLeft = 8;
                item.style.paddingRight = 8;
                item.style.paddingTop = 6;
                item.style.paddingBottom = 6;
                item.style.marginBottom = 6;

                var row1 = new VisualElement();
                row1.style.flexDirection = FlexDirection.Row;
                row1.style.justifyContent = Justify.SpaceBetween;

                var nameLbl = new Label($"{m.MissionName}");
                nameLbl.style.fontSize = 11;
                nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLbl.style.color = new Color(0.9f, 0.95f, 1f);
                row1.Add(nameLbl);

                var progLbl = new Label(m.ProgressPercent);
                progLbl.style.fontSize = 11;
                progLbl.style.color = new Color(0.3f, 0.85f, 0.95f);
                row1.Add(progLbl);

                item.Add(row1);

                var row2 = new Label($"Hedef: {m.DestinationId.ToUpperInvariant()} · Kalan: {m.DaysRemaining} gün");
                row2.style.fontSize = 10;
                row2.style.color = new Color(0.6f, 0.7f, 0.82f);
                row2.style.marginTop = 2;
                item.Add(row2);

                _missionsContainer.Add(item);

                // Add Spacecraft Pin on Map Canvas
                if (_orreryCanvas != null)
                {
                    var craftPin = new VisualElement();
                    craftPin.style.position = Position.Absolute;
                    craftPin.style.left = Length.Percent(m.NormalizedX);
                    craftPin.style.top = Length.Percent(m.NormalizedY);
                    craftPin.style.width = 16;
                    craftPin.style.height = 16;
                    craftPin.style.marginLeft = -8;
                    craftPin.style.marginTop = -8;
                    craftPin.style.backgroundColor = new Color(0.2f, 0.9f, 0.95f);
                    SetBorderWidth(craftPin, 1);
                    SetBorderColor(craftPin, Color.white);
                    SetBorderRadius(craftPin, 2);

                    var pinLabel = new Label(m.MissionName);
                    pinLabel.style.fontSize = 8;
                    pinLabel.style.color = Color.white;
                    pinLabel.style.marginTop = 16;
                    pinLabel.style.marginLeft = -10;
                    craftPin.Add(pinLabel);

                    _orreryCanvas.Add(craftPin);
                    _missionPins.Add(craftPin);
                }
            }
        }

        private Label CreateStatRow(VisualElement parent, string title, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 4;

            var tLbl = new Label(title);
            tLbl.style.fontSize = 11;
            tLbl.style.color = new Color(0.6f, 0.7f, 0.82f);
            row.Add(tLbl);

            var vLbl = new Label(value);
            vLbl.style.fontSize = 11;
            vLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            vLbl.style.color = new Color(0.9f, 0.96f, 1f);
            row.Add(vLbl);

            parent.Add(row);
            return vLbl;
        }

        private Button CreateSpeedButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.width = 34;
            btn.style.height = 24;
            btn.style.fontSize = 11;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            btn.style.backgroundColor = new Color(0.12f, 0.18f, 0.28f);
            SetBorderWidth(btn, 1);
            SetBorderColor(btn, new Color(0.25f, 0.35f, 0.5f));
            SetBorderRadius(btn, 4);
            btn.style.color = Color.white;
            return btn;
        }

        private void HighlightSpeedButton(TimeSpeed speed)
        {
            SetBtnActive(_pauseBtn, speed == TimeSpeed.Paused);
            SetBtnActive(_speed1xBtn, speed == TimeSpeed.Normal);
            SetBtnActive(_speed2xBtn, speed == TimeSpeed.Fast);
            SetBtnActive(_speed3xBtn, speed == TimeSpeed.VeryFast);
        }

        private void SetBtnActive(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active ? new Color(0.08f, 0.55f, 0.75f) : new Color(0.12f, 0.18f, 0.28f);
            SetBorderColor(btn, active ? new Color(0.2f, 0.85f, 0.95f) : new Color(0.25f, 0.35f, 0.5f));
        }

        private Button CreateNavButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 28;
            btn.style.fontSize = 11;
            btn.style.marginRight = 8;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.backgroundColor = new Color(0.1f, 0.15f, 0.24f);
            SetBorderWidth(btn, 1);
            SetBorderColor(btn, new Color(0.22f, 0.32f, 0.48f));
            SetBorderRadius(btn, 4);
            btn.style.color = new Color(0.85f, 0.92f, 1f);
            return btn;
        }

        private static void SetBorderWidth(VisualElement el, float width)
        {
            el.style.borderTopWidth = width;
            el.style.borderBottomWidth = width;
            el.style.borderLeftWidth = width;
            el.style.borderRightWidth = width;
        }

        private static void SetBorderColor(VisualElement el, Color color)
        {
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
        }

        private static void SetBorderRadius(VisualElement el, float radius)
        {
            el.style.borderTopLeftRadius = radius;
            el.style.borderTopRightRadius = radius;
            el.style.borderBottomLeftRadius = radius;
            el.style.borderBottomRightRadius = radius;
        }
    }
}
