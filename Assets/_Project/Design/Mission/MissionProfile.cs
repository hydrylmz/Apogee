using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents the operational and engineering requirement profile for a space mission.
    /// Derived from destination, mission type, and selected objectives.
    /// Acts as the target requirement specification against which a <see cref="Blueprint"/> is validated.
    /// Complies with GDD §5.4, §7.4, §17.3, and §19.2/§19.3.
    /// </summary>
    public sealed class MissionProfile
    {
        private readonly List<string> _requiredModuleCategories = new List<string>();
        private readonly List<string> _requiredResearchIds = new List<string>();

        public string Id { get; }
        public string Name { get; }
        public string TargetDestinationId { get; }
        public string TargetMissionTypeId { get; }

        /// <summary>
        /// Total required mission Delta-V in m/s (launch losses + transfer + insertion + landing/return).
        /// GDD §5.4 "menzil ihtiyacı".
        /// </summary>
        public double RequiredDeltaV { get; }

        /// <summary>
        /// Minimum required overall system reliability [0.0 - 1.0].
        /// GDD §5.4 "güvenilirlik eşiği".
        /// </summary>
        public double MinReliabilityThreshold { get; }

        /// <summary>
        /// Minimum launch pad tier level required for this mission (1-5).
        /// GDD §6.1 / §16.7.
        /// </summary>
        public int RequiredLaunchPadLevel { get; }

        /// <summary>
        /// Minimum downlink rate required in GB/day for telemetry and science.
        /// </summary>
        public double MinDownlinkRate { get; }

        /// <summary>
        /// Minimum on-board storage capacity in GB.
        /// </summary>
        public double MinDataStorage { get; }

        /// <summary>
        /// Whether an Entry, Descent & Landing (EDL) module is mandatory (e.g. lander/rover).
        /// GDD §7.7.
        /// </summary>
        public bool RequiresEdl { get; }

        /// <summary>
        /// Whether life support and crew cabin systems are mandatory (crewed missions).
        /// GDD §7.6.
        /// </summary>
        public bool RequiresLifeSupport { get; }

        /// <summary>
        /// Semantic category tags for modules that must be present in the design.
        /// GDD §5.4 "zorunlu modüller".
        /// </summary>
        public IReadOnlyList<string> RequiredModuleCategories => _requiredModuleCategories;

        /// <summary>
        /// Specific research node IDs required for this mission.
        /// </summary>
        public IReadOnlyList<string> RequiredResearchIds => _requiredResearchIds;

        public MissionProfile(
            string id,
            string name = null,
            string targetDestinationId = null,
            string targetMissionTypeId = null,
            double requiredDeltaV = 0.0,
            double minReliabilityThreshold = 0.80,
            int requiredLaunchPadLevel = 1,
            double minDownlinkRate = 0.0,
            double minDataStorage = 0.0,
            bool requiresEdl = false,
            bool requiresLifeSupport = false,
            IEnumerable<string> requiredModuleCategories = null,
            IEnumerable<string> requiredResearchIds = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissionProfile ID cannot be empty.", nameof(id));

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : id;
            TargetDestinationId = targetDestinationId ?? "leo";
            TargetMissionTypeId = targetMissionTypeId ?? "orbiter";

            RequiredDeltaV = Math.Max(0.0, requiredDeltaV);
            MinReliabilityThreshold = Math.Max(0.0, Math.Min(1.0, minReliabilityThreshold));
            RequiredLaunchPadLevel = Math.Max(1, requiredLaunchPadLevel);
            MinDownlinkRate = Math.Max(0.0, minDownlinkRate);
            MinDataStorage = Math.Max(0.0, minDataStorage);

            RequiresEdl = requiresEdl;
            RequiresLifeSupport = requiresLifeSupport;

            if (requiredModuleCategories != null)
            {
                foreach (var cat in requiredModuleCategories)
                {
                    if (!string.IsNullOrWhiteSpace(cat) && !_requiredModuleCategories.Contains(cat))
                        _requiredModuleCategories.Add(cat);
                }
            }

            if (requiredResearchIds != null)
            {
                foreach (var res in requiredResearchIds)
                {
                    if (!string.IsNullOrWhiteSpace(res) && !_requiredResearchIds.Contains(res))
                        _requiredResearchIds.Add(res);
                }
            }
        }

        public override string ToString() =>
            $"[MissionProfile '{Name}' Dest: {TargetDestinationId} Type: {TargetMissionTypeId} Δv Req: {RequiredDeltaV:0} m/s Rel Req: {MinReliabilityThreshold:P0}]";
    }
}
