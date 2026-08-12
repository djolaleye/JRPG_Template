using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    public enum LevelUpMode
    {
        FixedGrowth,
        ManualAllocation,
        Hybrid,
        RandomVariance,
    }


    [CreateAssetMenu(menuName = "JRPG/Progression/Character Growth Data", fileName = "CharacterGrowthData")]
    public class CharacterGrowthData : GameDataBase
    {
        public string characterId;
        public string progressionCurveId;
        public LevelUpMode levelUpMode = LevelUpMode.FixedGrowth;
        [Min(0)] public int attributePointsPerLevel;

        /// Flat stat gains granted when reaching the entry's level (FixedGrowth/Hybrid modes).
        public List<StatGrowthEntry> fixedGrowthPerLevel = new();

        /// Skills granted on reaching a level. Other sources (tutors, quests, equipment) can be added
        /// later without touching this table — SkillLearningService is the single entry point.
        [Tooltip("Skills learned at level thresholds. If the character is already at the skill cap, " +
                 "the player is prompted to choose one to discard.")]
        public List<SkillLearnEntry> learnedSkills = new();

        /// [TODO] Weights for auto-allocation of attribute points. Authored but not
        /// yet read; allocation is manual today.
        public List<StatAllocationRule> autoAllocationRules = new();

        /// Skills this character should know at the given level, in learn order.
        public List<string> SkillsUpToLevel(int level)
        {
            var ids = new List<string>();

            for (int i = 0; i < learnedSkills.Count; i++)
            {
                var e = learnedSkills[i];
                if (e.level <= level && !string.IsNullOrEmpty(e.skillId) && !ids.Contains(e.skillId))
                    ids.Add(e.skillId);
            }

            return ids;
        }

        /// Skills unlocked by crossing from oldLevel to newLevel (exclusive → inclusive).
        public List<string> SkillsLearnedBetween(int oldLevel, int newLevel)
        {
            var ids = new List<string>();

            for (int i = 0; i < learnedSkills.Count; i++)
            {
                var e = learnedSkills[i];
                if (e.level > oldLevel && e.level <= newLevel && !string.IsNullOrEmpty(e.skillId) && !ids.Contains(e.skillId))
                    ids.Add(e.skillId);
            }
            
            return ids;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (levelUpMode == LevelUpMode.RandomVariance)
                Debug.LogWarning($"[JRPG.Data] '{name}': RandomVariance is not implemented.", this);

            for (int i = 0; i < fixedGrowthPerLevel.Count; i++)
            {
                var stat = fixedGrowthPerLevel[i].stat;
                if (stat == StatType.MaxHP || stat == StatType.MaxMP || stat == StatType.MaxSP)
                {
                    Debug.LogWarning($"[JRPG.Data] '{name}': fixedGrowthPerLevel entry for {stat} is ignored — " +
                                     "MaxHP/MP/SP grow automatically every level. Use the fixed table for combat stats only.", this);
                    break;
                }
            }
        }
#endif
    }

    [Serializable]
    public struct StatGrowthEntry
    {
        public int level;
        public StatType stat;
        public float value;
    }

    [Serializable]
    public struct StatAllocationRule
    {
        public StatType stat;
        public int weight;
    }

    /// "At level N, learn skill X."
    [Serializable]
    public struct SkillLearnEntry
    {
        [Min(1)] public int level;
        public string skillId;
    }
}
