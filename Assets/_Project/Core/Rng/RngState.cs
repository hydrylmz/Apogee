using System;

namespace Apogee.Core
{
    /// <summary>
    /// Serializable snapshot of SeededRng internal state.
    /// Used for campaign save/load and determinism replay.
    /// </summary>
    [Serializable]
    public struct RngState : IEquatable<RngState>
    {
        public ulong S0;
        public ulong S1;
        public ulong S2;
        public ulong S3;

        public RngState(ulong s0, ulong s1, ulong s2, ulong s3)
        {
            S0 = s0;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }

        public bool Equals(RngState other)
        {
            return S0 == other.S0 && S1 == other.S1 && S2 == other.S2 && S3 == other.S3;
        }

        public override bool Equals(object obj)
        {
            return obj is RngState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)S0;
                hash = (hash * 397) ^ (int)(S0 >> 32);
                hash = (hash * 397) ^ (int)S1;
                hash = (hash * 397) ^ (int)(S1 >> 32);
                hash = (hash * 397) ^ (int)S2;
                hash = (hash * 397) ^ (int)(S2 >> 32);
                hash = (hash * 397) ^ (int)S3;
                hash = (hash * 397) ^ (int)(S3 >> 32);
                return hash;
            }
        }

        public static bool operator ==(RngState left, RngState right) => left.Equals(right);
        public static bool operator !=(RngState left, RngState right) => !left.Equals(right);
    }
}
