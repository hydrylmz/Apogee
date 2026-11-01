using System;
using System.Collections.Generic;

namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Complete result and diagnostic telemetry record of a resolved launch attempt.
    /// Complies with GDD §8.1, §10.1, §10.4, §10.5, and §13.2.
    /// </summary>
    public sealed class LaunchSimulationResult
    {
        private readonly List<LaunchPhaseEvent> _events = new List<LaunchPhaseEvent>();

        public LaunchOutcome Outcome { get; }
        public bool IsSuccessful => Outcome == LaunchOutcome.Success;
        public bool IsCrewSafe { get; }
        public LaunchWeatherCondition WeatherCondition { get; }
        public double WeatherRisk { get; }
        public double DeliveredDeltaV { get; }
        public string PrimaryCauseKey { get; }
        public string PlainLanguageSummary { get; }
        public IReadOnlyList<LaunchPhaseEvent> PhaseEvents => _events;

        public LaunchSimulationResult(
            LaunchOutcome outcome,
            bool isCrewSafe,
            LaunchWeatherCondition weatherCondition,
            double weatherRisk,
            double deliveredDeltaV,
            string primaryCauseKey,
            string plainLanguageSummary,
            IEnumerable<LaunchPhaseEvent> phaseEvents = null)
        {
            Outcome = outcome;
            IsCrewSafe = isCrewSafe;
            WeatherCondition = weatherCondition;
            WeatherRisk = weatherRisk;
            DeliveredDeltaV = Math.Max(0.0, deliveredDeltaV);
            PrimaryCauseKey = primaryCauseKey ?? string.Empty;
            PlainLanguageSummary = plainLanguageSummary ?? string.Empty;

            if (phaseEvents != null)
            {
                _events.AddRange(phaseEvents);
            }
        }

        public override string ToString() => $"[LaunchResult] {Outcome}: {PlainLanguageSummary} (Events={_events.Count}, CrewSafe={IsCrewSafe})";
    }
}
