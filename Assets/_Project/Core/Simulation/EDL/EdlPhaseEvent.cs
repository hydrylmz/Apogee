using System;

namespace Apogee.Core.Simulation.EDL
{
    /// <summary>
    /// Telemetry log record for an individual milestone in the EDL sequence.
    /// Supports cause-and-effect reporting and visual presentation (GDD §10.1, §10.5, §13.2).
    /// </summary>
    public sealed class EdlPhaseEvent
    {
        public EdlPhaseType Phase { get; }
        public double ElapsedSeconds { get; }
        public EdlPhaseStatus Status { get; }
        public string TitleKey { get; }
        public string NarrativeKey { get; }
        public double AltitudeKm { get; }
        public double VelocityMps { get; }
        public double HeatShieldIntegrity { get; }
        public string TechnicalDetails { get; }

        public EdlPhaseEvent(
            EdlPhaseType phase,
            double elapsedSeconds,
            EdlPhaseStatus status,
            string titleKey,
            string narrativeKey,
            double altitudeKm,
            double velocityMps,
            double heatShieldIntegrity,
            string technicalDetails = null)
        {
            Phase = phase;
            ElapsedSeconds = Math.Max(0.0, elapsedSeconds);
            Status = status;
            TitleKey = titleKey ?? string.Empty;
            NarrativeKey = narrativeKey ?? string.Empty;
            AltitudeKm = Math.Max(0.0, altitudeKm);
            VelocityMps = Math.Max(0.0, velocityMps);
            HeatShieldIntegrity = Math.Max(0.0, Math.Min(1.0, heatShieldIntegrity));
            TechnicalDetails = technicalDetails ?? string.Empty;
        }

        public override string ToString() => $"T+{ElapsedSeconds:0}s [{Phase}] {Status}: {TitleKey} (Alt={AltitudeKm:0.#}km, Vel={VelocityMps:0}m/s, Shield={HeatShieldIntegrity:P0})";
    }
}
