using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Quest.UI
{
    /// <summary>
    /// The banner that says a quest just finished: <b>Complete!</b> over the quest's name, held for a
    /// moment and then gone.
    ///
    /// <para><b>It takes no input and opens no menu frame.</b> A quest can complete while the player is
    /// walking, mid-battle, or reading another screen.
    /// A self-contained overlay canvas, so a bare GameObject on the persistent Startup scene is the whole
    /// installation.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestCompletionNotice : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Seconds the banner holds before clearing.")]
        [Min(0.5f)][SerializeField] private float holdSeconds = 2.2f;

        [Tooltip("Gap between consecutive banners. A completion can chain into a follow-up that " +
                 "completes immediately, and the first line still has to be readable.")]
        [Min(0f)][SerializeField] private float gapSeconds = 0.25f;

        [Header("Text")]
        [SerializeField] private string headingText = "Complete!";

        [Header("Optional — auto-built at runtime if unassigned")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text titleLabel;

        private readonly Queue<string> _pending = new();
        private Coroutine _runner;
        private IEventBus _bus;

        private void Awake()
        {
            if (group == null || titleLabel == null) BuildDefaultUI();

            SetVisible(false);
        }

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            _bus?.Subscribe<QuestCompleted>(OnQuestCompleted);
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<QuestCompleted>(OnQuestCompleted);
            _bus = null;
        }

        private void OnQuestCompleted(QuestCompleted evt)
        {
            _pending.Enqueue(ResolveTitle(evt.QuestId));
            _runner ??= StartCoroutine(Drain());
        }

        private IEnumerator Drain()
        {
            while (_pending.Count > 0)
            {
                string title = _pending.Dequeue();

                if (headingLabel != null) headingLabel.text = headingText;
                if (titleLabel != null) titleLabel.text = title;

                SetVisible(true);

                // Unscaled: a quest can complete while the game sits behind a paused menu.
                yield return new WaitForSecondsRealtime(holdSeconds);

                SetVisible(false);

                if (gapSeconds > 0f) yield return new WaitForSecondsRealtime(gapSeconds);
            }

            _runner = null;
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false;   // never intercept input
            group.interactable = false;
        }

        private static string ResolveTitle(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return string.Empty;

            if (AppContext.Services != null
                && AppContext.Services.TryResolve<IQuestService>(out var service)
                && service is QuestService quests)
            {
                var quest = quests.GetQuest(questId);
                if (quest != null) return quest.DisplayTitle;
            }

            return questId;
        }

        // ---- Self-built default UI (top-centre banner) ------------------------------------------

        private void BuildDefaultUI()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<CanvasScaler>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the passive dialogue banner (110) and the interactive presenter (120).
            canvas.sortingOrder = 130;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            var panel = CreateRect("CompletionPanel", transform);
            panel.anchorMin = new Vector2(0.30f, 0.84f);
            panel.anchorMax = new Vector2(0.70f, 0.96f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var backing = panel.gameObject.AddComponent<Image>();
            backing.color = new Color(0.06f, 0.08f, 0.14f, 0.88f);
            backing.raycastTarget = false;

            headingLabel = CreateText("Heading", panel, 34, new Color(0.98f, 0.85f, 0.35f));
            Anchor(headingLabel.rectTransform, new Vector2(0f, 0.46f), Vector2.one, new Vector2(16, 0), new Vector2(-16, -6));

            titleLabel = CreateText("Title", panel, 24, Color.white);
            Anchor(titleLabel.rectTransform, Vector2.zero, new Vector2(1f, 0.46f), new Vector2(16, 6), new Vector2(-16, 0));
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateText(string name, Transform parent, float size, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;

            return text;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
