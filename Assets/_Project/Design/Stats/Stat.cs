using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents an individual engineering statistic with base value, modifiers,
    /// cached evaluation, dirty state management, and source breakdown generation.
    /// </summary>
    public sealed class Stat
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private double _baseValue;
        private double _minValue = double.NegativeInfinity;
        private double _maxValue = double.PositiveInfinity;
        private double _cachedValue;
        private bool _isDirty = true;

        public StatType Type { get; }

        public double BaseValue
        {
            get => _baseValue;
            set
            {
                if (Math.Abs(_baseValue - value) > 1e-9)
                {
                    _baseValue = value;
                    MarkDirty();
                }
            }
        }

        public double MinValue
        {
            get => _minValue;
            set
            {
                if (Math.Abs(_minValue - value) > 1e-9)
                {
                    _minValue = value;
                    MarkDirty();
                }
            }
        }

        public double MaxValue
        {
            get => _maxValue;
            set
            {
                if (Math.Abs(_maxValue - value) > 1e-9)
                {
                    _maxValue = value;
                    MarkDirty();
                }
            }
        }

        public double Value
        {
            get
            {
                if (_isDirty)
                {
                    _cachedValue = ComputeFinalValue();
                    _isDirty = false;
                }
                return _cachedValue;
            }
        }

        public bool IsDirty => _isDirty;
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public event Action<Stat> OnValueChanged;

        public Stat(StatType type, double baseValue = 0.0, double minValue = double.NegativeInfinity, double maxValue = double.PositiveInfinity)
        {
            Type = type;
            _baseValue = baseValue;
            _minValue = minValue;
            _maxValue = maxValue;
            _isDirty = true;
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));

            _modifiers.Add(modifier);
            _modifiers.Sort(CompareModifiers);
            MarkDirty();
        }

        public bool RemoveModifier(string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId)) return false;

            int index = _modifiers.FindIndex(m => m.Id == modifierId);
            if (index >= 0)
            {
                _modifiers.RemoveAt(index);
                MarkDirty();
                return true;
            }
            return false;
        }

        public int RemoveModifiersFromSource(object source)
        {
            if (source == null) return 0;

            int removed = _modifiers.RemoveAll(m => Equals(m.Source, source));
            if (removed > 0)
            {
                MarkDirty();
            }
            return removed;
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count > 0)
            {
                _modifiers.Clear();
                MarkDirty();
            }
        }

        /// <summary>
        /// Computes a comprehensive breakdown of the base value, each modifier's net contribution,
        /// and the final clamped result for UI explanation tooltips.
        /// </summary>
        public StatBreakdown GetBreakdown()
        {
            var entries = new List<StatBreakdownEntry>(_modifiers.Count);

            double currentFlatSum = 0.0;
            foreach (var mod in _modifiers)
            {
                if (mod.Type == StatModifierType.Flat)
                {
                    currentFlatSum += mod.Value;
                    entries.Add(new StatBreakdownEntry(
                        mod.SourceName,
                        mod.Type,
                        mod.Value,
                        netContribution: mod.Value,
                        descriptionKey: mod.DescriptionKey
                    ));
                }
            }

            double basePlusFlat = _baseValue + currentFlatSum;

            double percentAddSum = 0.0;
            foreach (var mod in _modifiers)
            {
                if (mod.Type == StatModifierType.PercentAdd)
                {
                    percentAddSum += mod.Value;
                    double netCont = basePlusFlat * mod.Value;
                    entries.Add(new StatBreakdownEntry(
                        mod.SourceName,
                        mod.Type,
                        mod.Value,
                        netContribution: netCont,
                        descriptionKey: mod.DescriptionKey
                    ));
                }
            }

            double currentRunningValue = basePlusFlat * (1.0 + percentAddSum);

            foreach (var mod in _modifiers)
            {
                if (mod.Type == StatModifierType.PercentMult)
                {
                    double netCont = currentRunningValue * mod.Value;
                    currentRunningValue *= (1.0 + mod.Value);
                    entries.Add(new StatBreakdownEntry(
                        mod.SourceName,
                        mod.Type,
                        mod.Value,
                        netContribution: netCont,
                        descriptionKey: mod.DescriptionKey
                    ));
                }
            }

            double finalClamped = Math.Max(_minValue, Math.Min(_maxValue, currentRunningValue));
            return new StatBreakdown(Type, _baseValue, finalClamped, entries);
        }

        private double ComputeFinalValue()
        {
            double flatSum = 0.0;
            double percentAddSum = 0.0;
            double runningMult = 1.0;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var mod = _modifiers[i];
                switch (mod.Type)
                {
                    case StatModifierType.Flat:
                        flatSum += mod.Value;
                        break;
                    case StatModifierType.PercentAdd:
                        percentAddSum += mod.Value;
                        break;
                    case StatModifierType.PercentMult:
                        runningMult *= (1.0 + mod.Value);
                        break;
                }
            }

            double result = (_baseValue + flatSum) * (1.0 + percentAddSum) * runningMult;
            return Math.Max(_minValue, Math.Min(_maxValue, result));
        }

        private void MarkDirty()
        {
            _isDirty = true;
            OnValueChanged?.Invoke(this);
        }

        private static int CompareModifiers(StatModifier a, StatModifier b)
        {
            return a.Order.CompareTo(b.Order);
        }

        public override string ToString() => $"{Type}: {Value:0.###} (Base: {BaseValue:0.###})";
    }
}
