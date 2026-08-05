using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// The reusable yes/no modal: save-overwrite, exit-to-title, equip confirm, discard item.
    ///
    /// <para><b>Focus defaults to Cancel.</b> Every prompt this widget exists for is destructive, and a
    /// player mashing Confirm through a menu should not lose a save file to muscle memory. Callers that
    /// are asking about something harmless pass <c>defaultToCancel: false</c>.</para>
    ///
    /// <para>It must draw over whatever asked it, so it takes the same route as
    /// <c>DefeatMenuController</c>: <see cref="Canvas.overrideSorting"/> plus a serialized order (the
    /// project precedent is 200). <c>overrideSorting</c> cannot be authored on a prefab root, so it is
    /// applied in code once the prompt is live and nested under a parent canvas.</para>
    ///
    /// <para>It owns no domain state — it invokes the callbacks it was given and closes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConfirmPromptController : MonoBehaviour
    {
        /// <summary>Serialized references for one of the two options.</summary>
        [Serializable]
        private class OptionRefs
        {
            public GameObject root;
            public TMP_Text label;
            public Image background;
            public Outline focusOutline;
            public Image cursorIcon;
            public TMP_Text cursorGlyph;
        }

        [Header("Sorting")]
        [Tooltip("Canvas whose sorting is overridden so the prompt draws above the menu that raised it.")]
        [SerializeField] private Canvas canvas;

        [Tooltip("Project precedent is 200 — same value DefeatMenuController uses for the same reason.")]
        [SerializeField] private int sortingOrder = 200;

        [Header("Wiring")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private Image backdrop;
        [SerializeField] private TMP_Text questionLabel;

        [SerializeField] private OptionRefs confirmOption = new();
        [SerializeField] private OptionRefs cancelOption = new();

        [Header("Input")]
        [Tooltip("When set, the prompt reads Submit/Cancel/Navigate itself. Leave null to drive it " +
                 "entirely from the screen via Confirm()/Cancel()/MoveFocus().")]
        [SerializeField] private InputActionAsset playerControls;

        [SerializeField] private string actionMapName = "Menu";

        [Header("Profiles")]
        [SerializeField] private MenuNavigationProfile navigationProfile;
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Header("Appearance")]
        [Range(0f, 1f)][SerializeField] private float backdropAlpha = 0.7f;
        [SerializeField] private Color focusedBackground = new(0.24f, 0.28f, 0.40f, 1f);
        [SerializeField] private Color unfocusedBackground = new(0.12f, 0.13f, 0.18f, 1f);

        private Action _onConfirm;
        private Action _onCancel;

        private InputActionMap _map;
        private InputAction _navigate, _submit, _cancel;
        private bool _hooked;
        private bool _enabledMap;
        private bool _warned;

        /// <summary>Raised after the prompt closes, with true when the player confirmed.</summary>
        public event Action<bool> Answered;

        /// <summary>True between <see cref="Ask"/> and the player answering.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>True when focus is on the confirm option.</summary>
        public bool FocusOnConfirm { get; private set; }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (group == null) group = GetComponent<CanvasGroup>();

            if (backdrop != null)
            {
                var c = backdrop.color;
                c.a = Mathf.Clamp01(backdropAlpha);
                backdrop.color = c;
            }

            ApplyClosedState();
        }

        private void OnDisable() => HookInput(false);

        // ---- public API --------------------------------------------------------------------------

        /// <summary>
        /// Open the prompt. <paramref name="defaultToCancel"/> defaults to true — see the type summary
        /// for why. <paramref name="onCancel"/> may be null; the prompt still closes cleanly.
        /// </summary>
        public void Ask(string question, string confirmLabel, string cancelLabel,
                        Action onConfirm, Action onCancel = null, bool defaultToCancel = true)
        {
            EnsureWiredWarning();

            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (questionLabel != null) questionLabel.text = question ?? string.Empty;
            if (confirmOption?.label != null) confirmOption.label.text = confirmLabel ?? "Yes";
            if (cancelOption?.label != null) cancelOption.label.text = cancelLabel ?? "No";

            IsOpen = true;

            ApplySorting();

            if (cardRoot != null && !cardRoot.activeSelf) cardRoot.SetActive(true);
            if (backdrop != null) backdrop.enabled = true;

            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            SetFocus(!defaultToCancel);
            HookInput(true);
        }

        /// <summary>Answer yes. Public so a screen (or a mouse click) can drive the prompt directly.</summary>
        public void Confirm()
        {
            if (!IsOpen) return;

            var callback = _onConfirm;
            Close();
            callback?.Invoke();
            Answered?.Invoke(true);
        }

        /// <summary>Answer no. Also the Cancel-button / backdrop-click path.</summary>
        public void Cancel()
        {
            if (!IsOpen) return;

            var callback = _onCancel;
            Close();
            callback?.Invoke();
            Answered?.Invoke(false);
        }

        /// <summary>Move focus between the two options. Sign only; wraps trivially across two entries.</summary>
        public void MoveFocus(int dir)
        {
            if (!IsOpen || dir == 0) return;
            SetFocus(!FocusOnConfirm);
        }

        /// <summary>Place focus explicitly.</summary>
        public void SetFocus(bool onConfirm)
        {
            FocusOnConfirm = onConfirm;
            ApplyOption(confirmOption, onConfirm);
            ApplyOption(cancelOption, !onConfirm);
        }

        /// <summary>Dismiss without invoking either callback — for a screen tearing down beneath it.</summary>
        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _onConfirm = null;
            _onCancel = null;

            HookInput(false);
            ApplyClosedState();
        }

        /// <summary>Runtime profile overrides so a screen can share its profiles.</summary>
        public void SetProfiles(MenuNavigationProfile navigation, MenuAnimationProfile animation)
        {
            navigationProfile = navigation;
            animationProfile = animation;
            if (IsOpen) SetFocus(FocusOnConfirm);
        }

        // ---- visuals -----------------------------------------------------------------------------

        private void ApplySorting()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            // Nested canvases inherit the parent's sorting unless this is set, and it cannot be
            // authored on a prefab root — same constraint DefeatMenuController documents.
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        private void ApplyClosedState()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            if (cardRoot != null) cardRoot.SetActive(false);
            if (backdrop != null) backdrop.enabled = false;
        }

        private void ApplyOption(OptionRefs option, bool focused)
        {
            if (option == null) return;

            if (option.background != null)
                option.background.color = focused ? focusedBackground : unfocusedBackground;

            // Non-colour-only focus: a cursor glyph plus an outline, both driven by the shared profile.
            bool hasCursorSprite = navigationProfile != null && navigationProfile.cursorSprite != null;

            if (option.cursorIcon != null)
            {
                option.cursorIcon.sprite = hasCursorSprite ? navigationProfile.cursorSprite : null;
                option.cursorIcon.enabled = focused && hasCursorSprite;
                option.cursorIcon.preserveAspect = true;
            }

            if (option.cursorGlyph != null)
            {
                string glyph = navigationProfile != null && !string.IsNullOrEmpty(navigationProfile.cursorFallbackGlyph)
                    ? navigationProfile.cursorFallbackGlyph
                    : ">";
                option.cursorGlyph.text = glyph;
                option.cursorGlyph.enabled = focused && !hasCursorSprite;
            }

            if (option.focusOutline != null)
            {
                float width = navigationProfile != null ? navigationProfile.focusOutlineWidth : 2f;
                option.focusOutline.effectColor = navigationProfile != null
                    ? navigationProfile.focusOutlineColor
                    : new Color(1f, 1f, 1f, 0.9f);
                option.focusOutline.effectDistance = new Vector2(width, width);
                option.focusOutline.enabled = focused && width > 0f;
            }

            if (option.label != null)
                option.label.fontStyle = focused ? FontStyles.Bold : FontStyles.Normal;
        }

        // ---- input -------------------------------------------------------------------------------

        private void HookInput(bool subscribe)
        {
            if (playerControls == null) return;

            if (_map == null)
            {
                _map = playerControls.FindActionMap(actionMapName, false);
                if (_map == null)
                {
                    WarnOnce($"ConfirmPromptController could not find action map '{actionMapName}'; " +
                             "the prompt must be driven from the screen.");
                    return;
                }

                _navigate = _map.FindAction("Navigate", false);
                _submit = _map.FindAction("Submit", false);
                _cancel = _map.FindAction("Cancel", false);
            }

            if (subscribe)
            {
                if (_hooked) return;

                if (_navigate != null) _navigate.performed += OnNavigatePerformed;
                if (_submit != null) _submit.performed += OnSubmitPerformed;
                if (_cancel != null) _cancel.performed += OnCancelPerformed;
                _hooked = true;

                // Only claim the map if nobody else had it enabled; release it exactly as we found it.
                if (!_map.enabled) { _map.Enable(); _enabledMap = true; }
            }
            else
            {
                if (!_hooked) return;

                if (_navigate != null) _navigate.performed -= OnNavigatePerformed;
                if (_submit != null) _submit.performed -= OnSubmitPerformed;
                if (_cancel != null) _cancel.performed -= OnCancelPerformed;
                _hooked = false;

                if (_enabledMap && _map.enabled) _map.Disable();
                _enabledMap = false;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            var v = ctx.ReadValue<Vector2>();
            if (Mathf.Abs(v.x) <= 0.5f) return;
            MoveFocus(v.x > 0f ? +1 : -1);
        }

        private void OnSubmitPerformed(InputAction.CallbackContext ctx)
        {
            if (FocusOnConfirm) Confirm();
            else Cancel();
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx) => Cancel();

        // ---- helpers -----------------------------------------------------------------------------

        private void EnsureWiredWarning()
        {
            if (_warned) return;
            if (questionLabel != null && confirmOption?.label != null && cancelOption?.label != null) return;

            _warned = true;
            Debug.LogWarning("[JRPG.Menu] ConfirmPromptController is missing questionLabel or an option label — " +
                             "the prompt will render incompletely.", this);
        }

        private void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[JRPG.Menu] {message}", this);
        }
    }
}
