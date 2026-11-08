using System;

namespace Apogee.Core.Simulation.Science
{
    /// <summary>
    /// Discrete event logged during a science operation turn / day.
    /// Used by cause-and-effect reporting and live operations log.
    /// Complies with GDD §8.1, §10.1, §10.5, §13.2.
    /// </summary>
    public sealed class SciencePhaseEvent
    {
        public int DayNumber { get; }
        public ScienceEventType EventType { get; }
        public ScienceEventStatus Status { get; }
        public string TitleKey { get; }
        public string Description { get; }
        public double MetricValue { get; }

        public SciencePhaseEvent(
            int dayNumber,
            ScienceEventType eventType,
            ScienceEventStatus status,
            string titleKey,
            string description,
            double metricValue = 0.0)
        {
            DayNumber = dayNumber;
            EventType = eventType;
            Status = status;
            TitleKey = titleKey ?? string.Empty;
            Description = description ?? string.Empty;
            MetricValue = metricValue;
        }

        public override string ToString() => $"[Day {DayNumber}] ({EventType}:{Status}) {TitleKey}: {Description}";
    }
}
