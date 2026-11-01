using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Runtime record of an active pending event or historical resolved event (GDD §19.10, §19.13).
    /// </summary>
    public sealed class ActiveEventInstance
    {
        public string InstanceId { get; }
        public CampaignEventRecord EventRecord { get; }
        public GameDate TriggeredDate { get; }
        public long TriggeredTick { get; }

        public ActiveEventInstance(CampaignEventRecord record, GameDate date, long tick, string instanceId = null)
        {
            EventRecord = record ?? throw new ArgumentNullException(nameof(record));
            TriggeredDate = date;
            TriggeredTick = tick;
            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? $"EVT-{Guid.NewGuid().ToString().Substring(0, 8)}" : instanceId;
        }

        public override string ToString() => $"[ActiveEvent: {EventRecord.Id} at {TriggeredDate}]";
    }

    /// <summary>
    /// Historical entry of a resolved event option.
    /// </summary>
    public sealed class EventResolutionHistoryEntry
    {
        public string EventId { get; }
        public string SelectedOptionId { get; }
        public GameDate ResolvedDate { get; }
        public EventOutcomeRecord AppliedOutcome { get; }

        public EventResolutionHistoryEntry(string eventId, string optionId, GameDate date, EventOutcomeRecord outcome)
        {
            EventId = eventId;
            SelectedOptionId = optionId;
            ResolvedDate = date;
            AppliedOutcome = outcome;
        }
    }

    /// <summary>
    /// Manages campaign event pools, cooldowns, random checks on daily tick, and active pending events.
    /// Uses deterministic RNG stream <see cref="RngStreamType.Events"/> (GDD §8.4, §17.3).
    /// </summary>
    public sealed class CampaignEventManager
    {
        private readonly List<CampaignEventRecord> _catalogEvents = new List<CampaignEventRecord>();
        private readonly Dictionary<string, int> _cooldowns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _completedNonRepeatable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<EventResolutionHistoryEntry> _history = new List<EventResolutionHistoryEntry>();

        private readonly SeededRng _rng;

        /// <summary>Currently active pending event requiring player decision (null if none).</summary>
        public ActiveEventInstance PendingEvent { get; internal set; }

        /// <summary>Read-only view of historical resolved events.</summary>
        public IReadOnlyList<EventResolutionHistoryEntry> History => _history;

        /// <summary>Read-only view of active cooldown days per event ID.</summary>
        public IReadOnlyDictionary<string, int> Cooldowns => _cooldowns;

        /// <summary>Read-only collection of non-repeatable events that were completed.</summary>
        public IReadOnlyCollection<string> CompletedNonRepeatableEvents => _completedNonRepeatable;

        /// <summary>Read-only catalog of all registered events.</summary>
        public IReadOnlyList<CampaignEventRecord> CatalogEvents => _catalogEvents;

        /// <summary>Check interval in days (default: check every 30 days).</summary>
        public int CheckIntervalDays { get; set; } = 30;

        /// <summary>Probability of triggering an event on check day (default: 0.65 = 65%).</summary>
        public double TriggerChanceOnCheck { get; set; } = 0.65;

        private int _daysSinceLastCheck = 0;

        public CampaignEventManager(IEnumerable<CampaignEventRecord> events = null, ulong seed = 12345)
        {
            _rng = new SeededRng(seed);
            if (events != null)
            {
                _catalogEvents.AddRange(events);
            }
        }

        /// <summary>
        /// Registers or replaces event definitions in the manager.
        /// </summary>
        public void LoadEvents(IEnumerable<CampaignEventRecord> events)
        {
            _catalogEvents.Clear();
            if (events != null)
            {
                _catalogEvents.AddRange(events);
            }
        }

        /// <summary>
        /// Decrements cooldowns and evaluates whether an event triggers on tick advance.
        /// Returns newly triggered <see cref="ActiveEventInstance"/> or null.
        /// </summary>
        public ActiveEventInstance AdvanceDays(int days, GameDate newDate, CampaignState state)
        {
            if (days <= 0) return null;

            // Decrement cooldowns
            var keys = new List<string>(_cooldowns.Keys);
            foreach (var key in keys)
            {
                _cooldowns[key] -= days;
                if (_cooldowns[key] <= 0)
                {
                    _cooldowns.Remove(key);
                }
            }

            // If an event is already pending decision, don't trigger another one to avoid popup spam
            if (PendingEvent != null)
                return null;

            _daysSinceLastCheck += days;
            if (_daysSinceLastCheck < CheckIntervalDays)
                return null;

            _daysSinceLastCheck = 0;

            // Roll chance
            if (_rng.NextDouble() > TriggerChanceOnCheck)
                return null;

            // Find eligible events
            var eligibleEvents = new List<CampaignEventRecord>();
            var weights = new List<double>();
            double totalWeight = 0.0;

            foreach (var evt in _catalogEvents)
            {
                if (EventEvaluator.IsEventEligible(evt, state, _cooldowns, _completedNonRepeatable))
                {
                    double w = EventEvaluator.CalculateEffectiveWeight(evt, state);
                    if (w > 0.001)
                    {
                        eligibleEvents.Add(evt);
                        weights.Add(w);
                        totalWeight += w;
                    }
                }
            }

            if (eligibleEvents.Count == 0 || totalWeight <= 0.0)
                return null;

            // Weighted random selection
            double roll = _rng.NextDouble() * totalWeight;
            double currentSum = 0.0;
            CampaignEventRecord selected = eligibleEvents[0];

            for (int i = 0; i < eligibleEvents.Count; i++)
            {
                currentSum += weights[i];
                if (roll <= currentSum)
                {
                    selected = eligibleEvents[i];
                    break;
                }
            }

            // Create instance
            var instance = new ActiveEventInstance(selected, newDate, state.Time.Tick);
            PendingEvent = instance;

            // Apply cooldown immediately so it won't re-trigger while active
            if (selected.CooldownDays > 0)
            {
                _cooldowns[selected.Id] = selected.CooldownDays;
            }

            return instance;
        }

        /// <summary>
        /// Manually triggers a specific event (useful for scripted events or tests).
        /// </summary>
        public ActiveEventInstance TriggerEventDirectly(string eventId, CampaignState state)
        {
            var evt = _catalogEvents.Find(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
            if (evt == null)
                throw new ArgumentException($"Event '{eventId}' not found in catalog.");

            var instance = new ActiveEventInstance(evt, state.Time.CurrentDate, state.Time.Tick);
            PendingEvent = instance;

            if (evt.CooldownDays > 0)
            {
                _cooldowns[evt.Id] = evt.CooldownDays;
            }

            return instance;
        }

        /// <summary>
        /// Records event resolution and clears pending event.
        /// </summary>
        internal void RecordResolution(string eventId, string optionId, GameDate date, EventOutcomeRecord outcome, bool isRepeatable)
        {
            _history.Add(new EventResolutionHistoryEntry(eventId, optionId, date, outcome));
            if (!isRepeatable)
            {
                _completedNonRepeatable.Add(eventId);
            }

            if (PendingEvent != null && string.Equals(PendingEvent.EventRecord.Id, eventId, StringComparison.OrdinalIgnoreCase))
            {
                PendingEvent = null;
            }
        }

        /// <summary>
        /// Restores state when undoing an event resolution.
        /// </summary>
        internal void UndoResolution(string eventId, string optionId, bool isRepeatable, ActiveEventInstance restoredPending)
        {
            if (_history.Count > 0 && string.Equals(_history[_history.Count - 1].EventId, eventId, StringComparison.OrdinalIgnoreCase))
            {
                _history.RemoveAt(_history.Count - 1);
            }

            if (!isRepeatable)
            {
                _completedNonRepeatable.Remove(eventId);
            }

            PendingEvent = restoredPending;
        }

        /// <summary>
        /// Restores state during AdvanceTime undo.
        /// </summary>
        internal void UndoAdvance(ActiveEventInstance previousPending)
        {
            PendingEvent = previousPending;
        }
    }
}
