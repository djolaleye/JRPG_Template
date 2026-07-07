using TMPro;
using UnityEditor;
using UnityEngine;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Progression.UI;

namespace JRPG.Progression.Editor
{
    /// Builds the five Phase 8 post-battle menu prefabs by cloning the list-menu template and
    /// swapping in each controller, then registers them in the ContextualCanvasRegistry.
    /// Same pattern as CombatUIBuilder. Idempotent.
    public static class ProgressionUIBuilder
    {
        private const string TemplatePath = "Assets/UI/InventoryList.prefab";
        private const string OutFolder = "Assets/UI/PostBattle";

        [MenuItem("JRPG/Setup/Build Phase 8 Progression UI")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath) == null)
            {
                Debug.LogError($"[JRPG.Progression] Template prefab not found at {TemplatePath}.");
                return;
            }
            EnsureFolder(OutFolder);

            var victory = BuildMenuPrefab<VictorySummaryMenuController>("PostBattleVictory", "postbattle_victory", "Victory");
            var rewards = BuildMenuPrefab<RewardReviewMenuController>("PostBattleRewards", "postbattle_rewards", "Rewards");
            var xp = BuildMenuPrefab<XpPreviewMenuController>("PostBattleXpPreview", "postbattle_xp", "Experience");
            var levelUp = BuildMenuPrefab<LevelUpReviewMenuController>("PostBattleLevelUp", "postbattle_levelup", "Level Up!");
            var allocate = BuildMenuPrefab<AttributeAllocationMenuController>("PostBattleAllocate", "postbattle_allocate", "Attribute Points");

            RegisterEntries(
                (victory, "postbattle_victory", OverlayState.RewardScreen),
                (rewards, "postbattle_rewards", OverlayState.RewardScreen),
                (xp, "postbattle_xp", OverlayState.RewardScreen),
                (levelUp, "postbattle_levelup", OverlayState.LevelUpScreen),
                (allocate, "postbattle_allocate", OverlayState.LevelUpScreen));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Progression] Built Phase 8 post-battle UI prefabs + registry entries.");
        }

        private static GameObject BuildMenuPrefab<T>(string prefabName, string menuId, string title) where T : MenuController
        {
            string outPath = $"{OutFolder}/{prefabName}.prefab";
            var root = PrefabUtility.LoadPrefabContents(TemplatePath);
            try
            {
                Object populatorRef = null, playerControlsRef = null;
                string mapName = "Menu";
                var oldCtrl = root.GetComponent<MenuController>();
                if (oldCtrl != null)
                {
                    var so = new SerializedObject(oldCtrl);
                    populatorRef = so.FindProperty("populator")?.objectReferenceValue;
                    playerControlsRef = so.FindProperty("playerControls")?.objectReferenceValue;
                    mapName = so.FindProperty("actionMapName")?.stringValue ?? "Menu";
                    Object.DestroyImmediate(oldCtrl, true);
                }

                var newCtrl = root.AddComponent<T>();
                var nso = new SerializedObject(newCtrl);
                nso.FindProperty("populator").objectReferenceValue = populatorRef;
                nso.FindProperty("playerControls").objectReferenceValue = playerControlsRef;
                nso.FindProperty("actionMapName").stringValue = mapName;
                nso.FindProperty("menuId").stringValue = menuId;
                nso.ApplyModifiedPropertiesWithoutUndo();

                SetTitle(root, title);
                root.name = prefabName;

                return PrefabUtility.SaveAsPrefabAsset(root, outPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetTitle(GameObject root, string title)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.gameObject.name == "Title") { t.text = title; return; }
            }
        }

        private static void RegisterEntries(params (GameObject prefab, string menuId, OverlayState overlay)[] menus)
        {
            var registry = FindRegistry();
            if (registry == null)
            {
                Debug.LogError("[JRPG.Progression] No ContextualCanvasRegistry found — prefabs built but not registered.");
                return;
            }

            foreach (var (prefab, menuId, overlay) in menus)
            {
                if (prefab == null) continue;
                var existing = registry.Find(menuId);
                if (existing == null)
                {
                    existing = new ContextualCanvasRegistry.Entry { menuId = menuId };
                    registry.entries.Add(existing);
                }
                existing.canvasPrefab = prefab;
                existing.mode = GameMode.Combat;
                existing.overlay = overlay;
                existing.input = InputContext.Menu;
            }
            EditorUtility.SetDirty(registry);
        }

        private static ContextualCanvasRegistry FindRegistry()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ContextualCanvasRegistry"))
            {
                var r = AssetDatabase.LoadAssetAtPath<ContextualCanvasRegistry>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null) return r;
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
