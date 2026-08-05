using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JRPG.Progression.UI;

namespace JRPG.Progression.Editor
{
    /// Wires the Phase 8 post-battle flow into every scene that can run a battle: CombatSandbox
    /// (plus the ProgressionSandboxRunner harness), TestExplore, and Bootstrap. Idempotent.
    public static class ProgressionSceneSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/CombatSandbox.unity",
            "Assets/Scenes/TestExplore.unity",
            "Assets/Scenes/Startup.unity",   // Bootstrap.unity, renamed in Phase 12.2
        };

        [MenuItem("JRPG/Setup/Wire Phase 8 Post-Battle Flow")]
        public static void WireAllScenes()
        {
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                if (Object.FindObjectOfType<PostBattleFlowController>() == null)
                    new GameObject("PostBattleFlowController").AddComponent<PostBattleFlowController>();

                if (path.Contains("CombatSandbox")
                    && Object.FindObjectOfType<JRPG.Progression.ProgressionSandboxRunner>() == null)
                    new GameObject("ProgressionSandboxRunner").AddComponent<JRPG.Progression.ProgressionSandboxRunner>();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[JRPG.Progression] Wired post-battle flow into '{scene.name}'.");
            }
        }
    }
}
