using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace JRPG.Exploration
{
    /// <summary>
    /// Smoothly snaps the orbital follow back behind the player along the player's current facing.
    /// </summary>
    public class CameraRigController : MonoBehaviour
    {
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private Transform playerBody;
        [SerializeField, Min(0.05f)] private float recenterDuration = 0.35f;

        private Coroutine _running;

        public void RecenterBehindPlayer()
        {
            if (orbitalFollow == null || playerBody == null) return;

            float targetYaw = Mathf.DeltaAngle(0f, playerBody.eulerAngles.y);
            float targetVer = (orbitalFollow.VerticalAxis.Range.x + orbitalFollow.VerticalAxis.Range.y) * 0.5f;

            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(SmoothRecenter(targetYaw, targetVer, recenterDuration));
        }

        private IEnumerator SmoothRecenter(float targetYaw, float targetVer, float duration)
        {
            var hor = orbitalFollow.HorizontalAxis;
            var ver = orbitalFollow.VerticalAxis;

            float startYaw = hor.Value;
            float startVer = ver.Value;
            // Use the shortest path for the angle.
            float delta = Mathf.DeltaAngle(startYaw, targetYaw);

            float t = 0f;
            float yawVel = 0f, verVel = 0f;
            float dampTime = duration * 0.5f; // SmoothDamp's "smoothTime" is roughly time to ~halfway.

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float yaw = Mathf.SmoothDampAngle(hor.Value, startYaw + delta, ref yawVel, dampTime);
                float vv = Mathf.SmoothDamp(ver.Value, targetVer, ref verVel, dampTime);
                hor.Value = yaw;
                ver.Value = vv;
                orbitalFollow.HorizontalAxis = hor;
                orbitalFollow.VerticalAxis = ver;
                yield return null;
            }
            hor.Value = startYaw + delta;
            ver.Value = targetVer;
            orbitalFollow.HorizontalAxis = hor;
            orbitalFollow.VerticalAxis = ver;
            _running = null;
        }
    }
}
