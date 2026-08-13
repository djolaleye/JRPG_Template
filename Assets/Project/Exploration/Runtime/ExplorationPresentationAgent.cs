using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// <summary>
    /// Implements <see cref="IExplorationPresentation"/> for a world scene: the player pose the battle
    /// returns to, and the camera handoff into and out of combat.
    ///
    /// <para><b>Focus is a priority change, not a camera move.</b> A second virtual camera aimed at
    /// the encounter is raised above the follow camera, so Cinemachine blends into the framing with
    /// the project's authored easing instead of this component animating a transform.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExplorationPresentationAgent : MonoBehaviour, IExplorationPresentation
    {
        [Header("Player")]
        [Tooltip("Transform whose position and yaw are captured and restored. Usually the player root.")]
        [SerializeField] private Transform player;

        [Tooltip("Disabled for one frame around a pose restore, or it overwrites the transform write.")]
        [SerializeField] private CharacterController playerController;

        [Header("Camera")]
        [Tooltip("The scene's real camera. Disabled while combat owns the screen.")]
        [SerializeField] private Camera explorationCamera;

        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private AudioListener listener;

        [Tooltip("Normal third-person follow camera.")]
        [SerializeField] private CinemachineCamera followCamera;

        [Tooltip("Raised above the follow camera to frame the encounter before the transition.")]
        [SerializeField] private CinemachineCamera encounterFocusCamera;

        [Tooltip("Priority the focus camera takes while framing. Must beat the follow camera.")]
        [SerializeField] private int focusPriority = 30;

        private void Awake()
        {
            if (player == null) player = transform;
            if (playerController == null) playerController = player.GetComponent<CharacterController>();
            if (explorationCamera == null) explorationCamera = Camera.main;
            if (brain == null && explorationCamera != null) brain = explorationCamera.GetComponent<CinemachineBrain>();
            if (listener == null && explorationCamera != null) listener = explorationCamera.GetComponent<AudioListener>();
        }

        private void OnEnable() => ExplorationPresentationSink.Current = this;

        private void OnDisable()
        {
            if (ReferenceEquals(ExplorationPresentationSink.Current, this))
                ExplorationPresentationSink.Current = null;
        }

        // ---- IExplorationPresentation ---------------------------------------------------------

        public bool TryCapturePlayerPose(out Vector3 position, out float yaw)
        {
            if (player == null)
            {
                position = Vector3.zero;
                yaw = 0f;
                Debug.LogError("[JRPG.Exploration] No player transform wired — the return pose cannot be captured.", this);
                return false;
            }

            position = player.position;
            yaw = player.eulerAngles.y;
            return true;
        }

        public void RestorePlayerPose(Vector3 position, float yaw)
        {
            if (player == null) return;

            if (isActiveAndEnabled) StartCoroutine(RestoreRoutine(position, yaw));
            else ApplyPose(position, yaw);   // no coroutine host; the direct write still lands
        }

        /// The CharacterController re-applies its own cached position after a direct transform write,
        /// so it is switched off across a frame boundary.
        private IEnumerator RestoreRoutine(Vector3 position, float yaw)
        {
            bool wasEnabled = playerController != null && playerController.enabled;
            if (playerController != null) playerController.enabled = false;

            ApplyPose(position, yaw);

            yield return null;

            if (playerController != null) playerController.enabled = wasEnabled;
        }

        private void ApplyPose(Vector3 position, float yaw)
        {
            player.position = position;
            player.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void FocusOn(Transform target)
        {
            if (encounterFocusCamera == null)
            {
                // Not fatal: the transition still runs, it just cuts rather than framing the enemy.
                Debug.LogWarning("[JRPG.Exploration] No encounter focus camera wired; the pre-battle " +
                                 "framing will be skipped.", this);
                return;
            }

            if (target == null)
            {
                ReleaseFocus();
                return;
            }

            encounterFocusCamera.LookAt = target;
            encounterFocusCamera.Follow = target;
            encounterFocusCamera.Priority = focusPriority;
        }

        public void ReleaseFocus()
        {
            if (encounterFocusCamera == null) return;

            encounterFocusCamera.Priority = 0;
            encounterFocusCamera.LookAt = null;
            encounterFocusCamera.Follow = null;
        }

        public void SetCameraActive(bool active)
        {
            if (explorationCamera != null) explorationCamera.enabled = active;
            if (brain != null) brain.enabled = active;

            // The listener moves with the screen: two live listeners warn every frame, none is silence.
            if (listener != null) listener.enabled = active;
        }

        /// True when the follow camera is wired — used by the builder's report, and by the director to
        /// decide whether the focus leg of the transition is worth waiting for.
        public bool HasFocusCamera => encounterFocusCamera != null && followCamera != null;
    }
}
