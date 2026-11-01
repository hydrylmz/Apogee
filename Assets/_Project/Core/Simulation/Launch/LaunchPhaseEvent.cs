using System;

namespace Apogee.Core.Simulation.Launch
{
    /// <summary>
    /// Telemetry log record for an individual milestone in the launch ascent sequence.
    /// Supports cause-and-effect reporting and visual presentation (GDD §10.1, §10.5).
    /// </summary>
    public sealed class LaunchPhaseEvent
    {
        public LaunchPhaseType Phase { get; }
        public double TimestampSeconds { get; }
        public LaunchPhaseStatus Status { get; }
        public string TitleKey { get; }
        public string NarrativeKey { get; }
        public double AltitudeKm { get; }
        public double VelocityMps { get; }
        public string TechnicalDetails { get; }

        public LaunchPhaseEvent(
            LaunchPhaseType phase,
            double timestampSeconds,
            LaunchPhaseStatus status,
            string titleKey,
            string narrativeKey,
            double altitudeKm,
            double velocityMps,
            string technicalDetails = null)
        {
            Phase = phase;
            TimestampSeconds = timestampSeconds;
            Status = status;
            TitleKey = titleKey ?? string.Empty;
            NarrativeKey = narrativeKey ?? string.Empty;
            AltitudeKm = Math.Max(0.0, altitudeKm);
            VelocityMps = Math.Max(0.0, velocityMps);
            TechnicalDetails = technicalDetails ?? string.Empty;
        }

        public override string ToString() => $"T+{TimestampSeconds:0}s [{Phase}] {Status}: {TitleKey} (Alt={AltitudeKm:0.#}km, Vel={VelocityMps:0}m/s)";
    }
}
