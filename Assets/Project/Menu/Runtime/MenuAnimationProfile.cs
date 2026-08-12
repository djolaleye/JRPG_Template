using UnityEngine;

namespace JRPG.Menu
{
    /// <summary>
    /// Shared motion timings for menus and screen transitions.
    ///
    /// <para><b>Reduced motion is the point of this asset.</b> Every animating UI component reads its
    /// durations through <see cref="Duration"/> instead of hard-coding them, so an eventual accessibility
    /// toggle can flip <see cref="reducedMotion"/> once and have every screen honour
    /// it. Components must never read the raw fields directly.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/UI/Menu Animation Profile", fileName = "MenuAnimationProfile")]
    public class MenuAnimationProfile : ScriptableObject
    {
        [Header("Accessibility")]
        [Tooltip("When true, Duration() collapses every animation to instant. Set at runtime by the " +
                 "settings service; the serialized value is the project default.")]
        public bool reducedMotion;

        [Header("Row feedback")]
        [Min(0f)] public float selectionMoveDuration = 0.08f;
        [Min(0f)] public float confirmFlashDuration = 0.12f;
        [Min(0f)] public float invalidShakeDuration = 0.18f;

        [Tooltip("Horizontal shake amplitude, in pixels, when an invalid row is submitted. " +
                 "Suppressed entirely under reduced motion.")]
        [Min(0f)] public float invalidShakeAmplitude = 6f;

        [Header("Screens")]
        [Min(0f)] public float menuOpenDuration = 0.15f;
        [Min(0f)] public float menuCloseDuration = 0.12f;
        [Tooltip("Length of one half of a screen fade (out, or in).")]
        [Min(0f)] public float screenFadeDuration = 0.8f;

        [Header("Dialogue")]
        [Tooltip("Characters revealed per second by the typewriter. 0 = instant. " +
                 "Overridden per-player by the Text Speed setting")]
        [Min(0f)] public float textCharactersPerSecond = 45f;

        [Tooltip("Seconds a fully-revealed auto-advance line waits before advancing.")]
        [Min(0f)] public float autoAdvanceDelay = 2.5f;

        /// <summary>
        /// The single accessor every animating component must use. Returns 0 under reduced motion so
        /// callers can keep their tween code and simply land on the final value immediately.
        /// </summary>
        public float Duration(float authored) => reducedMotion ? 0f : Mathf.Max(0f, authored);

        /// <summary>Amplitude gate for purely decorative motion, which reduced motion removes outright.</summary>
        public float Amplitude(float authored) => reducedMotion ? 0f : Mathf.Max(0f, authored);
    }
}
