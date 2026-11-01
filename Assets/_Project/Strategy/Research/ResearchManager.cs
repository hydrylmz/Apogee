using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Manages the campaign's research tree: node lifecycle, queue, point spending,
    /// progress advancement, and module/stat unlock notifications.
    ///
    /// Design principles (GDD §0.1, §9.2):
    /// - Pure C# — no UnityEngine dependency.
    /// - All mutations called from Commands; ResearchManager itself is mutated, not read.
    /// - Deterministic: no DateTime.Now, no System.Random (uses campaign tick for ordering).
    /// - Content-driven: node data comes from <see cref="ResearchNodeRecord"/> catalog.
    ///
    /// GDD §9.2 branches: Propulsion, Structure, EDL, LifeSupport, Power, Comms, Reliability, ADCS.
    /// </summary>
    public sealed class ResearchManager
    {
        // ── Catalog (immutable after construction) ────────────────────────────────

        private readonly IReadOnlyDictionary<string, ResearchNodeRecord> _catalog;

        // ── Runtime state ─────────────────────────────────────────────────────────

        /// <summary>Per-node runtime states. Key = node ID.</summary>
        private readonly Dictionary<string, ResearchNodeState> _nodeStates;

        /// <summary>
        /// Ordered research queue. The first element is actively being researched.
        /// GDD §9.2 — one active research per slot (single research center; multi-center not in scope for M4-05).
        /// </summary>
        private readonly List<string> _queue;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when a research node completes.
        /// Subscribers (e.g. CampaignState integration layer) apply module unlocks and stat changes.
        /// Parameters: (nodeId, completedRecord).
        /// </summary>
        public event Action<string, ResearchNodeRecord> OnNodeCompleted;

        /// <summary>
        /// Raised when the active research node changes (start, cancel, queue reorder).
        /// Parameter: new active node ID, or null if queue is empty.
        /// </summary>
        public event Action<string> OnActiveResearchChanged;

        // ── Construction ─────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes the research manager from the full node catalog.
        /// Nodes with no prerequisites begin in <see cref="ResearchStatus.Available"/>;
        /// all others start <see cref="ResearchStatus.Locked"/>.
        /// </summary>
        public ResearchManager(IEnumerable<ResearchNodeRecord> catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            _catalog = catalog.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
            _nodeStates = new Dictionary<string, ResearchNodeState>(StringComparer.OrdinalIgnoreCase);
            _queue = new List<string>();

            // Initialise node states
            foreach (var record in _catalog.Values)
            {
                bool available = record.Prerequisites == null || record.Prerequisites.Count == 0;
                _nodeStates[record.Id] = new ResearchNodeState(
                    record.Id,
                    available ? ResearchStatus.Available : ResearchStatus.Locked);
            }
        }

        // ── Queries ───────────────────────────────────────────────────────────────

        /// <summary>Returns the current status of a node by ID. Returns Locked for unknown IDs.</summary>
        public ResearchStatus GetStatus(string nodeId)
        {
            if (nodeId == null) return ResearchStatus.Locked;
            return _nodeStates.TryGetValue(nodeId, out var state) ? state.Status : ResearchStatus.Locked;
        }

        /// <summary>Returns true if the node has been fully completed.</summary>
        public bool IsCompleted(string nodeId) =>
            GetStatus(nodeId) == ResearchStatus.Completed;

        /// <summary>Returns the node state, or null if not found.</summary>
        public ResearchNodeState GetNodeState(string nodeId) =>
            _nodeStates.TryGetValue(nodeId, out var s) ? s : null;

        /// <summary>Returns the catalog record for a node, or null if not found.</summary>
        public ResearchNodeRecord GetRecord(string nodeId) =>
            _catalog.TryGetValue(nodeId, out var r) ? r : null;

        /// <summary>The currently active (first in queue) research node ID, or null.</summary>
        public string ActiveNodeId => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>Snapshot of the current research queue (ordered).</summary>
        public IReadOnlyList<string> Queue => _queue;

        /// <summary>
        /// All nodes grouped by branch for UI display.
        /// Returns dict: branchName → list of (record, state) pairs.
        /// </summary>
        public Dictionary<string, List<(ResearchNodeRecord Record, ResearchNodeState State)>> GetTreeByBranch()
        {
            var result = new Dictionary<string, List<(ResearchNodeRecord, ResearchNodeState)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in _catalog.Values)
            {
                if (!result.TryGetValue(record.Branch, out var list))
                {
                    list = new List<(ResearchNodeRecord, ResearchNodeState)>();
                    result[record.Branch] = list;
                }
                var state = _nodeStates.TryGetValue(record.Id, out var s) ? s : null;
                list.Add((record, state));
            }
            return result;
        }

        /// <summary>All completed node IDs (unordered). Used to rebuild unlock state from saves.</summary>
        public IEnumerable<string> CompletedNodeIds =>
            _nodeStates.Values
                .Where(s => s.Status == ResearchStatus.Completed)
                .Select(s => s.NodeId);

        // ── Mutations (called from Commands only) ─────────────────────────────────

        /// <summary>
        /// Enqueues a research node.
        /// Returns false with a reason string if the operation is invalid.
        /// </summary>
        public (bool success, string reason) TryEnqueue(string nodeId, long currentTick, double campaignBudget, out double budgetCost)
        {
            budgetCost = 0;

            if (!_catalog.TryGetValue(nodeId, out var record))
                return (false, $"Araştırma düğümü '{nodeId}' katalogda bulunamadı.");

            if (!_nodeStates.TryGetValue(nodeId, out var state))
                return (false, $"Araştırma düğümü '{nodeId}' için durum bulunamadı.");

            if (state.Status == ResearchStatus.Completed)
                return (false, $"'{nodeId}' araştırması zaten tamamlandı.");

            if (state.Status == ResearchStatus.Locked)
                return (false, $"'{nodeId}' araştırması kilitli; önce ön koşulları tamamlayın: {string.Join(", ", record.Prerequisites)}");

            if (_queue.Any(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase)))
                return (false, $"'{nodeId}' zaten araştırma kuyruğunda.");

            // Budget check for start cost
            if (record.StartCostMUSD > 0 && campaignBudget < record.StartCostMUSD)
                return (false, $"'{nodeId}' araştırmasını başlatmak için {record.StartCostMUSD:F0}M$ gerekiyor; mevcut bütçe {campaignBudget:F0}M$.");

            budgetCost = record.StartCostMUSD;
            _queue.Add(nodeId);

            // If first in queue, immediately start
            if (_queue.Count == 1)
            {
                state.Start(currentTick);
                OnActiveResearchChanged?.Invoke(nodeId);
            }

            return (true, null);
        }

        /// <summary>
        /// Cancels a queued or in-progress research node, removing it from the queue.
        /// </summary>
        public (bool success, string reason) TryCancel(string nodeId)
        {
            if (!_queue.Any(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase)))
                return (false, $"'{nodeId}' araştırma kuyruğunda değil.");

            bool wasActive = string.Equals(_queue[0], nodeId, StringComparison.OrdinalIgnoreCase);

            _queue.Remove(nodeId);

            if (_nodeStates.TryGetValue(nodeId, out var state))
                state.Cancel();

            if (wasActive)
            {
                string nextId = _queue.Count > 0 ? _queue[0] : null;
                if (nextId != null && _nodeStates.TryGetValue(nextId, out var nextState))
                    nextState.Start(0); // tick unknown at this point; command will pass it
                OnActiveResearchChanged?.Invoke(nextId);
            }

            return (true, null);
        }

        /// <summary>
        /// Advances research by <paramref name="days"/> game-days, spending research points from the pool.
        /// Returns any nodes completed during this advance (may be >1 if days spans multiple node durations).
        /// Research-center output (<paramref name="researchPointsPerDay"/>) is the rate per day.
        /// </summary>
        public IReadOnlyList<ResearchNodeRecord> AdvanceDays(int days, double researchPointsPerDay, long currentTick)
        {
            if (days <= 0 || _queue.Count == 0) return Array.Empty<ResearchNodeRecord>();

            var completed = new List<ResearchNodeRecord>();
            int remainingDays = days;

            while (remainingDays > 0 && _queue.Count > 0)
            {
                string activeId = _queue[0];
                if (!_catalog.TryGetValue(activeId, out var record))
                {
                    _queue.RemoveAt(0); // corrupted entry, skip
                    continue;
                }

                var state = _nodeStates[activeId];
                if (state.Status == ResearchStatus.Available)
                    state.Start(currentTick);

                int daysNeeded = record.BaseDurationDays - state.ProgressDays;
                int daysApplied = Math.Min(remainingDays, daysNeeded);
                double pointsApplied = daysApplied * researchPointsPerDay;

                state.AdvanceProgress(daysApplied, pointsApplied);
                remainingDays -= daysApplied;

                // Check completion
                if (state.ProgressDays >= record.BaseDurationDays)
                {
                    state.Complete(currentTick);
                    _queue.RemoveAt(0);
                    completed.Add(record);

                    // Unlock successors
                    UnlockSuccessors(record.Id);

                    // Start next if available
                    if (_queue.Count > 0 && _nodeStates.TryGetValue(_queue[0], out var nextState))
                        nextState.Start(currentTick);

                    OnNodeCompleted?.Invoke(record.Id, record);
                    OnActiveResearchChanged?.Invoke(_queue.Count > 0 ? _queue[0] : null);
                }
            }

            return completed;
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Re-evaluates all locked nodes whose prerequisites are now satisfied.
        /// Called after a node completes.
        /// </summary>
        private void UnlockSuccessors(string completedNodeId)
        {
            foreach (var record in _catalog.Values)
            {
                if (!_nodeStates.TryGetValue(record.Id, out var state))
                    continue;

                if (state.Status != ResearchStatus.Locked)
                    continue;

                bool allPrereqsMet = record.Prerequisites == null
                    || record.Prerequisites.Count == 0
                    || record.Prerequisites.All(prereqId => IsCompleted(prereqId));

                if (allPrereqsMet)
                    state.Unlock();
            }
        }

        /// <summary>
        /// Recomputes unlock states for all nodes.
        /// Used after bulk state restoration (e.g. load-from-save).
        /// </summary>
        public void RefreshAllUnlockStates()
        {
            foreach (var record in _catalog.Values)
            {
                if (!_nodeStates.TryGetValue(record.Id, out var state))
                    continue;

                if (state.Status != ResearchStatus.Locked)
                    continue;

                bool allMet = record.Prerequisites == null
                    || record.Prerequisites.Count == 0
                    || record.Prerequisites.All(prereqId => IsCompleted(prereqId));

                if (allMet)
                    state.Unlock();
            }
        }
    }
}
