using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat.Editor
{
    /// One-shot setup tool that authors the Phase 6 starter combat actions + test encounter and
    /// registers them in the GameDatabase. Idempotent: re-running overwrites the same assets in place.
    /// This is setup tooling, not the combat simulator.
    public static class CombatTestDataBuilder
    {
        private const string DatabaseFolder = "Assets/Settings/Database";
        private const string ActionsFolder = DatabaseFolder + "/CombatActions";
        private const string EncountersFolder = DatabaseFolder + "/Encounters";

        private const string PotionItemId = "item_potion_small";
        private const string SlimeEnemyId = "enemy_slime";

        [MenuItem("JRPG/Setup/Build Phase 6 Combat Test Data")]
        public static void Build()
        {
            EnsureFolder(ActionsFolder);
            EnsureFolder(EncountersFolder);

            var actions = new List<CombatActionData>
            {
                CreateAction("attack_melee", "Attack", CombatActionCategory.Melee,
                    Rule(TargetTeam.Enemies, TargetSelectionMode.Single, requireLiving: true, allowSelf: false),
                    NoCost(),
                    new List<CombatEffect> { Damage(6, StatType.Strength, StatType.Defense, 1f) },
                    players: true, enemies: true),

                CreateAction("guard", "Guard", CombatActionCategory.Guard,
                    Rule(TargetTeam.Self, TargetSelectionMode.Self, requireLiving: true, allowSelf: true),
                    NoCost(),
                    new List<CombatEffect> { Guard(0.5f) },
                    players: true, enemies: true),

                CreateAction("item_potion", "Potion", CombatActionCategory.Item,
                    Rule(TargetTeam.Allies, TargetSelectionMode.Single, requireLiving: true, allowSelf: true),
                    new List<CombatCost> { ItemCost(PotionItemId, 1) },
                    new List<CombatEffect> { Heal(30, StatType.Magic, 0f) },
                    players: true, enemies: false),

                CreateAction("skill_fire_01", "Fire", CombatActionCategory.Skill,
                    Rule(TargetTeam.Enemies, TargetSelectionMode.Single, requireLiving: true, allowSelf: false),
                    new List<CombatCost> { MpCost(4) },
                    new List<CombatEffect> { Damage(8, StatType.Magic, StatType.Resistance, 1f) },
                    players: true, enemies: false),

                CreateAction("skill_heal_01", "Heal", CombatActionCategory.Skill,
                    Rule(TargetTeam.Allies, TargetSelectionMode.Single, requireLiving: true, allowSelf: true),
                    new List<CombatCost> { MpCost(4) },
                    new List<CombatEffect> { Heal(20, StatType.Magic, 0.5f) },
                    players: true, enemies: false),
            };

            var encounter = CreateEncounter("encounter_test_slimes",
                new List<string> { SlimeEnemyId, SlimeEnemyId }, escapable: false);

            RegisterInDatabase(actions, encounter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Combat] Built Phase 6 combat test data (5 actions + 1 encounter) and registered in GameDatabase.");
        }

        // ---- Asset authoring ----------------------------------------------------------------

        private static CombatActionData CreateAction(string id, string displayName, CombatActionCategory category,
            TargetRule rule, List<CombatCost> costs, List<CombatEffect> effects, bool players, bool enemies)
        {
            string path = $"{ActionsFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CombatActionData>(path);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CombatActionData>();
                created = true;
            }

            asset.displayName = displayName;
            asset.category = category;
            asset.targetRule = rule;
            asset.costs = costs;
            asset.effects = effects;
            asset.usableByPlayers = players;
            asset.usableByEnemies = enemies;
            SetStableId(asset, id);

            if (created) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EncounterData CreateEncounter(string id, List<string> enemyIds, bool escapable)
        {
            string path = $"{EncountersFolder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EncounterData>(path);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EncounterData>();
                created = true;
            }

            asset.enemyIds = enemyIds;
            asset.battleSceneId = string.Empty;
            asset.victoryRewardTableId = string.Empty;
            asset.escapable = escapable;
            SetStableId(asset, id);

            if (created) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// The stable Id field is private+serialized on GameDataBase; set it through SerializedObject.
        private static void SetStableId(Object asset, string id)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("id");
            if (prop != null)
            {
                prop.stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            asset.name = id;
        }

        /// Locates the GameDatabase container asset. NOTE: the "t:GameDatabase" filter is
        /// case-insensitive and collides with the base class GameDataBase, so it matches every data
        /// asset — we must load each candidate and keep the one that is actually a GameDatabase.
        internal static GameDatabase FindGameDatabase()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db != null) return db;
            }
            return null;
        }

        private static void RegisterInDatabase(List<CombatActionData> actions, EncounterData encounter)
        {
            var db = FindGameDatabase();
            if (db == null)
            {
                Debug.LogError("[JRPG.Combat] No GameDatabase asset found — created assets are not registered.");
                return;
            }

            db.combatActions ??= new List<CombatActionData>();
            db.encounters ??= new List<EncounterData>();

            foreach (var a in actions)
                if (!db.combatActions.Contains(a)) db.combatActions.Add(a);
            if (!db.encounters.Contains(encounter)) db.encounters.Add(encounter);

            EditorUtility.SetDirty(db);
        }

        // ---- Small builders -----------------------------------------------------------------

        private static TargetRule Rule(TargetTeam team, TargetSelectionMode mode, bool requireLiving, bool allowSelf)
            => new() { team = team, selectionMode = mode, requireLiving = requireLiving, allowSelf = allowSelf };

        private static List<CombatCost> NoCost() => new() { new CombatCost { type = CombatCostType.None } };
        private static CombatCost MpCost(int amount) => new() { type = CombatCostType.MP, mpAmount = amount };
        private static CombatCost ItemCost(string itemId, int qty) => new() { type = CombatCostType.Item, itemId = itemId, quantity = qty };

        private static CombatEffect Damage(int basePower, StatType atk, StatType def, float scale)
            => new() { type = CombatEffectType.Damage, basePower = basePower, attackStat = atk, defenseStat = def, statScale = scale };

        private static CombatEffect Heal(int basePower, StatType scalingStat, float scale)
            => new() { type = CombatEffectType.Heal, basePower = basePower, scalingStat = scalingStat, statScale = scale };

        private static CombatEffect Guard(float multiplier)
            => new() { type = CombatEffectType.Guard, guardMultiplier = multiplier };

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
