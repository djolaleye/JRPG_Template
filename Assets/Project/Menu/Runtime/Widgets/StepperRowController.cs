using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// A <c>‹ value ›</c> row — attribute allocation, quantity pickers, difficulty cyclers.
    ///
    /// <para>The arrows disable <b>visibly</b> at the bounds (dimmed, glyph swapped to a bracket stop,
    /// button non-interactable) so a player at min or max can see why nothing is happening rather than
    /// pressing into silence.</para>
    ///
    /// <para>The row owns its own integer only. It never applies the value to anything — it raises
    /// <see cref="ValueChanged"/> and the screen decides what that means.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StepperRowController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;

        [SerializeField] private TMP_Text leftArrowText;
        [SerializeField] private TMP_Text rightArrowText;

        [Tooltip("Optional — lets the arrows work with the mouse. Nudge() is the keyboard/gamepad path.")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;

        [Header("Glyphs")]
        [SerializeField] private string leftGlyph = "‹";
        [SerializeField] private string rightGlyph = "›";

        [Tooltip("Drawn instead of the arrow when that direction is at its bound. " +
                 "A shape change, not just a colour change.")]
        [SerializeField] private string boundGlyph = "·";

        [Range(0f, 1f)][SerializeField] private float disabledArrowAlpha = 0.25f;

        [Header("Format")]
        [Tooltip("{0} = value, {1} = min, {2} = max.")]
        [SerializeField] private string valueFormat = "{0}";

        [Tooltip("Step applied per Nudge / arrow press.")]
        [Min(1)][SerializeField] private int step = 1;

        [SerializeField] private bool wrap;

        private int _value;
        private int _min;
        private int _max;
        private bool _hooked;
        private bool _warned;

        /// <summary>Fires with the new value whenever it actually changes.</summary>
        public event Action<int> ValueChanged;

        /// <summary>Current value, always inside [<see cref="Min"/>, <see cref="Max"/>].</summary>
        public int Value => _value;

        public int Min => _min;
        public int Max => _max;

        /// <summary>True when the value cannot go any lower.</summary>
        public bool AtMin => !wrap && _value <= _min;

        /// <summary>True when the value cannot go any higher.</summary>
        public bool AtMax => !wrap && _value >= _max;

        private void Awake() => HookButtons(true);
        private void OnDestroy() => HookButtons(false);

        // ---- public API --------------------------------------------------------------------------

        /// <summary>
        /// Set up the row. <paramref name="max"/> below <paramref name="min"/> is corrected rather than
        /// thrown on, and the value is clamped into range.
        /// </summary>
        public void Configure(string label, int value, int min, int max)
        {
            if (max < min) (min, max) = (max, min);

            _min = min;
            _max = max;
            _value = Mathf.Clamp(value, _min, _max);

            if (labelText != null) labelText.text = label ?? string.Empty;
            else WarnOnce("StepperRowController has no labelText assigned.");

            Render();
        }

        /// <summary>Step by <paramref name="dir"/> (sign only). Raises <see cref="ValueChanged"/> on a real move.</summary>
        public void Nudge(int dir)
        {
            if (dir == 0) return;
            SetValue(_value + (dir > 0 ? step : -step), notify: true);
        }

        /// <summary>
        /// Jump straight to a value. Clamps (or wraps) into range; only notifies when the stored value
        /// actually moved and <paramref name="notify"/> is set.
        /// </summary>
        public void SetValue(int value, bool notify = true)
        {
            int resolved = Resolve(value);
            bool changed = resolved != _value;

            _value = resolved;
            Render();

            if (changed && notify) ValueChanged?.Invoke(_value);
        }

        /// <summary>Change the allowed range without re-labelling. Re-clamps the current value.</summary>
        public void SetRange(int min, int max, bool notify = false)
        {
            if (max < min) (min, max) = (max, min);
            _min = min;
            _max = max;
            SetValue(_value, notify);
        }

        /// <summary>Amount added per nudge/arrow press.</summary>
        public void SetStep(int newStep) => step = Mathf.Max(1, newStep);

        // ---- internals ---------------------------------------------------------------------------

        private int Resolve(int value)
        {
            if (_max <= _min) return _min;

            if (!wrap) return Mathf.Clamp(value, _min, _max);

            int span = _max - _min + 1;
            int offset = ((value - _min) % span + span) % span;
            return _min + offset;
        }

        private void Render()
        {
            if (valueText != null)
            {
                valueText.text = string.IsNullOrEmpty(valueFormat)
                    ? _value.ToString()
                    : string.Format(valueFormat, _value, _min, _max);
            }

            bool leftUsable = !AtMin && _max > _min;
            bool rightUsable = !AtMax && _max > _min;

            ApplyArrow(leftArrowText, leftButton, leftUsable, leftGlyph);
            ApplyArrow(rightArrowText, rightButton, rightUsable, rightGlyph);
        }

        private void ApplyArrow(TMP_Text text, Button button, bool usable, string glyph)
        {
            if (text != null)
            {
                text.text = usable ? glyph : boundGlyph;

                var c = text.color;
                c.a = usable ? 1f : Mathf.Clamp01(disabledArrowAlpha);
                text.color = c;
            }

            if (button != null)
            {
                button.interactable = usable;
                var img = button.targetGraphic;
                if (img != null) img.raycastTarget = usable;
            }
        }

        private void HookButtons(bool subscribe)
        {
            if (subscribe)
            {
                if (_hooked) return;
                if (leftButton != null) leftButton.onClick.AddListener(NudgeLeft);
                if (rightButton != null) rightButton.onClick.AddListener(NudgeRight);
                _hooked = true;
            }
            else
            {
                if (!_hooked) return;
                if (leftButton != null) leftButton.onClick.RemoveListener(NudgeLeft);
                if (rightButton != null) rightButton.onClick.RemoveListener(NudgeRight);
                _hooked = false;
            }
        }

        private void NudgeLeft() => Nudge(-1);
        private void NudgeRight() => Nudge(+1);

        private void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[JRPG.Menu] {message}", this);
        }
    }
}
