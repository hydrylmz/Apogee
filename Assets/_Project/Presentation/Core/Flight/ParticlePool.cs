using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Frame-budget pooled particle system for the Flight Theatre.
    ///
    /// Design rules (GDD §15.1, §3.4):
    ///   - Zero per-frame heap allocation: all particles pre-allocated in the pool.
    ///   - Drawn via the caller's Painter2D pass (no extra canvases).
    ///   - Each particle kind has a fixed colour, size range, and lifetime.
    ///   - Maximum active particles capped by the pool size (default 20).
    ///   - ReduceMotion: caller clears pool via <see cref="Clear"/>.
    /// </summary>
    public sealed class ParticlePool
    {
        // ── Particle Kinds ────────────────────────────────────────────────────────
        public enum Kind { Exhaust, Debris, Vapour, Heat, Dust }

        // ── Particle Data ─────────────────────────────────────────────────────────
        private struct Particle
        {
            public bool    Active;
            public Kind    ParticleKind;
            public Vector2 Position;   // normalized [0–1] canvas space
            public Vector2 Velocity;   // normalized units/sec
            public float   Life;       // remaining seconds
            public float   MaxLife;
            public float   Size;
            public Color   StartColor;
            public Color   EndColor;
        }

        // ── State ─────────────────────────────────────────────────────────────────
        private readonly Particle[] _pool;
        private readonly int        _capacity;
        private readonly VisualElement _owner; // for future MarkDirtyRepaint if needed
        private int                 _next; // round-robin write head

        // ── Colour presets ────────────────────────────────────────────────────────
        private static readonly Color ExhaustStart = new Color(1.00f, 0.90f, 0.70f, 0.90f);
        private static readonly Color ExhaustEnd   = new Color(0.60f, 0.30f, 0.10f, 0.00f);
        private static readonly Color DebrisStart  = new Color(0.80f, 0.80f, 0.90f, 0.80f);
        private static readonly Color DebrisEnd    = new Color(0.40f, 0.40f, 0.50f, 0.00f);
        private static readonly Color VapourStart  = new Color(0.90f, 0.95f, 1.00f, 0.50f);
        private static readonly Color VapourEnd    = new Color(0.80f, 0.85f, 0.95f, 0.00f);
        private static readonly Color HeatStart    = new Color(1.00f, 0.55f, 0.10f, 0.80f);
        private static readonly Color HeatEnd      = new Color(0.70f, 0.20f, 0.00f, 0.00f);
        private static readonly Color DustStart    = new Color(0.65f, 0.50f, 0.35f, 0.70f);
        private static readonly Color DustEnd      = new Color(0.65f, 0.50f, 0.35f, 0.00f);

        // ── Construction ──────────────────────────────────────────────────────────
        public ParticlePool(int capacity, VisualElement owner)
        {
            _capacity = Math.Max(1, capacity);
            _pool     = new Particle[_capacity];
            _owner    = owner;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Emit <paramref name="count"/> particles of the given kind at a normalized canvas position.</summary>
        public void Emit(int count, Kind kind, Vector2 normalizedOrigin)
        {
            var rng = new System.Random(Environment.TickCount);
            for (int i = 0; i < count; i++)
            {
                ref Particle p = ref _pool[_next % _capacity];
                _next++;

                (Color s, Color e, float speed, float life, float size) = GetPreset(kind, rng);

                float angle = (float)(rng.NextDouble() * 360.0 * Mathf.Deg2Rad);
                float vel   = speed * (0.5f + (float)rng.NextDouble() * 0.5f);

                p.Active       = true;
                p.ParticleKind = kind;
                p.Position     = normalizedOrigin + new Vector2(
                    (float)(rng.NextDouble() - 0.5) * 0.03f,
                    (float)(rng.NextDouble() - 0.5) * 0.03f);
                p.Velocity   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * vel;
                p.Life       = life * (0.7f + (float)rng.NextDouble() * 0.3f);
                p.MaxLife    = p.Life;
                p.Size       = size * (0.6f + (float)rng.NextDouble() * 0.4f);
                p.StartColor = s;
                p.EndColor   = e;
            }
        }

        /// <summary>Advance all active particles by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Particle p = ref _pool[i];
                if (!p.Active) continue;
                p.Life     -= dt;
                p.Position += p.Velocity * dt;
                if (p.Life <= 0f) p.Active = false;
            }
        }

        /// <summary>Draw all active particles onto the provided Painter2D.</summary>
        public void Draw(Painter2D painter, float canvasW, float canvasH)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Particle p = ref _pool[i];
                if (!p.Active) continue;

                float t   = 1f - Mathf.Clamp01(p.Life / p.MaxLife);
                Color col = Color.Lerp(p.StartColor, p.EndColor, t);
                float px  = p.Position.x * canvasW;
                float py  = p.Position.y * canvasH;
                float sz  = p.Size;

                painter.fillColor = col;
                painter.BeginPath();
                painter.Arc(new Vector2(px, py), sz, 0, 360, ArcDirection.Clockwise);
                painter.Fill();
            }
        }

        /// <summary>Deactivate all particles immediately (ReduceMotion or screen hide).</summary>
        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
                _pool[i].Active = false;
        }

        // ── Presets ───────────────────────────────────────────────────────────────
        private static (Color start, Color end, float speed, float life, float size)
            GetPreset(Kind kind, System.Random rng)
        {
            return kind switch
            {
                Kind.Exhaust => (ExhaustStart, ExhaustEnd, 0.08f, 1.4f, 4.5f),
                Kind.Debris  => (DebrisStart,  DebrisEnd,  0.12f, 2.0f, 3.0f),
                Kind.Vapour  => (VapourStart,  VapourEnd,  0.04f, 2.5f, 7.0f),
                Kind.Heat    => (HeatStart,    HeatEnd,    0.06f, 1.8f, 5.5f),
                Kind.Dust    => (DustStart,    DustEnd,    0.07f, 1.6f, 4.0f),
                _            => (ExhaustStart, ExhaustEnd, 0.08f, 1.4f, 4.5f),
            };
        }
    }
}
