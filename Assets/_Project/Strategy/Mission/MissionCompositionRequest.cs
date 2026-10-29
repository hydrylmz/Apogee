using System;
using System.Collections.Generic;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Input request parameters provided to <see cref="MissionComposer"/> to construct a <see cref="Apogee.Design.MissionProfile"/>.
    /// Encapsulates player choices from the Mission Composer wizard.
    /// Complies with GDD §5.1, §5.4, and §19.12.
    /// </summary>
    public sealed class MissionCompositionRequest
    {
        public string MissionId { get; set; }
        public string MissionName { get; set; }
        public DestinationRecord Destination { get; set; }
        public MissionTypeRecord MissionType { get; set; }
        public IReadOnlyList<ObjectiveRecord> Objectives { get; set; } = Array.Empty<ObjectiveRecord>();
        public int WindowIndex { get; set; } = 0;
        public double? BudgetCeiling { get; set; }

        public MissionCompositionRequest() { }

        public MissionCompositionRequest(
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives = null,
            int windowIndex = 0,
            double? budgetCeiling = null,
            string missionName = null,
            string missionId = null)
        {
            Destination = destination;
            MissionType = missionType;
            Objectives = objectives != null ? new List<ObjectiveRecord>(objectives) : Array.Empty<ObjectiveRecord>();
            WindowIndex = windowIndex;
            BudgetCeiling = budgetCeiling;
            MissionName = missionName;
            MissionId = missionId;
        }
    }
}
