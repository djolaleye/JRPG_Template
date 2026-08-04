using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// The universal fill bar: HP/MP/SP gauges, EXP, allocation meters, equip comparisons.
    ///
    /// <para>Two fills are stacked. The solid fill shows the <i>lesser</i> of current and projected; the
    /// ghost fill behind it shows the <i>greater</i>. That single arrangement renders both directions of
    /// an equip comparison — a gain paints a ghost cap beyond the solid bar, a loss paints the ghost
    /// where the bar is about to retreat from — with no branching in the caller.</para>
    ///
    /// <para><c>max &lt;= 0</c> is a legal input (an unused resource, a stat with no cap). It renders an
    /// empty bar and never divides.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StatBarView : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Filled Image (Image.Type.Filled) showing the settled value.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Filled Image behind the fill showing the projected value during a preview. Optional.")]
        [SerializeField] private Image ghostImage;

        [SerializeField] private Image trackImage;
        [SerializeField] private TMP_Text numerals;

        [Tooltip("Optional label for the resource name, e.g. \"HP\".")]
        [SerializeField] private TMP_Text captionLabel;

        [Header("Format")]
        [Tooltip("{0} = current, {1} = max.")]
        [SerializeField] private string numeralFormat = "{0} / {1}";

        [Tooltip("{0} = current, {1} = max, {2} = projected. Used while a preview is active.")]
        [SerializeField] private string previewNumeralFormat = "{0} → {2} / {1}";

        [Header("Colours")]
        [SerializeField] private Color fillColor = new(0.35f, 0.70f, 0.95f, 1f);

        [Tooltip("Ghost colour when the projected value is higher than the current one.")]
        [SerializeField] private Color gainColor = new(0.45f, 0.90f, 0.55f, 0.85f);

        [Tooltip("Ghost colour when the projected value is lower than the current one.")]
        [SerializeField] private Color lossColor = new(0.95f, 0.45f, 0.40f, 0.85f);

        [Header("Animation")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Tooltip("Authored seconds for a full-length fill sweep. Routed through " +
                 "MenuAnimationProfile.Duration, so reduced motion snaps instead.")]
        [Min(0f)][SerializeField] private float fillDuration = 0.25f;

        private int _current;
        private int _max;
        private int _projected;
        private bool _hasPreview;

        private float _displayed01;
        private Coroutine _tween;
        private bool _warned;

        /// <summary>Latest current value pushed in.</summary>
        public int Current => _current;

        /// <summary>Latest max value pushed in.</summary>
        public int Max => _max;

        /// <summary>Settled fill ratio in 0..1. Zero whenever <see cref="Max"/> is non-positive.</summary>
        public float Normalized01 => Ratio(_current);

        /// <summary>The ratio actually rendered right now — mid-tween this differs from <see cref="Normalized01"/>.</summary>
        public float DisplayedRatio => _displayed01;

        /// <summary>True while a before→after ghost is showing.</summary>
        public bool HasPreview => _hasPreview;

        private void Awake()
        {
            if (fillImage != null) fillImage.color = fillColor;
            if (ghostImage != null) ghostImage.enabled = false;
        }

        private void OnDisable()
        {
            // Coroutines die with the object; land on the final value so re-enabling looks settled.
            _tween = null;
            _displayed01 = Ratio(_current);
            ApplyFill(_displayed01);
        }

        // ---- public API --------------------------------------------------------------------------

        /// <summary>Optional resource caption ("HP", "MP", "EXP"). Ignored when no caption label is wired.</summary>
        public void SetCaption(string caption)
        {
            if (captionLabel != null) captionLabel.text = caption ?? string.Empty;
        }

        /// <summary>
        /// Push a value. Clears any active preview. <paramref name="animate"/> is a request, not a
        /// promise: reduced motion, an inactive object, or a zero duration all snap instead.
        /// </summary>
        public void SetValue(int current, int max, bool animate = true)
        {
            if (!EnsureWired()) return;

            _current = current;
            _max = max;
            _hasPreview = false;
            _projected = current;

            if (ghostImage != null) ghostImage.enabled = false;

            UpdateNumerals();

            float target = Ratio(_current);
            float duration = ResolveDuration(animate);

            // No coroutines outside play mode — an edit-time preview should land on the value directly.
            if (duration <= 0f || !isActiveAndEnabled || !Application.isPlaying)
            {
                StopTween();
                _displayed01 = target;
                ApplyFill(target);
                return;
            }

            StopTween();
            _tween = StartCoroutine(TweenTo(target, duration));
        }

        /// <summary>
        /// Render a before→after ghost against the current value. Call <see cref="ClearPreview"/> — or
        /// any <see cref="SetValue"/> — to drop it.
        /// </summary>
        public void SetPreview(int projected)
        {
            if (!EnsureWired()) return;

            _projected = projected;
            _hasPreview = true;

            float currentRatio = Ratio(_current);
            float projectedRatio = Ratio(_projected);

            // Solid = the part that is true either way; ghost = the contested remainder.
            StopTween();
            _displayed01 = Mathf.Min(currentRatio, projectedRatio);
            ApplyFill(_displayed01);

            if (ghostImage != null)
            {
                ghostImage.enabled = true;
                ghostImage.fillAmount = Mathf.Max(currentRatio, projectedRatio);
                ghostImage.color = _projected >= _current ? gainColor : lossColor;
            }

            UpdateNumerals();
        }

        /// <summary>Drop the ghost and settle back onto the real value.</summary>
        public void ClearPreview()
        {
            if (!_hasPreview) return;

            _hasPreview = false;
            _projected = _current;
            if (ghostImage != null) ghostImage.enabled = false;

            StopTween();
            _displayed01 = Ratio(_current);
            ApplyFill(_displayed01);
            UpdateNumerals();
        }

        /// <summary>Runtime override so a screen can share one animation profile with its widgets.</summary>
        public void SetAnimationProfile(MenuAnimationProfile profile) => animationProfile = profile;

        /// <summary>Recolour the solid fill (per-resource tinting is the screen's call, not the widget's).</summary>
        public void SetFillColor(Color color)
        {
            fillColor = color;
            if (fillImage != null) fillImage.color = color;
        }

        // ---- internals ---------------------------------------------------------------------------

        private float Ratio(int value)
        {
            if (_max <= 0) return 0f;                 // the whole point of this guard
            return Mathf.Clamp01((float)value / _max);
        }

        private float ResolveDuration(bool animate)
        {
            if (!animate) return 0f;
            return animationProfile != null ? animationProfile.Duration(fillDuration) : Mathf.Max(0f, fillDuration);
        }

        private void StopTween()
        {
            if (_tween == null) return;
            StopCoroutine(_tween);
            _tween = null;
        }

        private IEnumerator TweenTo(float target, float duration)
        {
            float from = _displayed01;
            float t = 0f;

            while (t < duration)
            {
                // Unscaled: menus routinely run with timeScale at 0.
                t += Time.unscaledDeltaTime;
                _displayed01 = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                ApplyFill(_displayed01);
                yield return null;
            }

            _displayed01 = target;
            ApplyFill(target);
            _tween = null;
        }

        private void ApplyFill(float ratio)
        {
            if (fillImage == null) return;
            fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        private void UpdateNumerals()
        {
            if (numerals == null) return;

            string format = _hasPreview && !string.IsNullOrEmpty(previewNumeralFormat)
                ? previewNumeralFormat
                : numeralFormat;

            if (string.IsNullOrEmpty(format)) { numerals.text = string.Empty; return; }

            numerals.text = string.Format(format, _current, _max, _projected);
        }

        private bool EnsureWired()
        {
            if (fillImage != null || numerals != null) return true;

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning("[JRPG.Menu] StatBarView has neither fillImage nor numerals assigned — " +
                                 "SetValue/SetPreview are no-ops.", this);
            }
            return false;
        }

        // Keep the authored colour visible while editing the prefab.
        private void OnValidate()
        {
            if (fillImage != null) fillImage.color = fillColor;
            if (trackImage != null) trackImage.raycastTarget = false;
        }
    }
}
