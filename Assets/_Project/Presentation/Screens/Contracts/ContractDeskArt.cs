using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Contracts
{
    /// <summary>
    /// Painter2D backdrop of the contract desk: walnut planks with subtle grain
    /// lines and an occasional knot. Drawn behind all desk furniture; ignores
    /// picking so it never intercepts clicks. Palette mirrors the
    /// .theme-contract-desk USS tokens.
    /// </summary>
    public sealed class ContractDeskBackdrop : VisualElement
    {
        public const string BackdropClassName = "contract-desk-backdrop";

        // Palette (matches .theme-contract-desk tokens)
        private static readonly Color SeamColor = new(0.055f, 0.031f, 0.020f, 0.85f);      // #150C08-ish
        private static readonly Color GrainColor = new(0.000f, 0.000f, 0.000f, 0.16f);
        private static readonly Color GrainLightColor = new(0.910f, 0.780f, 0.560f, 0.05f);
        private static readonly Color KnotColor = new(0.020f, 0.010f, 0.006f, 0.28f);

        private const float PlankWidth = 132f;

        public ContractDeskBackdrop()
        {
            AddToClassList(BackdropClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = ctx.painter2D;

            // Vertical plank seams.
            painter.strokeColor = SeamColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            for (float x = PlankWidth; x < rect.width; x += PlankWidth)
            {
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x + 2f, rect.height));
            }
            painter.Stroke();

            // Long horizontal grain waves, alternating dark and light.
            int rows = Mathf.Max(2, Mathf.RoundToInt(rect.height / 26f));
            for (int i = 0; i < rows; i++)
            {
                float y = (i + 0.5f) * rect.height / rows;
                bool light = i % 3 == 1;

                painter.strokeColor = light ? GrainLightColor : GrainColor;
                painter.lineWidth = light ? 1f : 1.2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));

                float step = rect.width / 4f;
                for (int s = 0; s < 4; s++)
                {
                    float x0 = s * step;
                    float wobble = (i % 2 == 0 ? 3.5f : -3.5f) * (s % 2 == 0 ? 1f : 0.6f);
                    painter.BezierCurveTo(
                        new Vector2(x0 + step * 0.33f, y + wobble),
                        new Vector2(x0 + step * 0.66f, y - wobble),
                        new Vector2(x0 + step, y));
                }
                painter.Stroke();
            }

            // A few knots near plank seams.
            painter.fillColor = KnotColor;
            float knotOffset = 37f;
            for (float x = PlankWidth + knotOffset; x < rect.width; x += PlankWidth * 2f)
            {
                float y = rect.height * 0.5f + Mathf.Sin(x * 0.05f) * rect.height * 0.3f;
                painter.BeginPath();
                painter.Arc(new Vector2(x, y), 5.5f, 0f, Mathf.PI * 2f);
                painter.Fill();
                painter.BeginPath();
                painter.Arc(new Vector2(x, y), 2.5f, 0f, Mathf.PI * 2f);
                painter.Fill();
            }
        }
    }

    /// <summary>
    /// Painter2D wax seal: a deep-red scalloped disc with an inner ring and a
    /// stamped star. Tinted through the element's color style like IconElement.
    /// </summary>
    public sealed class WaxSealView : VisualElement
    {
        private const float ScallopCount = 14f;

        public WaxSealView(float size = 34f)
        {
            style.width = size;
            style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = ctx.painter2D;
            var center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = outer * 0.62f;

            // Scalloped outer disc.
            painter.fillColor = new Color(0.612f, 0.231f, 0.180f);      // #9C3B2E
            painter.BeginPath();
            float step = Mathf.PI * 2f / ScallopCount;
            for (int i = 0; i <= ScallopCount; i++)
            {
                float a0 = i * step;
                float a1 = (i + 0.5f) * step;
                var p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outer;
                var p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outer * 1.12f;
                if (i == 0)
                {
                    painter.MoveTo(p0);
                }
                else
                {
                    painter.LineTo(p0);
                }
                painter.QuadraticCurveTo(center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outer * 1.18f, p1);
            }
            painter.Fill();

            // Inner ring.
            painter.strokeColor = new Color(0.486f, 0.173f, 0.129f);    // #7C2C21
            painter.lineWidth = 1.6f;
            painter.BeginPath();
            painter.Arc(center, inner, 0f, Mathf.PI * 2f);
            painter.Stroke();

            // Stamped five-point star.
            painter.fillColor = new Color(0.486f, 0.173f, 0.129f);      // #7C2C21
            painter.BeginPath();
            float starR = inner * 0.72f;
            for (int i = 0; i < 10; i++)
            {
                float ang = -Mathf.PI * 0.5f + i * Mathf.PI * 0.2f;
                float r = i % 2 == 0 ? starR : starR * 0.45f;
                var p = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (i == 0)
                {
                    painter.MoveTo(p);
                }
                else
                {
                    painter.LineTo(p);
                }
            }
            painter.Fill();
        }
    }

    /// <summary>
    /// Painter2D signature stroke: a quick "cursive" flourish drawn above the
    /// signature line of the signing dialog. Pure decoration; picking ignored.
    /// </summary>
    public sealed class SignStrokeView : VisualElement
    {
        public SignStrokeView()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = ctx.painter2D;
            painter.strokeColor = new Color(0.200f, 0.161f, 0.110f);    // ink #33291C
            painter.lineWidth = 2f;

            float x0 = rect.x + 4f;
            float yMid = rect.y + rect.height * 0.55f;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x0, yMid + rect.height * 0.25f));
            painter.BezierCurveTo(
                new Vector2(x0 + rect.width * 0.10f, yMid - rect.height * 0.55f),
                new Vector2(x0 + rect.width * 0.22f, yMid - rect.height * 0.55f),
                new Vector2(x0 + rect.width * 0.26f, yMid));
            painter.BezierCurveTo(
                new Vector2(x0 + rect.width * 0.30f, yMid + rect.height * 0.50f),
                new Vector2(x0 + rect.width * 0.40f, yMid + rect.height * 0.50f),
                new Vector2(x0 + rect.width * 0.42f, yMid - rect.height * 0.10f));
            painter.BezierCurveTo(
                new Vector2(x0 + rect.width * 0.44f, yMid - rect.height * 0.55f),
                new Vector2(x0 + rect.width * 0.55f, yMid + rect.height * 0.60f),
                new Vector2(x0 + rect.width * 0.62f, yMid));
            painter.BezierCurveTo(
                new Vector2(x0 + rect.width * 0.68f, yMid - rect.height * 0.40f),
                new Vector2(x0 + rect.width * 0.80f, yMid - rect.height * 0.10f),
                new Vector2(x0 + rect.width * 0.97f, yMid - rect.height * 0.30f));
            painter.Stroke();
        }
    }
}
