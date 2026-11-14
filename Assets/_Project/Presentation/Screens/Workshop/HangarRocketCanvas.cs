using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Design;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Workshop
{
    /// <summary>
    /// Interactive 2D vector canvas rendering the complete rocket stack and spacecraft in the Hangar (GDD §7.2, §14.3, §15.1).
    /// Uses UI Toolkit Painter2D for hardware-accelerated vector drawing with 4 switchable layers:
    /// Exterior (Dış Görünüm), Cutaway (Kesit), Subsystems (İç Sistemler), and Schematic (Şematik).
    /// Supports smooth pan & zoom, section focus, leader line callouts, dashed empty sockets, and ghost previews.
    /// </summary>
    public class HangarRocketCanvas : VisualElement
    {
        private RocketVisualAnatomy _anatomy;
        private HangarLayerMode _layerMode = HangarLayerMode.Cutaway;
        private VehicleSlotId? _selectedSlotId;
        private RocketAnatomySection _hoveredSection;
        private DesignComponent _ghostComponent;
        private VehicleSlotId? _ghostSlotId;

        // Camera Pan & Zoom state
        private float _zoom = 1.0f;
        private Vector2 _pan = Vector2.zero;
        private float _targetZoom = 1.0f;
        private Vector2 _targetPan = Vector2.zero;
        private bool _isDragging = false;
        private Vector2 _lastDragPos = Vector2.zero;
        private IVisualElementScheduledItem _animSchedule;

        // Hover callout element
        private readonly VisualElement _calloutCard;
        private readonly Label _calloutTitle;
        private readonly Label _calloutSubtitle;
        private readonly Label _calloutBadge;

        // Events
        public event Action<VehicleSlotId?> SlotSelected;
        public event Action<RocketAnatomySection> SectionHovered;

        public HangarLayerMode LayerMode
        {
            get => _layerMode;
            set
            {
                if (_layerMode != value)
                {
                    _layerMode = value;
                    MarkDirtyRepaint();
                }
            }
        }

        public HangarRocketCanvas()
        {
            style.flexGrow = 1;
            style.position = Position.Relative;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(0.02f, 0.04f, 0.08f);

            // Register visual content generation (Painter2D)
            generateVisualContent += OnGenerateVisualContent;

            // Setup pointer navigation (Pan & Zoom)
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            // Floating Leader Callout Card
            _calloutCard = new VisualElement();
            _calloutCard.AddToClassList("apogee-panel");
            _calloutCard.style.position = Position.Absolute;
            _calloutCard.style.display = DisplayStyle.None;
            _calloutCard.style.paddingLeft = 10;
            _calloutCard.style.paddingRight = 10;
            _calloutCard.style.paddingTop = 6;
            _calloutCard.style.paddingBottom = 6;
            _calloutCard.style.borderTopLeftRadius = 6;
            _calloutCard.style.borderTopRightRadius = 6;
            _calloutCard.style.borderBottomLeftRadius = 6;
            _calloutCard.style.borderBottomRightRadius = 6;
            _calloutCard.style.backgroundColor = new Color(0.06f, 0.12f, 0.22f, 0.95f);
            _calloutCard.style.borderLeftWidth = 2;
            _calloutCard.style.borderLeftColor = new Color(0.06f, 0.71f, 0.83f); // Cyan accent

            _calloutBadge = new Label("SLOT");
            _calloutBadge.style.fontSize = 9;
            _calloutBadge.style.color = new Color(0.40f, 0.75f, 0.95f);
            _calloutBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _calloutCard.Add(_calloutBadge);

            _calloutTitle = new Label("Parça Başlığı");
            _calloutTitle.style.fontSize = 12;
            _calloutTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _calloutTitle.style.color = Color.white;
            _calloutCard.Add(_calloutTitle);

            _calloutSubtitle = new Label("Alt Bilgi");
            _calloutSubtitle.style.fontSize = 10;
            _calloutSubtitle.style.color = new Color(0.70f, 0.80f, 0.90f);
            _calloutCard.Add(_calloutSubtitle);

            Add(_calloutCard);
        }

        public void SetAnatomy(RocketVisualAnatomy anatomy)
        {
            _anatomy = anatomy;
            ResetView();
            MarkDirtyRepaint();
        }

        public void SetSelectedSlot(VehicleSlotId? slotId)
        {
            _selectedSlotId = slotId;
            if (slotId.HasValue && _anatomy != null)
            {
                var section = _anatomy.FindSectionBySlot(slotId.Value);
                if (section != null)
                {
                    FocusOnSection(section);
                }
            }
            MarkDirtyRepaint();
        }

        public void SetGhostPreview(VehicleSlotId? slotId, DesignComponent component)
        {
            _ghostSlotId = slotId;
            _ghostComponent = component;
            MarkDirtyRepaint();
        }

        public void ResetView()
        {
            _targetZoom = 1.0f;
            _targetPan = Vector2.zero;
            StartCameraAnimation();
        }

        public void FocusOnSection(RocketAnatomySection section)
        {
            if (section == null || _anatomy == null) return;

            float canvasH = contentRect.height > 10 ? contentRect.height : 600f;
            float rocketTotalH = canvasH * 0.78f;

            // Calculate center of section in canvas space
            float secCenterNormY = (float)(section.NormalizedYBottom + section.NormalizedHeight * 0.5);
            // Invert Y so bottom is at bottom of viewport
            float secPixelY = (canvasH * 0.5f) - ((secCenterNormY - 0.5f) * rocketTotalH);

            _targetZoom = 1.6f;
            _targetPan = new Vector2(0f, (canvasH * 0.5f) - secPixelY);
            StartCameraAnimation();
        }

        private void StartCameraAnimation()
        {
            _animSchedule?.Pause();
            _animSchedule = schedule.Execute(() =>
            {
                float t = 0.25f;
                _zoom = Mathf.Lerp(_zoom, _targetZoom, t);
                _pan = Vector2.Lerp(_pan, _targetPan, t);

                if (Mathf.Abs(_zoom - _targetZoom) < 0.005f && (_pan - _targetPan).sqrMagnitude < 0.5f)
                {
                    _zoom = _targetZoom;
                    _pan = _targetPan;
                    _animSchedule.Pause();
                }

                UpdateCalloutPosition();
                MarkDirtyRepaint();
            }).Every(16);
        }

        // ── Input & Navigation Handlers ─────────────────────────────────────────────

        private void OnWheel(WheelEvent evt)
        {
            float zoomDelta = -evt.delta.y * 0.0015f;
            _targetZoom = Mathf.Clamp(_targetZoom + zoomDelta, 0.6f, 3.2f);
            StartCameraAnimation();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0) // Left click
            {
                _isDragging = true;
                _lastDragPos = evt.localPosition;
                this.CapturePointer(evt.pointerId);

                // Check hit section
                var hit = HitTestSection(evt.localPosition);
                if (hit != null && hit.SlotId.HasValue)
                {
                    SetSelectedSlot(hit.SlotId.Value);
                    SlotSelected?.Invoke(hit.SlotId.Value);
                }
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isDragging)
            {
                Vector2 delta = (Vector2)evt.localPosition - _lastDragPos;
                _lastDragPos = evt.localPosition;
                _pan += delta;
                _targetPan = _pan;
                UpdateCalloutPosition();
                MarkDirtyRepaint();
            }
            else
            {
                var hit = HitTestSection(evt.localPosition);
                if (hit != _hoveredSection)
                {
                    _hoveredSection = hit;
                    SectionHovered?.Invoke(hit);
                    UpdateCallout(hit);
                    MarkDirtyRepaint();
                }
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleasePointer(evt.pointerId);
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            _isDragging = false;
            _hoveredSection = null;
            _calloutCard.style.display = DisplayStyle.None;
            MarkDirtyRepaint();
        }

        // ── Hit Testing ─────────────────────────────────────────────────────────────

        private RocketAnatomySection HitTestSection(Vector2 localPos)
        {
            if (_anatomy == null) return null;

            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 10 || h < 10) return null;

            Vector2 center = new Vector2(w * 0.5f, h * 0.55f) + _pan;
            float rocketTotalH = h * 0.78f * _zoom;
            float maxDiaPx = 160f * _zoom;

            // Test payload internal subsystems first if Cutaway or Subsystems
            if (_layerMode == HangarLayerMode.Cutaway || _layerMode == HangarLayerMode.Subsystems)
            {
                foreach (var sec in _anatomy.PayloadSubsystems)
                {
                    var rect = GetSectionRect(sec, center, rocketTotalH, maxDiaPx);
                    if (rect.Contains(localPos)) return sec;
                }
            }

            // Test main sections
            foreach (var sec in _anatomy.Sections)
            {
                if (sec.IsInternalPayloadSubsystem && _layerMode == HangarLayerMode.Exterior) continue;
                if (!sec.IsInternalPayloadSubsystem && sec.PartType == CrossSectionPartType.FairingNose &&
                    (_layerMode == HangarLayerMode.Cutaway || _layerMode == HangarLayerMode.Subsystems))
                {
                    // In cutaway, clicks inside fairing hit internal modules
                    continue;
                }

                var rect = GetSectionRect(sec, center, rocketTotalH, maxDiaPx);
                if (rect.Contains(localPos)) return sec;
            }

            return null;
        }

        private Rect GetSectionRect(RocketAnatomySection sec, Vector2 center, float rocketTotalH, float maxDiaPx)
        {
            float secH = (float)sec.NormalizedHeight * rocketTotalH;
            float secW = (float)sec.NormalizedDiameter * maxDiaPx;

            // Invert Y: bottom is 0.0, top is 1.0
            float secBottomY = center.y + (rocketTotalH * 0.5f) - ((float)sec.NormalizedYBottom * rocketTotalH);
            float secTopY = secBottomY - secH;

            return new Rect(center.x - secW * 0.5f, secTopY, secW, secH);
        }

        // ── Callout Card Updates ────────────────────────────────────────────────────

        private void UpdateCallout(RocketAnatomySection sec)
        {
            if (sec == null)
            {
                _calloutCard.style.display = DisplayStyle.None;
                return;
            }

            _calloutBadge.text = sec.SlotId.HasValue ? $"{sec.SlotId.Value.ToString().ToUpperInvariant()}": "BİLEŞEN";
            _calloutTitle.text = sec.Title;
            _calloutSubtitle.text = sec.Subtitle;
            _calloutCard.style.display = DisplayStyle.Flex;

            UpdateCalloutPosition();
        }

        private void UpdateCalloutPosition()
        {
            if (_hoveredSection == null || _calloutCard.style.display == DisplayStyle.None) return;

            float w = contentRect.width;
            float h = contentRect.height;
            Vector2 center = new Vector2(w * 0.5f, h * 0.55f) + _pan;
            float rocketTotalH = h * 0.78f * _zoom;
            float maxDiaPx = 160f * _zoom;

            var rect = GetSectionRect(_hoveredSection, center, rocketTotalH, maxDiaPx);
            _calloutCard.style.left = rect.xMax + 16;
            _calloutCard.style.top = rect.center.y - 20;
        }

        // ── Painter2D Rendering Pipeline ────────────────────────────────────────────

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 10 || h < 10) return;

            var p = mgc.painter2D;

            Vector2 center = new Vector2(w * 0.5f, h * 0.55f) + _pan;
            float rocketTotalH = h * 0.78f * _zoom;
            float maxDiaPx = 160f * _zoom;

            // 1. Draw Hangar Backdrop (Ruler, grid lines, launch pad base)
            DrawHangarEnvironment(p, w, h, center, rocketTotalH);

            if (_anatomy == null) return;

            // 2. Draw Main Rocket Sections (Exterior / Cutaway / Subsystems / Schematic)
            DrawRocketSections(p, center, rocketTotalH, maxDiaPx);

            // 3. Draw Subsystem Energy & Data Flow (if Subsystems mode)
            if (_layerMode == HangarLayerMode.Subsystems)
            {
                DrawSubsystemTrunks(p, center, rocketTotalH, maxDiaPx);
            }

            // 4. Draw Empty Sockets & Ghost Previews
            DrawSocketsAndGhosts(p, center, rocketTotalH, maxDiaPx);

            // 5. Draw Selection & Hover Highlight Overlays
            DrawInteractionHighlights(p, center, rocketTotalH, maxDiaPx);
        }

        private void DrawHangarEnvironment(Painter2D p, float w, float h, Vector2 center, float rocketTotalH)
        {
            // Subtle CAD / Hangar grid
            p.strokeColor = new Color(0.08f, 0.14f, 0.24f, 0.40f);
            p.lineWidth = 1.0f;

            float gridSpacing = 40f * _zoom;
            if (gridSpacing > 15f)
            {
                for (float x = center.x % gridSpacing; x < w; x += gridSpacing)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, 0));
                    p.LineTo(new Vector2(x, h));
                    p.Stroke();
                }
                for (float y = center.y % gridSpacing; y < h; y += gridSpacing)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(0, y));
                    p.LineTo(new Vector2(w, y));
                    p.Stroke();
                }
            }

            // Launch Stand Mount Clamps at Rocket Base
            float baseY = center.y + (rocketTotalH * 0.5f);
            p.fillColor = new Color(0.12f, 0.18f, 0.28f);
            p.strokeColor = new Color(0.24f, 0.38f, 0.55f);
            p.lineWidth = 2.0f;

            p.BeginPath();
            p.MoveTo(new Vector2(center.x - 140 * _zoom, baseY + 20 * _zoom));
            p.LineTo(new Vector2(center.x + 140 * _zoom, baseY + 20 * _zoom));
            p.LineTo(new Vector2(center.x + 100 * _zoom, baseY));
            p.LineTo(new Vector2(center.x - 100 * _zoom, baseY));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Ground safety light line
            p.strokeColor = new Color(0.96f, 0.62f, 0.04f, 0.70f); // Amber glow line
            p.lineWidth = 2.0f;
            p.BeginPath();
            p.MoveTo(new Vector2(center.x - 160 * _zoom, baseY + 22 * _zoom));
            p.LineTo(new Vector2(center.x + 160 * _zoom, baseY + 22 * _zoom));
            p.Stroke();
        }

        private void DrawRocketSections(Painter2D p, Vector2 center, float rocketTotalH, float maxDiaPx)
        {
            // First pass: Draw main structural sections (bottom-up: fins, engines, tanks, decouplers, fairing)
            foreach (var sec in _anatomy.Sections)
            {
                if (sec.IsInternalPayloadSubsystem) continue;

                var rect = GetSectionRect(sec, center, rocketTotalH, maxDiaPx);

                switch (sec.PartType)
                {
                    case CrossSectionPartType.AeroFins:
                        DrawFins(p, rect, sec);
                        break;
                    case CrossSectionPartType.StageEngine:
                        DrawEngine(p, rect, sec);
                        break;
                    case CrossSectionPartType.StageTank:
                        DrawTank(p, rect, sec);
                        break;
                    case CrossSectionPartType.InterstageDecoupler:
                        DrawDecoupler(p, rect, sec);
                        break;
                    case CrossSectionPartType.FairingNose:
                        DrawFairing(p, rect, sec);
                        break;
                    case CrossSectionPartType.PayloadAbortSystem:
                        DrawAbortTower(p, rect, sec);
                        break;
                }
            }

            // Second pass: If Cutaway or Subsystems, render internal payload modules
            if (_layerMode == HangarLayerMode.Cutaway || _layerMode == HangarLayerMode.Subsystems)
            {
                foreach (var sec in _anatomy.PayloadSubsystems)
                {
                    if (sec.PartType == CrossSectionPartType.PayloadAbortSystem) continue;
                    var rect = GetSectionRect(sec, center, rocketTotalH, maxDiaPx);
                    DrawInternalPayloadModule(p, rect, sec);
                }
            }
        }

        private void DrawTank(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            Color hullColor = _layerMode == HangarLayerMode.Exterior
                ? new Color(0.18f, 0.25f, 0.36f)
                : (_layerMode == HangarLayerMode.Schematic
                    ? new Color(0.04f, 0.10f, 0.20f)
                    : new Color(0.10f, 0.16f, 0.25f, 0.70f));

            Color strokeColor = _layerMode == HangarLayerMode.Schematic
                ? new Color(0.20f, 0.75f, 0.95f)
                : new Color(0.28f, 0.42f, 0.60f);

            // Tank Outer Wall
            p.fillColor = hullColor;
            p.strokeColor = strokeColor;
            p.lineWidth = 1.5f;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.xMin, rect.yMin));
            p.LineTo(new Vector2(rect.xMax, rect.yMin));
            p.LineTo(new Vector2(rect.xMax, rect.yMax));
            p.LineTo(new Vector2(rect.xMin, rect.yMax));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Cutaway fuel fluid level
            if (_layerMode == HangarLayerMode.Cutaway || _layerMode == HangarLayerMode.Subsystems)
            {
                float fuelH = rect.height * 0.82f;
                float fuelY = rect.yMax - fuelH;

                Color fuelColor = sec.StageIndex == 0
                    ? new Color(0.95f, 0.55f, 0.12f, 0.50f)  // Kerosene gold
                    : new Color(0.15f, 0.65f, 0.95f, 0.50f); // Hydrolox / Cryo blue

                p.fillColor = fuelColor;
                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMin + 3, fuelY));
                p.LineTo(new Vector2(rect.xMax - 3, fuelY));
                p.LineTo(new Vector2(rect.xMax - 3, rect.yMax - 3));
                p.LineTo(new Vector2(rect.xMin + 3, rect.yMax - 3));
                p.ClosePath();
                p.Fill();

                // Liquid surface meniscus line
                p.strokeColor = new Color(1f, 1f, 1f, 0.65f);
                p.lineWidth = 1.0f;
                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMin + 3, fuelY));
                p.LineTo(new Vector2(rect.xMax - 3, fuelY));
                p.Stroke();
            }

            // Supplier stripe
            if (_layerMode != HangarLayerMode.Schematic && ColorUtility.TryParseHtmlString(sec.SupplierColorHex, out var supCol))
            {
                p.fillColor = supCol;
                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMin, rect.yMin + 4));
                p.LineTo(new Vector2(rect.xMax, rect.yMin + 4));
                p.LineTo(new Vector2(rect.xMax, rect.yMin + 12));
                p.LineTo(new Vector2(rect.xMin, rect.yMin + 12));
                p.ClosePath();
                p.Fill();
            }
        }

        private void DrawEngine(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            Color bellColor = _layerMode == HangarLayerMode.Schematic
                ? new Color(0.05f, 0.12f, 0.22f)
                : new Color(0.12f, 0.16f, 0.22f);

            Color bellBorder = _layerMode == HangarLayerMode.Schematic
                ? new Color(0.20f, 0.75f, 0.95f)
                : new Color(0.35f, 0.45f, 0.60f);

            p.fillColor = bellColor;
            p.strokeColor = bellBorder;
            p.lineWidth = 1.5f;

            // Bell nozzle shape (narrow throat at top, wide flared bell at bottom)
            float throatW = rect.width * 0.35f;
            float exitW = rect.width * 0.85f;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x - throatW * 0.5f, rect.yMin));
            p.LineTo(new Vector2(rect.center.x + throatW * 0.5f, rect.yMin));
            p.BezierCurveTo(
                new Vector2(rect.center.x + throatW * 0.55f, rect.center.y),
                new Vector2(rect.center.x + exitW * 0.5f, rect.yMax - 4),
                new Vector2(rect.center.x + exitW * 0.5f, rect.yMax)
            );
            p.LineTo(new Vector2(rect.center.x - exitW * 0.5f, rect.yMax));
            p.BezierCurveTo(
                new Vector2(rect.center.x - exitW * 0.5f, rect.yMax - 4),
                new Vector2(rect.center.x - throatW * 0.55f, rect.center.y),
                new Vector2(rect.center.x - throatW * 0.5f, rect.yMin)
            );
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Internal combustion chamber glow in cutaway
            if (_layerMode == HangarLayerMode.Cutaway || _layerMode == HangarLayerMode.Subsystems)
            {
                p.fillColor = new Color(0.95f, 0.35f, 0.15f, 0.65f); // Orange throat glow
                p.BeginPath();
                p.MoveTo(new Vector2(rect.center.x - throatW * 0.35f, rect.yMin + 2));
                p.LineTo(new Vector2(rect.center.x + throatW * 0.35f, rect.yMin + 2));
                p.LineTo(new Vector2(rect.center.x + throatW * 0.2f, rect.yMin + rect.height * 0.35f));
                p.LineTo(new Vector2(rect.center.x - throatW * 0.2f, rect.yMin + rect.height * 0.35f));
                p.ClosePath();
                p.Fill();
            }
        }

        private void DrawFins(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            Color finColor = _layerMode == HangarLayerMode.Schematic
                ? new Color(0.04f, 0.10f, 0.20f)
                : new Color(0.15f, 0.22f, 0.32f);

            p.fillColor = finColor;
            p.strokeColor = new Color(0.30f, 0.45f, 0.65f);
            p.lineWidth = 1.5f;

            float finSpan = rect.width * 0.45f;

            // Left Fin
            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x - rect.width * 0.25f, rect.yMin));
            p.LineTo(new Vector2(rect.center.x - rect.width * 0.25f - finSpan, rect.yMax));
            p.LineTo(new Vector2(rect.center.x - rect.width * 0.25f, rect.yMax - 6));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Right Fin
            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x + rect.width * 0.25f, rect.yMin));
            p.LineTo(new Vector2(rect.center.x + rect.width * 0.25f + finSpan, rect.yMax));
            p.LineTo(new Vector2(rect.center.x + rect.width * 0.25f, rect.yMax - 6));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private void DrawDecoupler(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            p.fillColor = new Color(0.14f, 0.18f, 0.25f);
            p.strokeColor = new Color(0.35f, 0.45f, 0.58f);
            p.lineWidth = 1.0f;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.xMin, rect.yMin));
            p.LineTo(new Vector2(rect.xMax, rect.yMin));
            p.LineTo(new Vector2(rect.xMax, rect.yMax));
            p.LineTo(new Vector2(rect.xMin, rect.yMax));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Decoupler truss hash marks
            p.strokeColor = new Color(0.24f, 0.32f, 0.44f);
            float step = 10f * _zoom;
            for (float x = rect.xMin + step; x < rect.xMax; x += step)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, rect.yMin));
                p.LineTo(new Vector2(x + step * 0.5f, rect.yMax));
                p.Stroke();
            }
        }

        private void DrawFairing(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            Color fairingColor = _layerMode == HangarLayerMode.Exterior
                ? new Color(0.22f, 0.28f, 0.38f)
                : (_layerMode == HangarLayerMode.Schematic
                    ? new Color(0.04f, 0.10f, 0.22f)
                    : new Color(0.12f, 0.18f, 0.28f, 0.40f)); // Translucent shell in cutaway

            Color strokeColor = _layerMode == HangarLayerMode.Schematic
                ? new Color(0.20f, 0.75f, 0.95f)
                : new Color(0.32f, 0.48f, 0.68f);

            p.fillColor = fairingColor;
            p.strokeColor = strokeColor;
            p.lineWidth = 1.5f;

            // Ogive bullet nose cone fairing shape
            float baseH = rect.height * 0.45f;
            float coneBaseY = rect.yMax - baseH;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x, rect.yMin)); // Apex
            p.BezierCurveTo(
                new Vector2(rect.center.x + rect.width * 0.25f, rect.yMin + rect.height * 0.15f),
                new Vector2(rect.xMax, coneBaseY - rect.height * 0.1f),
                new Vector2(rect.xMax, coneBaseY)
            );
            p.LineTo(new Vector2(rect.xMax, rect.yMax));
            p.LineTo(new Vector2(rect.xMin, rect.yMax));
            p.LineTo(new Vector2(rect.xMin, coneBaseY));
            p.BezierCurveTo(
                new Vector2(rect.xMin, coneBaseY - rect.height * 0.1f),
                new Vector2(rect.center.x - rect.width * 0.25f, rect.yMin + rect.height * 0.15f),
                new Vector2(rect.center.x, rect.yMin)
            );
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Center separation line (shows fairing halves)
            p.strokeColor = new Color(0.15f, 0.25f, 0.40f, 0.70f);
            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x, rect.yMin));
            p.LineTo(new Vector2(rect.center.x, rect.yMax));
            p.Stroke();
        }

        private void DrawAbortTower(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            p.strokeColor = new Color(0.85f, 0.35f, 0.20f);
            p.fillColor = new Color(0.25f, 0.12f, 0.10f);
            p.lineWidth = 1.5f;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.center.x, rect.yMin)); // Tower tip
            p.LineTo(new Vector2(rect.center.x + rect.width * 0.4f, rect.yMax));
            p.LineTo(new Vector2(rect.center.x - rect.width * 0.4f, rect.yMax));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        private void DrawInternalPayloadModule(Painter2D p, Rect rect, RocketAnatomySection sec)
        {
            Color moduleBg = sec.IsEmptySlot
                ? new Color(0.08f, 0.12f, 0.18f, 0.40f)
                : new Color(0.12f, 0.20f, 0.32f, 0.90f);

            p.fillColor = moduleBg;
            p.strokeColor = new Color(0.25f, 0.45f, 0.65f);
            p.lineWidth = 1.0f;

            p.BeginPath();
            p.MoveTo(new Vector2(rect.xMin + 4, rect.yMin + 2));
            p.LineTo(new Vector2(rect.xMax - 4, rect.yMin + 2));
            p.LineTo(new Vector2(rect.xMax - 4, rect.yMax - 2));
            p.LineTo(new Vector2(rect.xMin + 4, rect.yMax - 2));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Draw specific visual characteristics based on category
            if (!sec.IsEmptySlot)
            {
                var prof = sec.VisualProfile;
                if (prof.SolarType != SolarArrayVisualType.None)
                {
                    // Draw expanded solar wings
                    p.fillColor = new Color(0.10f, 0.30f, 0.65f, 0.85f);
                    float wingSpan = rect.width * 0.35f;

                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMin - wingSpan, rect.center.y - 4));
                    p.LineTo(new Vector2(rect.xMin, rect.center.y - 4));
                    p.LineTo(new Vector2(rect.xMin, rect.center.y + 4));
                    p.LineTo(new Vector2(rect.xMin - wingSpan, rect.center.y + 4));
                    p.ClosePath();
                    p.Fill();

                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMax, rect.center.y - 4));
                    p.LineTo(new Vector2(rect.xMax + wingSpan, rect.center.y - 4));
                    p.LineTo(new Vector2(rect.xMax + wingSpan, rect.center.y + 4));
                    p.LineTo(new Vector2(rect.xMax, rect.center.y + 4));
                    p.ClosePath();
                    p.Fill();
                }
                else if (prof.AntennaType == AntennaVisualType.HighGainDish)
                {
                    // Draw dish arc
                    p.strokeColor = new Color(0.85f, 0.85f, 0.90f);
                    p.lineWidth = 2.0f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.center.x - 12 * _zoom, rect.center.y - 2));
                    p.BezierCurveTo(
                        new Vector2(rect.center.x - 6 * _zoom, rect.center.y - 8 * _zoom),
                        new Vector2(rect.center.x + 6 * _zoom, rect.center.y - 8 * _zoom),
                        new Vector2(rect.center.x + 12 * _zoom, rect.center.y - 2)
                    );
                    p.Stroke();
                }
            }
        }

        private void DrawSubsystemTrunks(Painter2D p, Vector2 center, float rocketTotalH, float maxDiaPx)
        {
            // Power Backbone (Golden glow lines)
            p.strokeColor = new Color(0.96f, 0.72f, 0.12f, 0.85f);
            p.lineWidth = 2.0f;
            p.BeginPath();
            p.MoveTo(new Vector2(center.x - 15 * _zoom, center.y - rocketTotalH * 0.35f));
            p.LineTo(new Vector2(center.x - 15 * _zoom, center.y + rocketTotalH * 0.40f));
            p.Stroke();

            // Data / Avionics Bus (Cyan glow lines)
            p.strokeColor = new Color(0.06f, 0.85f, 0.95f, 0.85f);
            p.lineWidth = 2.0f;
            p.BeginPath();
            p.MoveTo(new Vector2(center.x + 15 * _zoom, center.y - rocketTotalH * 0.38f));
            p.LineTo(new Vector2(center.x + 15 * _zoom, center.y + rocketTotalH * 0.42f));
            p.Stroke();
        }

        private void DrawSocketsAndGhosts(Painter2D p, Vector2 center, float rocketTotalH, float maxDiaPx)
        {
            // Empty sockets with dashed pulsing box + required indicator
            foreach (var sec in _anatomy.Sections)
            {
                if (!sec.IsEmptySlot) continue;

                var rect = GetSectionRect(sec, center, rocketTotalH, maxDiaPx);

                p.strokeColor = sec.IsRequired
                    ? new Color(0.95f, 0.45f, 0.15f, 0.90f) // Amber for required
                    : new Color(0.40f, 0.55f, 0.70f, 0.65f);
                p.lineWidth = 1.5f;

                // Border frame
                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMin + 4, rect.yMin + 2));
                p.LineTo(new Vector2(rect.xMax - 4, rect.yMin + 2));
                p.LineTo(new Vector2(rect.xMax - 4, rect.yMax - 2));
                p.LineTo(new Vector2(rect.xMin + 4, rect.yMax - 2));
                p.ClosePath();
                p.Stroke();

                // Plus symbol
                p.BeginPath();
                p.MoveTo(new Vector2(rect.center.x - 6 * _zoom, rect.center.y));
                p.LineTo(new Vector2(rect.center.x + 6 * _zoom, rect.center.y));
                p.MoveTo(new Vector2(rect.center.x, rect.center.y - 6 * _zoom));
                p.LineTo(new Vector2(rect.center.x, rect.center.y + 6 * _zoom));
                p.Stroke();
            }

            // Ghost preview silhouette
            if (_ghostSlotId.HasValue && _ghostComponent != null)
            {
                var targetSec = _anatomy.FindSectionBySlot(_ghostSlotId.Value);
                if (targetSec != null)
                {
                    var rect = GetSectionRect(targetSec, center, rocketTotalH, maxDiaPx);

                    p.fillColor = new Color(0.06f, 0.85f, 0.55f, 0.22f); // Mint green ghost fill
                    p.strokeColor = new Color(0.06f, 0.85f, 0.55f, 0.90f); // Bright ghost outline
                    p.lineWidth = 2.0f;

                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMin + 2, rect.yMin + 2));
                    p.LineTo(new Vector2(rect.xMax - 2, rect.yMin + 2));
                    p.LineTo(new Vector2(rect.xMax - 2, rect.yMax - 2));
                    p.LineTo(new Vector2(rect.xMin + 2, rect.yMax - 2));
                    p.ClosePath();
                    p.Fill();
                    p.Stroke();
                }
            }
        }

        private void DrawInteractionHighlights(Painter2D p, Vector2 center, float rocketTotalH, float maxDiaPx)
        {
            // Selected Section (Cyan glowing brackets)
            if (_selectedSlotId.HasValue)
            {
                var selSec = _anatomy.FindSectionBySlot(_selectedSlotId.Value);
                if (selSec != null)
                {
                    var rect = GetSectionRect(selSec, center, rocketTotalH, maxDiaPx);

                    p.strokeColor = new Color(0.06f, 0.85f, 0.95f, 1.0f);
                    p.lineWidth = 2.5f;

                    float bracketLen = 14f * _zoom;
                    // Top-Left bracket
                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMin - 6, rect.yMin + bracketLen));
                    p.LineTo(new Vector2(rect.xMin - 6, rect.yMin - 4));
                    p.LineTo(new Vector2(rect.xMin + bracketLen, rect.yMin - 4));
                    p.Stroke();

                    // Top-Right bracket
                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMax + 6, rect.yMin + bracketLen));
                    p.LineTo(new Vector2(rect.xMax + 6, rect.yMin - 4));
                    p.LineTo(new Vector2(rect.xMax - bracketLen, rect.yMin - 4));
                    p.Stroke();

                    // Bottom-Left bracket
                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMin - 6, rect.yMax - bracketLen));
                    p.LineTo(new Vector2(rect.xMin - 6, rect.yMax + 4));
                    p.LineTo(new Vector2(rect.xMin + bracketLen, rect.yMax + 4));
                    p.Stroke();

                    // Bottom-Right bracket
                    p.BeginPath();
                    p.MoveTo(new Vector2(rect.xMax + 6, rect.yMax - bracketLen));
                    p.LineTo(new Vector2(rect.xMax + 6, rect.yMax + 4));
                    p.LineTo(new Vector2(rect.xMax - bracketLen, rect.yMax + 4));
                    p.Stroke();
                }
            }

            // Hovered Section (Gentle cyan glow & leader line)
            if (_hoveredSection != null && _hoveredSection.SlotId != _selectedSlotId)
            {
                var rect = GetSectionRect(_hoveredSection, center, rocketTotalH, maxDiaPx);

                p.strokeColor = new Color(0.06f, 0.85f, 0.95f, 0.60f);
                p.lineWidth = 1.5f;

                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMin - 3, rect.yMin - 2));
                p.LineTo(new Vector2(rect.xMax + 3, rect.yMin - 2));
                p.LineTo(new Vector2(rect.xMax + 3, rect.yMax + 2));
                p.LineTo(new Vector2(rect.xMin - 3, rect.yMax + 2));
                p.ClosePath();
                p.Stroke();

                // Leader line to callout
                p.strokeColor = new Color(0.06f, 0.85f, 0.95f, 0.75f);
                p.lineWidth = 1.0f;
                p.BeginPath();
                p.MoveTo(new Vector2(rect.xMax + 3, rect.center.y));
                p.LineTo(new Vector2(rect.xMax + 16, rect.center.y));
                p.Stroke();
            }
        }
    }
}
