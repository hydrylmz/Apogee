using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Vehicle
{
    /// <summary>
    /// Lightweight 2D silhouette of the mission vehicle using Painter2D.
    /// Three display levels controlled by <see cref="SilhouetteLevel"/>:
    ///   - Launch  : full rocket stack (booster + upper stages + fairing)
    ///   - Cruise  : payload capsule / spacecraft bus only
    ///   - Landing : payload type shape (rover / lander / orbiter)
    ///
    /// Uses the same Painter2D pipeline established by HangarRocketCanvas (ADR-054).
    /// No sim state changes — purely presentational. (GDD §3.1, §15.1)
    /// </summary>
    public sealed class VehicleSilhouetteView : VisualElement
    {
        // ── Level & Payload enums (local to avoid Design layer coupling) ──────────
        public enum SilhouetteLevel { Launch, Cruise, Landing }

        /// <summary>
        /// Payload category used to pick the landing silhouette.
        /// Caller maps EdlSimulationParameters / MissionProfile data to this.
        /// </summary>
        public enum PayloadType { Rover, Lander, Orbiter }

        private SilhouetteLevel _level = SilhouetteLevel.Launch;
        private PayloadType     _payloadType = PayloadType.Orbiter;
        private string          _destinationId = "leo";

        // Destination colour accent (read from flight_visuals.json via caller)
        private Color _accentColor   = new Color(0.024f, 0.714f, 0.831f); // cyan default
        private Color _exhaustColor  = new Color(0.961f, 0.620f, 0.043f); // amber
        private bool  _animateExhaust;
        private float _exhaustPhase; // 0–1, driven by caller tick

        private IVisualElementScheduledItem _exhaustSchedule;

        // ── Construction ─────────────────────────────────────────────────────────
        public VehicleSilhouetteView()
        {
            style.width  = Length.Percent(100);
            style.height = Length.Percent(100);
            style.position = Position.Relative;
            generateVisualContent += OnGenerateVisualContent;
        }

        // ── Public API ───────────────────────────────────────────────────────────
        /// <summary>Destination identifier for context-aware payload accents.</summary>
        public string DestinationId => _destinationId;

        public void SetDestinationId(string id)
        {
            _destinationId = id ?? "leo";
            MarkDirtyRepaint();
        }

        /// <summary>Switch the display level and optionally repaint.</summary>
        public void SetLevel(SilhouetteLevel level)
        {
            if (_level == level) return;
            _level = level;
            MarkDirtyRepaint();
        }

        /// <summary>Payload type governs the landing shape (rover / lander / orbiter).</summary>
        public void SetPayloadType(PayloadType type)
        {
            _payloadType = type;
            MarkDirtyRepaint();
        }

        /// <summary>Accent colour derived from flight_visuals.json destination profile.</summary>
        public void SetAccentColor(Color accent)
        {
            _accentColor = accent;
            MarkDirtyRepaint();
        }

        /// <summary>Start a looping exhaust flicker animation at ~≤3 Hz (accessibility).</summary>
        public void StartExhaustAnimation()
        {
            if (_animateExhaust) return;
            _animateExhaust = true;
            // Update every 333 ms → ≤3 Hz, satisfying ReduceMotion spec §3.5
            _exhaustSchedule = schedule.Execute(() =>
            {
                _exhaustPhase = (_exhaustPhase + 0.15f) % 1f;
                MarkDirtyRepaint();
            }).Every(333);
        }

        /// <summary>Stop exhaust animation (ReduceMotion or non-launch phases).</summary>
        public void StopExhaustAnimation()
        {
            if (!_animateExhaust) return;
            _animateExhaust = false;
            _exhaustSchedule?.Pause();
            _exhaustSchedule = null;
            _exhaustPhase = 0f;
            MarkDirtyRepaint();
        }

        // ── Painter2D Draw ───────────────────────────────────────────────────────
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 10 || h < 10) return;

            switch (_level)
            {
                case SilhouetteLevel.Launch:  DrawLaunchRocket(painter, w, h);  break;
                case SilhouetteLevel.Cruise:  DrawCruiseCapsule(painter, w, h); break;
                case SilhouetteLevel.Landing: DrawLandingShape(painter, w, h);  break;
            }
        }

        // ── Launch: Full Rocket Stack ─────────────────────────────────────────────
        private void DrawLaunchRocket(Painter2D p, float w, float h)
        {
            float cx    = w * 0.5f;
            float baseY = h * 0.88f;
            float scale = Mathf.Min(w * 0.18f, h * 0.08f);

            // ─── Booster body
            float bW = scale * 1.2f;
            float bH = scale * 5.5f;
            DrawRect(p, cx - bW * 0.5f, baseY - bH, bW, bH, new Color(0.18f, 0.26f, 0.38f));

            // ─── Upper stage (narrower)
            float uW = bW * 0.75f;
            float uH = bH * 0.55f;
            DrawRect(p, cx - uW * 0.5f, baseY - bH - uH, uW, uH, new Color(0.22f, 0.32f, 0.46f));

            // ─── Fairing nose cone
            float fW = uW * 0.95f;
            float fH = uH * 0.7f;
            DrawNoseCone(p, cx, baseY - bH - uH, fW, fH, _accentColor);

            // ─── Fins (2 visible)
            float finW = bW * 0.6f;
            float finH = bH * 0.22f;
            DrawFin(p, cx - bW * 0.5f - finW, baseY - finH, finW, finH, false, new Color(0.14f, 0.22f, 0.32f));
            DrawFin(p, cx + bW * 0.5f,         baseY - finH, finW, finH, true,  new Color(0.14f, 0.22f, 0.32f));

            // ─── Exhaust plume (animated)
            if (_animateExhaust)
            {
                float plumeH = bH * (0.35f + _exhaustPhase * 0.2f);
                float plumeAlpha = 0.5f + _exhaustPhase * 0.4f;
                Color plumeOuter = new Color(_exhaustColor.r, _exhaustColor.g, _exhaustColor.b, plumeAlpha * 0.4f);
                Color plumeInner = new Color(1f, 0.95f, 0.8f, plumeAlpha * 0.9f);
                DrawExhaustPlume(p, cx, baseY, bW, plumeH, plumeInner, plumeOuter);
            }

            // ─── Accent stripe
            p.strokeColor = _accentColor;
            p.lineWidth   = 2f;
            float stripeY = baseY - bH * 0.65f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - bW * 0.5f, stripeY));
            p.LineTo(new Vector2(cx + bW * 0.5f, stripeY));
            p.Stroke();
        }

        // ── Cruise: Payload Capsule / Spacecraft Bus ──────────────────────────────
        private void DrawCruiseCapsule(Painter2D p, float w, float h)
        {
            float cx    = w * 0.5f;
            float cy    = h * 0.5f;
            float scale = Mathf.Min(w, h) * 0.22f;

            // Bus body (hexagonal approximation via rect)
            float busW = scale * 1.6f;
            float busH = scale * 1.0f;
            DrawRect(p, cx - busW * 0.5f, cy - busH * 0.5f, busW, busH, new Color(0.18f, 0.26f, 0.40f));

            // Solar arrays (left and right)
            float panelW = scale * 1.4f;
            float panelH = scale * 0.3f;
            float panelY = cy - panelH * 0.5f;
            DrawSolarPanel(p, cx - busW * 0.5f - panelW - 4f, panelY, panelW, panelH, _accentColor);
            DrawSolarPanel(p, cx + busW * 0.5f + 4f,           panelY, panelW, panelH, _accentColor);

            // High-gain antenna (top)
            float antH = scale * 0.6f;
            p.strokeColor = new Color(0.6f, 0.75f, 0.9f);
            p.lineWidth   = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - busH * 0.5f));
            p.LineTo(new Vector2(cx, cy - busH * 0.5f - antH));
            p.Stroke();
            // Dish
            p.fillColor = new Color(0.70f, 0.80f, 0.95f, 0.7f);
            p.BeginPath();
            p.Arc(new Vector2(cx, cy - busH * 0.5f - antH), scale * 0.18f, 0, 360, ArcDirection.Clockwise);
            p.Fill();

            // Accent dot (destination colour)
            p.fillColor = _accentColor;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy), scale * 0.08f, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        // ── Landing: Shape by Payload Type ───────────────────────────────────────
        private void DrawLandingShape(Painter2D p, float w, float h)
        {
            switch (_payloadType)
            {
                case PayloadType.Rover:   DrawRover(p, w, h);   break;
                case PayloadType.Lander:  DrawLander(p, w, h);  break;
                default:                  DrawOrbiter(p, w, h); break;
            }
        }

        private void DrawRover(Painter2D p, float w, float h)
        {
            float cx = w * 0.5f;
            float cy = h * 0.6f;
            float s  = Mathf.Min(w, h) * 0.15f;

            // Body
            DrawRect(p, cx - s, cy - s * 0.5f, s * 2f, s * 0.9f, new Color(0.22f, 0.33f, 0.48f));
            // Wheels (4)
            float wR = s * 0.28f;
            foreach (var xo in new[] { -s * 0.65f, s * 0.65f })
            {
                foreach (var yo in new[] { -s * 0.1f, s * 0.65f })
                {
                    p.fillColor = new Color(0.15f, 0.22f, 0.33f);
                    p.BeginPath();
                    p.Arc(new Vector2(cx + xo, cy + yo), wR, 0, 360, ArcDirection.Clockwise);
                    p.Fill();
                    p.strokeColor = _accentColor;
                    p.lineWidth   = 1.5f;
                    p.BeginPath();
                    p.Arc(new Vector2(cx + xo, cy + yo), wR, 0, 360, ArcDirection.Clockwise);
                    p.Stroke();
                }
            }
            // Mast
            p.strokeColor = new Color(0.6f, 0.75f, 0.9f);
            p.lineWidth   = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx + s * 0.3f, cy - s * 0.5f));
            p.LineTo(new Vector2(cx + s * 0.3f, cy - s * 1.3f));
            p.LineTo(new Vector2(cx + s * 0.7f, cy - s * 1.0f));
            p.Stroke();
        }

        private void DrawLander(Painter2D p, float w, float h)
        {
            float cx = w * 0.5f;
            float cy = h * 0.55f;
            float s  = Mathf.Min(w, h) * 0.15f;

            // Descent stage body (octagon-ish)
            DrawRect(p, cx - s, cy - s * 0.6f, s * 2f, s * 1.1f, new Color(0.20f, 0.30f, 0.44f));
            // Legs (4 diagonal)
            p.strokeColor = new Color(0.50f, 0.65f, 0.80f);
            p.lineWidth   = 2.5f;
            foreach (var sign in new[] { -1f, 1f })
            {
                p.BeginPath();
                p.MoveTo(new Vector2(cx + sign * s, cy + s * 0.4f));
                p.LineTo(new Vector2(cx + sign * s * 1.8f, cy + s * 1.2f));
                p.Stroke();
                // Foot pad
                p.strokeColor = _accentColor;
                p.lineWidth   = 3f;
                p.BeginPath();
                p.MoveTo(new Vector2(cx + sign * (s * 1.8f - s * 0.25f), cy + s * 1.2f));
                p.LineTo(new Vector2(cx + sign * (s * 1.8f + s * 0.25f), cy + s * 1.2f));
                p.Stroke();
                p.strokeColor = new Color(0.50f, 0.65f, 0.80f);
                p.lineWidth   = 2.5f;
            }
            // Engine nozzle
            p.fillColor = new Color(0.14f, 0.20f, 0.30f);
            p.BeginPath();
            p.Arc(new Vector2(cx, cy + s * 0.65f), s * 0.28f, 0, 360, ArcDirection.Clockwise);
            p.Fill();
            // Accent dot
            p.fillColor = _accentColor;
            p.BeginPath();
            p.Arc(new Vector2(cx, cy - s * 0.1f), s * 0.12f, 0, 360, ArcDirection.Clockwise);
            p.Fill();
        }

        private void DrawOrbiter(Painter2D p, float w, float h)
        {
            // Reuse cruise capsule for orbiter - same spacecraft bus
            DrawCruiseCapsule(p, w, h);
        }

        // ── Primitive Helpers ─────────────────────────────────────────────────────
        private static void DrawRect(Painter2D p, float x, float y, float rw, float rh, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + rw, y));
            p.LineTo(new Vector2(x + rw, y + rh));
            p.LineTo(new Vector2(x, y + rh));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawNoseCone(Painter2D p, float cx, float baseY, float coneW, float coneH, Color fill)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, baseY - coneH));         // tip
            p.LineTo(new Vector2(cx + coneW * 0.5f, baseY));  // base right
            p.LineTo(new Vector2(cx - coneW * 0.5f, baseY));  // base left
            p.ClosePath();
            p.Fill();
        }

        private static void DrawFin(Painter2D p, float x, float y, float fw, float fh, bool flipX, Color fill)
        {
            float dir = flipX ? -1f : 1f;
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(x + (flipX ? fw : 0f), y));
            p.LineTo(new Vector2(x + (flipX ? 0f : fw), y));
            p.LineTo(new Vector2(x + (flipX ? fw : 0f), y + fh));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawSolarPanel(Painter2D p, float x, float y, float pw, float ph, Color accent)
        {
            // Panel background
            DrawRect(p, x, y, pw, ph, new Color(0.08f, 0.14f, 0.26f));
            // Grid lines
            p.strokeColor = accent;
            p.lineWidth   = 1f;
            int cells = 3;
            for (int i = 1; i < cells; i++)
            {
                float lx = x + pw * i / cells;
                p.BeginPath();
                p.MoveTo(new Vector2(lx, y));
                p.LineTo(new Vector2(lx, y + ph));
                p.Stroke();
            }
            // Border
            p.strokeColor = accent;
            p.lineWidth   = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(x,      y));
            p.LineTo(new Vector2(x + pw, y));
            p.LineTo(new Vector2(x + pw, y + ph));
            p.LineTo(new Vector2(x,      y + ph));
            p.ClosePath();
            p.Stroke();
        }

        private static void DrawExhaustPlume(
            Painter2D p, float cx, float baseY, float rocketW,
            float plumeH, Color inner, Color outer)
        {
            // Outer glow
            p.fillColor = outer;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - rocketW * 0.5f, baseY));
            p.LineTo(new Vector2(cx + rocketW * 0.5f, baseY));
            p.LineTo(new Vector2(cx + rocketW * 0.15f, baseY + plumeH));
            p.LineTo(new Vector2(cx - rocketW * 0.15f, baseY + plumeH));
            p.ClosePath();
            p.Fill();
            // Inner core
            p.fillColor = inner;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - rocketW * 0.22f, baseY));
            p.LineTo(new Vector2(cx + rocketW * 0.22f, baseY));
            p.LineTo(new Vector2(cx + rocketW * 0.04f, baseY + plumeH * 0.55f));
            p.LineTo(new Vector2(cx - rocketW * 0.04f, baseY + plumeH * 0.55f));
            p.ClosePath();
            p.Fill();
        }
    }
}
