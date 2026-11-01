using System;

namespace Apogee.Strategy
{
    /// <summary>
    /// Represents a discrete launch / departure opportunity towards a destination.
    /// Complies with GDD §6.2, §9.4, §16.1.
    /// </summary>
    public readonly struct LaunchWindowInfo : IEquatable<LaunchWindowInfo>
    {
        /// <summary>Target destination identifier (e.g. "mars", "moon").</summary>
        public string DestinationId { get; }

        /// <summary>0-based sequence index of this window throughout the campaign.</summary>
        public int WindowIndex { get; }

        /// <summary>Opening date of the departure window.</summary>
        public GameDate StartDate { get; }

        /// <summary>Closing date of the departure window.</summary>
        public GameDate EndDate { get; }

        /// <summary>Window duration in days.</summary>
        public int DurationDays => EndDate.TotalDays - StartDate.TotalDays;

        public LaunchWindowInfo(string destinationId, int windowIndex, GameDate startDate, GameDate endDate)
        {
            DestinationId = destinationId ?? string.Empty;
            WindowIndex = Math.Max(0, windowIndex);
            StartDate = startDate;
            EndDate = endDate >= startDate ? endDate : startDate;
        }

        /// <summary>Checks whether the given date falls within the active window.</summary>
        public bool IsActive(GameDate date) => date >= StartDate && date <= EndDate;

        /// <summary>Checks whether this window has elapsed and closed relative to date.</summary>
        public bool IsClosed(GameDate date) => date > EndDate;

        /// <summary>Checks whether this window lies in the future relative to date.</summary>
        public bool IsUpcoming(GameDate date) => date < StartDate;

        /// <summary>Calculates days remaining until window opens, or 0 if already opened.</summary>
        public int DaysUntilOpen(GameDate date) => Math.Max(0, StartDate.TotalDays - date.TotalDays);

        public bool Equals(LaunchWindowInfo other) =>
            DestinationId == other.DestinationId && WindowIndex == other.WindowIndex &&
            StartDate == other.StartDate && EndDate == other.EndDate;

        public override bool Equals(object obj) => obj is LaunchWindowInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(DestinationId, WindowIndex, StartDate, EndDate);

        public override string ToString() =>
            $"LaunchWindow[{DestinationId} #{WindowIndex}: {StartDate} -> {EndDate} ({DurationDays}d)]";
    }
}
