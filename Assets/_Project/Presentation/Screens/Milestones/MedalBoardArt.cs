using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Milestones
{
    /// <summary>
    /// Painter2D artwork for the "Madalya Panosu" identity (GDD §4.4/§14,
    /// ADR-063 template): an oak wall with a hung deep-green felt patch board,
    /// double gold-stitch frame, corner tacks and a large embroidered compass
    /// rose behind the milestone cards. Also draws the circular medal patch
    /// badge (gold ring + crimson ribbon + star) reused on cards and plaque.
    /// Colors mirror the .theme-medal-board USS tokens and are kept in sync
    /// manually (ADR-063 known constraint — canvas constants cannot read USS).
    /// </summary>
    public static class MedalBoardArt
    {
        // ── Palette — keep in sync with ThemeMedalBoard.uss ────────────────────
        public static readonly Color OakWallColor = new Color(0.141f, 0.106f, 0.078f, 1f);       // --color-bg-canvas-dark
        public static readonly Color OakFrameColor = new Color(0.227f, 0.173f, 0.118f, 1f);      // --oak-frame
        public static readonly Color FeltColor = new Color(0.090f, 0.224f, 0.173f, 1f);          // --felt-bg
        public static readonly Color FeltDeepColor = new Color(0.071f, 0.173f, 0.133f, 1f);      // --felt-bg-deep
        public static readonly Color StitchColor = new Color(0.941f, 0.902f, 0.784f, 0.20f);     // --felt-stitch
        public static readonly Color GoldThreadColor = new Color(0.831f, 0.690f, 0.369f, 0.85f); // --gold-thread
        public static readonly Color GoldFaintColor = new Color(0.831f, 0.690f, 0.369f, 0.28f);  // felt-edge alpha
        public static readonly Color RoseInkColor = new Color(0.937f, 0.902f, 0.800f, 0.10f);    // embroidered rose
        public static readonly Color RibbonCrimsonColor = new Color(0.627f, 0.227f, 0.227f, 1f); // --ribbon-crimson

        /// <summary>
        /// Draws the display-case wall: oak paneling, the hung felt board with
        /// oak frame, inner gold stitch border and corner tacks, and a large
        /// embroidered compass rose centered on the felt.
        /// </summary>
        public static void DrawFeltBoard(MeshGenerationContext ctx, Rect rect)
        {
            if (rect.width <= 24f || rect.height <= 24f)
            {
                return;
            }

            var painter = ctx.painter2D;

            // 1. Oak wall paneling (subtle vertical grain strips)
            painter.strokeColor = new Color(1f, 1f, 1f, 0.02f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            float strip = 42f;
            for (float x = rect.x + strip; x < rect.x + rect.width; x += strip)
            {
                painter.MoveTo(new Vector2(x, rect.y));
                painter.LineTo(new Vector2(x, rect.y + rect.height));
            }
            painter.Stroke();

            // 2. The hung felt board (inset rect with oak frame)
            float inset = 10f;
            var board = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f);

            painter.fillColor = OakFrameColor;
            painter.BeginPath();
            painter.MoveTo(board.min);
            painter.LineTo(new Vector2(board.xMax, board.yMin));
            painter.LineTo(new Vector2(board.xMax, board.yMax));
            painter.LineTo(new Vector2(board.xMin, board.yMax));
            painter.ClosePath();
            painter.Fill();

            float frame = 6f;
            var felt = new Rect(board.x + frame, board.y + frame, board.width - frame * 2f, board.height - frame * 2f);
            painter.fillColor = FeltColor;
            painter.BeginPath();
            painter.MoveTo(felt.min);
            painter.LineTo(new Vector2(felt.xMax, felt.yMin));
            painter.LineTo(new Vector2(felt.xMax, felt.yMax));
            painter.LineTo(new Vector2(felt.xMin, felt.yMax));
            painter.ClosePath();
            painter.Fill();

            // 3. Inner gold stitch border (dashed feel via short segments)
            float s = 5f;
            var stitchRect = new Rect(felt.x + s, felt.y + s, felt.width - s * 2f, felt.height - s * 2f);
            painter.strokeColor = StitchColor;
            painter.lineWidth = 1.1f;
            DrawRectSegments(painter, stitchRect, 14f, 9f);

            // 4. Corner tacks (small gold studs)
            painter.fillColor = GoldThreadColor;
            DrawTack(painter, stitchRect.xMin + 8f, stitchRect.yMin + 8f);
            DrawTack(painter, stitchRect.xMax - 8f, stitchRect.yMin + 8f);
            DrawTack(painter, stitchRect.xMax - 8f, stitchRect.yMax - 8f);
            DrawTack(painter, stitchRect.xMin + 8f, stitchRect.yMax - 8f);

            // 5. Embroidered compass rose at the felt center
            float cx = felt.x + felt.width * 0.5f;
            float cy = felt.y + felt.height * 0.5f;
            float roseR = Mathf.Min(felt.width, felt.height) * 0.30f;
            DrawCompassRose(painter, cx, cy, roseR);
        }

        private static void DrawTack(Painter2D painter, float x, float y)
        {
            painter.BeginPath();
            painter.Arc(new Vector2(x, y), 2.2f, 0f, Mathf.PI * 2f);
            painter.Fill();
        }

        /// <summary>Draws a rectangle as short dash segments (stitch look).</summary>
        private static void DrawRectSegments(Painter2D painter, Rect r, float dash, float gap)
        {
            painter.BeginPath();

            // top & bottom
            for (float x = r.xMin; x < r.xMax; x += dash + gap)
            {
                float xEnd = Mathf.Min(x + dash, r.xMax);
                painter.MoveTo(new Vector2(x, r.yMin));
                painter.LineTo(new Vector2(xEnd, r.yMin));
                painter.MoveTo(new Vector2(r.xMax - x, r.yMax));
                painter.LineTo(new Vector2(r.xMax - xEnd, r.yMax));
            }

            // left & right
            for (float y = r.yMin; y < r.yMax; y += dash + gap)
            {
                float yEnd = Mathf.Min(y + dash, r.yMax);
                painter.MoveTo(new Vector2(r.xMin, y));
                painter.LineTo(new Vector2(r.xMin, yEnd));
                painter.MoveTo(new Vector2(r.xMax, r.yMax - y));
                painter.LineTo(new Vector2(r.xMax, r.yMax - yEnd));
            }

            painter.Stroke();
        }

        /// <summary>
        /// Embroidered compass rose: faint outer ring, 8-point star (long
        /// cardinal + short diagonal points) and a needle pointing north.
        /// </summary>
        private static void DrawCompassRose(Painter2D painter, float cx, float cy, float radius)
        {
            // Outer embroidered ring
            painter.strokeColor = RoseInkColor;
            painter.lineWidth = 1.4f;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), radius, 0f, Mathf.PI * 2f);
            painter.Stroke();

            painter.strokeColor = RoseInkColor;
            painter.lineWidth = 0.9f;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), radius * 0.62f, 0f, Mathf.PI * 2f);
            painter.Stroke();

            // 8-point star
            painter.fillColor = RoseInkColor;
            for (int i = 0; i < 8; i++)
            {
                float rad = i * Mathf.PI * 0.25f - Mathf.PI * 0.5f; // start pointing up
                bool cardinal = i % 2 == 0;
                float len = cardinal ? radius * 0.95f : radius * 0.55f;
                float halfW = cardinal ? radius * 0.10f : radius * 0.07f;

                var tip = new Vector2(cx + Mathf.Cos(rad) * len, cy + Mathf.Sin(rad) * len);
                var perp = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * halfW;
                var baseL = new Vector2(cx, cy) + perp;
                var baseR = new Vector2(cx, cy) - perp;

                painter.BeginPath();
                painter.MoveTo(tip);
                painter.LineTo(baseL);
                painter.LineTo(baseR);
                painter.ClosePath();
                painter.Fill();
            }

            // Needle: crimson north half + faint south half
            painter.fillColor = new Color(RibbonCrimsonColor.r, RibbonCrimsonColor.g, RibbonCrimsonColor.b, 0.16f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - radius * 0.88f));
            painter.LineTo(new Vector2(cx + radius * 0.06f, cy));
            painter.LineTo(new Vector2(cx - radius * 0.06f, cy));
            painter.ClosePath();
            painter.Fill();

            painter.fillColor = new Color(0.937f, 0.902f, 0.800f, 0.10f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy + radius * 0.55f));
            painter.LineTo(new Vector2(cx - radius * 0.06f, cy));
            painter.LineTo(new Vector2(cx + radius * 0.06f, cy));
            painter.ClosePath();
            painter.Fill();

            // Hub
            painter.fillColor = new Color(GoldThreadColor.r, GoldThreadColor.g, GoldThreadColor.b, 0.30f);
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), radius * 0.06f, 0f, Mathf.PI * 2f);
            painter.Fill();
        }

        /// <summary>
        /// Draws the circular medal patch inside <paramref name="rect"/>:
        /// gold outer ring, dark felt disc, 5-point star and a crimson
        /// ribbon hanging from the bottom. Called from MedalPatchBadge.
        /// </summary>
        public static void DrawMedalPatch(MeshGenerationContext ctx, Rect rect)
        {
            if (rect.width <= 8f || rect.height <= 8f)
            {
                return;
            }

            var painter = ctx.painter2D;
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.42f;
            float r = Mathf.Min(rect.width, rect.height * 0.84f) * 0.5f;

            // Ribbon (crimson, V-notch tail below the disc)
            float ribW = r * 0.42f;
            float ribTop = cy + r * 0.55f;
            float ribBot = Mathf.Min(rect.yMax, ribTop + r * 0.95f);
            painter.fillColor = RibbonCrimsonColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx - ribW * 0.5f, ribTop));
            painter.LineTo(new Vector2(cx + ribW * 0.5f, ribTop));
            painter.LineTo(new Vector2(cx + ribW * 0.5f, ribBot));
            painter.LineTo(new Vector2(cx, ribBot - ribW * 0.28f));
            painter.LineTo(new Vector2(cx - ribW * 0.5f, ribBot));
            painter.ClosePath();
            painter.Fill();

            // Gold ring
            painter.fillColor = GoldThreadColor;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), r, 0f, Mathf.PI * 2f);
            painter.Fill();

            // Felt disc
            painter.fillColor = FeltDeepColor;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), r * 0.86f, 0f, Mathf.PI * 2f);
            painter.Fill();

            // 5-point embroidered star
            painter.fillColor = GoldThreadColor;
            float starR = r * 0.55f;
            for (int i = 0; i < 5; i++)
            {
                float rad = i * Mathf.PI * 0.4f - Mathf.PI * 0.5f;
                DrawStarSpoke(painter, cx, cy, rad, starR, starR * 0.38f);
            }

            // Ring stitch
            painter.strokeColor = StitchColor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), r * 0.72f, 0f, Mathf.PI * 2f);
            painter.Stroke();
        }

        private static void DrawStarSpoke(Painter2D painter, float cx, float cy, float rad, float len, float halfW)
        {
            var tip = new Vector2(cx + Mathf.Cos(rad) * len, cy + Mathf.Sin(rad) * len);
            var perp = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * halfW;
            var center = new Vector2(cx, cy);

            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(center + perp);
            painter.LineTo(center + perp * -0.55f + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * len * 0.35f);
            painter.LineTo(center - perp);
            painter.ClosePath();
            painter.Fill();
        }
    }

    /// <summary>
    /// Felt patch-board backdrop element (generateVisualContent). Decorative
    /// only: picking mode is ignored so cards and buttons stay clickable.
    /// </summary>
    public sealed class MedalBoardBackdrop : VisualElement
    {
        public const string UssClassName = "medal-backdrop";

        public MedalBoardBackdrop()
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
            MedalBoardArt.DrawFeltBoard(ctx, contentRect);
        }
    }

    /// <summary>
    /// Circular medal patch badge (gold ring + felt star + crimson ribbon).
    /// Decorative only (PickingMode.Ignore).
    /// </summary>
    public sealed class MedalPatchBadge : VisualElement
    {
        public const string UssClassName = "medal-patch";

        public MedalPatchBadge()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            MedalBoardArt.DrawMedalPatch(ctx, contentRect);
        }
    }
}
