using System;

namespace Apogee.Strategy
{
    /// <summary>
    /// Per-node runtime state inside a campaign's research tree.
    /// Tracks how far along a node is, when it was started/completed,
    /// and accumulated research points spent.
    ///
    /// This is a plain data object (no Unity dependency).
    /// GDD §9.2, §19.7, §8.4 (determinism — no DateTime.Now).
    /// </summary>
    public sealed class ResearchNodeState
    {
        // ── Identity ─────────────────────────────────────────────────────────────

        /// <summary>Matches <c>ResearchNodeRecord.Id</c>.</summary>
        public string NodeId { get; }

        // ── Status ────────────────────────────────────────────────────────────────

        /// <summary>Current lifecycle status of this research node.</summary>
        public ResearchStatus Status { get; private set; }

        // ── Progress (calendar-based) ─────────────────────────────────────────────

        /// <summary>
        /// Game-days of active research accumulated so far.
        /// Increases each tick while <see cref="Status"/> is <see cref="ResearchStatus.InProgress"/>.
        /// </summary>
        public int ProgressDays { get; private set; }

        /// <summary>
        /// Game-day tick on which research was started (for record-keeping and undo).
        /// -1 = not started yet.
        /// </summary>
        public long StartTick { get; private set; } = -1L;

        /// <summary>
        /// Game-day tick on which research was completed.
        /// -1 = not completed yet.
        /// </summary>
        public long CompletedTick { get; private set; } = -1L;

        /// <summary>
        /// Research points spent on this node so far (may differ from node cost if paused/cancelled).
        /// </summary>
        public double PointsSpent { get; private set; }

        // ── Construction ─────────────────────────────────────────────────────────

        public ResearchNodeState(string nodeId, ResearchStatus initialStatus = ResearchStatus.Locked)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node ID must not be null or empty.", nameof(nodeId));

            NodeId = nodeId;
            Status = initialStatus;
        }

        // ── Mutation helpers (called only by ResearchManager via Commands) ─────────

        /// <summary>Transitions node to <see cref="ResearchStatus.Available"/> if currently Locked.</summary>
        internal void Unlock()
        {
            if (Status == ResearchStatus.Locked)
                Status = ResearchStatus.Available;
        }

        /// <summary>
        /// Begins or resumes active research.
        /// Sets status to <see cref="ResearchStatus.InProgress"/> and records the start tick.
        /// </summary>
        internal void Start(long currentTick, double initialPointsSpent = 0)
        {
            if (Status != ResearchStatus.Available && Status != ResearchStatus.InProgress)
                throw new InvalidOperationException($"Cannot start research on node '{NodeId}' with status {Status}.");

            Status = ResearchStatus.InProgress;
            if (StartTick < 0) StartTick = currentTick; // preserve original start on resume
            PointsSpent = initialPointsSpent;
        }

        /// <summary>
        /// Advances progress by the given number of game-days and points.
        /// Does not check completion; caller (<see cref="ResearchManager"/>) decides.
        /// </summary>
        internal void AdvanceProgress(int days, double pointsEarned)
        {
            if (Status != ResearchStatus.InProgress)
                return;

            ProgressDays += days;
            PointsSpent  += pointsEarned;
        }

        /// <summary>Marks the node as completed at the given tick.</summary>
        internal void Complete(long completedTick)
        {
            Status         = ResearchStatus.Completed;
            CompletedTick  = completedTick;
        }

        /// <summary>
        /// Cancels an in-progress research, reverting to Available.
        /// Progress and spent points are lost (by design — GDD does not define refunds here).
        /// </summary>
        internal void Cancel()
        {
            if (Status != ResearchStatus.InProgress)
                return;

            Status       = ResearchStatus.Available;
            ProgressDays = 0;
            PointsSpent  = 0;
            StartTick    = -1L;
        }

        /// <summary>Progress fraction [0, 1] toward completion (based on days).</summary>
        public double GetProgressFraction(int baseDurationDays)
        {
            if (Status == ResearchStatus.Completed) return 1.0;
            if (baseDurationDays <= 0) return 0.0;
            return Math.Min(1.0, (double)ProgressDays / baseDurationDays);
        }

        public override string ToString() =>
            $"ResearchNodeState[{NodeId}  {Status}  {ProgressDays}d  {PointsSpent:F1}rp]";
    }
}
