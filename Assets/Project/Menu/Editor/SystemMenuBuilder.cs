using TMPro;
using UnityEditor;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Menu.Editor
{
    /// Builds the pause-menu "System" submenu prefab and registers it.
    public static class SystemMenuBuilder
    {
        private const string TemplatePath = "Assets/UI/InventoryList.prefab";
        private const string OutPath = "Assets/UI/SystemMenu.prefab";

        [MenuItem("JRPG/Setup/Build System Menu")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath) == null)
            {
                Debug.LogError($"[JRPG.Menu] Template prefab not found at {TemplatePath}.");
                return;
            }

            var prefab = BuildMenuPrefab<SystemMenuController>("SystemMenu", "system", "System");
            RegisterEntry(prefab, "system", GameMode.Exploration, OverlayState.PauseMenu, InputContext.Menu);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Menu] Built System menu prefab + registry entry.");
        }

        private static GameObject BuildMenuPrefab<T>(string prefabName, string menuId, string title) where T : MenuController
        {
            var root = PrefabUtility.LoadPrefabContents(TemplatePath);

            try
            {
                Object populatorRef = null;
                Object playerControlsRef = null;
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

                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.gameObject.name == "Title") { t.text = title; break; }
                }

                root.name = prefabName;

                return PrefabUtility.SaveAsPrefabAsset(root, OutPath);
            }
            
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RegisterEntry(GameObject prefab, string menuId, GameMode mode, OverlayState overlay, InputContext input)
        {
            if (prefab == null) return;

            var registry = FindRegistry();
            if (registry == null)
            {
                Debug.LogError("[JRPG.Menu] No ContextualCanvasRegistry found — prefab built but not registered.");
                return;
            }

            var entry = registry.Find(menuId);
            if (entry == null)
            {
                entry = new ContextualCanvasRegistry.Entry { menuId = menuId };
                registry.entries.Add(entry);
            }

            entry.canvasPrefab = prefab;
            entry.mode = mode;
            entry.overlay = overlay;
            entry.input = input;

            EditorUtility.SetDirty(registry);
        }

        private static ContextualCanvasRegistry FindRegistry()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ContextualCanvasRegistry"))
            {
                var registry = AssetDatabase.LoadAssetAtPath<ContextualCanvasRegistry>(AssetDatabase.GUIDToAssetPath(guid));
                if (registry != null) return registry;
            }

            return null;
        }
    }
}
