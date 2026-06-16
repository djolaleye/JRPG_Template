using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Characters
{
    /// Base stats are seeded once from authoring data. Modifiers (flat/percent/override) stack on top.
    public sealed class StatBlockRuntime
    {
        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        private readonly Dictionary<StatType, int> _base = new();
        private readonly List<StatModifier> _modifiers = new();
        private readonly Dictionary<StatType, int> _finalCache = new();
        private bool _cacheValid;

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public void SetBase(StatType stat, int value)
        {
            _base[stat] = value;
            _cacheValid = false;
        }

        public int GetBase(StatType stat) => _base.TryGetValue(stat, out var v) ? v : 0;

        public void AddModifier(StatModifier m)
        {
            _modifiers.Add(m);
            _cacheValid = false;
        }

        public void RemoveModifiersFrom(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;

            int removed = _modifiers.RemoveAll(m => m.sourceId == sourceId);
            if (removed > 0) _cacheValid = false;
        }

        /// Application order: base -> sum Flat -> *(1 + sum PercentAdd) -> *each PercentMult -> Override (last wins) -> clamp >= 0 -> round.
        public int GetFinal(StatType stat)
        {
            if (!_cacheValid) Recalculate();

            return _finalCache.TryGetValue(stat, out var val) ? val : 0;
        }

        public void Recalculate()
        {
            _finalCache.Clear();
            foreach (var s in AllStats)
            {
                _finalCache[s] = ComputeFinal(s);
            }
            _cacheValid = true;
        }

        private int ComputeFinal(StatType stat)
        {
            float value = GetBase(stat);
            float flatSum = 0f;
            float percentAddSum = 0f;
            bool hasOverride = false;
            float overrideValue = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m.stat != stat) continue;

                switch (m.modifierType)
                {
                    case ModifierType.Flat:        flatSum += m.value; break;
                    case ModifierType.PercentAdd:  percentAddSum += m.value; break;
                    case ModifierType.PercentMult: break; // second pass
                    case ModifierType.Override:    hasOverride = true; overrideValue = m.value; break;
                }
            }

            value += flatSum;
            value *= (1f + percentAddSum);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                var m = _modifiers[i];
                if (m.stat != stat || m.modifierType != ModifierType.PercentMult) continue;

                value *= m.value;
            }

            if (hasOverride) value = overrideValue;

            if (value < 0f) value = 0f;
            
            return Mathf.RoundToInt(value);
        }
    }
}
