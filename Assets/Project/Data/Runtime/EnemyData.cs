using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : GameDataBase
    {
        public List<StatEntry> baseStats = new();
        public int level = 1;

        [Header("Rewards")]
        [Min(0)] public int baseXpReward;
        [Min(0)] public int baseCurrencyReward;

        /// Item drops rolled once per defeated instance of this enemy (progression reward resolver.) 
        public List<ItemDropEntry> possibleDrops = new();
        // Later: actionProfileId.
    }

    [Serializable]
    public struct ItemDropEntry
    {
        public string itemId;
        [Range(0f, 1f)] public float dropChance;
        [Min(0)] public int minQuantity;
        [Min(0)] public int maxQuantity;
    }
}
