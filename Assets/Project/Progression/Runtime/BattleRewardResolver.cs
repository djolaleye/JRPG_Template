using System.Collections.Generic;
using UnityEngine;
using JRPG.Combat;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Progression
{
    /// Resolves victory rewards from defeated enemies.
    public sealed class BattleRewardResolver
    {
        private readonly DataRegistry _data;

        public BattleRewardResolver(DataRegistry data)
        {
            _data = data;
        }

        public ResolvedRewards Resolve(BattleResultData result, int dropSeed)
        {
            var rewards = new ResolvedRewards
            {
                totalXp = result.baseXP,
                currency = result.currency,
            };

            // Accumulate drops by item so the reward screen shows one merged row per item.
            var drops = new Dictionary<string, int>();

            // Any drops combat already attached to the result.
            for (int i = 0; i < result.itemDrops.Count; i++)
                Add(drops, result.itemDrops[i].itemId, result.itemDrops[i].quantity);

            // One drop roll per defeated enemy instance, seeded for determinism.
            var rng = new System.Random(dropSeed);
            for (int i = 0; i < result.defeatedEnemyIds.Count; i++)
            {
                if (!_data.TryGet<EnemyData>(result.defeatedEnemyIds[i], out var enemy)) continue;
                for (int d = 0; d < enemy.possibleDrops.Count; d++)
                {
                    var drop = enemy.possibleDrops[d];
                    if (string.IsNullOrEmpty(drop.itemId)) continue;
                    if (rng.NextDouble() > drop.dropChance) continue;

                    int min = Mathf.Max(1, drop.minQuantity);
                    int max = Mathf.Max(min, drop.maxQuantity);
                    Add(drops, drop.itemId, rng.Next(min, max + 1));
                }
            }

            foreach (var kv in drops)
                rewards.drops.Add(new GrantedDrop { itemId = kv.Key, quantity = kv.Value });
            
            return rewards;
        }

        /// Adds the resolved drops to the inventory. Overflow (full stacks) is logged and dropped.
        public void Grant(ResolvedRewards rewards, IInventoryService inventory)
        {
            if (inventory == null) return;
            
            for (int i = 0; i < rewards.drops.Count; i++)
            {
                var d = rewards.drops[i];
                int overflow = inventory.Add(d.itemId, d.quantity);
                if (overflow > 0)
                    Debug.LogWarning($"[JRPG.Progression] Reward drop '{d.itemId}' x{d.quantity}: {overflow} did not fit in inventory.");
            }
        }

        private static void Add(Dictionary<string, int> drops, string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return;
            drops.TryGetValue(itemId, out int have);
            drops[itemId] = have + quantity;
        }
    }
}
