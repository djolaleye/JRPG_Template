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

        /// [Planned — Phase 12] Weights for auto-allocation of attribute points. Authored but not
        /// yet read; allocation is manual today.
        public List<StatAllocationRule> autoAllocationRules = new();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (levelUpMode == LevelUpMode.RandomVariance)
                Debug.LogWarning($"[JRPG.Data] '{name}': RandomVariance is not implemented in Phase 8.", this);

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
}
