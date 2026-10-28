using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents a complete vehicle design template (rocket stages + spacecraft payload).
    /// Stores slot choices, stages, payload subsystems, and links to target mission/destination.
    /// Complies with GDD §7.1, §7.2, and §19.11.
    /// </summary>
    public sealed class Blueprint
    {
        private readonly List<StageDesign> _stages = new List<StageDesign>();

        public string Id { get; }
        public string Name { get; }
        public int Version { get; }

        public string TargetDestinationId { get; }
        public string TargetMissionTypeId { get; }

        public PayloadDesign Payload { get; }
        public IReadOnlyList<StageDesign> Stages => _stages;
        public DesignComponent FairingAdapter { get; }

        public Blueprint(
            string id,
            string name,
            int version = 1,
            string targetDestinationId = null,
            string targetMissionTypeId = null,
            PayloadDesign payload = null,
            IEnumerable<StageDesign> stages = null,
            DesignComponent fairingAdapter = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Blueprint ID cannot be empty.", nameof(id));

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : id;
            Version = version > 0 ? version : 1;

            TargetDestinationId = targetDestinationId;
            TargetMissionTypeId = targetMissionTypeId;

            Payload = payload;
            FairingAdapter = fairingAdapter;

            if (stages != null)
            {
                _stages.AddRange(stages);
            }
        }

        public override string ToString() => $"[Blueprint '{Name}' (v{Version}) Stages: {_stages.Count}]";
    }
}
