using System;

namespace Apogee.Strategy
{
    /// <summary>
    /// Immutable value type representing an in-game calendar date as a tick (day) offset from
    /// the campaign epoch (2031-01-01). Arithmetic is day-granular; no real wall-clock dependency.
    /// Complies with GDD §4.1, §6.3, §17.3 item 3.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        // --- Constants ---
        public const int EpochYear  = 2031;
        public const int EpochMonth = 1;
        public const int EpochDay   = 1;

        /// <summary>Days elapsed since the campaign epoch (2031-01-01). Day 0 = epoch.</summary>
        public readonly int TotalDays;

        // ---------------------------------------------------------------- construction
        public GameDate(int totalDays)
        {
            TotalDays = Math.Max(0, totalDays);
        }

        /// <summary>Creates a GameDate from a calendar triple (year, month, day).</summary>
        public static GameDate FromCalendar(int year, int month, int day)
        {
            // Use Gregorian day-count arithmetic without DateTime to keep engine-free.
            int epochAbsolute = GregorianAbsoluteDays(EpochYear, EpochMonth, EpochDay);
            int targetAbsolute = GregorianAbsoluteDays(
                Math.Max(EpochYear, year),
                Math.Clamp(month, 1, 12),
                Math.Clamp(day, 1, 31));
            int delta = targetAbsolute - epochAbsolute;
            return new GameDate(Math.Max(0, delta));
        }

        // ---------------------------------------------------------------- properties
        public int Year  => DecomposeDate().year;
        public int Month => DecomposeDate().month;
        public int Day   => DecomposeDate().day;

        /// <summary>Campaign year index (0 = first campaign year).</summary>
        public int CampaignYear => Year - EpochYear;

        // ---------------------------------------------------------------- arithmetic
        public GameDate AddDays(int days)    => new GameDate(TotalDays + days);
        public GameDate AddMonths(int months) => FromCalendar(Year, Month + months, Day);
        public GameDate AddYears(int years)   => FromCalendar(Year + years, Month, Day);

        public int DaysUntil(GameDate other) => other.TotalDays - TotalDays;

        // ---------------------------------------------------------------- display
        /// <summary>Returns a short locale-neutral ISO-style date string: YYYY-MM-DD.</summary>
        public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

        /// <summary>Returns a human-friendly campaign date string, e.g. "Jan 2035".</summary>
        public string ToShortString() => $"{MonthAbbreviation(Month)} {Year}";

        // ---------------------------------------------------------------- equality / comparison
        public bool Equals(GameDate other) => TotalDays == other.TotalDays;
        public override bool Equals(object obj) => obj is GameDate d && Equals(d);
        public override int GetHashCode() => TotalDays;
        public int CompareTo(GameDate other) => TotalDays.CompareTo(other.TotalDays);

        public static bool operator ==(GameDate a, GameDate b) => a.TotalDays == b.TotalDays;
        public static bool operator !=(GameDate a, GameDate b) => a.TotalDays != b.TotalDays;
        public static bool operator  <(GameDate a, GameDate b) => a.TotalDays  < b.TotalDays;
        public static bool operator  >(GameDate a, GameDate b) => a.TotalDays  > b.TotalDays;
        public static bool operator <=(GameDate a, GameDate b) => a.TotalDays <= b.TotalDays;
        public static bool operator >=(GameDate a, GameDate b) => a.TotalDays >= b.TotalDays;

        // ---------------------------------------------------------------- static helpers
        // Standard Euclidean affine day counting algorithm (Howard Hinnant formula)
        // Highly accurate, overflow-safe, reversible without magic constants
        private static int DaysFromCivil(int y, int m, int d)
        {
            y -= m <= 2 ? 1 : 0;
            int era = (y >= 0 ? y : y - 399) / 400;
            int yoe = y - era * 400;                                   // [0, 399]
            int doy = (153 * (m > 2 ? m - 3 : m + 9) + 2) / 5 + d - 1;  // [0, 365]
            int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;           // [0, 146096]
            return era * 146097 + doe - 719468;
        }

        private static (int year, int month, int day) CivilFromDays(int z)
        {
            z += 719468;
            int era = (z >= 0 ? z : z - 146096) / 146097;
            int doe = z - era * 146097;                                  // [0, 146096]
            int yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365; // [0, 399]
            int y = yoe + era * 400;
            int doy = doe - (365 * yoe + yoe / 4 - yoe / 100);           // [0, 365]
            int mp = (5 * doy + 2) / 153;                                // [0, 11]
            int d = doy - (153 * mp + 2) / 5 + 1;                        // [1, 31]
            int m = mp < 10 ? mp + 3 : mp - 9;                           // [1, 12]
            return (m <= 2 ? y + 1 : y, m, d);
        }

        private static int GregorianAbsoluteDays(int y, int m, int d) => DaysFromCivil(y, m, d);

        private (int year, int month, int day) DecomposeDate()
        {
            int epochAbs = DaysFromCivil(EpochYear, EpochMonth, EpochDay);
            return CivilFromDays(epochAbs + TotalDays);
        }

        private static string MonthAbbreviation(int month)
        {
            switch (month)
            {
                case  1: return "Jan";
                case  2: return "Feb";
                case  3: return "Mar";
                case  4: return "Apr";
                case  5: return "May";
                case  6: return "Jun";
                case  7: return "Jul";
                case  8: return "Aug";
                case  9: return "Sep";
                case 10: return "Oct";
                case 11: return "Nov";
                case 12: return "Dec";
                default: return "???";
            }
        }
    }
}
