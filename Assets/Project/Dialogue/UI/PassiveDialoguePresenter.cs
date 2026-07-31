using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JRPG.Services;

namespace JRPG.Dialogue.UI
{
    /// Lines are queued and shown one at a time with an auto-dismiss timer, so overlapping messages
    /// coalesce into a sequence instead of stacking overlays.
    public sealed class PassiveDialoguePresenter : MonoBehaviour, IPassiveDialoguePresenter
    {
        [Tooltip("Fallback display time")]
        [SerializeField] private float defaultDismissSeconds = 2.5f;

        [Header("Optional — auto-built at runtime if unassigned")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;

        private readonly Queue<Line> _queue = new();
        private Coroutine _runner;

        private struct Line { public string speaker; public string body; public float seconds; }

        private void Awake()
        {
            if (group == null || bodyText == null) BuildDefaultUI();

            SetVisible(false);
        }

        private void OnEnable() => PassiveDialogueSink.Current = this;

        private void OnDisable()
        {
            if (PassiveDialogueSink.Current == (IPassiveDialoguePresenter)this)
                PassiveDialogueSink.Current = null;
        }

        // ---- IPassiveDialoguePresenter ------------------------------------------------------

        public void ShowLine(string speaker, string body, float seconds)
        {
            if (string.IsNullOrEmpty(body)) return;

            _queue.Enqueue(new Line
            {
                speaker = speaker,
                body = body,
                seconds = seconds > 0f ? seconds : defaultDismissSeconds,
            });

            _runner ??= StartCoroutine(Drain());
        }

        private IEnumerator Drain()
        {
            while (_queue.Count > 0)
            {
                var line = _queue.Dequeue();
                if (speakerText != null) speakerText.text = line.speaker ?? string.Empty;
                if (bodyText != null) bodyText.text = line.body ?? string.Empty;

                SetVisible(true);

                yield return new WaitForSeconds(line.seconds);
            }

            SetVisible(false);
            _runner = null;
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false; // never intercept input
            group.interactable = false;
        }

        // ---- Self-built default UI (top-anchored banner) ------------------------------------

        private void BuildDefaultUI()
        {
            var canvas = GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the interactive dialogue presenter (120) so interactive dialogue draws on top.
            canvas.sortingOrder = 110;

            var scaler = GetComponent<CanvasScaler>();

            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            var panel = CreateRect("PassivePanel", transform);
            panel.anchorMin = new Vector2(0.15f, 0.82f);
            panel.anchorMax = new Vector2(0.85f, 0.95f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            var img = panel.gameObject.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.14f, 0.85f);
            img.raycastTarget = false;

            speakerText = CreateText("Speaker", panel, 22, TextAlignmentOptions.TopLeft,
                new Color(0.9f, 0.8f, 0.4f, 1f));
            Anchor(speakerText.rectTransform, new Vector2(0, 0.55f), new Vector2(1, 1), new Vector2(14, 2), new Vector2(-14, -2));

            bodyText = CreateText("Body", panel, 20, TextAlignmentOptions.TopLeft, Color.white);
            Anchor(bodyText.rectTransform, new Vector2(0, 0f), new Vector2(1, 0.55f), new Vector2(14, 2), new Vector2(-14, -2));
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            return go.GetComponent<RectTransform>();
        }

        private static TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.alignment = align; t.color = color; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            
            return t;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }
    }
}
