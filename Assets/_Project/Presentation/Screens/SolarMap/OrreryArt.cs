using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// Painter2D artwork for the planetaryum / brass astrolabe identity
    /// (GDD §6.2, §14, ADR-063 template): a night-sky field with deterministic
    /// stars, concentric brass astrolabe rings and degree ticks behind the
    /// dynamic planet markers. Colors mirror the .theme-orrery USS tokens and
    /// are kept in sync manually (ADR-063 known constraint — canvas constants
    /// cannot read USS).
    /// </summary>
    public static class OrreryArt
    {
        // ── Palette — keep in sync with ThemeOrrery.uss ────────────────────────
        public static readonly Color StarColor = new Color(0.953f, 0.937f, 0.894f, 0.65f);   // --orrery-star
        public static readonly Color RingColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.38f);  // --orrery-ring
        public static readonly Color RingFaintColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.16f); // --orrery-ring-faint
        public static readonly Color TickColor = new Color(201f / 255f, 162f / 255f, 94f / 255f, 0.55f);  // --orrery-tick
        public static readonly Color RingBrightColor = new Color(232f / 255f, 206f / 255f, 156f / 255f, 0.85f); // --orrery-ring-bright
        public static readonly Color SunGlowColor = new Color(232f / 255f, 184f / 255f, 75f / 255f, 0.16f);

        /// <summary>Ring radii as fractions of the canvas dimensions. Kept in
        /// sync with CelestialBodyItemViewModel.OrbitVisualRadiusPercent bands
        /// (Venus ~14%, LEO/Moon/Earth ~25%, Mars ~35%, Europa ~44%). Markers are
        /// positioned in per-axis percentages, so rings are drawn as matching
        /// ellipses (rx = fraction × width, ry = fraction × height).</summary>
        public static readonly float[] RingFractions = { 0.14f, 0.25f, 0.35f, 0.44f };

        /// <summary>Deterministic star field (same layout every repaint).</summary>
        private static readonly (float U, float V, float R)[] Stars = BuildStars();

        private static (float, float, float)[] BuildStars()
        {
            var rng = new System.Random(19620426); // deterministic seed — same sky every run
            var stars = new (float, float, float)[90];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = ((float)rng.NextDouble(), (float)rng.NextDouble(), 0.6f + (float)rng.NextDouble());
            }
            return stars;
        }

        /// <summary>Point on the ellipse for angle <paramref name="rad"/> (0 = +x axis).</summary>
        private static Vector2 EllipsePoint(float cx, float cy, float rx, float ry, float rad)
        {
            return new Vector2(cx + Mathf.Cos(rad) * rx, cy + Mathf.Sin(rad) * ry);
        }

        /// <summary>
        /// Draws the astrolabe plate: star field, concentric brass rings (elliptical,
        /// aligned with the normalized planet positions) with degree ticks (every 10°,
        /// long tick every 30°) and a faint sun glow at the plate center.
        /// </summary>
        public static void DrawAstrolabe(MeshGenerationContext ctx, Rect rect)
        {
            if (rect.width <= 8f || rect.height <= 8f)
            {
                return;
            }

            var painter = ctx.painter2D;
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.5f;

            // Star field (avoid the central plate area so rings read cleanly)
            float centerR = Mathf.Min(rect.width, rect.height) * 0.22f;
            painter.fillColor = StarColor;
            foreach (var (u, v, r) in Stars)
            {
                float sx = rect.x + u * rect.width;
                float sy = rect.y + v * rect.height;
                float dx = sx - cx;
                float dy = sy - cy;
                if (dx * dx + dy * dy < centerR * centerR)
                {
                    continue; // too close to the sun glow
                }

                painter.BeginPath();
                painter.Arc(new Vector2(sx, sy), r, 0f, Mathf.PI * 2f);
                painter.Fill();
            }

            // Faint sun glow disc at center
            painter.fillColor = SunGlowColor;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), Mathf.Min(rect.width, rect.height) * 0.16f, 0f, Mathf.PI * 2f);
            painter.Fill();

            // Concentric brass rings; outermost ring carries the degree ticks
            for (int i = 0; i < RingFractions.Length; i++)
            {
                float rx = rect.width * 0.5f * RingFractions[i] * 2f;
                float ry = rect.height * 0.5f * RingFractions[i] * 2f;
                bool outermost = i == RingFractions.Length - 1;

                painter.strokeColor = outermost ? RingColor : RingFaintColor;
                painter.lineWidth = outermost ? 1.6f : 1.1f;
                painter.BeginPath();
                const int Segments = 96;
                for (int s = 0; s <= Segments; s++)
                {
                    var p = EllipsePoint(cx, cy, rx, ry, s * Mathf.PI * 2f / Segments);
                    if (s == 0)
                    {
                        painter.MoveTo(p);
                    }
                    else
                    {
                        painter.LineTo(p);
                    }
                }
                painter.Stroke();

                // Degree ticks on every ring; longer on the outermost
                float tickInnerRx = outermost ? rx - 7f : rx - 3.5f;
                float tickInnerRy = outermost ? ry - 7f : ry - 3.5f;
                painter.strokeColor = TickColor;
                painter.lineWidth = outermost ? 1.2f : 0.8f;
                painter.BeginPath();
                for (int deg = 0; deg < 360; deg += 10)
                {
                    float rad = deg * Mathf.Deg2Rad;
                    bool major = deg % 30 == 0;
                    float innerRx = major ? tickInnerRx - (outermost ? 3f : 1.5f) : tickInnerRx;
                    float innerRy = major ? tickInnerRy - (outermost ? 3f : 1.5f) : tickInnerRy;

                    painter.MoveTo(EllipsePoint(cx, cy, innerRx, innerRy, rad));
                    painter.LineTo(EllipsePoint(cx, cy, rx, ry, rad));
                }
                painter.Stroke();

                // Cardinal diamonds on the outermost ring
                if (outermost)
                {
                    painter.fillColor = RingBrightColor;
                    for (int deg = 0; deg < 360; deg += 90)
                    {
                        var p = EllipsePoint(cx, cy, rx, ry, deg * Mathf.Deg2Rad);
                        painter.BeginPath();
                        painter.MoveTo(p + new Vector2(0f, -3.5f));
                        painter.LineTo(p + new Vector2(3.5f, 0f));
                        painter.LineTo(p + new Vector2(0f, 3.5f));
                        painter.LineTo(p + new Vector2(-3.5f, 0f));
                        painter.ClosePath();
                        painter.Fill();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Astrolabe backdrop element (generateVisualContent). Decorative only:
    /// picking mode is ignored so planet markers and pins stay clickable.
    /// </summary>
    public sealed class OrreryBackdrop : VisualElement
    {
        public const string UssClassName = "orrery-backdrop";

        public OrreryBackdrop()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            OrreryArt.DrawAstrolabe(ctx, contentRect);
        }
    }
}
