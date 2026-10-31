using System;
using System.Collections.Generic;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Presentation-ready briefing card model for celestial destinations.
    /// Maps to GDD §6.2 ("Hedef kartı: menzil ihtiyacı, seyir süresi, haberleşme gecikmesi, tehlike simgeleri").
    /// </summary>
    public sealed class DestinationBriefingCard
    {
        public string DestinationId { get; }
        public string DisplayName { get; }
        public double RequiredDeltaVMps { get; }
        public string TransitDurationText { get; }
        public string CommDelayText { get; }
        public string WindowStatusText { get; }
        public bool IsWindowActive { get; }
        public int DaysUntilNextWindow { get; }
        public IReadOnlyList<string> HazardTags { get; }
        public bool IsUnlocked { get; }

        public DestinationBriefingCard(
            DestinationRecord destination,
            GameDate currentDate,
            bool isUnlocked = true)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            DestinationId = destination.Id;
            DisplayName = destination.NameKey ?? destination.Id.ToUpperInvariant();
            RequiredDeltaVMps = destination.DvTotal;

            TransitDurationText = destination.TransitDaysMin == destination.TransitDaysMax
                ? $"{destination.TransitDaysMin} days"
                : $"{destination.TransitDaysMin}–{destination.TransitDaysMax} days";

            CommDelayText = destination.CommDelaySecondsMax < 60.0
                ? $"{destination.CommDelaySecondsMin:0.#}s"
                : $"{Math.Round(destination.CommDelaySecondsMin / 60.0, 1)}–{Math.Round(destination.CommDelaySecondsMax / 60.0, 1)} min";

            IsWindowActive = LaunchWindowCalculator.IsWindowOpen(destination, currentDate);
            DaysUntilNextWindow = LaunchWindowCalculator.CalculateWaitDaysForNextWindow(destination, currentDate);

            if (destination.WindowPeriodDays <= 0)
            {
                WindowStatusText = "Always Open";
            }
            else if (IsWindowActive)
            {
                WindowStatusText = "WINDOW OPEN NOW";
            }
            else
            {
                WindowStatusText = $"Opens in {DaysUntilNextWindow}d";
            }

            var hazards = new List<string>();
            if (destination.Hazards != null)
            {
                foreach (var kvp in destination.Hazards)
                {
                    if (kvp.Value > 0.0)
                    {
                        hazards.Add($"{kvp.Key} ({kvp.Value * 100:0}%)");
                    }
                }
            }
            HazardTags = hazards;
            IsUnlocked = isUnlocked;
        }

        public override string ToString() =>
            $"BriefingCard[{DestinationId} {DisplayName} ?v={RequiredDeltaVMps:0}m/s Window={WindowStatusText}]";
    }
}
