using System;
using System.Collections.Generic;

namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Complete result and diagnostic telemetry record of a resolved EDL / arrival attempt.
    /// Complies with GDD §8.1, §10.1, §10.4, §10.5, and §13.2.
    /// </summary>
    public sealed class EdlSimulationResult
    {
        private readonly List<EdlPhaseEvent> _events = new List<EdlPhaseEvent>();

        public EdlOutcome Outcome { get; }
        public bool IsSuccessful => Outcome == EdlOutcome.Success || Outcome == EdlOutcome.PartialSuccess;
        public bool IsCrewSafe { get; }
        public double RemainingHeatShieldIntegrity { get; }
        public double TouchdownVelocityMps { get; }
        public string PrimaryCauseKey { get; }
        public string PlainLanguageSummary { get; }
        public IReadOnlyList<EdlPhaseEvent> PhaseEvents => _events;

        public EdlSimulationResult(
            EdlOutcome outcome,
            bool isCrewSafe,
            double remainingHeatShieldIntegrity,
            double touchdownVelocityMps,
            string primaryCauseKey,
            string plainLanguageSummary,
            IEnumerable<EdlPhaseEvent> phaseEvents = null)
        {
            Outcome = outcome;
            IsCrewSafe = isCrewSafe;
            RemainingHeatShieldIntegrity = Math.Max(0.0, Math.Min(1.0, remainingHeatShieldIntegrity));
            TouchdownVelocityMps = Math.Max(0.0, touchdownVelocityMps);
            PrimaryCauseKey = primaryCauseKey ?? string.Empty;
            PlainLanguageSummary = plainLanguageSummary ?? string.Empty;

            if (phaseEvents != null)
            {
                _events.AddRange(phaseEvents);
            }
        }

        public override string ToString() => $"[EdlResult] {Outcome}: {PlainLanguageSummary} (Events={_events.Count}, TouchdownVel={TouchdownVelocityMps:F1}m/s, CrewSafe={IsCrewSafe})";
    }
}
