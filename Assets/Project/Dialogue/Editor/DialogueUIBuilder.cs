using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Dialogue.UI;

namespace JRPG.Dialogue.Editor
{
    /// Builds the placeholder DialoguePresenter prefab (speaker + body + continue prompt + a choice
    /// list reusing the MenuRow row prefab) and registers it as "dialogue_interactive" in the
    /// ContextualCanvasRegistry. Idempotent.
    public static class DialogueUIBuilder
    {
        private const string OutFolder = "Assets/UI/Dialogue";
        private const string PrefabPath = OutFolder + "/DialoguePresenter.prefab";
        private const string MenuRowPath = "Assets/UI/MenuRow.prefab";
        private const string InputAssetPath = "Assets/Settings/Input/InputSystem_Actions.inputactions";
        private const string MenuId = "dialogue_interactive";
        private const string CombatMenuId = "dialogue_combat";

        [MenuItem("JRPG/Setup/Build Phase 9 Dialogue UI")]
        public static void Build()
        {
            EnsureFolder(OutFolder);

            var rowPrefab = AssetDatabase.LoadAssetAtPath<RowUIController>(MenuRowPath);
            if (rowPrefab == null) { Debug.LogError($"[JRPG.Dialogue] MenuRow prefab not found at {MenuRowPath}."); return; }
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);

            var root = BuildPresenter(rowPrefab, input);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            RegisterEntry(saved);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Dialogue] Built DialoguePresenter prefab + registered 'dialogue_interactive' and 'dialogue_combat'.");
        }

        private static GameObject BuildPresenter(RowUIController rowPrefab, InputActionAsset input)
        {
            var root = new GameObject("DialoguePresenter", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Bottom panel.
            var panel = CreateRect("Panel", root.transform);
            panel.anchorMin = new Vector2(0.08f, 0.05f);
            panel.anchorMax = new Vector2(0.92f, 0.42f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            AddImage(panel.gameObject, new Color(0.10f, 0.10f, 0.16f, 0.96f));

            // Clean non-overlapping bands: speaker (top) / body (upper-mid) / choices (lower) / prompt.
            var speaker = CreateText("SpeakerName", panel, "", 26, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.85f, 0.4f, 1f));
            Anchor(speaker.rectTransform, new Vector2(0, 0.80f), new Vector2(1, 1), new Vector2(16, 2), new Vector2(-16, -6));

            var body = CreateText("Body", panel, "", 22, TextAlignmentOptions.TopLeft, Color.white);
            Anchor(body.rectTransform, new Vector2(0, 0.45f), new Vector2(1, 0.80f), new Vector2(16, 2), new Vector2(-16, -2));

            // Choice list (lower band) — a VerticalListPopulator over a vertical-layout content root.
            var choiceContent = CreateRect("ChoiceContent", panel);
            Anchor(choiceContent, new Vector2(0, 0.08f), new Vector2(1, 0.45f), new Vector2(16, 0), new Vector2(-16, 0));
            var vlg = choiceContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            var fitter = choiceContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var populator = choiceContent.gameObject.AddComponent<VerticalListPopulator>();
            var pso = new SerializedObject(populator);
            pso.FindProperty("contentRoot").objectReferenceValue = choiceContent;
            pso.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var continuePrompt = CreateText("ContinuePrompt", panel, "▼  Continue", 20, TextAlignmentOptions.BottomRight, new Color(0.8f, 0.8f, 0.9f, 1f));
            Anchor(continuePrompt.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 6), new Vector2(-16, 30));

            // Wire the controller.
            var controller = root.AddComponent<DialoguePresenterController>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("speakerName").objectReferenceValue = speaker;
            cso.FindProperty("body").objectReferenceValue = body;
            cso.FindProperty("continuePrompt").objectReferenceValue = continuePrompt.gameObject;
            cso.FindProperty("choicePopulator").objectReferenceValue = populator;
            cso.FindProperty("playerControls").objectReferenceValue = input;
            cso.FindProperty("actionMapName").stringValue = "Menu";
            cso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- UI helpers -----------------------------------------------------------------------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private static void AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        // ---- Registry -------------------------------------------------------------------------

        private static void RegisterEntry(GameObject prefab)
        {
            var registry = FindRegistry();
            if (registry == null) { Debug.LogError("[JRPG.Dialogue] No ContextualCanvasRegistry — prefab built but not registered."); return; }

            // Exploration-mode interactive dialogue.
            Upsert(registry, MenuId, prefab, GameMode.Exploration);
            // Combat-mode interactive dialogue (same presenter prefab) so a mid-battle interruption keeps
            // GameMode.Combat and Close restores the battle's layered state.
            Upsert(registry, CombatMenuId, prefab, GameMode.Combat);

            EditorUtility.SetDirty(registry);
        }

        private static void Upsert(ContextualCanvasRegistry registry, string menuId, GameObject prefab, GameMode mode)
        {
            var entry = registry.Find(menuId);
            if (entry == null) { entry = new ContextualCanvasRegistry.Entry { menuId = menuId }; registry.entries.Add(entry); }
            entry.canvasPrefab = prefab;
            entry.mode = mode;
            entry.overlay = OverlayState.DialogueInteractive;
            entry.input = InputContext.Dialogue;
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
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
