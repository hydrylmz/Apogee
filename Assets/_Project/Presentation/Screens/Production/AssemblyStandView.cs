using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Production
{
    /// <summary>
    /// Painter2D backdrop of the vehicle assembly stand: steel scaffold posts and
    /// braces, platform decks, an overhead crane rail with hook, ground hazard line
    /// and an occasional welding spark while a job is actively in progress.
    /// Drawn BEHIND a VehicleSilhouetteView layered on top of it (ADR-064).
    /// Color constants are kept in sync with Styles/ThemeHangarFloor.uss tokens.
    /// </summary>
    public sealed class AssemblyStandView : VisualElement
    {
        // Palette (matches .theme-hangar-floor tokens)
        private static readonly Color SteelColor = new(0.337f, 0.369f, 0.408f);        // --floor-steel
        private static readonly Color SteelLightColor = new(0.541f, 0.576f, 0.620f);   // --floor-steel-light
        private static readonly Color SafetyColor = new(0.961f, 0.722f, 0.290f);       // --floor-safety
        private static readonly Color SparkColor = new(1.000f, 0.851f, 0.561f);        // --floor-spark
        private static readonly Color GroundColor = new(0.078f, 0.086f, 0.102f);       // --color-bg-canvas-dark

        private bool _working;
        private float _sparkPhase;
        private IVisualElementScheduledItem _sparkSchedule;

        public AssemblyStandView()
        {
            style.flexGrow = 1;
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>Toggles the welding-spark flicker used while a job is in progress.</summary>
        public void SetWorking(bool working)
        {
            if (_working == working) return;
            _working = working;

            if (working && panel != null)
            {
                StartSpark();
            }
            else
            {
                _sparkSchedule?.Pause();
                _sparkSchedule = null;
            }

            MarkDirtyRepaint();
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            if (_working) StartSpark();
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            _sparkSchedule?.Pause();
            _sparkSchedule = null;
        }

        private void StartSpark()
        {
            // schedule.Execute returns an already-running scheduled item; no explicit Start() exists.
            _sparkSchedule ??= schedule.Execute(() =>
            {
                _sparkPhase = (_sparkPhase + 0.37f) % 1f;
                MarkDirtyRepaint();
            }).Every(260);
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 10 || h < 10) return;

            var p = mgc.painter2D;

            DrawScaffold(p, w, h);
            DrawCraneRail(p, w, h);
            DrawGround(p, w, h);

            if (_working)
            {
                DrawWeldingSpark(p, w, h);
            }
        }

        private void DrawScaffold(Painter2D p, float w, float h)
        {
            float postLeft = w * 0.16f;
            float postRight = w * 0.84f;
            float groundY = h * 0.94f;

            // Vertical posts
            p.strokeColor = SteelColor;
            p.lineWidth = 3f;
            p.BeginPath();
            p.MoveTo(new Vector2(postLeft, h * 0.08f));
            p.LineTo(new Vector2(postLeft, groundY));
            p.MoveTo(new Vector2(postRight, h * 0.08f));
            p.LineTo(new Vector2(postRight, groundY));
            p.Stroke();

            // Horizontal braces + diagonal braces between the posts
            p.lineWidth = 1.5f;
            for (int i = 1; i <= 4; i++)
            {
                float y = Mathf.Lerp(h * 0.12f, groundY - 6f, i / 4f);

                p.strokeColor = SteelColor;
                p.BeginPath();
                p.MoveTo(new Vector2(postLeft, y));
                p.LineTo(new Vector2(postRight, y));
                p.Stroke();

                p.strokeColor = new Color(SteelColor.r, SteelColor.g, SteelColor.b, 0.55f);
                p.BeginPath();
                if (i % 2 == 0)
                {
                    p.MoveTo(new Vector2(postLeft, y));
                    p.LineTo(new Vector2(postRight, y - h * 0.11f));
                }
                else
                {
                    p.MoveTo(new Vector2(postLeft, y - h * 0.11f));
                    p.LineTo(new Vector2(postRight, y));
                }
                p.Stroke();
            }

            // Platform decks (three inspection levels)
            p.strokeColor = SteelLightColor;
            p.lineWidth = 3f;
            for (int i = 1; i <= 3; i++)
            {
                float y = h * (0.24f + 0.2f * i);
                p.BeginPath();
                p.MoveTo(new Vector2(postLeft - 12f, y));
                p.LineTo(new Vector2(postRight + 12f, y));
                p.Stroke();
            }
        }

        private void DrawCraneRail(Painter2D p, float w, float h)
        {
            // Overhead crane beam
            p.strokeColor = SteelLightColor;
            p.lineWidth = 4f;
            p.BeginPath();
            p.MoveTo(new Vector2(w * 0.04f, h * 0.05f));
            p.LineTo(new Vector2(w * 0.96f, h * 0.05f));
            p.Stroke();

            // Trolley + hook cable hanging over the stand center
            float trolleyX = w * 0.5f;
            p.strokeColor = SteelLightColor;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(new Vector2(trolleyX - 8f, h * 0.05f));
            p.LineTo(new Vector2(trolleyX + 8f, h * 0.05f));
            p.MoveTo(new Vector2(trolleyX, h * 0.05f));
            p.LineTo(new Vector2(trolleyX, h * 0.14f));
            p.Stroke();

            // Hook
            p.strokeColor = SafetyColor;
            p.lineWidth = 2.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(trolleyX, h * 0.14f));
            p.Arc(new Vector2(trolleyX, h * 0.155f), 4f, 270f, 300f);
            p.Stroke();
        }

        private void DrawGround(Painter2D p, float w, float h)
        {
            float groundY = h * 0.94f;

            // Concrete floor slab
            p.fillColor = GroundColor;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, groundY));
            p.LineTo(new Vector2(w, groundY));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();

            // Safety line along the stand edge
            p.strokeColor = SafetyColor;
            p.lineWidth = 3f;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, groundY));
            p.LineTo(new Vector2(w, groundY));
            p.Stroke();
        }

        private void DrawWeldingSpark(Painter2D p, float w, float h)
        {
            // Flickering star-shaped spark near the vehicle's mid section
            float sx = w * (0.52f + 0.06f * Mathf.Sin(_sparkPhase * 12.6f));
            float sy = h * (0.42f + 0.05f * Mathf.Cos(_sparkPhase * 9.4f));
            float size = 4f + 5f * _sparkPhase;

            p.fillColor = new Color(SparkColor.r, SparkColor.g, SparkColor.b, 0.35f + 0.55f * (1f - _sparkPhase));
            p.BeginPath();
            p.MoveTo(new Vector2(sx - size, sy));
            p.LineTo(new Vector2(sx, sy - size * 0.35f));
            p.LineTo(new Vector2(sx + size, sy));
            p.LineTo(new Vector2(sx, sy + size * 0.35f));
            p.ClosePath();
            p.Fill();

            p.BeginPath();
            p.MoveTo(new Vector2(sx, sy - size));
            p.LineTo(new Vector2(sx + size * 0.35f, sy));
            p.LineTo(new Vector2(sx, sy + size));
            p.LineTo(new Vector2(sx - size * 0.35f, sy));
            p.ClosePath();
            p.Fill();
        }
    }
}
