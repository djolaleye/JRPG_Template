using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JRPG.Menu
{
    /// <summary>
    /// The loading screen shown while the screen is covered: a black fill plus a small animated icon
    /// pinned to a corner (bottom-right, matching the reference), and an optional status line.
    ///
    /// <para><b>No art dependency.</b> The icon is animated procedurally — rotation plus a scale pulse
    /// on whatever sprite the prefab happens to carry — so this works before a single asset exists. The
    /// real corner icon is listed in <c>docs/ui-art-manifest.md</c>; dropping it into
    /// <c>iconImage.sprite</c> is the entire integration.</para>
    ///
    /// <para><b>Reduced motion.</b> Timings go through <see cref="MenuAnimationProfile.Duration"/> and
    /// <see cref="MenuAnimationProfile.Amplitude"/>. When they collapse to zero the icon stops moving
    /// but stays <i>visible</i> and is reset to its neutral pose — the player still sees the "something
    /// is happening" affordance, just without the motion.</para>
    ///
    /// <para>Animation runs in <c>Update</c> rather than a coroutine so that enabling and disabling this
    /// view can never strand a running tween.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Everything the view owns. Toggled by Show/Hide. Defaults to this GameObject.")]
        [SerializeField] private GameObject content;
        [SerializeField] private Graphic background;
        [SerializeField] private RectTransform iconTransform;
        [SerializeField] private Image iconImage;

        [Tooltip("Optional status line, e.g. \"Loading…\". Hidden when the status is empty.")]
        [SerializeField] private TMP_Text statusLabel;

        [Header("Timing")]
        [Tooltip("Optional. Null falls back to the authored values below with no reduced-motion gating.")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Tooltip("Seconds for one full revolution of the icon. 0 disables rotation.")]
        [Min(0f)] [SerializeField] private float spinRevolutionDuration = 1.6f;

        [Tooltip("Seconds for one full grow/shrink cycle. 0 disables the pulse.")]
        [Min(0f)] [SerializeField] private float pulseCycleDuration = 1.1f;

        [Tooltip("Peak scale offset of the pulse, as a fraction of the icon's authored size.")]
        [Min(0f)] [SerializeField] private float pulseAmplitude = 0.12f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Content")]
        [SerializeField] private string defaultStatus = "Loading…";

        private float _elapsed;
        private Vector3 _iconBaseScale = Vector3.one;
        private bool _cachedBaseScale;

        /// <summary>Whether the view is currently showing.</summary>
        public bool IsVisible => Content != null && Content.activeSelf;

        /// <summary>True when the profile has collapsed the icon animation to nothing.</summary>
        public bool MotionSuppressed => SpinDuration <= 0f && (PulseDuration <= 0f || PulseAmplitude <= 0f);

        private GameObject Content => content != null ? content : gameObject;

        private float SpinDuration => animationProfile != null
            ? animationProfile.Duration(spinRevolutionDuration)
            : Mathf.Max(0f, spinRevolutionDuration);

        private float PulseDuration => animationProfile != null
            ? animationProfile.Duration(pulseCycleDuration)
            : Mathf.Max(0f, pulseCycleDuration);

        private float PulseAmplitude => animationProfile != null
            ? animationProfile.Amplitude(pulseAmplitude)
            : Mathf.Max(0f, pulseAmplitude);

        private void Awake()
        {
            CacheBaseScale();
            if (statusLabel != null && string.IsNullOrEmpty(statusLabel.text)) SetStatus(defaultStatus);
        }

        private void OnDisable() => ResetIconPose();

        /// <summary>Shows the view and restarts the animation from its neutral pose.</summary>
        public void Show()
        {
            CacheBaseScale();
            _elapsed = 0f;
            ResetIconPose();

            if (background != null) background.enabled = true;

            var go = Content;
            if (go != null && !go.activeSelf) go.SetActive(true);
        }

        /// <summary>Hides the view.</summary>
        public void Hide()
        {
            ResetIconPose();

            var go = Content;
            if (go != null && go.activeSelf) go.SetActive(false);
        }

        /// <summary>Sets the status line. Null or empty hides the label rather than showing a blank row.</summary>
        public void SetStatus(string status)
        {
            if (statusLabel == null) return;

            bool has = !string.IsNullOrEmpty(status);
            statusLabel.text = has ? status : string.Empty;
            statusLabel.enabled = has;
        }

        private void Update()
        {
            if (iconTransform == null) return;

            float spin = SpinDuration;
            float pulse = PulseDuration;
            float amplitude = PulseAmplitude;

            // Reduced motion: hold the neutral pose. The icon remains on screen — only the movement goes.
            if (spin <= 0f && (pulse <= 0f || amplitude <= 0f))
            {
                ResetIconPose();
                return;
            }

            _elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (spin > 0f)
            {
                float degrees = -360f * (_elapsed / spin);
                iconTransform.localRotation = Quaternion.Euler(0f, 0f, degrees % 360f);
            }
            else
            {
                iconTransform.localRotation = Quaternion.identity;
            }

            if (pulse > 0f && amplitude > 0f)
            {
                float phase = Mathf.Sin(_elapsed / pulse * Mathf.PI * 2f);
                iconTransform.localScale = _iconBaseScale * (1f + phase * amplitude);
            }
            else
            {
                iconTransform.localScale = _iconBaseScale;
            }
        }

        private void CacheBaseScale()
        {
            if (_cachedBaseScale || iconTransform == null) return;

            _iconBaseScale = iconTransform.localScale;
            _cachedBaseScale = true;
        }

        private void ResetIconPose()
        {
            if (iconTransform == null) return;

            CacheBaseScale();
            iconTransform.localRotation = Quaternion.identity;
            iconTransform.localScale = _iconBaseScale;
        }
    }
}
