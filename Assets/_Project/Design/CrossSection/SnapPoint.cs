using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents a modular attachment point on a cross-section part for auto-snapping (GDD §15.1).
    /// </summary>
    public readonly struct SnapPoint : IEquatable<SnapPoint>
    {
        public double X { get; }
        public double Y { get; }
        public bool IsConnected { get; }

        public SnapPoint(double x, double y, bool isConnected = false)
        {
            X = x;
            Y = y;
            IsConnected = isConnected;
        }

        public SnapPoint WithConnection(bool connected) => new SnapPoint(X, Y, connected);

        public bool Equals(SnapPoint other) =>
            Math.Abs(X - other.X) < 0.001 &&
            Math.Abs(Y - other.Y) < 0.001 &&
            IsConnected == other.IsConnected;

        public override bool Equals(object obj) => obj is SnapPoint other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, IsConnected);

        public override string ToString() => $"SnapPoint({X:F2}, {Y:F2}, Connected={IsConnected})";
    }
}
