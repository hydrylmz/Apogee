using System;

namespace Apogee.Core
{
    /// <summary>
    /// Deterministic pseudo-random number generator based on Xoshiro256** and SplitMix64.
    /// Provides 2^256 - 1 period, zero platform variance (including WebGL), and serializable state.
    /// System.Random and DateTime are strictly not used.
    /// </summary>
    public sealed class SeededRng : IRandom
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public SeededRng(ulong seed)
        {
            Reinitialize(seed);
        }

        public SeededRng(int seed) : this((ulong)(long)seed)
        {
        }

        public SeededRng(RngState state)
        {
            SetState(state);
        }

        public void Reinitialize(ulong seed)
        {
            ulong smState = seed;
            _s0 = SplitMix64(ref smState);
            _s1 = SplitMix64(ref smState);
            _s2 = SplitMix64(ref smState);
            _s3 = SplitMix64(ref smState);

            // Avoid all-zero degenerate state
            if (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0)
            {
                _s0 = 1;
            }
        }

        public ulong NextUInt64()
        {
            ulong result = RotateLeft(_s1 * 5, 7) * 9;

            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;

            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);

            return result;
        }

        public uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        public int NextInt()
        {
            // Non-negative integer [0, int.MaxValue)
            return (int)(NextUInt64() & 0x7FFFFFFFUL);
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");
            }

            return (int)(NextDouble() * maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentException("maxExclusive must be greater than or equal to minInclusive.");
            }

            if (minInclusive == maxExclusive)
            {
                return minInclusive;
            }

            long range = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextDouble() * range));
        }

        public float NextFloat()
        {
            // 24 bits of precision: [0.0f, 1.0f)
            return (NextUInt32() >> 8) * (1.0f / (1 << 24));
        }

        public double NextDouble()
        {
            // 53 bits of precision: [0.0, 1.0)
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        public bool NextBool(float probability = 0.5f)
        {
            if (probability <= 0.0f) return false;
            if (probability >= 1.0f) return true;
            return NextFloat() < probability;
        }

        public RngState GetState()
        {
            return new RngState(_s0, _s1, _s2, _s3);
        }

        public void SetState(RngState state)
        {
            if (state.S0 == 0 && state.S1 == 0 && state.S2 == 0 && state.S3 == 0)
            {
                state.S0 = 1;
            }

            _s0 = state.S0;
            _s1 = state.S1;
            _s2 = state.S2;
            _s3 = state.S3;
        }

        /// <summary>
        /// Deterministically forks a new child SeededRng from the current generator state.
        /// </summary>
        public SeededRng Fork()
        {
            ulong childSeed = NextUInt64();
            return new SeededRng(childSeed);
        }

        private static ulong RotateLeft(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }

        private static ulong SplitMix64(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
