using System;
using System.Collections.Generic;

namespace Apogee.Core
{
    /// <summary>
    /// Manages independent, deterministic RNG streams for the campaign simulation.
    /// Ensures that consuming numbers in one stream (e.g. Events) never affects another stream (e.g. Launch).
    /// Supports full state capture and restoration for CampaignState serialization.
    /// </summary>
    public sealed class RngManager
    {
        private readonly Dictionary<RngStreamType, SeededRng> _streams = new Dictionary<RngStreamType, SeededRng>();
        private ulong _masterSeed;

        public ulong MasterSeed => _masterSeed;

        public RngManager(ulong masterSeed)
        {
            Initialize(masterSeed);
        }

        public RngManager(int masterSeed) : this((ulong)(long)masterSeed)
        {
        }

        public RngManager(RngManagerState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            SetState(state);
        }

        public void Initialize(ulong masterSeed)
        {
            _masterSeed = masterSeed;
            _streams.Clear();

            foreach (RngStreamType type in (RngStreamType[])Enum.GetValues(typeof(RngStreamType)))
            {
                ulong streamSeed = DeriveStreamSeed(masterSeed, type);
                _streams[type] = new SeededRng(streamSeed);
            }
        }

        /// <summary>
        /// Gets the independent RNG stream for a specific simulation system.
        /// </summary>
        public IRandom GetStream(RngStreamType streamType)
        {
            if (!_streams.TryGetValue(streamType, out SeededRng rng))
            {
                ulong streamSeed = DeriveStreamSeed(_masterSeed, streamType);
                rng = new SeededRng(streamSeed);
                _streams[streamType] = rng;
            }

            return rng;
        }

        /// <summary>
        /// Gets the raw SeededRng instance for a stream type (to inspect state or fork).
        /// </summary>
        public SeededRng GetRawStream(RngStreamType streamType)
        {
            return (SeededRng)GetStream(streamType);
        }

        /// <summary>
        /// Creates a new deterministically derived child stream with a specific name or key,
        /// independent of standard simulation streams.
        /// </summary>
        public SeededRng CreateDerivedStream(string streamKey)
        {
            ulong hash = ComputeStringHash64(streamKey);
            ulong derivedSeed = DeriveStreamSeed(_masterSeed ^ hash, RngStreamType.General);
            return new SeededRng(derivedSeed);
        }

        public RngManagerState GetState()
        {
            var state = new RngManagerState(_masterSeed);
            foreach (var kvp in _streams)
            {
                state.StreamStates.Add(new RngManagerState.StreamStateEntry(kvp.Key, kvp.Value.GetState()));
            }
            return state;
        }

        public void SetState(RngManagerState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _masterSeed = state.MasterSeed;
            _streams.Clear();

            // First ensure all enum streams exist
            foreach (RngStreamType type in (RngStreamType[])Enum.GetValues(typeof(RngStreamType)))
            {
                ulong streamSeed = DeriveStreamSeed(_masterSeed, type);
                _streams[type] = new SeededRng(streamSeed);
            }

            // Restore recorded states
            if (state.StreamStates != null)
            {
                foreach (var entry in state.StreamStates)
                {
                    if (_streams.TryGetValue(entry.StreamType, out SeededRng rng))
                    {
                        rng.SetState(entry.State);
                    }
                    else
                    {
                        _streams[entry.StreamType] = new SeededRng(entry.State);
                    }
                }
            }
        }

        private static ulong DeriveStreamSeed(ulong masterSeed, RngStreamType streamType)
        {
            // Decorrelate streams using unique multipliers per stream
            ulong streamOffset = ((ulong)streamType + 1UL) * 0x9E3779B97F4A7C15UL;
            ulong state = masterSeed ^ streamOffset;

            // SplitMix64 step
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong ComputeStringHash64(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0UL;

            // FNV-1a 64-bit
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
