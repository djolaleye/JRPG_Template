using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Characters
{
    [Serializable]
    public class CharacterSaveData : SaveDataBase
    {
        public string instanceId;
        public string sourceDataId;
        public int level;
        public int currentXp;
        public int currentHP;
        public int currentMP;
        public int currentSP;
        public List<StatModifier> permanentModifiers = new();
        public List<string> equippedItemIds = new();
    }

    public static class CharacterRuntimeInstanceSerialization
    {
        public static CharacterSaveData CaptureState(this CharacterRuntimeInstance inst, int saveVersion)
        {
            var dto = new CharacterSaveData
            {
                version = saveVersion,
                instanceId = inst.InstanceId,
                sourceDataId = inst.SourceDataId,
                level = inst.level,
                currentXp = inst.currentXp,
                currentHP = inst.currentHP,
                currentMP = inst.currentMP,
                currentSP = inst.currentSP,
                equippedItemIds = new List<string>(inst.equippedItemIds)
            };

            var mods = inst.stats.Modifiers;

            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].isPermanent) dto.permanentModifiers.Add(mods[i]);
            }

            return dto;
        }


        /// Reseeds base stats from the source asset (via DataRegistry), reapplies permanent modifiers,
        /// then restores level/XP/current resources. Temporary modifiers are re-derived by their owners later.
        public static void RestoreState(this CharacterRuntimeInstance inst, DataRegistry registry, CharacterSaveData dto)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            inst.InstanceId = dto.instanceId;
            inst.SourceDataId = dto.sourceDataId;
            inst.stats = new StatBlockRuntime();

            // Reseed base stats from the authoring asset.
            if (registry.TryGet<CharacterData>(dto.sourceDataId, out var cdata))
            {
                for (int i = 0; i < cdata.baseStats.Count; i++)
                    inst.stats.SetBase(cdata.baseStats[i].stat, cdata.baseStats[i].value);
            }
            else if (registry.TryGet<EnemyData>(dto.sourceDataId, out var edata))
            {
                for (int i = 0; i < edata.baseStats.Count; i++)
                    inst.stats.SetBase(edata.baseStats[i].stat, edata.baseStats[i].value);
            }
            else
            {
                throw new InvalidOperationException($"Cannot restore: unknown SourceDataId '{dto.sourceDataId}'.");
            }

            for (int i = 0; i < dto.permanentModifiers.Count; i++)
                inst.stats.AddModifier(dto.permanentModifiers[i]);

            inst.level = dto.level;
            inst.currentXp = dto.currentXp;
            inst.equippedItemIds = dto.equippedItemIds != null ? new List<string>(dto.equippedItemIds) : new List<string>();

            inst.stats.Recalculate();
            inst.currentHP = dto.currentHP;
            inst.currentMP = dto.currentMP;
            inst.currentSP = dto.currentSP;
            // Re-clamp in case the asset's caps shrank since save time.
            inst.Recalculate();
        }
    }
}
