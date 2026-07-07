using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// Total-XP-per-level table. formula/AnimationCurve variant can supersede this later.
    [CreateAssetMenu(menuName = "JRPG/Progression/Progression Curve", fileName = "ProgressionCurveData")]
    public class ProgressionCurveData : GameDataBase
    {
        [Min(1)] public int maxLevel = 99;
        public List<LevelXpEntry> levelThresholds = new();

        /// Total XP required to be the given level. Level 1 is always 0.
        public int GetTotalXpRequiredForLevel(int level)
        {
            if (level <= 1) return 0;
            if (level > maxLevel) level = maxLevel;

            int best = int.MaxValue;
            bool found = false;

            for (int i = 0; i < levelThresholds.Count; i++)
            {
                if (levelThresholds[i].level == level)
                {
                    best = levelThresholds[i].totalXpRequired;
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                // Clamp to the highest authored threshold at or below the requested level.
                int bestLevel = 1;
                best = 0;
                for (int i = 0; i < levelThresholds.Count; i++)
                {
                    var e = levelThresholds[i];
                    if (e.level <= level && e.level > bestLevel) { bestLevel = e.level; best = e.totalXpRequired; }
                }
            }
            return best;
        }

        /// Highest level whose threshold is satisfied by the given total XP (clamped to maxLevel).
        public int GetLevelForTotalXp(int totalXp)
        {
            int level = 1;
            for (int i = 0; i < levelThresholds.Count; i++)
            {
                var e = levelThresholds[i];
                if (e.level <= maxLevel && totalXp >= e.totalXpRequired && e.level > level)
                    level = e.level;
            }
            return level;
        }
    }

    [Serializable]
    public struct LevelXpEntry
    {
        public int level;
        public int totalXpRequired;
    }
}
