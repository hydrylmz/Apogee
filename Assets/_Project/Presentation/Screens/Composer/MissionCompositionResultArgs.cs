using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Design;

namespace Apogee.UI.Screens.Composer
{
    /// <summary>
    /// Navigation arguments produced when Mission Composer wizard finalizes a mission (GDD §5.1, §5.4).
    /// Carries the fully derived <see cref="MissionProfile"/> and all explicit player selections.
    /// Passed to subsequent screens (e.g., ScreenId.MissionBrief or ScreenId.Workshop).
    /// </summary>
    public sealed class MissionCompositionResultArgs
    {
        public MissionProfile Profile { get; }
        public DestinationRecord Destination { get; }
        public MissionTypeRecord MissionType { get; }
        public IReadOnlyList<ObjectiveRecord> Objectives { get; }
        public int WindowIndex { get; }
        public double? BudgetCeiling { get; }
        public string MissionName { get; }

        public MissionCompositionResultArgs(
            MissionProfile profile,
            DestinationRecord destination,
            MissionTypeRecord missionType,
            IEnumerable<ObjectiveRecord> objectives = null,
            int windowIndex = 0,
            double? budgetCeiling = null,
            string missionName = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Destination = destination;
            MissionType = missionType;
            Objectives = objectives != null ? objectives.Where(o => o != null).ToList() : Array.Empty<ObjectiveRecord>();
            WindowIndex = windowIndex;
            BudgetCeiling = budgetCeiling;
            MissionName = missionName;
        }
    }
}
