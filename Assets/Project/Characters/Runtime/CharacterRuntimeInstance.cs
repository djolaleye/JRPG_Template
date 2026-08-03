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

        /// Skills this character currently has equipped/known, capped at MaxSkills. Seeded from
        /// CharacterData.defaultSkillIds and grown by level-up learning; persisted by progression.
        public List<string> skillIds = new();
        public const int MaxSkills = 8;

        public bool KnowsSkill(string skillId)
            => !string.IsNullOrEmpty(skillId) && skillIds.Contains(skillId);

        public bool IsSkillListFull => skillIds.Count >= MaxSkills;

        /// Adds a skill when there is room. Returns false when the list is full.
        public bool TryLearnSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || KnowsSkill(skillId)) return false;
            if (IsSkillListFull) return false;

            skillIds.Add(skillId);
            return true;
        }

        /// Swaps a known skill for a new one, preserving list order.
        public bool ReplaceSkill(string oldSkillId, string newSkillId)
        {
            if (string.IsNullOrEmpty(newSkillId) || KnowsSkill(newSkillId)) return false;

            int index = skillIds.IndexOf(oldSkillId);
            if (index < 0) return false;

            skillIds[index] = newSkillId;
            return true;
        }


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
