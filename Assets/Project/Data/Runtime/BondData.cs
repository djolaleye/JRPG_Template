using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// One stage of a character's bond. Reaching <see cref="requiredBondProgress"/> unlocks
    /// <see cref="questId"/>; completing that quest is what raises the level and pays
    /// <see cref="benefits"/>.
    ///
    /// <para>Unlocking and advancing are two events. Progress alone never raises a
    /// level — the quest is the gate, which is what makes a bond a story.</para>
    /// </summary>
    [Serializable]
    public class BondLevelData
    {
        [Min(1)] public int level = 1;

        [Tooltip("Player-facing name for this stage.")]
        public string displayTitle;

        [Tooltip("Cumulative progress that unlocks this level's quest. Progress is never reset by " +
                 "unlocking or completing, so thresholds rise across levels.")]
        [Min(0)] public int requiredBondProgress = 10;

        [Tooltip("The Party quest that must be completed to reach this level.")]
        public string questId;

        [Tooltip("Granted once, as part of the quest completion transaction.")]
        public List<QuestRewardData> benefits = new();

        [Tooltip("Extra gating on top of the progress threshold. Never replaces it.")]
        public List<DialogueCondition> availabilityConditions = new();
    }

    /// <summary>
    /// A character's whole bond progression. One asset per character.
    ///
    /// <para>Level 0 means no bond stage has been completed yet. Levels run 1..N with no gaps.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Bond Data", fileName = "BondData")]
    public sealed class BondData : GameDataBase
    {
        [Tooltip("Must resolve to a CharacterData id.")]
        public string characterId;

        [Min(1)] public int maxLevel = 10;

        public List<BondLevelData> levels = new();

        /// The authored stage for a level, or null when the level is not authored.
        public BondLevelData GetLevel(int level)
        {
            if (levels == null) return null;

            for (int i = 0; i < levels.Count; i++)
                if (levels[i] != null && levels[i].level == level) return levels[i];

            return null;
        }

        /// The highest authored level, which is the practical ceiling regardless of maxLevel.
        public int HighestAuthoredLevel
        {
            get
            {
                int highest = 0;
                if (levels == null) return highest;

                for (int i = 0; i < levels.Count; i++)
                    if (levels[i] != null && levels[i].level > highest) highest = levels[i].level;

                return highest;
            }
        }
    }
}
