using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;   // Observable.Call
using TMPro;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Owns the title scene's two states over one shared 3D background
    ///
    /// <list type="bullet">
    /// <item><b>Attract</b> — logo / product name plus a pulsing "Press Any Button".</item>
    /// <item><b>Menu</b> — the same background with the <c>main</c> row stack overlaid.</item>
    /// </list>
    ///
    /// <para><b>Why the attract state is a scene object and not a menu.</b> It has no rows and needs no
    /// menu frame, and pushing one would put the title permanently at the bottom of the menu stack —
    /// where <c>CloseAll()</c> (which <see cref="ISessionService.NewGame"/> and <c>ReturnToTitle</c>
    /// both call) would destroy it. Keeping it in the scene means the stack is genuinely empty
    /// whenever the player is on the title screen, which is what the rest of the menu system assumes.</para>
    ///
    /// <para>Only the menu state is a real screen, so only it is in the registry. Backing out of it
    /// empties the stack, and the <see cref="MenuClosed"/> event brings the attract state back.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Header("Attract state")]
        [Tooltip("Everything shown before the player presses a button. Hidden while the menu is up.")]
        [SerializeField] private CanvasGroup attractGroup;

        [Tooltip("Optional logo. Left null, the Image is hidden and the product-name label stands.")]
        [SerializeField] private UnityEngine.UI.Image logoImage;

        [Tooltip("Fallback/companion title text. Defaults to Application.productName when left empty.")]
        [SerializeField] private TMP_Text productNameLabel;

        [SerializeField] private TMP_Text pressAnyButtonLabel;

        [Header("Menu state")]
        [Tooltip("Registry id of the row-stack screen opened when the player advances.")]
        [SerializeField] private string mainMenuId = "main";

        [Header("Behaviour")]
        [Tooltip("Grace period after this screen appears before a press is accepted, so the button that " +
                 "skipped the splash does not fall straight through into the menu.")]
        [Min(0f)] [SerializeField] private float inputLockoutSeconds = 0.4f;

        [Tooltip("Seconds for one full fade cycle of the \"Press Any Button\" label. 0 holds it steady.")]
        [Min(0f)] [SerializeField] private float promptPulseSeconds = 1.6f;

        [Tooltip("Optional. Reduced motion holds the prompt at full opacity instead of pulsing.")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        private IEventBus _bus;
        private IMenuService _menus;
        private System.IDisposable _anyButtonListener;
        private float _elapsed;
        private bool _menuOpen;

        private float PulseSeconds => animationProfile != null
            ? animationProfile.Duration(promptPulseSeconds)
            : Mathf.Max(0f, promptPulseSeconds);

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            if (AppContext.Services != null) AppContext.Services.TryResolve(out _menus);

            if (_bus != null) _bus.Subscribe<MenuClosed>(OnMenuClosed);

            ApplyLabels();
            ShowAttract();
        }

        private void OnDisable()
        {
            if (_bus != null) _bus.Unsubscribe<MenuClosed>(OnMenuClosed);

            StopListening();
        }

        // ---- State swap ---------------------------------------------------------------------------

        private void ShowAttract()
        {
            _menuOpen = false;
            _elapsed = 0f;

            SetAttractVisible(true);
            StartListening();
        }

        private void ShowMenu()
        {
            if (_menuOpen) return;

            if (_menus == null || !_menus.HasMenu(mainMenuId))
            {
                Debug.LogError($"[JRPG.Menu] Title cannot open '{mainMenuId}': the menu service is " +
                               "missing or has no entry for it. Run JRPG/Setup/Build Title Screen.", this);
                return;
            }

            _menuOpen = true;

            // Stop listening before opening: onAnyButtonPress is device-level and would otherwise keep
            // firing underneath the menu, and the same press must not also reach the menu's Submit.
            StopListening();
            SetAttractVisible(false);

            _menus.Open(mainMenuId, null);
        }

        private void OnMenuClosed(MenuClosed e)
        {
            // Backing out of the row stack returns to attract. Any other menu closing is not ours.
            if (!_menuOpen || e.MenuId != mainMenuId) return;

            ShowAttract();
        }

        private void SetAttractVisible(bool visible)
        {
            if (attractGroup == null) return;

            attractGroup.alpha = visible ? 1f : 0f;
            attractGroup.blocksRaycasts = visible;
            attractGroup.interactable = visible;
        }

        // ---- Input --------------------------------------------------------------------------------

        private void StartListening()
        {
            _anyButtonListener ??= InputSystem.onAnyButtonPress.Call(OnAnyButton);
        }

        private void StopListening()
        {
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;
        }

        private void OnAnyButton(InputControl control)
        {
            if (_menuOpen || _elapsed < inputLockoutSeconds) return;

            ShowMenu();
        }

        // ---- Presentation -------------------------------------------------------------------------

        private void Update()
        {
            if (_menuOpen) return;

            _elapsed += Time.unscaledDeltaTime;

            if (pressAnyButtonLabel == null) return;

            float pulse = PulseSeconds;
            if (pulse <= 0f)
            {
                SetPromptAlpha(1f);
                return;
            }

            // Ease between half and full opacity — never fully invisible, so the affordance is always
            // legible.
            float t = Mathf.Sin(_elapsed / pulse * Mathf.PI * 2f) * 0.5f + 0.5f;
            SetPromptAlpha(Mathf.Lerp(0.45f, 1f, t));
        }

        private void SetPromptAlpha(float alpha)
        {
            var c = pressAnyButtonLabel.color;
            c.a = alpha;
            pressAnyButtonLabel.color = c;
        }

        private void ApplyLabels()
        {
            if (logoImage != null) logoImage.enabled = logoImage.sprite != null;

            if (productNameLabel != null && string.IsNullOrEmpty(productNameLabel.text))
                productNameLabel.text = Application.productName;
        }
    }
}
