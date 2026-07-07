using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Progression.Editor
{
    /// One-shot setup tool for the Phase 8 sample content: XP curve, per-character growth data,
    /// reward table, and the char_guardian sample character. Registers everything in the
    /// GameDatabase and wires the growthDataId / victoryRewardTableId hooks. Idempotent.
    public static class ProgressionTestDataBuilder
    {
        private const string DatabaseFolder = "Assets/Settings/Database";
        private const string ProgressionFolder = DatabaseFolder + "/Progression";
        private const string CharactersFolder = DatabaseFolder + "/Characters";

        [MenuItem("JRPG/Setup/Build Phase 8 Progression Test Data")]
        public static void Build()
        {
            EnsureFolder(ProgressionFolder);

            var curve = CreateCurve("curve_default");
            var guardian = CreateGuardianCharacter("char_guardian");

            // Resource stats (MaxHP/MP/SP) are intentionally omitted — they auto-grow every level
            // for all characters via ProgressionEngine.AutoMax*PerLevel. The fixed table is combat
            // stats only.
            var growthHero = CreateGrowth("growth_hero_fixed", "char_hero", "curve_default",
                LevelUpMode.FixedGrowth, pointsPerLevel: 0,
                perLevelGains: new() { (StatType.Strength, 2f), (StatType.Defense, 1f), (StatType.Speed, 1f) });

            var growthAlly = CreateGrowth("growth_ally_hybrid", "char_ally", "curve_default",
                LevelUpMode.Hybrid, pointsPerLevel: 2,
                perLevelGains: new() { (StatType.Magic, 2f) });

            var growthGuardian = CreateGrowth("growth_guardian_manual", "char_guardian", "curve_default",
                LevelUpMode.ManualAllocation, pointsPerLevel: 3,
                perLevelGains: new());

            WireCharacterGrowthId("char_hero", "growth_hero_fixed");
            WireCharacterGrowthId("char_ally", "growth_ally_hybrid");
            WireCharacterGrowthId("char_guardian", "growth_guardian_manual");

            // Enemies are the sole source of XP, currency, and drops. Give the sample slime enough
            // XP that two of them cross a level threshold in the combat sandbox demo, plus a drop.
            ConfigureEnemyRewards("enemy_slime", xp: 55, currency: 15,
                drops: new() { new ItemDropEntry { itemId = "item_potion_small", dropChance = 1f, minQuantity = 1, maxQuantity = 1 } });

            RegisterInDatabase(curve, new[] { growthHero, growthAlly, growthGuardian }, guardian);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Progression] Built Phase 8 test data (curve, 3 growth assets, char_guardian, enemy_slime rewards) and registered in GameDatabase.");
        }

        // ---- Asset authoring ------------------------------------------------------------------

        private static ProgressionCurveData CreateCurve(string id)
        {
            var asset = LoadOrCreate<ProgressionCurveData>($"{ProgressionFolder}/{id}.asset", out bool created);
            asset.displayName = "Default Curve";
            asset.maxLevel = 10;
            asset.levelThresholds = new List<LevelXpEntry>
            {
                new() { level = 2, totalXpRequired = 50 },
                new() { level = 3, totalXpRequired = 120 },
                new() { level = 4, totalXpRequired = 220 },
                new() { level = 5, totalXpRequired = 360 },
                new() { level = 6, totalXpRequired = 540 },
                new() { level = 7, totalXpRequired = 770 },
                new() { level = 8, totalXpRequired = 1050 },
                new() { level = 9, totalXpRequired = 1400 },
                new() { level = 10, totalXpRequired = 1800 },
            };
            Finalize(asset, id, created, $"{ProgressionFolder}/{id}.asset");
            return asset;
        }

        private static CharacterGrowthData CreateGrowth(string id, string characterId, string curveId,
            LevelUpMode mode, int pointsPerLevel, List<(StatType stat, float value)> perLevelGains)
        {
            var asset = LoadOrCreate<CharacterGrowthData>($"{ProgressionFolder}/{id}.asset", out bool created);
            asset.displayName = id;
            asset.characterId = characterId;
            asset.progressionCurveId = curveId;
            asset.levelUpMode = mode;
            asset.attributePointsPerLevel = pointsPerLevel;
            asset.fixedGrowthPerLevel = new List<StatGrowthEntry>();
            for (int level = 2; level <= 10; level++)
                foreach (var (stat, value) in perLevelGains)
                    asset.fixedGrowthPerLevel.Add(new StatGrowthEntry { level = level, stat = stat, value = value });
            Finalize(asset, id, created, $"{ProgressionFolder}/{id}.asset");
            return asset;
        }

        private static CharacterData CreateGuardianCharacter(string id)
        {
            var asset = LoadOrCreate<CharacterData>($"{CharactersFolder}/{id}.asset", out bool created);
            asset.displayName = "Guardian";
            asset.baseStats = new List<StatEntry>
            {
                new() { stat = StatType.MaxHP, value = 120 },
                new() { stat = StatType.MaxMP, value = 30 },
                new() { stat = StatType.MaxSP, value = 30 },
                new() { stat = StatType.Strength, value = 10 },
                new() { stat = StatType.Magic, value = 6 },
                new() { stat = StatType.Defense, value = 12 },
                new() { stat = StatType.Resistance, value = 10 },
                new() { stat = StatType.Speed, value = 6 },
                new() { stat = StatType.Evasion, value = 4 },
                new() { stat = StatType.Luck, value = 5 },
            };
            Finalize(asset, id, created, $"{CharactersFolder}/{id}.asset");
            return asset;
        }

        // ---- Hook wiring ------------------------------------------------------------------------

        private static void WireCharacterGrowthId(string characterId, string growthId)
        {
            var character = FindAssetByStableId<CharacterData>(characterId);
            if (character == null)
            {
                Debug.LogWarning($"[JRPG.Progression] Character '{characterId}' not found — growthDataId not wired.");
                return;
            }
            character.growthDataId = growthId;
            EditorUtility.SetDirty(character);
        }

        private static void ConfigureEnemyRewards(string enemyId, int xp, int currency, List<ItemDropEntry> drops)
        {
            var enemy = FindAssetByStableId<EnemyData>(enemyId);
            if (enemy == null)
            {
                Debug.LogWarning($"[JRPG.Progression] Enemy '{enemyId}' not found — rewards not configured.");
                return;
            }
            enemy.baseXpReward = xp;
            enemy.baseCurrencyReward = currency;
            enemy.possibleDrops = drops;
            EditorUtility.SetDirty(enemy);
        }

        private static void RegisterInDatabase(ProgressionCurveData curve, CharacterGrowthData[] growth,
            CharacterData guardian)
        {
            var db = FindGameDatabase();
            if (db == null)
            {
                Debug.LogError("[JRPG.Progression] No GameDatabase asset found — created assets are not registered.");
                return;
            }

            db.progressionCurves ??= new List<ProgressionCurveData>();
            db.characterGrowth ??= new List<CharacterGrowthData>();

            if (!db.progressionCurves.Contains(curve)) db.progressionCurves.Add(curve);
            foreach (var g in growth)
                if (!db.characterGrowth.Contains(g)) db.characterGrowth.Add(g);
            if (!db.characters.Contains(guardian)) db.characters.Add(guardian);

            EditorUtility.SetDirty(db);
        }

        // ---- Helpers (same patterns as CombatTestDataBuilder) ----------------------------------

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<T>();
            return asset;
        }

        private static void Finalize(Object asset, string id, bool created, string path)
        {
            SetStableId(asset, id);
            if (created) AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
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

        private static T FindAssetByStableId<T>(string stableId) where T : JRPG.Core.GameDataBase
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.Id == stableId) return asset;
            }
            return null;
        }

        /// NOTE: the "t:GameDatabase" filter is case-insensitive and collides with GameDataBase,
        /// so verify each candidate's concrete type (same workaround as CombatTestDataBuilder).
        private static GameDatabase FindGameDatabase()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:GameDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db != null) return db;
            }
            return null;
        }

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
