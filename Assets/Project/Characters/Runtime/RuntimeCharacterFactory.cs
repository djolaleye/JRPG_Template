using System;
using JRPG.Data;

namespace JRPG.Characters
{
    public sealed class RuntimeCharacterFactory
    {
        private readonly DataRegistry _registry;

        public RuntimeCharacterFactory(DataRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public CharacterRuntimeInstance Create(string characterId)
        {
            if (!_registry.TryGet<CharacterData>(characterId, out var data))
                throw new ArgumentException($"Unknown character id '{characterId}'.", nameof(characterId));

            var inst = new CharacterRuntimeInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                SourceDataId = data.Id,
                displayName = data.displayName,
                level = 1,
                currentXp = 0
            };
            SeedBaseStats(inst, data.baseStats);
            inst.stats.Recalculate();
            inst.currentHP = inst.stats.GetFinal(StatType.MaxHP);
            inst.currentMP = inst.stats.GetFinal(StatType.MaxMP);
            inst.currentSP = inst.stats.GetFinal(StatType.MaxSP);
            return inst;
        }

        public CharacterRuntimeInstance Create(EnemyData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var inst = new CharacterRuntimeInstance
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                SourceDataId = data.Id,
                displayName = data.displayName,
                level = data.level,
                currentXp = 0
            };

            SeedBaseStats(inst, data.baseStats);
            inst.stats.Recalculate();
            inst.currentHP = inst.stats.GetFinal(StatType.MaxHP);
            inst.currentMP = inst.stats.GetFinal(StatType.MaxMP);
            inst.currentSP = inst.stats.GetFinal(StatType.MaxSP);

            return inst;
        }

        private static void SeedBaseStats(CharacterRuntimeInstance inst, System.Collections.Generic.List<StatEntry> baseStats)
        {
            if (baseStats == null) return;

            for (int i = 0; i < baseStats.Count; i++)
            {
                var bs = baseStats[i];
                inst.stats.SetBase(bs.stat, bs.value);
            }
        }
    }
}
