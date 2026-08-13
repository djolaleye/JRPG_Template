using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;
using JRPG.Combat.Arena;

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

            // Arenas. Indexed first so the encounter pass can resolve arenaId references.
            var arenasById = ValidateArenas(db, errors, warnings);

            // Encounters.
            for (int i = 0; i < db.encounters.Count; i++)
            {
                var enc = db.encounters[i];
                if (enc == null) { errors.Add($"encounters[{i}] is null."); continue; }
                string label = string.IsNullOrEmpty(enc.Id) ? enc.name : enc.Id;

                if (string.IsNullOrWhiteSpace(enc.Id))
                    errors.Add($"Encounter '{enc.name}' has an empty stable Id.");
                // ResolveRoster, not the legacy enemyIds list: an encounter that authors only the
                // rich roster (which every boss does) would otherwise report "no enemies" and have
                // its enemy references skipped entirely.
                var roster = enc.ResolveRoster();
                if (roster.Count == 0)
                    warnings.Add($"Encounter '{label}' has no enemies.");
                else
                    foreach (var entry in roster)
                        if (!enemyIds.Contains(entry.enemyId))
                            errors.Add($"Encounter '{label}' references missing enemy '{entry.enemyId}'.");

                ValidateEncounterArena(enc, label, arenasById, errors, warnings);
            }

            foreach (var w in warnings) Debug.LogWarning($"[CombatValidator] {w}");
            foreach (var e in errors) Debug.LogError($"[CombatValidator] {e}");
            if (errors.Count == 0 && warnings.Count == 0)
                Debug.Log("[CombatValidator] Combat data validation passed.");
            else
                Debug.Log($"[CombatValidator] Done: {errors.Count} error(s), {warnings.Count} warning(s).");
        }

        /// <summary>
        /// Arena assets and the prefabs they point at. The structural checks live on
        /// <see cref="ArenaRoot.Validate"/> so the editor pass and the runtime staging guard cannot
        /// drift apart; this method adds the checks that need the database's view — duplicate ids,
        /// and coverage against the arena's own declared requirements.
        /// </summary>
        private static Dictionary<string, CombatArenaDefinition> ValidateArenas(
            GameDatabase db, List<string> errors, List<string> warnings)
        {
            var arenasById = new Dictionary<string, CombatArenaDefinition>();
            if (db.arenas == null) return arenasById;

            for (int i = 0; i < db.arenas.Count; i++)
            {
                var arena = db.arenas[i];
                if (arena == null) { errors.Add($"arenas[{i}] is null."); continue; }

                string label = string.IsNullOrEmpty(arena.Id) ? arena.name : arena.Id;

                if (string.IsNullOrWhiteSpace(arena.Id))
                    errors.Add($"Arena '{arena.name}' has an empty stable Id.");
                else if (arenasById.ContainsKey(arena.Id))
                    errors.Add($"Duplicate arena Id '{arena.Id}'.");
                else
                    arenasById[arena.Id] = arena;

                if (arena.arenaPrefab == null)
                {
                    errors.Add($"Arena '{label}' has no arena prefab assigned.");
                    continue;
                }

                var root = arena.arenaPrefab.GetComponentInChildren<ArenaRoot>(true);
                if (root == null)
                {
                    errors.Add($"Arena '{label}' prefab '{arena.arenaPrefab.name}' carries no ArenaRoot component.");
                    continue;
                }

                if (!root.Validate(out var structuralError))
                {
                    errors.Add($"Arena '{label}': {structuralError}");
                    continue;
                }

                int party = root.GetMaxSupported(ArenaTeam.Party);
                if (party < arena.requiredPartyFormations)
                    errors.Add($"Arena '{label}' requires {arena.requiredPartyFormations} player spawn points, " +
                               $"but only {party} are configured.");

                int enemy = root.GetMaxSupported(ArenaTeam.Enemy);
                if (enemy < arena.requiredEnemyFormations)
                    errors.Add($"Arena '{label}' requires {arena.requiredEnemyFormations} enemy spawn points, " +
                               $"but only {enemy} are configured.");

                if (root.Dressing == null)
                    warnings.Add($"Arena '{label}' has no dressing root — the staging will look bare.");
            }

            return arenasById;
        }

        private static void ValidateEncounterArena(EncounterData enc, string label,
                                                   Dictionary<string, CombatArenaDefinition> arenasById,
                                                   List<string> errors, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(enc.arenaId))
            {
                errors.Add($"Encounter '{label}' has no arenaId — the battle transition cannot stage it.");
                return;
            }

            if (!arenasById.TryGetValue(enc.arenaId, out var arena))
            {
                errors.Add($"Encounter '{label}' references missing arena '{enc.arenaId}'.");
                return;
            }

            // A boss on a shared arena still runs; it just throws away the one place unique staging
            // was meant to go. Warning, not an error.
            if (enc.isBoss && !arena.isBossArena)
                warnings.Add($"Boss encounter '{label}' uses shared arena '{arena.Id}' rather than a dedicated one.");

            int rosterSize = enc.ResolveRoster().Count;
            if (rosterSize > arena.requiredEnemyFormations)
                errors.Add($"Encounter '{label}' fields {rosterSize} enemies but arena '{arena.Id}' only " +
                           $"guarantees {arena.requiredEnemyFormations} enemy spawn points.");
        }
    }
}
