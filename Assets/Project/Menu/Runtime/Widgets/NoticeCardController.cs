using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// A dimmed-backdrop card — Clair Obscur's "EXPEDITION STATUS" plate, reused for tutorial cards and
    /// system dialogue in 12.9.
    ///
    /// <para>Fades through <see cref="MenuAnimationProfile.Duration"/>, so reduced motion turns it into
    /// an instant cut instead of a cross-fade.</para>
    ///
    /// <para>No input handling and no lifetime policy: the screen decides when the card goes away.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoticeCardController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Full-rect dim behind the card. Optional.")]
        [SerializeField] private Image backdrop;

        [Tooltip("The card itself. Optional; only used to toggle visibility when no CanvasGroup exists.")]
        [SerializeField] private GameObject cardRoot;

        [SerializeField] private TMP_Text headingLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [Tooltip("Trailing hint line, e.g. \"Press Confirm to continue\". Hidden when null/empty.")]
        [SerializeField] private TMP_Text inputHintLabel;

        [Header("Animation")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Min(0f)][SerializeField] private float fadeDuration = 0.18f;

        [Range(0f, 1f)][SerializeField] private float backdropAlpha = 0.65f;

        [Tooltip("Start hidden regardless of how the prefab was authored.")]
        [SerializeField] private bool hiddenOnAwake = true;

        private Coroutine _fade;
        private bool _warned;

        /// <summary>Raised once the card has finished fading out.</summary>
        public event Action Hidden;

        /// <summary>True from <see cref="Show"/> until the hide fade completes.</summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();

            if (backdrop != null)
            {
                var c = backdrop.color;
                c.a = Mathf.Clamp01(backdropAlpha);
                backdrop.color = c;
            }

            if (hiddenOnAwake) ApplyHiddenImmediate();
        }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>Populate and fade the card in. Calling it while already visible just retargets the text.</summary>
        public void Show(string heading, string body, string inputHint = null)
        {
            if (!EnsureWired()) return;

            if (headingLabel != null) headingLabel.text = heading ?? string.Empty;
            if (bodyLabel != null) bodyLabel.text = body ?? string.Empty;

            if (inputHintLabel != null)
            {
                inputHintLabel.text = inputHint ?? string.Empty;
                inputHintLabel.gameObject.SetActive(!string.IsNullOrEmpty(inputHint));
            }

            IsVisible = true;

            if (cardRoot != null && !cardRoot.activeSelf) cardRoot.SetActive(true);
            if (backdrop != null) backdrop.enabled = true;

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            FadeTo(1f, onDone: null);
        }

        /// <summary>Fade the card out. Safe to call when already hidden.</summary>
        public void Hide()
        {
            if (!IsVisible)
            {
                ApplyHiddenImmediate();
                return;
            }

            IsVisible = false;

            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            FadeTo(0f, onDone: () =>
            {
                if (cardRoot != null) cardRoot.SetActive(false);
                if (backdrop != null) backdrop.enabled = false;
                Hidden?.Invoke();
            });
        }

        /// <summary>Runtime profile override so a screen can share its animation profile.</summary>
        public void SetAnimationProfile(MenuAnimationProfile profile) => animationProfile = profile;

        // ---- internals ---------------------------------------------------------------------------

        private void ApplyHiddenImmediate()
        {
            IsVisible = false;
            StopFade();

            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (cardRoot != null) cardRoot.SetActive(false);
            if (backdrop != null) backdrop.enabled = false;
        }

        private void FadeTo(float target, Action onDone)
        {
            float duration = animationProfile != null
                ? animationProfile.Duration(fadeDuration)
                : Mathf.Max(0f, fadeDuration);

            StopFade();

            // No coroutines outside play mode — an edit-time preview should land on the value directly.
            if (group == null || duration <= 0f || !isActiveAndEnabled || !Application.isPlaying)
            {
                if (group != null) group.alpha = target;
                onDone?.Invoke();
                return;
            }

            _fade = StartCoroutine(FadeRoutine(target, duration, onDone));
        }

        private IEnumerator FadeRoutine(float target, float duration, Action onDone)
        {
            float from = group.alpha;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;   // notices routinely appear with the game paused
                group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            group.alpha = target;
            _fade = null;
            onDone?.Invoke();
        }

        private void StopFade()
        {
            if (_fade == null) return;
            StopCoroutine(_fade);
            _fade = null;
        }

        private bool EnsureWired()
        {
            if (headingLabel != null || bodyLabel != null) return true;

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("[JRPG.Menu] NoticeCardController has no headingLabel or bodyLabel assigned — " +
                                 "Show() renders an empty card.", this);
            }
            return true;   // still fade: an empty but visible card is better than a silent no-op
        }
    }
}
