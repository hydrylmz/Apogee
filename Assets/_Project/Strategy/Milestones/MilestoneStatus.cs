namespace Apogee.Strategy.Milestones
{
    /// <summary>
    /// Lifecycle status of an agency milestone.
    /// </summary>
    public enum MilestoneStatus
    {
        /// <summary>Prerequisite milestones not yet fulfilled.</summary>
        Locked = 0,

        /// <summary>Available on the quest board to pursue.</summary>
        Available = 1,

        /// <summary>Requirements achieved via completed missions, reward pending claim.</summary>
        CompletedPendingClaim = 2,

        /// <summary>Requirements achieved and reward claimed into campaign reserves.</summary>
        Claimed = 3
    }
}
