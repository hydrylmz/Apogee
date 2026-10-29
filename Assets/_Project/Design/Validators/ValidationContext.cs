using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Context passed to validators containing mission requirements, launch pad infrastructure,
    /// and current campaign research unlock status.
    /// Pure data container complying with GDD §17.3 item 6.
    /// </summary>
    public sealed class ValidationContext
    {
        private readonly HashSet<string> _unlockedResearchIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public MissionProfile MissionProfile { get; }
        public LaunchPadInfo LaunchPad { get; }
        public IReadOnlyCollection<string> UnlockedResearchIds => _unlockedResearchIds;

        // Margin warning thresholds (Balance / Game Design settings)
        public double DeltaVMarginWarningThreshold { get; }
        public double PowerMarginWarningThreshold { get; }
        public double ThermalMarginWarningThreshold { get; }
        public double ReliabilityMarginWarningThreshold { get; }
        public double MassMarginWarningThreshold { get; }

        public ValidationContext(
            MissionProfile missionProfile = null,
            LaunchPadInfo launchPad = null,
            IEnumerable<string> unlockedResearchIds = null,
            double deltaVMarginWarningThreshold = 0.05,
            double powerMarginWarningThreshold = 0.10,
            double thermalMarginWarningThreshold = 0.10,
            double reliabilityMarginWarningThreshold = 0.05,
            double massMarginWarningThreshold = 0.95)
        {
            MissionProfile = missionProfile;
            LaunchPad = launchPad ?? LaunchPadInfo.Level2Medium;

            DeltaVMarginWarningThreshold = Math.Max(0.0, deltaVMarginWarningThreshold);
            PowerMarginWarningThreshold = Math.Max(0.0, powerMarginWarningThreshold);
            ThermalMarginWarningThreshold = Math.Max(0.0, thermalMarginWarningThreshold);
            ReliabilityMarginWarningThreshold = Math.Max(0.0, reliabilityMarginWarningThreshold);
            MassMarginWarningThreshold = Math.Max(0.5, Math.Min(1.0, massMarginWarningThreshold));

            if (unlockedResearchIds != null)
            {
                foreach (var id in unlockedResearchIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        _unlockedResearchIds.Add(id);
                }
            }
        }

        public bool IsResearchUnlocked(string researchId)
        {
            if (string.IsNullOrWhiteSpace(researchId))
                return true; // No research required

            return _unlockedResearchIds.Contains(researchId);
        }
    }
}
