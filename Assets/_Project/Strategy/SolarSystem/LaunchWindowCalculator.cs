using System;
using System.Collections.Generic;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure mathematical generator and evaluator for interplanetary launch windows based on synodic periods.
    /// Maps to GDD §6.2, §9.4, §16.1.
    /// </summary>
    public static class LaunchWindowCalculator
    {
        /// <summary>
        /// Calculates the launch window for a specific destination and window index.
        /// Continuous windows (period = 0, e.g. LEO) are always open from day 0 to the campaign end date.
        /// </summary>
        public static LaunchWindowInfo GetWindow(DestinationRecord destination, int windowIndex)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int idx = Math.Max(0, windowIndex);

            if (destination.WindowPeriodDays <= 0)
            {
                // Continuous / always open (e.g. LEO)
                return new LaunchWindowInfo(
                    destination.Id,
                    windowIndex: idx,
                    startDate: new GameDate(0),
                    endDate: CampaignTime.EndDate);
            }

            int startDay = destination.WindowFirstOffsetDays + (idx * destination.WindowPeriodDays);
            int duration = Math.Max(1, destination.WindowDurationDays);
            int endDay = startDay + duration;

            return new LaunchWindowInfo(
                destination.Id,
                windowIndex: idx,
                startDate: new GameDate(startDay),
                endDate: new GameDate(endDay));
        }

        /// <summary>
        /// Finds the currently active window or the immediately next upcoming window for the destination.
        /// </summary>
        public static LaunchWindowInfo GetCurrentOrNextWindow(DestinationRecord destination, GameDate currentDate)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            if (destination.WindowPeriodDays <= 0)
            {
                return GetWindow(destination, 0);
            }

            // Find window index based on elapsed days
            int currentDay = currentDate.TotalDays;
            int offset = destination.WindowFirstOffsetDays;
            int period = destination.WindowPeriodDays;
            int duration = Math.Max(1, destination.WindowDurationDays);

            if (currentDay < offset)
            {
                return GetWindow(destination, 0);
            }

            int estimatedIdx = (currentDay - offset) / period;
            LaunchWindowInfo win = GetWindow(destination, estimatedIdx);

            if (win.IsClosed(currentDate))
            {
                // Move to next window
                win = GetWindow(destination, estimatedIdx + 1);
            }

            return win;
        }

        /// <summary>
        /// Generates a list of upcoming launch windows up to maxCount.
        /// </summary>
        public static List<LaunchWindowInfo> GetUpcomingWindows(DestinationRecord destination, GameDate currentDate, int maxCount = 5)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var list = new List<LaunchWindowInfo>();

            if (destination.WindowPeriodDays <= 0)
            {
                list.Add(GetWindow(destination, 0));
                return list;
            }

            LaunchWindowInfo first = GetCurrentOrNextWindow(destination, currentDate);
            list.Add(first);

            for (int i = 1; i < maxCount; i++)
            {
                list.Add(GetWindow(destination, first.WindowIndex + i));
            }

            return list;
        }

        /// <summary>
        /// Checks whether launch windows are currently open (active) for the target destination.
        /// </summary>
        public static bool IsWindowOpen(DestinationRecord destination, GameDate currentDate)
        {
            if (destination == null) return false;
            LaunchWindowInfo current = GetCurrentOrNextWindow(destination, currentDate);
            return current.IsActive(currentDate);
        }

        /// <summary>
        /// Computes missed window penalty duration in game-days if a launch date missed the target window.
        /// </summary>
        public static int CalculateWaitDaysForNextWindow(DestinationRecord destination, GameDate currentDate)
        {
            if (destination == null || destination.WindowPeriodDays <= 0) return 0;
            LaunchWindowInfo next = GetCurrentOrNextWindow(destination, currentDate);
            return next.DaysUntilOpen(currentDate);
        }
    }
}
