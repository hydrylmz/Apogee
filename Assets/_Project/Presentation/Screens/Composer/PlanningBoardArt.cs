using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// Painter2D artwork for the mission planning board identity (GDD §14, ADR-063 template).
    /// "Uçuş planı dosyası": a dark planning desk backdrop with cork-board seams and
    /// push-pins, plus a circular mission patch badge (orbit arc + chevron) used on the
    /// dossier panel. Colors mirror the .theme-planning-board USS tokens and are kept
    /// in sync manually (ADR-063 known constraint — canvas constants cannot read USS).
    /// </summary>
    public static class PlanningBoardArt
    {
        // ── Palette — keep in sync with ThemePlanningBoard.uss ─────────────────
        public static readonly Color DeskColor = new Color(0.086f, 0.106f, 0.122f, 1f);      // --color-bg-canvas-dark
        public static readonly Color SeamColor = new Color(0.160f, 0.200f, 0.227f, 0.55f);   // panel border subtle
        public static readonly Color PinColor = new Color(0.698f, 0.227f, 0.180f, 0.9f);     // --stamp-red
        public static readonly Color PatchRingColor = new Color(0.784f, 0.682f, 0.518f, 1f); // --kraft-bg
        public static readonly Color PatchOrbitColor = new Color(0.243f, 0.561f, 0.769f, 1f);// --chart-blue
        public static readonly Color PatchGroundColor = new Color(0.145f, 0.199f, 0.290f, 1f); // --ink-navy

        /// <summary>Draws the circular mission patch: ring, orbit arc, chevron star.</summary>
        public static void DrawPatch(MeshGenerationContext ctx, Rect rect)
        {
            var painter = ctx.painter2D;
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f - 1.5f;
            if (r <= 2f)
            {
                return;
            }

            // Outer stitched ring
            painter.strokeColor = PatchRingColor;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), r, 0f, Mathf.PI * 2f);
            painter.Stroke();

            // Inner disc
            painter.fillColor = PatchGroundColor;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), r - 3f, 0f, Mathf.PI * 2f);
            painter.Fill();

            // Orbit arc across the disc
            painter.strokeColor = PatchOrbitColor;
            painter.lineWidth = 1.8f;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), (r - 3f) * 0.62f, Mathf.PI * 0.15f, Mathf.PI * 1.35f);
            painter.Stroke();

            // Apex chevron ("Apogee" nod) above the orbit
            float apexY = cy - (r - 3f) * 0.62f;
            painter.strokeColor = PatchRingColor;
            painter.lineWidth = 2f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx - r * 0.28f, apexY + r * 0.22f));
            painter.LineTo(new Vector2(cx, apexY - r * 0.08f));
            painter.LineTo(new Vector2(cx + r * 0.28f, apexY + r * 0.22f));
            painter.Stroke();

            // Planet dot on the orbit
            float dotAngle = Mathf.PI * 0.62f;
            var dotPos = new Vector2(
                cx + Mathf.Cos(dotAngle) * (r - 3f) * 0.62f,
                cy + Mathf.Sin(dotAngle) * (r - 3f) * 0.62f);
            painter.fillColor = PatchRingColor;
            painter.BeginPath();
            painter.Arc(dotPos, Mathf.Max(1.6f, r * 0.09f), 0f, Mathf.PI * 2f);
            painter.Fill();
        }
    }

    /// <summary>
    /// Circular mission patch element (generateVisualContent). Decorative only:
    /// picking mode is ignored so it never blocks clicks.
    /// </summary>
    public sealed class PlanningPatchBadge : VisualElement
    {
        public PlanningPatchBadge(float size)
        {
            AddToClassList("planning-patch");
            style.width = size;
            style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            PlanningBoardArt.DrawPatch(ctx, contentRect);
        }
    }

    /// <summary>
    /// Planning desk backdrop: dark desk surface with cork-board seams and four
    /// push-pins in the corners. Decorative only (pickingMode = Ignore).
    /// </summary>
    public sealed class PlanningBoardBackdrop : VisualElement
    {
        public PlanningBoardBackdrop()
        {
            AddToClassList("planning-backdrop");
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
            var painter = ctx.painter2D;
            var rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            // Desk surface
            painter.fillColor = PlanningBoardArt.DeskColor;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.Fill();

            // Cork-board seams (vertical, like pinned notice boards)
            painter.strokeColor = PlanningBoardArt.SeamColor;
            painter.lineWidth = 1.5f;
            float seamStep = Mathf.Max(180f, rect.width / 5f);
            painter.BeginPath();
            for (float x = seamStep; x < rect.width - 24f; x += seamStep)
            {
                painter.MoveTo(new Vector2(rect.x + x, rect.y + 8f));
                painter.LineTo(new Vector2(rect.x + x, rect.yMax - 8f));
            }
            painter.Stroke();

            // Push pins near the panel corners
            painter.fillColor = PlanningBoardArt.PinColor;
            float pad = 14f;
            DrawPin(painter, rect.x + pad, rect.y + pad);
            DrawPin(painter, rect.xMax - pad, rect.y + pad);
            DrawPin(painter, rect.x + pad, rect.yMax - pad);
            DrawPin(painter, rect.xMax - pad, rect.yMax - pad);
        }

        private static void DrawPin(Painter2D painter, float x, float y)
        {
            painter.BeginPath();
            painter.Arc(new Vector2(x, y), 3f, 0f, Mathf.PI * 2f);
            painter.Fill();
        }
    }
}
