using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;   // Observable.Call, the subscribe helper for onAnyButtonPress
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The splash content scene's only behaviour: hold a logo/product name for a moment, then hand the
    /// screen to the title scene through <see cref="ISceneFlowService"/>.
    ///
    /// <para>Skipping listens on <see cref="InputSystem.onAnyButtonPress"/> rather than the Menu action
    /// map, because "press any button" genuinely means any button on any device, and no action map is
    /// active during boot anyway.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplashScreenController : MonoBehaviour
    {
        [Header("Flow")]
        [Tooltip("Content scene loaded when the splash finishes. Must be in Build Settings.")]
        [SerializeField] private string nextScene = "Title";

        [Tooltip("Seconds the splash holds before advancing on its own.")]
        [Min(0f)] [SerializeField] private float holdSeconds = 3f;

        [Header("Skipping")]
        [SerializeField] private bool allowSkip = true;

        [Tooltip("Grace period before a button press can skip. Stops the press that dismissed something " +
                 "on the previous screen from eating the splash in the same breath.")]
        [Min(0f)] [SerializeField] private float skipLockoutSeconds = 0.5f;

        private float _elapsed;
        private bool _advanced;
        private System.IDisposable _anyButtonListener;

        private void OnEnable()
        {
            _elapsed = 0f;
            _advanced = false;

            if (allowSkip) _anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        }

        private void OnDisable()
        {
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;
        }

        private void Update()
        {
            if (_advanced) return;

            // Unscaled: nothing has set a time scale yet at boot, but a splash that stops advancing
            // because something paused the game would be an unrecoverable hang.
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= holdSeconds) Advance("timer");
        }

        private void OnAnyButton(InputControl control)
        {
            if (_advanced || _elapsed < skipLockoutSeconds) return;

            Advance("skip");
        }

        private void Advance(string cause)
        {
            if (_advanced) return;
            _advanced = true;

            // Stop listening immediately: the scene lives on for the length of the fade, and a second
            // press during it must not queue a second transition.
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;

            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogError("[JRPG.Menu] SplashScreenController has no next scene configured; boot " +
                               "stops here.", this);
                return;
            }

            if (AppContext.Services != null
                && AppContext.Services.TryResolve<ISceneFlowService>(out var sceneFlow)
                && sceneFlow != null)
            {
                sceneFlow.SwapTo(nextScene);
                return;
            }

            Debug.LogError($"[JRPG.Menu] Splash cannot advance to '{nextScene}' ({cause}): no " +
                           "ISceneFlowService is registered. Is the Startup scene loaded, with a " +
                           "SceneFlowService on it?", this);
        }
    }
}
