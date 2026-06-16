using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Characters
{
    public class CharacterRuntimeInstance : RuntimeInstanceBase
    {
        public StatBlockRuntime stats = new();
        public int currentHP;
        public int currentMP;
        public int currentSP;
        public int level = 1;
        public int currentXp;
        public List<string> equippedItemIds = new();
        // Later: active statuses, temp modifiers added by other systems.


        /// Re-derive finals and clamp current resources to their new maxima.
        /// Call after adding/removing modifiers that touch MaxHP/MaxMP/MaxSP.

        public void Recalculate()
        {
            stats.Recalculate();
            int maxHp = stats.GetFinal(StatType.MaxHP);
            int maxMp = stats.GetFinal(StatType.MaxMP);
            int maxSp = stats.GetFinal(StatType.MaxSP);
            
            if (currentHP > maxHp) currentHP = maxHp;
            if (currentMP > maxMp) currentMP = maxMp;
            if (currentSP > maxSp) currentSP = maxSp;
            if (currentHP < 0) currentHP = 0;
            if (currentMP < 0) currentMP = 0;
            if (currentSP < 0) currentSP = 0;
        }
    }
}
