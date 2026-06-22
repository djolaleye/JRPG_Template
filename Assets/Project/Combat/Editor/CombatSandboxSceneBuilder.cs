using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JRPG.Bootstrap;

namespace JRPG.Combat.Editor
{
    /// Builds the Phase 6 CombatSandbox scene: a GameBootstrap (so services initialize) plus a
    /// CombatSandboxRunner. GameBootstrap's asset references are located and wired automatically.
    /// menuParent/menuRegistry are intentionally left unset — combat does not need the menu service,
    /// and GameBootstrap proceeds (with a warning) when they are absent.
    public static class CombatSandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CombatSandbox.unity";

        [MenuItem("JRPG/Setup/Create Combat Sandbox Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            WireBootstrap(bootstrap);

            var runnerGo = new GameObject("CombatSandboxRunner");
            runnerGo.AddComponent<CombatSandboxRunner>();

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (saved) Debug.Log($"[JRPG.Combat] Created combat sandbox scene at {ScenePath}.");
            else Debug.LogError("[JRPG.Combat] Failed to save CombatSandbox scene.");
        }

        private static void WireBootstrap(GameBootstrap bootstrap)
        {
            var so = new SerializedObject(bootstrap);

            // GameDatabase must be resolved specially: "t:GameDatabase" is case-insensitive and
            // collides with the base class GameDataBase, matching every data asset.
            var dbProp = so.FindProperty("database");
            var db = CombatTestDataBuilder.FindGameDatabase();
            if (dbProp != null && db != null) dbProp.objectReferenceValue = db;
            else Debug.LogWarning("[JRPG.Combat] GameDatabase not found — GameBootstrap.database left unset.");

            AssignFirst(so, "saveConfig", "t:SaveFileConfig");
            AssignFirst(so, "startingInventory", "t:StartingInventoryConfig");
            AssignFirst(so, "menuRegistry", "t:ContextualCanvasRegistry");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Assigns the first asset matching <paramref name="filter"/> to the named serialized field.
        /// Assets are loaded as plain Object so this editor assembly needs no reference to the types.
        private static void AssignFirst(SerializedObject so, string field, string filter)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;

            var guids = AssetDatabase.FindAssets(filter);
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[JRPG.Combat] No asset found for '{filter}' — GameBootstrap.{field} left unset.");
                return;
            }
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(path);
        }
    }
}
