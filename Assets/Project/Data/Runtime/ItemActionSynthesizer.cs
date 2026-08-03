using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// Builds CombatActionData for a combat-usable item
    ///
    public static class ItemActionSynthesizer
    {
        /// Prefix for the generated action id, so a synthesised action can never collide with an
        /// authored one and is obvious in logs: "itemuse:item_potion_small".
        public const string IdPrefix = "itemuse:";

        public static string ActionIdFor(string itemId) => IdPrefix + itemId;

        public static bool IsSynthesised(string actionId)
            => !string.IsNullOrEmpty(actionId) && actionId.StartsWith(IdPrefix);

        /// Returns the item id a synthesised action consumes, or null for authored actions.
        public static string ItemIdFrom(string actionId)
            => IsSynthesised(actionId) ? actionId.Substring(IdPrefix.Length) : null;

        public static CombatActionData Create(ItemData item)
        {
            if (item == null || !item.IsCombatAction) return null;

            var action = ScriptableObject.CreateInstance<CombatActionData>();
            action.name = ActionIdFor(item.Id);
            action.displayName = string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;
            action.description = item.description;
            action.category = CombatActionCategory.Item;
            action.targetRule = CloneRule(item.combatTargetRule);
            action.effects = ResolveEffects(item);
            action.usableByPlayers = true;
            action.usableByEnemies = false;
            action.cooldownTurns = 0;

            action.costs = item.usageRule != null && item.usageRule.consumedOnUse
                ? new List<CombatCost>
                  {
                      new CombatCost { type = CombatCostType.Item, itemId = item.Id, quantity = 1 },
                  }
                : new List<CombatCost>();

            action.SetRuntimeId(ActionIdFor(item.Id));
            return action;
        }

        /// Explicit combat effects win, otherwise translate the exploration effects
        private static List<CombatEffect> ResolveEffects(ItemData item)
        {
            if (item.combatEffects != null && item.combatEffects.Count > 0)
                return new List<CombatEffect>(item.combatEffects);

            var effects = new List<CombatEffect>();
            if (item.linkedEffects == null) return effects;

            for (int i = 0; i < item.linkedEffects.Count; i++)
            {
                var e = item.linkedEffects[i];
                switch (e.type)
                {
                    case ItemEffectType.Heal:
                        effects.Add(new CombatEffect
                        {
                            type = CombatEffectType.Heal,
                            basePower = e.amount,
                            statScale = 0f,
                            hitChance = 1f,
                        });
                        break;

                    case ItemEffectType.RestoreMP:
                        effects.Add(new CombatEffect
                        {
                            type = CombatEffectType.ResourceChange,
                            resourceType = CombatResource.MP,
                            resourceDelta = e.amount,
                            hitChance = 1f,
                        });
                        break;

                    case ItemEffectType.RestoreSP:
                        effects.Add(new CombatEffect
                        {
                            type = CombatEffectType.ResourceChange,
                            resourceType = CombatResource.SP,
                            resourceDelta = e.amount,
                            hitChance = 1f,
                        });
                        break;
                }
            }
            return effects;
        }

        private static TargetRule CloneRule(TargetRule source)
        {
            if (source == null)
                return new TargetRule { team = TargetTeam.Allies, selectionMode = TargetSelectionMode.Single };

            return new TargetRule
            {
                team = source.team,
                selectionMode = source.selectionMode,
                maxTargets = source.maxTargets,
                requireLiving = source.requireLiving,
                requireDefeated = source.requireDefeated,
                allowSelf = source.allowSelf,
                requiredStatusId = source.requiredStatusId,
                maxHpFraction = source.maxHpFraction,
            };
        }
    }
}
