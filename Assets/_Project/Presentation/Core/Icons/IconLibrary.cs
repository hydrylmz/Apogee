using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Icons
{
    /// <summary>
    /// Programmatic vector icon renderer for the APOGEE UI design system.
    /// Every icon is authored on a 24x24 design grid and rendered with Painter2D;
    /// strokes/fills use a single <see cref="Color"/> supplied by the caller so icons
    /// can be tinted through the host element's <c>color</c> style (GDD §14/§15).
    /// Angle convention (UI coordinates, Y-down): 0° points right, angles grow clockwise,
    /// 90° points down, 270° points up.
    /// </summary>
    public static class IconLibrary
    {
        /// <summary>Authoring grid size every icon is designed against.</summary>
        public const float DesignSize = 24f;

        private const float BaseStrokeWidth = 1.8f;

        /// <summary>Returns true when a drawing routine exists for the given icon.</summary>
        public static bool IsDefined(IconType icon)
        {
            return icon != IconType.None;
        }

        /// <summary>
        /// Draws an icon, fitted (aspect-preserving) and centered inside <paramref name="rect"/>.
        /// </summary>
        public static void Draw(Painter2D painter, IconType icon, Rect rect, Color color)
        {
            if (painter == null || icon == IconType.None)
            {
                return;
            }

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(rect.width, rect.height) / DesignSize;
            Vector2 origin = rect.center - new Vector2(DesignSize, DesignSize) * (scale * 0.5f);

            var canvas = new Canvas(painter, origin, scale, color);
            canvas.Render(icon);
        }

        /// <summary>
        /// Thin drawing facade that maps 24-grid coordinates onto the actual content rect.
        /// </summary>
        private sealed class Canvas
        {
            private readonly Painter2D _painter;
            private readonly Vector2 _origin;
            private readonly float _scale;
            private readonly Color _color;

            public Canvas(Painter2D painter, Vector2 origin, float scale, Color color)
            {
                _painter = painter;
                _origin = origin;
                _scale = scale;
                _color = color;
            }

            public void Render(IconType icon)
            {
                _painter.strokeColor = _color;
                _painter.fillColor = _color;
                _painter.lineCap = LineCap.Round;
                _painter.lineJoin = LineJoin.Round;

                switch (icon)
                {
                    case IconType.Mission: DrawMission(); break;
                    case IconType.Workshop: DrawWorkshop(); break;
                    case IconType.SolarSystem: DrawSolarSystem(); break;
                    case IconType.Research: DrawResearch(); break;
                    case IconType.Production: DrawProduction(); break;
                    case IconType.Contracts: DrawContracts(); break;
                    case IconType.Bases: DrawBases(); break;
                    case IconType.Milestones: DrawMilestones(); break;
                    case IconType.Back: DrawBack(); break;
                    case IconType.Budget: DrawBudget(); break;
                    case IconType.Time: DrawTime(); break;
                    case IconType.Risk: DrawRisk(); break;
                    case IconType.Warning: DrawWarning(); break;
                    case IconType.Approve: DrawApprove(); break;
                    case IconType.Lock: DrawLock(); break;
                    case IconType.Sign: DrawSign(); break;
                    case IconType.Plus: DrawPlus(); break;
                    case IconType.Cancel: DrawCancel(); break;
                }
            }

            // ---------------------------------------------------------------- helpers

            private Vector2 P(float x, float y)
            {
                return _origin + new Vector2(x, y) * _scale;
            }

            private Vector2 Polar(Vector2 center, float radius, float degrees)
            {
                float rad = degrees * Mathf.Deg2Rad;
                return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius * _scale;
            }

            private void Stroke(float widthMultiplier = 1f)
            {
                _painter.lineWidth = BaseStrokeWidth * widthMultiplier * _scale;
                _painter.Stroke();
            }

            private void Begin()
            {
                _painter.BeginPath();
            }

            private void Line(float x1, float y1, float x2, float y2)
            {
                Begin();
                _painter.MoveTo(P(x1, y1));
                _painter.LineTo(P(x2, y2));
                Stroke();
            }

            private void Polyline(float[] points, float widthMultiplier = 1f, bool close = false)
            {
                Begin();
                _painter.MoveTo(P(points[0], points[1]));
                for (int i = 2; i + 1 < points.Length; i += 2)
                {
                    _painter.LineTo(P(points[i], points[i + 1]));
                }

                if (close)
                {
                    _painter.ClosePath();
                }

                Stroke(widthMultiplier);
            }

            private void Ring(float cx, float cy, float radius, float widthMultiplier = 1f)
            {
                Begin();
                _painter.Arc(P(cx, cy), radius * _scale, 0f, 360f);
                Stroke(widthMultiplier);
            }

            /// <summary>Stroked arc; start/sweep in degrees on the 24-grid convention.</summary>
            private void ArcStroke(Vector2 center24, float radius, float startDegrees, float sweepDegrees, float widthMultiplier = 1f)
            {
                Begin();
                _painter.Arc(P(center24.x, center24.y), radius * _scale, startDegrees, sweepDegrees);
                Stroke(widthMultiplier);
            }

            /// <summary>Filled circle.</summary>
            private void Dot(float cx, float cy, float radius)
            {
                Begin();
                _painter.Arc(P(cx, cy), radius * _scale, 0f, 360f);
                _painter.Fill();
            }

            // ---------------------------------------------------------------- primary icons

            /// <summary>Mission: orbit arc sweeping past a target reticle.</summary>
            private void DrawMission()
            {
                var center = new Vector2(12f, 12f);

                Ring(center.x, center.y, 4f);
                Dot(center.x, center.y, 1.4f);

                // Reticle ticks (N/E/S/W).
                Line(18.5f, 12f, 21.5f, 12f);
                Line(2.5f, 12f, 5.5f, 12f);
                Line(12f, 2.5f, 12f, 5.5f);
                Line(12f, 18.5f, 12f, 21.5f);

                // Orbit arc passing across the lower-left.
                ArcStroke(new Vector2(16f, 8f), 12f, 90f, 90f);
            }

            /// <summary>Workshop: wrench — shaft plus open-jaw head.</summary>
            private void DrawWorkshop()
            {
                // Open jaw: C-shaped arc with the gap facing the upper-right (45°).
                ArcStroke(new Vector2(15f, 9f), 3.6f, 145f, 160f, 1.25f);

                // Shaft from lower-left into the head.
                Line(6f, 18f, 12.6f, 11.4f, 1.25f);
            }

            private void Line(float x1, float y1, float x2, float y2, float widthMultiplier)
            {
                Begin();
                _painter.MoveTo(P(x1, y1));
                _painter.LineTo(P(x2, y2));
                Stroke(widthMultiplier);
            }

            /// <summary>SolarSystem: two orbit rings, a sun core and planet dots.</summary>
            private void DrawSolarSystem()
            {
                Ring(12f, 12f, 9f);
                Ring(12f, 12f, 4.5f);
                Dot(12f, 12f, 1.9f);
                Dot(12f, 3f, 1.8f);            // outer planet, top of the outer ring
                Dot(15.2f, 15.2f, 1.3f);       // inner planet, 45° on the inner ring
            }

            /// <summary>Research: branching node tree (root, two branches, four leaves).</summary>
            private void DrawResearch()
            {
                // Edges first so dots sit cleanly on top.
                Line(12f, 20f, 7f, 13f);
                Line(12f, 20f, 17f, 13f);
                Line(7f, 13f, 4f, 6f);
                Line(7f, 13f, 9f, 6f);
                Line(17f, 13f, 15f, 6f);
                Line(17f, 13f, 20f, 6f);

                Dot(12f, 20f, 2f);   // root
                Dot(7f, 13f, 1.7f);  // branch left
                Dot(17f, 13f, 1.7f); // branch right
                Dot(4f, 6f, 1.3f);   // leaves
                Dot(9f, 6f, 1.3f);
                Dot(15f, 6f, 1.3f);
                Dot(20f, 6f, 1.3f);
            }

            /// <summary>Production: gear — body circle, hub and eight teeth.</summary>
            private void DrawProduction()
            {
                Ring(12f, 12f, 5.2f);
                Dot(12f, 12f, 1.6f);

                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f;
                    var from = Polar(new Vector2(12f, 12f), 6.2f, angle);
                    var to = Polar(new Vector2(12f, 12f), 8.6f, angle);
                    Begin();
                    _painter.MoveTo(from);
                    _painter.LineTo(to);
                    Stroke();
                }
            }

            /// <summary>Contracts: document sheet with folded corner and a wax seal.</summary>
            private void DrawContracts()
            {
                // Sheet with cut corner.
                Polyline(new[] { 6f, 3f, 13.5f, 3f, 18f, 7.5f, 18f, 21f, 6f, 21f }, 1f, true);
                // Fold crease.
                Polyline(new[] { 13.5f, 3f, 13.5f, 7.5f, 18f, 7.5f });
                // Text lines.
                Line(8.5f, 11.5f, 15.5f, 11.5f);
                Line(8.5f, 14.5f, 13f, 14.5f);
                // Seal.
                Dot(10.5f, 18f, 2.1f);
            }

            /// <summary>Bases: radar dish on a mast radiating signal waves.</summary>
            private void DrawBases()
            {
                // Mast + base feet.
                Line(12f, 20f, 12f, 13f);
                Line(7.5f, 20f, 16.5f, 20f);

                // Dish: bowl above the mast foot.
                ArcStroke(new Vector2(12f, 13f), 4.5f, 180f, 180f);

                // Signal waves radiating upward.
                ArcStroke(new Vector2(12f, 13f), 7.5f, 235f, 70f);
                ArcStroke(new Vector2(12f, 13f), 10.5f, 242f, 56f);
            }

            /// <summary>Milestones: ridge line with a flag planted on the main summit.</summary>
            private void DrawMilestones()
            {
                // Mountain ridge.
                Polyline(new[] { 3f, 20f, 9f, 9f, 12.5f, 13.5f, 16f, 7f, 21f, 20f });

                // Flag pole + pennant.
                Line(16f, 7f, 16f, 2.5f);
                Begin();
                _painter.MoveTo(P(16f, 2.5f));
                _painter.LineTo(P(20.5f, 4f));
                _painter.LineTo(P(16f, 5.5f));
                _painter.ClosePath();
                _painter.Fill();
            }

            // ---------------------------------------------------------------- auxiliary icons

            private void DrawBack()
            {
                Line(19.5f, 12f, 5f, 12f, 1.2f);
                Polyline(new[] { 10.5f, 7.5f, 5f, 12f, 10.5f, 16.5f }, 1.2f);
            }

            private void DrawBudget()
            {
                // Banknote body.
                Polyline(new[] { 4.5f, 7.5f, 19.5f, 7.5f, 21f, 9f, 21f, 15f, 19.5f, 16.5f, 4.5f, 16.5f, 3f, 15f, 3f, 9f }, 1f, true);
                Ring(12f, 12f, 2.8f);
                Dot(5.8f, 12f, 0.9f);
                Dot(18.2f, 12f, 0.9f);
            }

            private void DrawTime()
            {
                Ring(12f, 12f, 8f);
                Line(12f, 12f, 12f, 7f);
                Line(12f, 12f, 15.2f, 14.2f);
                Dot(12f, 12f, 1.2f);
            }

            private void DrawRisk()
            {
                Polyline(new[] { 12f, 3f, 21f, 12f, 12f, 21f, 3f, 12f }, 1f, true);
                Line(12f, 8.5f, 12f, 13.5f);
                Dot(12f, 16f, 1.1f);
            }

            private void DrawWarning()
            {
                Polyline(new[] { 12f, 4f, 21f, 19.5f, 3f, 19.5f }, 1f, true);
                Line(12f, 9f, 12f, 13.8f);
                Dot(12f, 16.6f, 1.1f);
            }

            private void DrawApprove()
            {
                Polyline(new[] { 4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 6.5f }, 1.35f);
            }

            private void DrawLock()
            {
                // Shackle.
                ArcStroke(new Vector2(12f, 11f), 3.8f, 180f, 180f);
                // Body.
                Polyline(new[] { 6f, 11f, 18f, 11f, 18f, 20f, 6f, 20f }, 1f, true);
                // Keyhole.
                Dot(12f, 14.5f, 1.3f);
                Line(12f, 15.5f, 12f, 17.5f);
            }

            private void DrawSign()
            {
                // Signature stroke.
                Polyline(new[] { 3.5f, 16f, 5f, 12.5f, 6.5f, 15.5f, 8f, 10.5f, 10f, 16f, 12f, 12.5f, 14.5f, 14.5f, 17f, 11f, 20.5f, 13.5f });
                // Baseline.
                Line(3.5f, 19.5f, 20.5f, 19.5f);
            }

            private void DrawPlus()
            {
                Line(12f, 5.5f, 12f, 18.5f, 1.35f);
                Line(5.5f, 12f, 18.5f, 12f, 1.35f);
            }

            private void DrawCancel()
            {
                Line(6f, 6f, 18f, 18f, 1.35f);
                Line(18f, 6f, 6f, 18f, 1.35f);
            }
        }
    }
}
