namespace Apogee.Core
{
    /// <summary>
    /// Deterministic pseudo-random number generator interface.
    /// Engine-agnostic, seed-based, and platform-independent.
    /// </summary>
    public interface IRandom
    {
        /// <summary>
        /// Generates next 64-bit unsigned integer.
        /// </summary>
        ulong NextUInt64();

        /// <summary>
        /// Generates next 32-bit unsigned integer.
        /// </summary>
        uint NextUInt32();

        /// <summary>
        /// Generates a non-negative random integer in range [0, int.MaxValue).
        /// </summary>
        int NextInt();

        /// <summary>
        /// Generates a random integer in range [0, maxExclusive).
        /// </summary>
        int NextInt(int maxExclusive);

        /// <summary>
        /// Generates a random integer in range [minInclusive, maxExclusive).
        /// </summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>
        /// Generates a random float in range [0.0f, 1.0f).
        /// </summary>
        float NextFloat();

        /// <summary>
        /// Generates a random double in range [0.0, 1.0).
        /// </summary>
        double NextDouble();

        /// <summary>
        /// Returns true with the given probability in range [0.0f, 1.0f].
        /// </summary>
        bool NextBool(float probability = 0.5f);
    }
}
