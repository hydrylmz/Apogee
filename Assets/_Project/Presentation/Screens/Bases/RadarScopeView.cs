using System;
using System.Collections.Generic;
using Apogee.UI.Core.Tooltips;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Bases
{
    /// <summary>
    /// Painter2D radar scope for the Base Management screen ("Radar Konsolu", GDD §14/§15).
    /// Draws a circular phosphor scope: range rings, compass ticks, a rotating sweep beam
    /// and region blips positioned from the same equirectangular world-map coordinates the
    /// Campaign Map pins use (regions.json lat/lon).
    /// </summary>
    public sealed class RadarScopeView : VisualElement
    {
        // Palette mirrors the .theme-radar USS tokens (kept as C# constants for Painter2D).
        private static readonly Color ScopeBackground = new(0.008f, 0.047f, 0.027f, 0.94f);
        private static readonly Color Phosphor = new(0.208f, 0.878f, 0.549f, 1f);
        private static readonly Color PhosphorBright = new(0.435f, 1f, 0.69f, 1f);
        private static readonly Color RingColor = new(0.208f, 0.878f, 0.549f, 0.30f);
        private static readonly Color TickColor = new(0.435f, 1f, 0.69f, 0.55f);
        private static readonly Color GridColor = new(0.208f, 0.878f, 0.549f, 0.14f);
        private static readonly Color BlipColor = new(0.435f, 1f, 0.69f, 1f);
        private static readonly Color BlipIdleColor = new(0.435f, 1f, 0.69f, 0.38f);
        private static readonly Color BlipSelectedColor = new(0.824f, 1f, 0.902f, 1f);
        private static readonly Color AmberColor = new(1f, 0.706f, 0.329f, 0.9f);

        private const float SweepStepDegrees = 4f;
        private const int SweepTrailRays = 14;
        private const float HitRadiusPx = 14f;

        /// <summary>One radar contact (a strategic region).</summary>
        public sealed class Blip
        {
            public string RegionId;
            public string Label;
            public string SiteName;
            public string ShortDescription;
            public float NormalizedX; // equirectangular [0..100], same source as map pins
            public float NormalizedY; // equirectangular [0..100]
            public bool HasFacility;
            public int FacilityCount;
            public bool IsSelected;
        }

        public event Action<string> RegionClicked;

        private readonly List<Blip> _blips = new();
        private float _sweepAngle;
        private IVisualElementScheduledItem _sweepSchedule;
        private Blip _hoveredBlip;

        public RadarScopeView()
        {
            generateVisualContent += OnGenerateVisualContent;
            this.RegisterCallback<AttachToPanelEvent>(OnAttach);
            this.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<ClickEvent>(OnClicked);
        }

        /// <summary>Replaces the radar contacts. Passing null clears the scope.</summary>
        public void SetBlips(IEnumerable<Blip> blips)
        {
            _blips.Clear();
            if (blips != null)
            {
                _blips.AddRange(blips);
            }

            MarkDirtyRepaint();
        }

        /// <summary>Highlights a single region (radial lock ring + brighter blip).</summary>
        public void SetSelectedRegion(string regionId)
        {
            bool changed = false;
            foreach (var blip in _blips)
            {
                bool selected = !string.IsNullOrEmpty(regionId) &&
                                string.Equals(blip.RegionId, regionId, StringComparison.OrdinalIgnoreCase);
                if (blip.IsSelected != selected)
                {
                    blip.IsSelected = selected;
                    changed = true;
                }
            }

            if (changed)
            {
                MarkDirtyRepaint();
            }
        }

        // ---------------------------------------------------------------- painting

        private void OnAttach(AttachToPanelEvent evt)
        {
            // schedule.Execute returns an already-running scheduled item; no explicit Start() exists.
            _sweepSchedule ??= this.schedule.Execute(StepSweep).Every(33);
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            _sweepSchedule?.Pause();
            HideHover();
        }

        private void StepSweep()
        {
            _sweepAngle = (_sweepAngle + SweepStepDegrees) % 360f;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 60f || h < 60f)
            {
                return;
            }

            var center = new Vector2(w * 0.5f, h * 0.5f);
            float radius = Mathf.Min(w, h) * 0.5f - 18f;

            DrawScopeBase(p, center, radius);
            DrawRingsAndTicks(p, center, radius);
            DrawSweep(p, center, radius);
            DrawBlips(p, center, radius);
        }

        private static void DrawScopeBase(Painter2D p, Vector2 center, float radius)
        {
            p.fillColor = ScopeBackground;
            p.BeginPath();
            p.Arc(center, radius, 0f, 360f);
            p.Fill();
        }

        private static void DrawRingsAndTicks(Painter2D p, Vector2 center, float radius)
        {
            // Range rings.
            p.strokeColor = RingColor;
            p.lineCap = LineCap.Round;
            p.lineWidth = 1f;
            foreach (float factor in new[] { 0.33f, 0.66f, 1f })
            {
                p.BeginPath();
                p.Arc(center, radius * factor, 0f, 360f);
                p.Stroke();
            }

            // Crosshair + diagonals.
            p.strokeColor = GridColor;
            foreach (float angleDeg in new[] { 0f, 90f, 45f, 135f })
            {
                float rad = angleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                p.BeginPath();
                p.MoveTo(center - dir * (radius * 0.97f));
                p.LineTo(center + dir * (radius * 0.97f));
                p.Stroke();
            }

            // Compass degree ticks: minor every 15°, major every 45°, cardinal longest.
            p.strokeColor = TickColor;
            foreach (float deg in GetTickAngles())
            {
                float rad = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float len = deg % 90f == 0f ? 10f : (deg % 45f == 0f ? 7f : 4f);
                p.lineWidth = deg % 90f == 0f ? 1.6f : 1f;
                p.BeginPath();
                p.MoveTo(center + dir * (radius + 2f));
                p.LineTo(center + dir * (radius + 2f + len));
                p.Stroke();
            }
        }

        private static IEnumerable<float> GetTickAngles()
        {
            for (float deg = 0f; deg < 360f; deg += 15f)
            {
                yield return deg;
            }
        }

        private void DrawSweep(Painter2D p, Vector2 center, float radius)
        {
            // Fading trail: rays behind the leading edge, alpha decaying.
            for (int i = SweepTrailRays; i >= 1; i--)
            {
                float angle = _sweepAngle - i * 2.4f;
                float alpha = (1f - (float)i / SweepTrailRays) * 0.30f;
                p.strokeColor = new Color(PhosphorBright.r, PhosphorBright.g, PhosphorBright.b, alpha);
                p.lineWidth = 1.4f;
                float rad = angle * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                p.BeginPath();
                p.MoveTo(center);
                p.LineTo(center + dir * radius);
                p.Stroke();
            }

            // Leading beam.
            p.strokeColor = new Color(PhosphorBright.r, PhosphorBright.g, PhosphorBright.b, 0.85f);
            p.lineWidth = 2f;
            float lead = _sweepAngle * Mathf.Deg2Rad;
            var leadDir = new Vector2(Mathf.Cos(lead), Mathf.Sin(lead));
            p.BeginPath();
            p.MoveTo(center);
            p.LineTo(center + leadDir * radius);
            p.Stroke();

            // Center pivot.
            p.fillColor = PhosphorBright;
            p.BeginPath();
            p.Arc(center, 2.5f, 0f, 360f);
            p.Fill();
        }

        private void DrawBlips(Painter2D p, Vector2 center, float radius)
        {
            foreach (var blip in _blips)
            {
                var pos = GetBlipPosition(blip, center, radius);
                var contact = GetContact(blip);

                if (blip.HasFacility)
                {
                    // Facility contact: bright filled dot + count halo.
                    p.strokeColor = contact;
                    p.lineWidth = 1.2f;
                    p.BeginPath();
                    p.Arc(pos, 5.5f, 0f, 360f);
                    p.Stroke();

                    p.fillColor = contact;
                    p.BeginPath();
                    p.Arc(pos, 2.6f, 0f, 360f);
                    p.Fill();
                }
                else
                {
                    // Unclaimed region: hollow dim dot, still hoverable.
                    p.strokeColor = BlipIdleColor;
                    p.lineWidth = 1.2f;
                    p.BeginPath();
                    p.Arc(pos, 3f, 0f, 360f);
                    p.Stroke();
                }

                if (blip.IsSelected)
                {
                    // Radial lock brackets on the selected region.
                    p.strokeColor = AmberColor;
                    p.lineWidth = 1.6f;
                    p.BeginPath();
                    p.Arc(pos, 9.5f, -30f, 60f);
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(pos, 9.5f, 150f, 60f);
                    p.Stroke();
                }

                if (ReferenceEquals(blip, _hoveredBlip) && !blip.IsSelected)
                {
                    p.strokeColor = PhosphorBright;
                    p.lineWidth = 1.2f;
                    p.BeginPath();
                    p.Arc(pos, 12f, 0f, 360f);
                    p.Stroke();
                }
            }
        }

        private static Color GetContact(Blip blip)
        {
            if (blip.IsSelected)
            {
                return BlipSelectedColor;
            }

            return blip.FacilityCount > 1 ? BlipSelectedColor : BlipColor;
        }

        // ---------------------------------------------------------------- interaction

        /// <summary>
        /// Equirectangular world-map percentage mapped into the scope's inner square,
        /// radially clamped to stay inside the circle (same coordinate source as map pins).
        /// </summary>
        private static Vector2 GetBlipPosition(Blip blip, Vector2 center, float radius)
        {
            float half = radius * 0.86f;
            var pos = new Vector2(
                center.x + (blip.NormalizedX / 100f - 0.5f) * 2f * half,
                center.y + (blip.NormalizedY / 100f - 0.5f) * 2f * half);

            var offset = pos - center;
            float dist = offset.magnitude;
            if (dist > radius * 0.95f && dist > 0.0001f)
            {
                pos = center + offset * (radius * 0.95f / dist);
            }

            return pos;
        }

        private Blip FindBlipAt(Vector2 localPos, Vector2 center, float radius)
        {
            Blip best = null;
            float bestDist = HitRadiusPx;
            foreach (var blip in _blips)
            {
                float dist = Vector2.Distance(GetBlipPosition(blip, center, radius), localPos);
                if (dist <= bestDist)
                {
                    best = blip;
                    bestDist = dist;
                }
            }

            return best;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 60f || h < 60f)
            {
                return;
            }

            var center = new Vector2(w * 0.5f, h * 0.5f);
            float radius = Mathf.Min(w, h) * 0.5f - 18f;
            var point = evt.localPosition;

            var hit = FindBlipAt(point, center, radius);
            if (hit != _hoveredBlip)
            {
                _hoveredBlip = hit;
                MarkDirtyRepaint();
            }

            if (hit != null)
            {
                TooltipService.Instance.ShowTooltip(BuildTooltip(hit), evt.position);
            }
            else
            {
                TooltipService.Instance.HideTooltip();
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            HideHover();
        }

        private void HideHover()
        {
            if (_hoveredBlip != null)
            {
                _hoveredBlip = null;
                MarkDirtyRepaint();
            }

            TooltipService.Instance.HideTooltip();
        }

        private void OnClicked(ClickEvent evt)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w < 60f || h < 60f)
            {
                return;
            }

            var center = new Vector2(w * 0.5f, h * 0.5f);
            float radius = Mathf.Min(w, h) * 0.5f - 18f;
            var point = evt.localPosition;

            var hit = FindBlipAt(point, center, radius);
            if (hit != null)
            {
                RegionClicked?.Invoke(hit.RegionId);
            }
        }

        private static TooltipData BuildTooltip(Blip blip)
        {
            string title = string.IsNullOrEmpty(blip.SiteName) ? blip.Label : $"{blip.Label} — {blip.SiteName}";
            string content = string.IsNullOrEmpty(blip.ShortDescription)
                ? "Stratejik bölge."
                : blip.ShortDescription;

            if (blip.HasFacility)
            {
                content += $"\nRadar kontağı: {blip.FacilityCount} tesis.";
            }
            else
            {
                content += "\nBölgede tesis yok.";
            }

            content += "\nDetay için tıklayın.";
            return new TooltipData(title, content, severity: blip.HasFacility ? TooltipSeverity.Success : TooltipSeverity.Info);
        }
    }
}
