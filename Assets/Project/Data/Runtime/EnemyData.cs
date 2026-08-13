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

        public List<ElementAffinityEntry> elementAffinities = new();
        public List<string> statusImmunityIds = new();
        public List<string> passiveEffectIds = new();

        [Tooltip("EnemyActionProfileData id driving enemy AI.")]
        public string actionProfileId;

        [Tooltip("Skill-category actions this enemy can use (max 8).")]
        public List<string> skillIds = new();

        [Tooltip("Optional. Battle body staged on an arena spawn point. Null uses the placeholder capsule.")]
        public GameObject battlePrefab;
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
