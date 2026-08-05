using UnityEngine;

namespace JRPG.Menu
{
    /// <summary>
    /// Slowly rotates its own transform, so a camera parented beneath it orbits whatever the pivot sits
    /// on. Used by the title scene's 3D backdrop; the title UI itself is a separate screen-space canvas
    /// and is unaffected.
    ///
    /// <para>Rotating a pivot rather than moving the camera keeps the framing authored in the scene: the
    /// designer positions the camera once, at the distance and pitch they want, and this only spins the
    /// parent.</para>
    ///
    /// <para><b>Reduced motion.</b> Speed goes through <see cref="MenuAnimationProfile.Amplitude"/>, so
    /// the accessibility toggle stops the orbit dead while leaving the backdrop composed exactly as
    /// authored. A purely decorative rotation is the clearest case for that gate.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackdropOrbitController : MonoBehaviour
    {
        [Tooltip("Degrees per second around the pivot's local up axis. Negative reverses the direction.")]
        [SerializeField] private float degreesPerSecond = 3f;

        [Tooltip("Optional. When assigned, reduced motion stops the orbit. Null orbits unconditionally.")]
        [SerializeField] private MenuAnimationProfile animationProfile;

        [Tooltip("Unscaled time keeps the backdrop moving on menus that zero the time scale.")]
        [SerializeField] private bool useUnscaledTime = true;

        private float Speed => animationProfile != null
            ? Mathf.Sign(degreesPerSecond) * animationProfile.Amplitude(Mathf.Abs(degreesPerSecond))
            : degreesPerSecond;

        private void Update()
        {
            float speed = Speed;
            if (Mathf.Approximately(speed, 0f)) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.Rotate(0f, speed * dt, 0f, Space.Self);
        }
    }
}
