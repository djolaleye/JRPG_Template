using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JRPG.Dialogue;
using JRPG.Exploration;

namespace JRPG.Dialogue.Editor
{
    /// Wires dialogue into TestExplore: a headless DialogueDebugPanel plus three NPC cubes carrying
    /// NpcDialogueTrigger on the Interactable layer, so both the debug path and the interaction path
    /// can be exercised. Idempotent.
    public static class DialogueSceneSetup
    {
        [MenuItem("JRPG/Setup/Wire TestExplore Dialogue")]
        public static void WireTestExplore()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/TestExplore.unity", OpenSceneMode.Single);
            var scene = EditorSceneManager.GetActiveScene();

            if (Object.FindFirstObjectByType<DialogueDebugPanel>() == null)
                new GameObject("DialogueDebugPanel").AddComponent<DialogueDebugPanel>();

            int layer = ResolveInteractableLayer();

            AddNpc("NPC_Blacksmith", new Vector3(-4f, 0.5f, 4f), "dialogue_blacksmith_greeting", "Blacksmith", layer);
            AddNpc("NPC_Guard", new Vector3(-2f, 0.5f, 4f), "dialogue_guard_gate", "Guard", layer);
            AddNpc("NPC_Rival", new Vector3(0f, 0.5f, 4f), "dialogue_rival_intro", "Rival", layer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[JRPG.Dialogue] Wired TestExplore dialogue (debug panel + 3 NPCs on layer {layer}).");
        }

        private static void AddNpc(string name, Vector3 pos, string graphId, string speaker, int layer)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube); // includes a BoxCollider
                go.name = name;
            }
            go.transform.position = pos;
            go.layer = layer;

            var trigger = go.GetComponent<NpcDialogueTrigger>();
            if (trigger == null) trigger = go.AddComponent<NpcDialogueTrigger>();
            var so = new SerializedObject(trigger);
            so.FindProperty("dialogueGraphId").stringValue = graphId;
            so.FindProperty("speakerContextId").stringValue = speaker;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
        }

        /// Prefer the existing TestInteractable's layer (it's already interactable); else derive from
        /// the Interactor's mask; else Default.
        private static int ResolveInteractableLayer()
        {

            var interactor = Object.FindFirstObjectByType<Interactor>();
            if (interactor != null)
            {
                var so = new SerializedObject(interactor);
                var maskProp = so.FindProperty("interactableMask");
                int mask = maskProp != null ? maskProp.intValue : ~0;
                if (mask != 0 && mask != ~0)
                    for (int i = 0; i < 32; i++)
                        if ((mask & (1 << i)) != 0) return i;
            }
            return 0;
        }
    }
}
