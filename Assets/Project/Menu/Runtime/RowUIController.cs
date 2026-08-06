using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// Visual controller for a single menu row.
    ///
    /// <para><b>Non-colour-only states.</b> Colour alone is not an accessible state cue, so every state
    /// also carries a shape/text/motion cue: a cursor caret plus a focus outline for
    /// <see cref="RowState.Selected"/>, a dimmed label plus a text affix (and optional glyph) for
    /// <see cref="RowState.Disabled"/>, a shake for <see cref="RowState.Invalid"/>, and a flash for
    /// <see cref="RowState.Confirmed"/>.</para>
    ///
    /// <para><b>Everything here is null-safe.</b> Each element is optional. A prefab that only wires
    /// <c>background</c> behaves exactly as the colour-only rows did before. Likewise both profiles may
    /// be null: with no <see cref="MenuNavigationProfile"/> the cursor/outline stay hidden, and with no
    /// <see cref="MenuAnimationProfile"/> the shake/flash collapse to their instant end state.</para>
    ///
    /// <para><b>Pooling.</b> Instances are recycled by <see cref="RowPool"/>, so <see cref="Bind"/> is
    /// the reset point: it stops any running feedback coroutine, restores the shake offset and the
    /// authored label colours, and clears the cursor/affix/state-icon before applying the new model.
    /// Nothing may leak from the previous row.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class RowUIController : MonoBehaviour, IPointerEnterHandler
    {
        [Header("Core elements")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text quantity;
        [SerializeField] private TMP_Text cost;
        [SerializeField] private Image icon;
        [SerializeField] private Image background;
        [SerializeField] private Selectable selectable;

        [Header("State elements (all optional)")]
        [Tooltip("Caret shown only while this row is selected. Toggled active/inactive as a whole.")]
        [SerializeField] private RectTransform cursorRoot;
        [Tooltip("Sprite half of the cursor, used when the navigation profile supplies a cursorSprite.")]
        [SerializeField] private Image cursorIcon;
        [Tooltip("Text half of the cursor, used when no cursorSprite exists (cursorFallbackGlyph).")]
        [SerializeField] private TMP_Text cursorGlyph;
        [Tooltip("Focus outline driven by the navigation profile's focusOutlineWidth/Color.")]
        [SerializeField] private Outline focusOutline;
        [Tooltip("Dedicated text element for state affixes. The row model's label is never mutated.")]
        [SerializeField] private TMP_Text stateAffix;
        [Tooltip("Optional glyph slot for the disabled (or other) state icon.")]
        [SerializeField] private Image stateIcon;
        [Tooltip("What the invalid shake displaces. Defaults to this row's own RectTransform.")]
        [SerializeField] private RectTransform shakeTarget;

        [Header("Variant slots (optional)")]
        [Tooltip("Second line / secondary value, used by the stat and slot-summary variants.")]
        [SerializeField] private TMP_Text auxLabel;
        [Tooltip("Filled Image used as a slider fill or a toggle indicator swatch.")]
        [SerializeField] private Image fillBar;

        [Header("State colors")]
        [SerializeField] private Color normalColor = new(0.20f, 0.20f, 0.25f, 0.92f);
        [SerializeField] private Color selectedColor = new(0.95f, 0.85f, 0.30f, 1.0f);
        [SerializeField] private Color disabledColor = new(0.30f, 0.30f, 0.30f, 0.6f);
        [SerializeField] private Color invalidColor = new(0.60f, 0.20f, 0.20f, 0.9f);
        [SerializeField] private Color confirmedColor = new(0.30f, 0.85f, 0.30f, 1.0f);

        [Header("Decorative (group heading) treatment")]
        [Tooltip("Background for a RowModel.decorative heading. Transparent by default so a heading reads " +
                 "as a caption between rows rather than as another row.")]
        [SerializeField] private Color separatorColor = new(0f, 0f, 0f, 0f);
        [SerializeField] private Color separatorLabelColor = new(0.72f, 0.72f, 0.78f, 0.9f);

        [Header("Disabled treatment")]
        [Tooltip("Appended in the stateAffix element (never into the model label) while disabled.")]
        [SerializeField] private string disabledAffix = "  —";
        [Tooltip("Optional art for the disabled state; shown in stateIcon when assigned.")]
        [SerializeField] private Sprite disabledIcon;
        [SerializeField] private Color disabledLabelColor = new(0.62f, 0.62f, 0.62f, 0.75f);

        [Header("Confirm flash")]
        [SerializeField] private Color confirmFlashColor = new(1f, 1f, 1f, 1f);

        [Header("Cursor/outline fallbacks (used only while navigationProfile is null)")]
        [SerializeField] private string fallbackCursorGlyph = ">";
        [SerializeField] private Vector2 fallbackCursorOffset = new(-18f, 0f);
        [SerializeField] private float fallbackFocusOutlineWidth = 2f;
        [SerializeField] private Color fallbackFocusOutlineColor = new(1f, 1f, 1f, 0.9f);

        [Header("Profiles (optional — null falls back to colour-only)")]
        [SerializeField] private MenuNavigationProfile navigationProfile;
        [SerializeField] private MenuAnimationProfile animationProfile;

        private Coroutine _feedback;
        private Vector2 _shakeOrigin;
        private bool _shakeApplied;
        private Color _labelBaseColor = Color.white;
        private Color _auxBaseColor = Color.white;
        private bool _cachedBaseColors;
        private string _disabledReason;

        public RowModel Model { get; private set; }
        public Selectable Selectable => selectable;
        public RowState State { get; private set; } = RowState.Normal;

        /// <summary>Secondary text element, for widgets that drive a two-line or before/after row.</summary>
        public TMP_Text AuxLabel => auxLabel;

        /// <summary>Fill/indicator image, for slider and toggle widgets.</summary>
        public Image FillBar => fillBar;

        /// <summary>
        /// Raised on pointer-enter when the navigation profile has hoverSelects enabled. The row never
        /// decides focus itself — the owning screen subscribes and moves selection.
        /// </summary>
        public event Action<RowUIController> HoverEntered;

        /// <summary>
        /// Lets a screen inject the shared profiles at runtime, so row prefabs do not each have to
        /// serialize a reference to them.
        /// </summary>
        public void SetProfiles(MenuNavigationProfile navigation, MenuAnimationProfile animation)
        {
            navigationProfile = navigation;
            animationProfile = animation;
            ApplyStaticState(State);
        }

        private void Awake()
        {
            CacheBaseColors();
            if (shakeTarget == null) shakeTarget = transform as RectTransform;
        }

        private void CacheBaseColors()
        {
            if (_cachedBaseColors) return;
            if (label != null) _labelBaseColor = label.color;
            if (auxLabel != null) _auxBaseColor = auxLabel.color;

            _cachedBaseColors = true;
        }

        public void Bind(RowModel model)
        {
            // Pooled instance: wipe everything the previous model may have left behind first.
            CacheBaseColors();
            ResetVolatileState();

            Model = model;
            if (label != null) label.text = model.label ?? "";
            if (quantity != null) quantity.text = model.quantityText ?? "";
            if (cost != null) cost.text = model.costText ?? "";

            if (auxLabel != null)
            {
                bool hasAux = !string.IsNullOrEmpty(model.auxText);
                auxLabel.text = hasAux ? model.auxText : "";

                // The GameObject, not the component: the row prefabs ship this slot inactive, so merely
                // enabling the text would leave it invisible. Deactivating also drops it out of the
                // layout entirely, which is what an unused slot should do.
                if (auxLabel.gameObject.activeSelf != hasAux) auxLabel.gameObject.SetActive(hasAux);
            }
            if (icon != null)
            {
                icon.sprite = model.icon;
                icon.enabled = model.icon != null;
            }
            if (selectable != null) selectable.interactable = model.enabled;

            _disabledReason = null;
            if (!model.enabled)
            {
                _disabledReason = !string.IsNullOrEmpty(model.disabledReason)
                    ? model.disabledReason
                    : model.action?.GetDisabledReason(model.context ?? default);
            }

            SetVisualState(model.enabled ? RowState.Normal : RowState.Disabled);
        }

        public void SetVisualState(RowState state)
        {
            State = state;
            StopFeedback();
            RestoreShake();
            ApplyStaticState(state);

            switch (state)
            {
                case RowState.Invalid:
                    StartFeedback(ShakeRoutine());
                    break;
                case RowState.Confirmed:
                    StartFeedback(FlashRoutine());
                    break;
            }
        }

        /// <summary>Everything a state changes that does not animate. Safe to re-run at any time.</summary>
        private void ApplyStaticState(RowState state)
        {
            // A heading is not an unavailable choice, so it takes none of the disabled treatment
            bool decorative = Model.decorative;

            if (background != null) background.color = decorative ? separatorColor : ColorFor(state);

            bool selected = state == RowState.Selected;
            bool disabled = state == RowState.Disabled && !decorative;

            ApplyCursor(selected);
            ApplyOutline(selected);

            if (label != null)
                label.color = decorative ? separatorLabelColor : disabled ? disabledLabelColor : _labelBaseColor;
            if (auxLabel != null)
                auxLabel.color = decorative ? separatorLabelColor : disabled ? disabledLabelColor : _auxBaseColor;

            if (stateAffix != null)
            {
                string affix = disabled
                    ? (!string.IsNullOrEmpty(_disabledReason) ? _disabledReason : disabledAffix)
                    : null;

                bool show = !string.IsNullOrEmpty(affix);
                stateAffix.text = show ? affix : "";
                stateAffix.enabled = show;
            }

            if (stateIcon != null)
            {
                bool show = disabled && disabledIcon != null;
                stateIcon.sprite = show ? disabledIcon : null;
                stateIcon.enabled = show;
            }
        }

        private Color ColorFor(RowState state) => state switch
        {
            RowState.Selected => selectedColor,
            RowState.Disabled => disabledColor,
            RowState.Invalid => invalidColor,
            RowState.Confirmed => confirmedColor,
            _ => normalColor
        };

        // A row with no cursor element wired is colour-only exactly as before — that is the guarantee
        // for the pre-existing MenuRow.prefab. Rows that DO have the element use the profile when one
        // is assigned and the serialized fallbacks until then, so the caret never silently disappears.
        private void ApplyCursor(bool selected)
        {
            if (cursorRoot == null) return;

            if (cursorRoot.gameObject.activeSelf != selected) cursorRoot.gameObject.SetActive(selected);
            if (!selected) return;

            cursorRoot.anchoredPosition = navigationProfile != null
                ? navigationProfile.cursorOffset
                : fallbackCursorOffset;

            var sprite = navigationProfile != null ? navigationProfile.cursorSprite : null;
            if (cursorIcon != null)
            {
                cursorIcon.sprite = sprite;
                cursorIcon.enabled = sprite != null;
            }
            if (cursorGlyph != null)
            {
                bool useGlyph = sprite == null || cursorIcon == null;
                string glyph = navigationProfile != null
                    ? navigationProfile.cursorFallbackGlyph
                    : fallbackCursorGlyph;
                cursorGlyph.text = useGlyph ? (glyph ?? "") : "";
                cursorGlyph.enabled = useGlyph;
            }
        }

        private void ApplyOutline(bool selected)
        {
            if (focusOutline == null) return;

            float width = navigationProfile != null
                ? navigationProfile.focusOutlineWidth
                : fallbackFocusOutlineWidth;

            if (!selected || width <= 0f)
            {
                focusOutline.enabled = false;
                return;
            }

            focusOutline.effectColor = navigationProfile != null
                ? navigationProfile.focusOutlineColor
                : fallbackFocusOutlineColor;
            focusOutline.effectDistance = new Vector2(width, width);
            focusOutline.useGraphicAlpha = false;
            focusOutline.enabled = true;
        }

        // ---- feedback coroutines -------------------------------------------------------------

        private void StartFeedback(IEnumerator routine)
        {
            // Pooled rows are disabled between uses; never start a coroutine we cannot stop cleanly.
            if (!isActiveAndEnabled)
            {
                FinishInstantly();
                return;
            }
            _feedback = StartCoroutine(routine);
        }

        private void StopFeedback()
        {
            if (_feedback != null)
            {
                StopCoroutine(_feedback);
                _feedback = null;
            }
        }

        /// <summary>Land on the state's end value without animating (reduced motion / inactive row).</summary>
        private void FinishInstantly()
        {
            RestoreShake();
            if (background != null) background.color = ColorFor(State);
        }

        private IEnumerator ShakeRoutine()
        {
            float duration = animationProfile != null
                ? animationProfile.Duration(animationProfile.invalidShakeDuration)
                : 0f;
            float amplitude = animationProfile != null
                ? animationProfile.Amplitude(animationProfile.invalidShakeAmplitude)
                : 0f;

            if (duration <= 0f || amplitude <= 0f || shakeTarget == null)
            {
                _feedback = null;
                yield break;
            }

            _shakeOrigin = shakeTarget.anchoredPosition;
            _shakeApplied = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t);
                shakeTarget.anchoredPosition = _shakeOrigin + new Vector2(offset, 0f);
                yield return null;
            }

            RestoreShake();
            _feedback = null;
        }

        private IEnumerator FlashRoutine()
        {
            float duration = animationProfile != null
                ? animationProfile.Duration(animationProfile.confirmFlashDuration)
                : 0f;

            if (duration <= 0f || background == null)
            {
                if (background != null) background.color = confirmedColor;
                _feedback = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                background.color = Color.Lerp(confirmFlashColor, confirmedColor, t);
                yield return null;
            }

            background.color = confirmedColor;
            _feedback = null;
        }

        private void RestoreShake()
        {
            if (!_shakeApplied) return;
            if (shakeTarget != null) shakeTarget.anchoredPosition = _shakeOrigin;

            _shakeApplied = false;
        }

        /// <summary>The single pooling reset: no coroutine, no offset, no leftover state decoration.</summary>
        private void ResetVolatileState()
        {
            StopFeedback();
            RestoreShake();

            if (label != null) label.color = _labelBaseColor;
            if (auxLabel != null) auxLabel.color = _auxBaseColor;
            if (stateAffix != null) { stateAffix.text = ""; stateAffix.enabled = false; }
            if (stateIcon != null) { stateIcon.sprite = null; stateIcon.enabled = false; }
            if (auxLabel != null && auxLabel.gameObject.activeSelf) auxLabel.gameObject.SetActive(false);
            _disabledReason = null;
            if (focusOutline != null) focusOutline.enabled = false;
            if (cursorRoot != null && cursorRoot.gameObject.activeSelf) cursorRoot.gameObject.SetActive(false);

            State = RowState.Normal;
        }

        private void OnDisable()
        {
            // Unity kills coroutines on deactivate. Make sure the row does not go back to the pool
            // holding a half-finished shake offset.
            StopFeedback();
            RestoreShake();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (navigationProfile == null || !navigationProfile.hoverSelects) return;
            HoverEntered?.Invoke(this);
        }
    }
}
