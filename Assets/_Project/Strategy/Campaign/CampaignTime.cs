using System;

namespace Apogee.Strategy
{
    /// <summary>
    /// Encapsulates the campaign clock: current date, speed setting, tick counter, and
    /// deterministic day-step logic. Lives inside CampaignState; mutated only via Commands.
    /// Complies with GDD §6.3, §17.3 items 3 &amp; 4.
    /// </summary>
    public sealed class CampaignTime
    {
        // ---------------------------------------------------------------- constants
        /// <summary>Campaign start date: 2031-01-01 (day 0).</summary>
        public static readonly GameDate StartDate = new GameDate(0);

        /// <summary>Campaign end date: 2061-01-01 (30 campaign years, §4.1).</summary>
        public static readonly GameDate EndDate = GameDate.FromCalendar(2061, 1, 1);

        // ---------------------------------------------------------------- state
        /// <summary>Current in-game date.</summary>
        public GameDate CurrentDate { get; private set; }

        /// <summary>Total simulation ticks elapsed (1 tick = 1 game-day).</summary>
        public long Tick { get; private set; }

        /// <summary>Current time speed. Paused means no ticks advance.</summary>
        public TimeSpeed Speed { get; private set; }

        // ---------------------------------------------------------------- construction
        public CampaignTime()
        {
            CurrentDate = StartDate;
            Tick        = 0;
            Speed       = TimeSpeed.Paused;
        }

        /// <summary>
        /// Restores time state from persisted values (used by save/load).
        /// </summary>
        public CampaignTime(GameDate currentDate, long tick, TimeSpeed speed)
        {
            CurrentDate = currentDate;
            Tick        = Math.Max(0, tick);
            Speed       = speed;
        }

        // ---------------------------------------------------------------- queries
        /// <summary>Whether the campaign clock has reached or passed the end date.</summary>
        public bool IsExpired => CurrentDate >= EndDate;

        /// <summary>Returns how many days remain until the end of the campaign.</summary>
        public int DaysRemaining => Math.Max(0, EndDate.DaysUntil(CurrentDate) * -1);

        // ---------------------------------------------------------------- mutations (called by Commands only)
        /// <summary>
        /// Advances the clock by exactly <paramref name="days"/> game-days and increments the tick counter.
        /// Returns the new date. Clamps to EndDate; does not advance further once expired.
        /// </summary>
        internal GameDate AdvanceDays(int days)
        {
            if (days <= 0) return CurrentDate;

            int actualDays = IsExpired ? 0 : days;
            if (actualDays > 0)
            {
                GameDate next = CurrentDate.AddDays(actualDays);
                // Clamp to end date
                if (next > EndDate) next = EndDate;
                int actual = next.TotalDays - CurrentDate.TotalDays;
                CurrentDate = next;
                Tick += actual;
            }

            return CurrentDate;
        }

        /// <summary>Sets the time speed. Always succeeds (even while paused → unpaused).</summary>
        internal void SetSpeed(TimeSpeed speed)
        {
            Speed = speed;
        }

        /// <summary>
        /// Restores date and tick to a prior snapshot (called by AdvanceTimeCommand.Undo).
        /// Internal to Strategy assembly only.
        /// </summary>
        internal void UndoAdvance(GameDate previousDate, long previousTick)
        {
            CurrentDate = previousDate;
            Tick        = previousTick;
        }

        // ---------------------------------------------------------------- display helpers
        /// <summary>Number of days elapsed since the epoch.</summary>
        public int ElapsedDays => CurrentDate.TotalDays;

        public override string ToString() =>
            $"CampaignTime[Date={CurrentDate}, Tick={Tick}, Speed={Speed}]";
    }
}
