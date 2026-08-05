using UnityEngine;
using JRPG.Core;

namespace JRPG.Exploration
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float rotationSmoothTime = 0.10f;
        [SerializeField] private float gravity = -9.81f;

        public Vector2 MoveInput { get; set; }
        public bool SprintHeld { get; set; }
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting => SprintHeld && MoveInput.sqrMagnitude > 0.0001f;

        private CharacterController _charController;
        private Camera _cam;
        private float _yawCurrent;
        private float _yawVelocity;
        private float _verticalVelocity;

        private void Awake()
        {
            _charController = GetComponent<CharacterController>();
            _yawCurrent = transform.eulerAngles.y;
        }

        private void Update()
        {
            // PlayerSaveAgent disables the controller for a frame so a restored position sticks.
            // Skip the whole tick rather than logging on every load.
            if (_charController == null || !_charController.enabled) return;

            if (_cam == null) _cam = Camera.main;

            Vector2 input = MoveInput;
            // Gate on InputContext: if not Exploration, zero horizontal input but still settle gravity.
            var state = AppContext.State;
            if (state != null && state.Current.Input != InputContext.Exploration) input = Vector2.zero;

            // Camera-relative move direction (flattened to XZ).
            Vector3 worldMove = Vector3.zero;
            if (input.sqrMagnitude > 0.0001f && _cam != null)
            {
                Vector3 fwd = _cam.transform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = _cam.transform.right; right.y = 0f; right.Normalize();
                worldMove = (right * input.x + fwd * input.y);
                if (worldMove.sqrMagnitude > 1f) worldMove.Normalize();
            }

            float targetSpeed = (SprintHeld ? sprintSpeed : walkSpeed) * input.magnitude;
            CurrentSpeed = targetSpeed;

            // Rotate body toward move direction.
            if (worldMove.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Mathf.Atan2(worldMove.x, worldMove.z) * Mathf.Rad2Deg;
                _yawCurrent = Mathf.SmoothDampAngle(_yawCurrent, targetYaw, ref _yawVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, _yawCurrent, 0f);
            }

            // Gravity.
            if (_charController.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = worldMove * targetSpeed + Vector3.up * _verticalVelocity;
            _charController.Move(velocity * Time.deltaTime);
        }
    }
}
