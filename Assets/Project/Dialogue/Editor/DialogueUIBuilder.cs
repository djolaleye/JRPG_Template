using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Dialogue.UI;

namespace JRPG.Dialogue.Editor
{
    /// <summary>
    /// Builds the DialoguePresenter prefab and registers it as "dialogue_interactive" +
    /// "dialogue_combat" in the ContextualCanvasRegistry. Idempotent.
    ///
    /// <para><b>Layout follows the Persona 5 reference:</b> a large speaker bust anchored bottom-left,
    /// a name plate straddling the top edge of the body box, and choices in a stack of their own rather
    /// than inline in the body. The shared InputPromptBar and NoticeCard are instantiated from
    /// <c>Assets/UI/Shared</c> rather than rebuilt here.</para>
    ///
    /// <para>One prefab serves both the exploration and combat registry entries; the presenter insets
    /// the box itself when the game is in combat, so the two layouts cannot drift apart.</para>
    /// </summary>
    public static class DialogueUIBuilder
    {
        private const string OutFolder = "Assets/UI/Dialogue";
        private const string PrefabPath = OutFolder + "/DialoguePresenter.prefab";
        private const string ProfilePath = OutFolder + "/DialoguePresentationProfile.asset";
        private const string RowPath = "Assets/UI/Rows/MenuRow_Basic.prefab";
        private const string FallbackRowPath = "Assets/UI/MenuRow.prefab";
        private const string PromptBarPath = "Assets/UI/Shared/InputPromptBar.prefab";
        private const string NoticeCardPath = "Assets/UI/Shared/NoticeCard.prefab";
        private const string InputAssetPath = "Assets/Settings/Input/InputSystem_Actions.inputactions";
        private const string MenuId = "dialogue_interactive";
        private const string CombatMenuId = "dialogue_combat";

        [MenuItem("JRPG/Setup/Build Phase 9 Dialogue UI")]
        public static void Build()
        {
            EnsureFolder(OutFolder);

            // MenuRow_Basic carries the stateAffix slot, which is how a disabled choice shows its reason.
            var rowPrefab = AssetDatabase.LoadAssetAtPath<RowUIController>(RowPath)
                            ?? AssetDatabase.LoadAssetAtPath<RowUIController>(FallbackRowPath);
            if (rowPrefab == null) { Debug.LogError($"[JRPG.Dialogue] No row prefab at {RowPath} or {FallbackRowPath}."); return; }

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var profile = AssetDatabase.LoadAssetAtPath<DialoguePresentationProfile>(ProfilePath);
            if (profile == null) Debug.LogWarning($"[JRPG.Dialogue] No presentation profile at {ProfilePath} — presenter falls back to built-in defaults.");

            var root = BuildPresenter(rowPrefab, input, profile);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            RegisterEntry(saved);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[JRPG.Dialogue] Built DialoguePresenter + registered 'dialogue_interactive' and 'dialogue_combat'.");
        }

        private static GameObject BuildPresenter(RowUIController rowPrefab, InputActionAsset input,
                                                 DialoguePresentationProfile profile)
        {
            var root = new GameObject("DialoguePresenter",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ---- Speaker bust, bottom-left. Tall enough to read as a bust rather than an icon.
            var portraitRoot = CreateRect("PortraitRoot", root.transform);
            Anchor(portraitRoot, new Vector2(0.02f, 0.04f), new Vector2(0.26f, 0.74f), Vector2.zero, Vector2.zero);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(portraitRoot, false);
            var portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            Anchor(portraitGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- Body box. Starts right of the bust so the two never overlap.
            var box = CreateRect("Box", root.transform);
            Anchor(box, new Vector2(0.20f, 0.05f), new Vector2(0.95f, 0.30f), Vector2.zero, Vector2.zero);

            // Frame sits *outside* the panel as an outset border, so toggling it cannot move the text.
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(box, false);
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.color = new Color(0.95f, 0.55f, 0.4f, 0.95f);
            frameImage.raycastTarget = false;
            frameImage.enabled = false;                       // only a framed importance turns it on
            Anchor(frameGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(-5, -5), new Vector2(5, 5));

            var panel = CreateRect("Panel", box);
            Anchor(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(panel.gameObject, new Color(0.10f, 0.10f, 0.16f, 0.96f));

            var body = CreateText("Body", panel, "", 24, TextAlignmentOptions.TopLeft, Color.white);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28, 40), new Vector2(-28, -22));

            var continuePrompt = CreateText("ContinuePrompt", panel, "▼", 22, TextAlignmentOptions.BottomRight,
                new Color(0.8f, 0.8f, 0.9f, 1f));
            Anchor(continuePrompt.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(16, 8), new Vector2(-24, 34));

            // ---- Name plate. A sibling AFTER the box so it draws on top, straddling the box's top edge.
            var namePlate = CreateRect("NamePlate", root.transform);
            Anchor(namePlate, new Vector2(0.21f, 0.285f), new Vector2(0.42f, 0.345f), Vector2.zero, Vector2.zero);
            AddImage(namePlate.gameObject, new Color(0.16f, 0.17f, 0.26f, 1f));

            var speaker = CreateText("SpeakerName", namePlate, "", 26, TextAlignmentOptions.Midline,
                new Color(0.95f, 0.85f, 0.4f, 1f));
            Anchor(speaker.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 2), new Vector2(-16, -2));

            // ---- Choice stack: its own surface above the box, never inline in the body.
            var choiceStack = CreateRect("ChoiceStack", root.transform);
            Anchor(choiceStack, new Vector2(0.52f, 0.33f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero);

            var choiceContent = CreateRect("ChoiceContent", choiceStack);
            // Bottom-anchored so the stack grows upward from the box, the way the reference reads.
            choiceContent.anchorMin = new Vector2(0f, 0f);
            choiceContent.anchorMax = new Vector2(1f, 0f);
            choiceContent.pivot = new Vector2(0.5f, 0f);
            choiceContent.offsetMin = new Vector2(0, 0);
            choiceContent.offsetMax = new Vector2(0, 0);

            var vlg = choiceContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            var fitter = choiceContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var populator = choiceContent.gameObject.AddComponent<VerticalListPopulator>();
            var pso = new SerializedObject(populator);
            pso.FindProperty("contentRoot").objectReferenceValue = choiceContent;
            pso.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // ---- Shared widgets.
            var promptBar = AttachShared(root.transform, PromptBarPath, "InputPromptBar",
                                         stretchFull: false)?.GetComponentInChildren<InputPromptBar>(true);

            var noticeGo = AttachShared(root.transform, NoticeCardPath, "NoticeCard", stretchFull: true);
            var noticeCard = noticeGo != null ? noticeGo.GetComponentInChildren<NoticeCardController>(true) : null;

            // ---- Controller.
            var controller = root.AddComponent<DialoguePresenterController>();
            var cso = new SerializedObject(controller);
            SetRef(cso, "boxRoot", box);
            SetRef(cso, "namePlateRoot", namePlate.gameObject);
            SetRef(cso, "speakerName", speaker);
            SetRef(cso, "body", body);
            SetRef(cso, "continuePrompt", continuePrompt.gameObject);
            SetRef(cso, "frameImage", frameImage);
            SetRef(cso, "portraitRoot", portraitRoot.gameObject);
            SetRef(cso, "portraitImage", portraitImage);
            SetRef(cso, "choiceStackRoot", choiceStack.gameObject);
            SetRef(cso, "noticeCard", noticeCard);
            SetRef(cso, "presentationProfile", profile);
            SetRef(cso, "populator", populator);
            SetRef(cso, "playerControls", input);
            SetRef(cso, "promptBar", promptBar);
            var mapProp = cso.FindProperty("actionMapName");
            if (mapProp != null) mapProp.stringValue = "Menu";
            var idProp = cso.FindProperty("menuId");
            if (idProp != null) idProp.stringValue = MenuId;
            cso.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- UI helpers -----------------------------------------------------------------------

        private static GameObject AttachShared(Transform parent, string prefabPath, string childName, bool stretchFull)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[JRPG.Dialogue] {prefabPath} missing — '{childName}' not attached.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = childName;
            instance.transform.SetAsLastSibling();

            if (stretchFull && instance.transform is RectTransform rt)
                Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return instance;
        }

        private static void SetRef(SerializedObject so, string property, Object value)
        {
            var prop = so.FindProperty(property);
            if (prop == null) { Debug.LogWarning($"[JRPG.Dialogue] Presenter has no '{property}' field."); return; }
            prop.objectReferenceValue = value;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size,
                                           TextAlignmentOptions align, Color color)
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
            img.raycastTarget = false;
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

            Upsert(registry, MenuId, prefab, GameMode.Exploration);
            // Same presenter, but GameMode.Combat so Close restores the battle's layered state.
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
