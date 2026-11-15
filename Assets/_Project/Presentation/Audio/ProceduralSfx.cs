using System.Collections.Generic;
using Apogee.Core.Audio;
using UnityEngine;

namespace Apogee.UI.Audio
{
    /// <summary>
    /// Procedurally synthesized one-shot SFX for events that have no sample file in
    /// Resources/Audio/UI (Kenney pack lacks a cash-register and a stamp sound).
    /// Clips are generated once, cached, and served to <see cref="UnityAudioService"/>
    /// as a fallback when the clip lookup misses. Test-harness safe: AudioClip creation
    /// failures simply return null (silence) instead of throwing.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<AudioEvent, AudioClip> Cache = new();

        /// <summary>Returns the synthesized clip for the event, or null if none/synthesis unavailable.</summary>
        public static AudioClip GetOrCreate(AudioEvent audioEvent)
        {
            if (audioEvent != AudioEvent.PurchaseCashRegister && audioEvent != AudioEvent.StampSlam)
            {
                return null;
            }

            if (Cache.TryGetValue(audioEvent, out var cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var clip = audioEvent == AudioEvent.PurchaseCashRegister
                    ? CreateCashRegisterChaChing()
                    : CreateStampSlam();

                if (clip != null)
                {
                    clip.hideFlags = HideFlags.DontSave;
                    Cache[audioEvent] = clip;
                }

                return clip;
            }
            catch
            {
                // Non-Unity harness or audio device unavailable — stay silent.
                return null;
            }
        }

        /// <summary>
        /// Classic cash-register "cha-ching": a mechanical drawer transient followed by
        /// two metallic bell dings and a bright coin shimmer. ~0.85 s, mono.
        /// </summary>
        private static AudioClip CreateCashRegisterChaChing()
        {
            const float duration = 0.85f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            var buffer = new float[sampleCount];

            // ── Bell partials: (frequency, startTime, amplitude, decayTime) ──
            // "CHA" — drawer/kachunk transient + first bell ...
            var bells = new (float freq, float start, float amp, float decay)[]
            {
                // Mechanical drawer thump (low body of the register)
                (140f,  0.000f, 0.55f, 0.055f),
                (95f,   0.000f, 0.42f, 0.070f),
                // "cha" — first metallic strike
                (2093f, 0.012f, 0.34f, 0.090f),
                (2637f, 0.012f, 0.22f, 0.080f),
                // "ching" — the classic two-note bell ding
                (2794f, 0.130f, 0.42f, 0.220f),
                (3520f, 0.130f, 0.26f, 0.180f),
                (2093f, 0.240f, 0.34f, 0.200f),
                (4699f, 0.245f, 0.14f, 0.140f),
            };

            // Coin shimmer: a sprinkle of very bright short pings after the ding.
            var rng = new System.Random(20261115);
            var shimmer = new List<(float freq, float start, float amp, float decay)>();
            for (int i = 0; i < 14; i++)
            {
                float freq = 5200f + (float)rng.NextDouble() * 3600f;         // 5.2–8.8 kHz
                float start = 0.30f + (float)rng.NextDouble() * 0.34f;         // sprinkled tail
                float amp = 0.05f + (float)rng.NextDouble() * 0.07f;
                shimmer.Add((freq, start, amp, 0.045f + (float)rng.NextDouble() * 0.03f));
            }

            foreach (var (freq, start, amp, decay) in bells)
            {
                AddTone(buffer, freq, start, amp, decay, harmonics: 3);
            }

            foreach (var (freq, start, amp, decay) in shimmer)
            {
                AddTone(buffer, freq, start, amp, decay, harmonics: 1);
            }

            // Short noise transient at t=0 — the physical "clack" of the drawer.
            AddNoiseBurst(buffer, 0.000f, 0.018f, 0.30f);

            return ToClip("sfx_cash_chaching", buffer);
        }

        /// <summary>
        /// Rubber-stamp slam on paper: a heavy low thump plus a crisp paper slap.
        /// ~0.28 s, mono.
        /// </summary>
        private static AudioClip CreateStampSlam()
        {
            const float duration = 0.28f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            var buffer = new float[sampleCount];

            // Stamp head hitting the desk: deep, fast-decaying body.
            AddTone(buffer, 82f, 0.000f, 0.95f, 0.055f, harmonics: 2);
            AddTone(buffer, 55f, 0.000f, 0.70f, 0.075f, harmonics: 1);

            // Paper slap + ink squelch: two noise layers.
            AddNoiseBurst(buffer, 0.000f, 0.030f, 0.65f);
            AddNoiseBurst(buffer, 0.012f, 0.055f, 0.22f);

            return ToClip("sfx_stamp_slam", buffer);
        }

        /// <summary>
        /// Adds an exponentially decaying sine (with optional integer harmonics) into the buffer.
        /// </summary>
        private static void AddTone(float[] buffer, float freq, float startTime, float amplitude, float decaySeconds, int harmonics)
        {
            int startSample = Mathf.Clamp((int)(startTime * SampleRate), 0, buffer.Length - 1);

            for (int i = startSample; i < buffer.Length; i++)
            {
                float t = (i - startSample) / (float)SampleRate;
                float envelope = Mathf.Exp(-t / Mathf.Max(0.005f, decaySeconds));
                if (envelope < 0.0008f)
                {
                    break; // decayed to inaudible — skip the rest of the tail
                }

                float value = 0f;
                for (int h = 1; h <= harmonics; h++)
                {
                    // Harmonic amplitude rolls off like a struck metal plate.
                    float harmonicAmp = amplitude / (h * h);
                    value += harmonicAmp * Mathf.Sin(2f * Mathf.PI * freq * h * t);
                }

                buffer[i] = Mathf.Clamp(buffer[i] + value * envelope, -1f, 1f);
            }
        }

        /// <summary>Adds a short exponentially decaying white-noise burst into the buffer.</summary>
        private static void AddNoiseBurst(float[] buffer, float startTime, float decaySeconds, float amplitude)
        {
            int startSample = Mathf.Clamp((int)(startTime * SampleRate), 0, buffer.Length - 1);
            var rng = new System.Random((int)(startTime * 100000f) + 7);

            for (int i = startSample; i < buffer.Length; i++)
            {
                float t = (i - startSample) / (float)SampleRate;
                float envelope = Mathf.Exp(-t / Mathf.Max(0.004f, decaySeconds));
                if (envelope < 0.0008f)
                {
                    break;
                }

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                buffer[i] = Mathf.Clamp(buffer[i] + noise * amplitude * envelope, -1f, 1f);
            }
        }

        private static AudioClip ToClip(string clipName, float[] buffer)
        {
            var clip = AudioClip.Create(clipName, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}
