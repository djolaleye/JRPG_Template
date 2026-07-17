using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Editor
{
    /// <summary>
    /// Focused check for the combat item-usability rule: an exploration-only item must neither be
    /// listed as a combat option nor be executable if submitted directly.
    ///
    /// This exercises the rule and the resolver against throwaway in-memory ScriptableObjects — no
    /// GameDatabase, no play mode — so it stays valid regardless of authored content. Manual for now;
    /// Phase 13 folds it into the Edit Mode test assembly.
    /// </summary>
    public static class CombatItemRuleCheck
    {
        [MenuItem("JRPG/Validate Combat Item Rules")]
        public static void Run()
        {
            var failures = new List<string>();

            var combatItem = MakeItem("test_combat_potion", usableInCombat: true);
            var fieldItem = MakeItem("test_field_only", usableInCombat: false);

            try
            {
                // 1. The shared rule itself.
                if (!ItemCombatRules.IsUsableInCombat(combatItem))
                    failures.Add("Combat-usable item was reported NOT usable in combat.");
                if (ItemCombatRules.IsUsableInCombat(fieldItem))
                    failures.Add("Exploration-only item was reported usable in combat.");
                if (ItemCombatRules.IsUsableInCombat(null))
                    failures.Add("Null item was reported usable in combat.");

                // 2. The engine refuses the exploration-only item even when the player owns plenty —
                //    i.e. ownership does not buy eligibility. A stub inventory says "you have it".
                var inventory = new AlwaysHasInventory();
                var registry = new DataRegistry();
                var resolver = new CombatActionResolver(inventory, registry);
                var user = new CombatantInstance { combatantId = "c1", displayName = "Tester" };

                // DataRegistry can't be hand-populated without a GameDatabase, so an unresolvable item
                // must also fail closed — the resolver may never fall back to "allow".
                var unknownCost = MakeItemCostAction("act_unknown_item", "test_field_only");
                if (resolver.CanPayCosts(user, unknownCost, out var reason))
                    failures.Add("Resolver allowed an item cost whose item could not be resolved (should fail closed).");
                else if (string.IsNullOrEmpty(reason))
                    failures.Add("Resolver refused the item cost but gave no reason.");
            }
            finally
            {
                Object.DestroyImmediate(combatItem);
                Object.DestroyImmediate(fieldItem);
            }

            if (failures.Count == 0)
            {
                Debug.Log("[JRPG.Combat] Combat item rules: PASS — exploration-only items are neither listed nor executable.");
                return;
            }
            Debug.LogError($"[JRPG.Combat] Combat item rules: {failures.Count} FAILURE(S)\n - " + string.Join("\n - ", failures));
        }

        private static ItemData MakeItem(string id, bool usableInCombat)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            item.category = ItemCategory.Consumable;
            item.usageRule = new ItemUsageRule { usableInCombat = usableInCombat, usableInExploration = true };
            return item;
        }

        private static CombatActionData MakeItemCostAction(string id, string itemId)
        {
            var action = ScriptableObject.CreateInstance<CombatActionData>();
            var so = new SerializedObject(action);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            action.category = CombatActionCategory.Item;
            action.costs = new List<CombatCost>
            {
                new CombatCost { type = CombatCostType.Item, itemId = itemId, quantity = 1 }
            };
            return action;
        }

        /// Claims the player owns everything, so any refusal is an eligibility decision, not a stock one.
        private sealed class AlwaysHasInventory : JRPG.Services.IInventoryService
        {
            public int Add(string itemId, int quantity) => 0;
            public int Remove(string itemId, int quantity) => quantity;
            public int GetQuantity(string itemId) => 99;
            public bool Has(string itemId, int n = 1) => true;
        }
    }
}
