using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Editor
{
    /// Lightweight Phase 6 combat data validation. Errors indicate data that would cause runtime
    /// failures (missing/duplicate ids, missing references); warnings flag suspicious-but-runnable data.
    public static class CombatDataValidator
    {
        [MenuItem("JRPG/Validate Combat Data")]
        public static void Validate()
        {
            var db = CombatTestDataBuilder.FindGameDatabase();
            if (db == null) { Debug.LogError("[CombatValidator] No GameDatabase asset found."); return; }

            var errors = new List<string>();
            var warnings = new List<string>();

            // Index existing items + enemies for reference checks.
            var itemsById = new Dictionary<string, ItemData>();
            foreach (var item in db.items) if (item != null && !string.IsNullOrEmpty(item.Id)) itemsById[item.Id] = item;
            var enemyIds = new HashSet<string>();
            foreach (var enemy in db.enemies) if (enemy != null && !string.IsNullOrEmpty(enemy.Id)) enemyIds.Add(enemy.Id);

            // Combat actions.
            var seenActionIds = new HashSet<string>();
            for (int i = 0; i < db.combatActions.Count; i++)
            {
                var action = db.combatActions[i];
                if (action == null) { errors.Add($"combatActions[{i}] is null."); continue; }

                string label = string.IsNullOrEmpty(action.Id) ? action.name : action.Id;

                if (string.IsNullOrWhiteSpace(action.Id))
                    errors.Add($"Combat action '{action.name}' has an empty stable Id.");
                else if (!seenActionIds.Add(action.Id))
                    errors.Add($"Duplicate combat action Id '{action.Id}'.");

                if (action.targetRule == null)
                    errors.Add($"Combat action '{label}' has no target rule.");

                bool isGuard = action.category == CombatActionCategory.Guard;
                if ((action.effects == null || action.effects.Count == 0) && !isGuard)
                    errors.Add($"Combat action '{label}' has no effects.");

                if (action.effects != null)
                {
                    for (int e = 0; e < action.effects.Count; e++)
                    {
                        if (action.effects[e].basePower < 0)
                            errors.Add($"Combat action '{label}' effect[{e}] has negative basePower ({action.effects[e].basePower}).");
                    }
                }

                if (action.costs != null)
                {
                    for (int c = 0; c < action.costs.Count; c++)
                    {
                        var cost = action.costs[c];
                        if (cost.type != CombatCostType.Item) continue;
                        if (string.IsNullOrEmpty(cost.itemId) || !itemsById.TryGetValue(cost.itemId, out var item))
                            errors.Add($"Combat action '{label}' item cost references missing item '{cost.itemId}'.");
                        else if (!ItemCombatRules.IsUsableInCombat(item))
                            // An error, not a warning: CombatActionResolver now refuses this cost, so the
                            // action is unexecutable — it would never appear in the item menu and would be
                            // rejected if submitted directly. (Also null-safe on usageRule, unlike before.)
                            errors.Add($"Combat action '{label}' consumes item '{cost.itemId}', which is not usable in combat — the action can never execute.");
                    }
                }
            }

            // Encounters.
            for (int i = 0; i < db.encounters.Count; i++)
            {
                var enc = db.encounters[i];
                if (enc == null) { errors.Add($"encounters[{i}] is null."); continue; }
                string label = string.IsNullOrEmpty(enc.Id) ? enc.name : enc.Id;

                if (string.IsNullOrWhiteSpace(enc.Id))
                    errors.Add($"Encounter '{enc.name}' has an empty stable Id.");
                if (enc.enemyIds == null || enc.enemyIds.Count == 0)
                    warnings.Add($"Encounter '{label}' has no enemies.");
                else
                    foreach (var id in enc.enemyIds)
                        if (!enemyIds.Contains(id))
                            errors.Add($"Encounter '{label}' references missing enemy '{id}'.");
            }

            foreach (var w in warnings) Debug.LogWarning($"[CombatValidator] {w}");
            foreach (var e in errors) Debug.LogError($"[CombatValidator] {e}");
            if (errors.Count == 0 && warnings.Count == 0)
                Debug.Log("[CombatValidator] Combat data validation passed.");
            else
                Debug.Log($"[CombatValidator] Done: {errors.Count} error(s), {warnings.Count} warning(s).");
        }
    }
}
