using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// Single full-screen fader. Everything that needs the screen to go black and come
    /// back — scene swaps and later menu open/close — drives this component rather than
    /// animating a CanvasGroup of its own.
    ///
    /// <para><b>Shape of the API.</b> <see cref="ISceneFlowService"/>-style loaders are coroutine based
    /// and need to do their work while the screen is fully black, so the three entry points are split
    /// around that midpoint: <see cref="FadeOut"/> goes to opaque and <i>stays</i> there, calling back
    /// once the screen is covered; <see cref="FadeIn"/> uncovers it; <see cref="Transition"/> is the
    /// convenience pairing for synchronous work. An async loader uses the FadeOut/FadeIn pair —
    /// <c>FadeOut(() =&gt; StartCoroutine(LoadThen(() =&gt; FadeIn())))</c> — because the midpoint
    /// callback of <see cref="Transition"/> must complete before the fade in begins.</para>
    ///
    /// <para><b>Reduced motion.</b> Durations come from <see cref="MenuAnimationProfile.Duration"/>,
    /// never the raw field, so the accessibility toggle collapses the fade to an instant cut. A
    /// zero-length fade takes a dedicated synchronous path: no coroutine is started at all, and the
    /// midpoint and completion callbacks fire inline, in the same order and exactly as many times as
    /// they would during a real fade. Callers can therefore assume the ordering contract holds
    /// regardless of the profile, and (usefully) it also holds in edit mode, where coroutines never
    /// tick.</para>
    ///
    /// <para><b>Overlap policy: interrupt-and-retarget.</b> There is only ever one in-flight request.
    /// A new call cancels the previous one, keeps the alpha wherever it happens to be, and fades from
    /// there to its own target — so the screen never snaps and never stacks coroutines. The superseded
    /// request's callbacks that have not yet fired are <i>dropped</i> (with a warning) rather than
    /// force-fired: firing a stale midpoint would kick off a scene load the caller already abandoned.
    /// Callers that must not be interrupted should gate on <see cref="IsTransitioning"/>.</para>
    ///
    /// <para><b>Lifetime.</b> This lives on the persistent root scene, which never unloads, so it does
    /// not call <c>DontDestroyOnLoad</c> itself. It does not assume it is never disabled either:
    /// disabling cancels the in-flight request, and re-enabling re-applies the visual state and
    /// resolves a stranded half-fade to fully clear, so a disable can never leave the player staring
    /// at a permanently black screen.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenTransitionController : MonoBehaviour
    {
        /// <summary>
        /// Sorting order for the fader's canvas.
        ///
        /// Renders above everything, including modals. 1000 is far above any other.
        /// </summary>
        public const int DefaultSortingOrder = 1000;

        private const float AlphaEpsilon = 0.001f;

        [Header("Wiring")]
        [SerializeField] private Canvas canvas;

        [Tooltip("Drives the fade. Alpha 0 = clear, 1 = fully covering.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("The full-screen black fill. Colour is authored on the prefab; only alpha is animated, " +
                 "through the CanvasGroup.")]
        [SerializeField] private Graphic fadeGraphic;

        [Tooltip("Optional loading view revealed by SetLoadingVisible while the screen is covered.")]
        [SerializeField] private LoadingScreenView loadingScreen;

        [Header("Timing")]
        [Tooltip("Optional. When assigned, the fade length is animationProfile.Duration(screenFadeDuration) " +
                 "so reduced motion turns the fade into an instant cut. Null falls back to fallbackFadeDuration.")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Tooltip("Fade length used only while animationProfile is null.")]
        [Min(0f)] [SerializeField] private float fallbackFadeDuration = 0.35f;

        [SerializeField] private bool useUnscaledTime = true;

        [Header("Layering")]
        [SerializeField] private int sortingOrder = DefaultSortingOrder;

        private Coroutine _routine;
        private int _requestId;
        private float _alpha;
        private bool _loadingVisible;

        /// <summary>True while a fade is actually animating. Always false under reduced motion, because
        /// a zero-length transition completes inside the call that started it.</summary>
        public bool IsTransitioning => _routine != null;

        /// <summary>True once the screen is fully covered. This is the safe window for a scene swap.</summary>
        public bool IsOpaque => _alpha >= 1f - AlphaEpsilon;

        /// <summary>Current cover alpha, 0 (clear) to 1 (opaque). Exposed for tests and tooling.</summary>
        public float Alpha => _alpha;

        private float FadeDuration =>
            animationProfile != null
                ? animationProfile.Duration(animationProfile.screenFadeDuration)
                : Mathf.Max(0f, fallbackFadeDuration);

        private void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (loadingScreen == null) loadingScreen = GetComponentInChildren<LoadingScreenView>(true);

            ApplyVisualState();
        }

        private void OnEnable()
        {
            ApplySorting();

            // A cancelled request left mid-fade has nobody to finish it.
            if (_alpha > AlphaEpsilon && !IsOpaque) _alpha = 0f;

            ApplyVisualState();
        }

        private void OnDisable()
        {
            // Coroutines die with the component; drop the request explicitly so no stale callback can
            // fire if we are re-enabled later.
            if (_routine != null)
            {
                Debug.LogWarning($"[JRPG.Menu] {nameof(ScreenTransitionController)} was disabled mid-transition; " +
                                 "the pending callbacks were dropped.", this);
            }

            Cancel(warnIfPending: false);
        }

        /// <summary>Covers the screen and calls <paramref name="onOpaque"/> once it is fully black.
        /// The screen <i>stays</i> black — pair with <see cref="FadeIn"/>. This is the entry point for
        /// asynchronous work such as a scene load.</summary>
        public void FadeOut(Action onOpaque = null) => Play(true, onOpaque, false, null);

        /// <summary>Uncovers the screen from wherever the alpha currently is, then calls
        /// <paramref name="onComplete"/>.</summary>
        public void FadeIn(Action onComplete = null) => Play(false, null, true, onComplete);

        /// <summary>Fade out, run <paramref name="atOpaque"/> while fully covered, fade back in, then
        /// call <paramref name="onComplete"/>. <paramref name="atOpaque"/> must be synchronous; use the
        /// <see cref="FadeOut"/>/<see cref="FadeIn"/> pair for work that spans frames.</summary>
        public void Transition(Action atOpaque, Action onComplete = null) => Play(true, atOpaque, true, onComplete);

        /// <summary>Shows or hides the loading view. Independent of the fade, so a loader can bring the
        /// view up at the midpoint and take it down before fading back in.</summary>
        public void SetLoadingVisible(bool visible)
        {
            _loadingVisible = visible;

            if (loadingScreen != null)
            {
                if (visible) loadingScreen.Show();
                else loadingScreen.Hide();
            }

            ApplyVisualState();
        }

        /// <summary>Sets the status line on the loading view, if one is wired.</summary>
        public void SetLoadingStatus(string status)
        {
            if (loadingScreen != null) loadingScreen.SetStatus(status);
        }

        /// <summary>Cancels any in-flight request and snaps to the given cover state. Callbacks are
        /// dropped. Intended for hard resets (boot, a failed load) rather than normal flow.</summary>
        public void SnapTo(bool opaque)
        {
            Cancel(warnIfPending: true);
            SetAlpha(opaque ? 1f : 0f);
        }

        private void Play(bool toOpaque, Action midpoint, bool toClear, Action completion)
        {
            Cancel(warnIfPending: true);

            int id = _requestId;
            float duration = FadeDuration;

            // Zero-length fade (reduced motion, an authored 0, or a disabled/edit-mode component that
            // cannot run coroutines) resolves inline. The callback order is identical to the animated
            // path, which is the whole contract callers rely on.
            if (duration <= 0f || !isActiveAndEnabled)
            {
                if (toOpaque) SetAlpha(1f);
                midpoint?.Invoke();

                if (_requestId != id) return;   // a callback started a new request; it owns the screen now

                if (toClear) SetAlpha(0f);
                completion?.Invoke();
                return;
            }

            _routine = StartCoroutine(TransitionRoutine(id, toOpaque, midpoint, toClear, completion, duration));
        }

        private IEnumerator TransitionRoutine(int id, bool toOpaque, Action midpoint, bool toClear,
                                              Action completion, float duration)
        {
            if (toOpaque)
            {
                yield return FadeRoutine(1f, duration);
                if (_requestId != id) yield break;
            }

            midpoint?.Invoke();
            if (_requestId != id) yield break;   // midpoint superseded us — do not touch shared state

            if (toClear)
            {
                yield return FadeRoutine(0f, duration);
                if (_requestId != id) yield break;
            }

            _routine = null;
            completion?.Invoke();
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float start = _alpha;
            float distance = Mathf.Abs(target - start);

            if (distance <= AlphaEpsilon)
            {
                SetAlpha(target);
                yield break;
            }

            // Scale by the distance still to cover so an interrupted fade retargets at the authored
            // speed instead of taking a full duration to travel the last sliver.
            float span = duration * distance;
            float t = 0f;

            while (t < span)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                // Eased
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / span));
                SetAlpha(Mathf.Lerp(start, target, k));
                yield return null;
            }

            SetAlpha(target);
        }

        private void Cancel(bool warnIfPending)
        {
            // Bumping the id orphans any routine still executing this frame, so a coroutine stopped from
            // inside its own callback can never write over the request that replaced it.
            _requestId++;

            if (_routine != null)
            {
                if (warnIfPending)
                {
                    Debug.LogWarning($"[JRPG.Menu] {nameof(ScreenTransitionController)} received a new transition " +
                                     "while one was in flight. Retargeting from the current alpha; the previous " +
                                     "request's remaining callbacks were dropped.", this);
                }

                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private void SetAlpha(float value)
        {
            _alpha = Mathf.Clamp01(value);
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            bool covering = _alpha > AlphaEpsilon;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = _alpha;

                // Block as soon as any cover exists, not only at full opacity: a half-loaded scene must
                // never receive a click, and the fader itself is never interactive.
                canvasGroup.blocksRaycasts = covering;
                canvasGroup.interactable = false;
            }

            if (canvas != null) canvas.enabled = covering || _loadingVisible;
            if (fadeGraphic != null) fadeGraphic.raycastTarget = covering;
        }

        private void ApplySorting()
        {
            if (canvas == null) return;

            // Only meaningful for a nested canvas; on a root canvas overrideSorting does nothing and the
            // sorting order applies directly.
            if (!canvas.isRootCanvas) canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (canvas != null && Application.isPlaying == false) canvas.sortingOrder = sortingOrder;
        }
#endif
    }
}
