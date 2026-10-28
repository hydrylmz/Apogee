using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Container and manager for multiple statistics on a design entity (such as a stage, bus, or module).
    /// Provides batch modifier management, events, and safe value retrieval.
    /// </summary>
    public sealed class StatCollection
    {
        private readonly Dictionary<StatType, Stat> _stats = new Dictionary<StatType, Stat>();

        public event Action<StatType, Stat> OnStatChanged;

        public IEnumerable<Stat> AllStats => _stats.Values;
        public int Count => _stats.Count;

        public Stat this[StatType type]
        {
            get => GetOrCreate(type);
        }

        public Stat GetOrCreate(
            StatType type,
            double defaultBaseValue = 0.0,
            double minValue = double.NegativeInfinity,
            double maxValue = double.PositiveInfinity)
        {
            if (!_stats.TryGetValue(type, out Stat stat))
            {
                stat = new Stat(type, defaultBaseValue, minValue, maxValue);
                stat.OnValueChanged += s => OnStatChanged?.Invoke(s.Type, s);
                _stats[type] = stat;
            }
            return stat;
        }

        public bool TryGet(StatType type, out Stat stat)
        {
            return _stats.TryGetValue(type, out stat);
        }

        public double GetValue(StatType type, double fallback = 0.0)
        {
            return _stats.TryGetValue(type, out Stat stat) ? stat.Value : fallback;
        }

        public void SetBaseValue(StatType type, double baseValue)
        {
            GetOrCreate(type).BaseValue = baseValue;
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            GetOrCreate(modifier.TargetStat).AddModifier(modifier);
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers)
        {
            if (modifiers == null) return;
            foreach (var mod in modifiers)
            {
                AddModifier(mod);
            }
        }

        public int RemoveModifiersFromSource(object source)
        {
            if (source == null) return 0;

            int totalRemoved = 0;
            foreach (var stat in _stats.Values)
            {
                totalRemoved += stat.RemoveModifiersFromSource(source);
            }
            return totalRemoved;
        }

        public void ClearAllModifiers()
        {
            foreach (var stat in _stats.Values)
            {
                stat.ClearModifiers();
            }
        }

        public StatBreakdown GetBreakdown(StatType type)
        {
            return GetOrCreate(type).GetBreakdown();
        }
    }
}
