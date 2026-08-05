using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

// `using System` above pulls in System.AppContext, which collides with the static service
// locator. This alias keeps both usable in one file.
using AppContext = JRPG.Core.AppContext;

namespace JRPG.Bootstrap
{
    /// <summary>
    /// Concrete <see cref="ISceneFlowService"/>. Lives on the persistent Startup scene, which is build
    /// index 0 and never unloads, and owns the single additive <i>content</i> scene layered on top of it
    /// (Splash → Title → world).
    ///
    /// <para><b>Why additive rather than single-scene loads.</b> <c>ServiceRegistry.Register</c> throws on
    /// a duplicate registration and <see cref="AppContext"/> has no reset, so re-entering the boot scene
    /// would re-run <see cref="GameBootstrap"/> and tear the whole service graph down. Keeping Startup
    /// resident and swapping only the content scene means bootstrap runs exactly once per process.</para>
    ///
    /// <para><b>Requests are serialized, never interleaved.</b> Every call is queued and drained by one
    /// coroutine. Two overlapping <see cref="SwapTo"/> calls therefore run in order rather than racing
    /// over <see cref="CurrentContentScene"/> and <c>SetActiveScene</c> — and the interface's "every
    /// callback fires exactly once, on the main thread" contract holds even when a callback enqueues
    /// more work of its own.</para>
    ///
    /// <para><b>Callbacks fire at the black midpoint, before the fade in.</b> This is load-bearing for
    /// <see cref="ISessionService.LoadGame"/>. Anything a caller does in <c>onComplete</c> is
    /// therefore invisible.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]   // after GameBootstrap (-10000) so AppContext.Services exists
    public sealed class SceneFlowService : MonoBehaviour, ISceneFlowService
    {
        private enum Kind { Load, Unload, Swap }

        [Header("Wiring")]
        [SerializeField] private ScreenTransitionController transition;

        [Header("Boot")]
        [Tooltip("Content scene loaded automatically on Start. This is the entire boot flow — Startup " +
                 "brings up the services, then hands the screen to this scene. Empty disables auto-boot.")]
        [SerializeField] private string bootContentScene = "Splash";

        [Header("Behaviour")]
        [Tooltip("Show the loading view (black + corner icon) while the screen is covered.")]
        [SerializeField] private bool showLoadingView = true;

        [Tooltip("Floor on how long the covered phase lasts, in seconds. Stops a fast load from " +
                 "producing a single-frame flash of the loading view. 0 disables the floor.")]
        [Min(0f)] [SerializeField] private float minimumCoveredSeconds = 0.25f;

        [Tooltip("Release memory held by the scene that was just unloaded. Runs while the screen is " +
                 "still covered, so the hitch is never visible.")]
        [SerializeField] private bool unloadUnusedAssetsAfterUnload = true;

        private readonly Queue<Request> _queue = new();
        private Coroutine _pump;
        private bool _registered;

        private struct Request
        {
            public Kind kind;
            public string sceneName;
            public Action onComplete;
        }

        public string CurrentContentScene { get; private set; }

        /// <summary>True while a load/unload/swap is in flight or queued.</summary>
        public bool IsBusy => _pump != null;

        private void Awake()
        {
            if (transition == null) transition = FindFirstObjectByType<ScreenTransitionController>(FindObjectsInactive.Include);

            // Boot starts covered so the first content scene fades up out of black rather than popping
            // in over an empty grey viewport.
            if (transition != null) transition.SnapTo(true);

            Register();
        }

        private void Start()
        {
            // Registration is retried here because AppContext is populated by GameBootstrap.Awake, and a
            // scene where execution order is disturbed (or where this component is enabled late) would
            // otherwise never register at all.
            Register();

            if (!string.IsNullOrEmpty(bootContentScene) && string.IsNullOrEmpty(CurrentContentScene))
                LoadContent(bootContentScene);
        }

        private void Register()
        {
            if (_registered || AppContext.Services == null) return;

            // Register throws on a duplicate; another instance winning the race is a scene-authoring
            // mistake worth naming rather than silently tolerating.
            if (AppContext.Services.TryResolve<ISceneFlowService>(out var existing) && existing != null)
            {
                if (!ReferenceEquals(existing, this))
                {
                    Debug.LogError($"[JRPG.SceneFlow] A second {nameof(SceneFlowService)} is present; only the " +
                                   "first is registered. Remove the duplicate from the Startup scene.", this);
                }

                _registered = ReferenceEquals(existing, this);
                return;
            }

            AppContext.Services.Register<ISceneFlowService>(this);
            _registered = true;
        }

        // ---- ISceneFlowService ------------------------------------------------------------------

        public void LoadContent(string sceneName, Action onComplete = null)
            => Enqueue(Kind.Load, sceneName, onComplete);

        public void UnloadContent(Action onComplete = null)
            => Enqueue(Kind.Unload, null, onComplete);

        public void SwapTo(string sceneName, Action onComplete = null)
            => Enqueue(Kind.Swap, sceneName, onComplete);

        // ---- Queue ------------------------------------------------------------------------------

        private void Enqueue(Kind kind, string sceneName, Action onComplete)
        {
            if (kind != Kind.Unload && string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[JRPG.SceneFlow] {kind} called with no scene name; ignored.", this);
                onComplete?.Invoke();
                return;
            }

            if (!isActiveAndEnabled)
            {
                // No coroutine can run, so the queue would never drain. Honour the "exactly once"
                // contract instead of stranding the caller.
                Debug.LogError($"[JRPG.SceneFlow] {kind} '{sceneName}' requested while the service is " +
                               "disabled; the scene was not changed.", this);
                onComplete?.Invoke();
                return;
            }

            _queue.Enqueue(new Request { kind = kind, sceneName = sceneName, onComplete = onComplete });
            _pump ??= StartCoroutine(Pump());
        }

        private IEnumerator Pump()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();
                yield return Run(request);
            }

            _pump = null;
        }

        private IEnumerator Run(Request request)
        {
            yield return Cover();

            float coveredAt = Time.realtimeSinceStartup;

            if (request.kind is Kind.Unload or Kind.Swap)
                yield return UnloadRoutine();

            if (request.kind is Kind.Load or Kind.Swap)
                yield return LoadRoutine(request.sceneName);

            // Hold the cover for the remainder of the floor, so a scene that loads in two frames still
            // reads as a transition rather than a flicker.
            float remaining = minimumCoveredSeconds - (Time.realtimeSinceStartup - coveredAt);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            // Callers restore save payloads here.
            SafeInvoke(request.onComplete);

            yield return Uncover();
        }

        // ---- Scene work -------------------------------------------------------------------------

        private IEnumerator LoadRoutine(string sceneName)
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                // Already resident (a repeated LoadContent, or the editor had it open). Adopt it rather
                // than loading a second copy.
                CurrentContentScene = sceneName;
                Activate(sceneName);
                yield break;
            }

            AsyncOperation op;
            try
            {
                op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JRPG.SceneFlow] Loading '{sceneName}' threw: {e.Message}", this);
                yield break;
            }

            if (op == null)
            {
                Debug.LogError($"[JRPG.SceneFlow] Scene '{sceneName}' could not be loaded. It is almost " +
                               "certainly missing from (or disabled in) Build Settings.", this);
                yield break;
            }

            SetStatus($"Loading {sceneName}…");
            while (!op.isDone) yield return null;

            CurrentContentScene = sceneName;
            Activate(sceneName);
        }

        private IEnumerator UnloadRoutine()
        {
            if (string.IsNullOrEmpty(CurrentContentScene)) yield break;

            var scene = SceneManager.GetSceneByName(CurrentContentScene);
            CurrentContentScene = null;

            if (!scene.IsValid() || !scene.isLoaded) yield break;

            SetStatus(null);

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op != null) while (!op.isDone) yield return null;

            // Startup owns the UI and the services, so it is the correct active scene whenever no
            // content scene exists — otherwise the next Instantiate would land in a dead scene.
            var startup = gameObject.scene;
            if (startup.IsValid() && startup.isLoaded) SceneManager.SetActiveScene(startup);

            if (unloadUnusedAssetsAfterUnload) yield return Resources.UnloadUnusedAssets();
        }

        /// Makes the content scene active so newly instantiated objects and the scene's lighting
        /// settings belong to it rather than to Startup.
        private void Activate(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
        }

        // ---- Presentation -----------------------------------------------------------------------

        private IEnumerator Cover()
        {
            if (transition == null) yield break;

            bool done = false;
            transition.FadeOut(() => done = true);
            while (!done) yield return null;

            if (showLoadingView) transition.SetLoadingVisible(true);
        }

        private IEnumerator Uncover()
        {
            if (transition == null) yield break;

            if (showLoadingView) transition.SetLoadingVisible(false);

            bool done = false;
            transition.FadeIn(() => done = true);
            while (!done) yield return null;
        }

        private void SetStatus(string status)
        {
            if (transition != null && showLoadingView) transition.SetLoadingStatus(status);
        }

        /// A throwing callback must not kill the pump — the queue would stall and every later request
        /// would hang with the screen black.
        private void SafeInvoke(Action callback)
        {
            if (callback == null) return;

            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogError($"[JRPG.SceneFlow] A scene-flow callback threw: {e}", this);
            }
        }
    }
}
