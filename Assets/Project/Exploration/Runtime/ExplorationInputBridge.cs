using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Exploration
{
    /// Routes Input System actions from the authored InputActionAsset (map "Exploration") to gameplay handlers.
    /// Action names expected: Move, Look, Sprint, Interact, Pause, QuickSave, Recenter, Attack.
    public class ExplorationInputBridge : MonoBehaviour
    {
        [Header("Action Asset")]
        [SerializeField] private InputActionAsset playerControls;
        [SerializeField] private string actionMapName = "Exploration";

        [Header("Targets")]
        [SerializeField] private PlayerMovementController movement;
        [SerializeField] private Interactor interactor;
        [SerializeField] private CameraRigController cameraRig;
        [Tooltip("Camera orbit input. Auto-found if left empty. Enabled only in the Exploration input context.")]
        [SerializeField] private CinemachineInputAxisController cameraInput;
        [SerializeField] private int quicksaveSlot = 0;

        private InputActionMap _map;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _recenterAction;
        private InputAction _pauseAction;
        private InputAction _interactAction;
        private InputAction _attackAction;
        private InputAction _quicksaveAction;

        private System.Action<GameStateChanged> _onStateChanged;

        private void OnEnable()
        {
            if (!ResolveActions()) return;
            HookActions(true);
            _map.Enable();

            var services = AppContext.Services;
            if (services != null && services.TryResolve<IEventBus>(out var bus))
            {
                _onStateChanged = OnStateChanged;
                bus.Subscribe(_onStateChanged);
            }
            ApplyMapEnableForCurrentState();
        }

        private void OnDisable()
        {
            if (_map == null) return;
            HookActions(false);
            _map.Disable();
            var services = AppContext.Services;
            if (services != null && services.TryResolve<IEventBus>(out var bus) && _onStateChanged != null)
                bus.Unsubscribe(_onStateChanged);
        }

        private bool ResolveActions()
        {
            if (playerControls == null)
            {
                Debug.LogError("[JRPG.Exploration] InputActionAsset is not assigned.", this);
                return false;
            }

            _map = playerControls.FindActionMap(actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError($"[JRPG.Exploration] Action map '{actionMapName}' not found on asset '{playerControls.name}'.", this);
                return false;
            }

            _moveAction      = _map.FindAction("Move",      throwIfNotFound: false);
            _sprintAction    = _map.FindAction("Sprint",    throwIfNotFound: false);
            _recenterAction  = _map.FindAction("Recenter",  throwIfNotFound: false);
            _pauseAction     = _map.FindAction("Pause",     throwIfNotFound: false);
            _interactAction  = _map.FindAction("Interact",  throwIfNotFound: true);
            _attackAction    = _map.FindAction("Attack",    throwIfNotFound: false);
            _quicksaveAction = _map.FindAction("QuickSave", throwIfNotFound: false);
            return true;
        }


        private void HookActions(bool subscribe)
        {
            if (subscribe)
            {
                if (_recenterAction  != null) _recenterAction.performed  += OnRecenter;
                if (_pauseAction     != null) _pauseAction.performed     += OnPause;
                if (_interactAction  != null) _interactAction.performed  += OnInteract;
                if (_attackAction    != null) _attackAction.performed    += OnAttack;
                if (_quicksaveAction != null) _quicksaveAction.performed += OnQuicksave;
            }
            else
            {
                if (_recenterAction  != null) _recenterAction.performed  -= OnRecenter;
                if (_pauseAction     != null) _pauseAction.performed     -= OnPause;
                if (_interactAction  != null) _interactAction.performed  -= OnInteract;
                if (_attackAction    != null) _attackAction.performed    -= OnAttack;
                if (_quicksaveAction != null) _quicksaveAction.performed -= OnQuicksave;
            }
        }

        private void Update()
        {
            if (movement == null || _moveAction == null) return;
            movement.MoveInput = _moveAction.ReadValue<Vector2>();
            movement.SprintHeld = _sprintAction != null && _sprintAction.IsPressed();
        }

        private void OnStateChanged(GameStateChanged evt) => ApplyMapEnableForCurrentState();

        private void ApplyMapEnableForCurrentState()
        {
            if (_map == null) return;
            var state = AppContext.State;
            bool exploration = state == null || state.Current.Input == InputContext.Exploration;

            if (exploration) _map.Enable();
            else _map.Disable();

            // Camera orbit is driven by Cinemachine's own input component, independent of the
            // Exploration action map — so gate it explicitly to the exploration input context.
            var cam = ResolveCameraInput();
            if (cam != null) cam.enabled = exploration;
        }

        private CinemachineInputAxisController ResolveCameraInput()
        {
            if (cameraInput == null) cameraInput = FindAnyObjectByType<CinemachineInputAxisController>(FindObjectsInactive.Include);
            return cameraInput;
        }

        private void OnRecenter(InputAction.CallbackContext ctx) => cameraRig?.RecenterBehindPlayer();

        private void OnPause(InputAction.CallbackContext ctx)
        {
            var services = AppContext.Services;
            if (services == null) return;

            if (!services.TryResolve<IMenuService>(out var menus))
            {
                Debug.LogWarning("[JRPG.Exploration] Pause: no IMenuService registered.");
                return;
            }
            
            // Open only — never toggle.
            //
            // Opening "pause" applies Exploration + PauseMenu + Menu, and this bridge disables its own
            // action map outside InputContext.Exploration. So once the menu is up this handler cannot
            // fire again.
            // Closing belongs to the Menu map's Cancel, which PauseMenuController already handles 
            // — Escape and Start open it, Escape and Gamepad East close it, and the player still experiences a toggle.
            if (menus.ActiveMenuId == "pause") return;

            menus.Open("pause", null);
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            interactor?.TryInteract();
            Debug.Log("Interaction Attempt.");

        } 

        private void OnAttack(InputAction.CallbackContext ctx)
        {
            Debug.Log("Attacked.");

            if (AppContext.Services != null && AppContext.Services.TryResolve<IEventBus>(out var bus))
                bus.Publish(new CombatInitiationRequested("player_attack_request"));
        }

        private void OnQuicksave(InputAction.CallbackContext ctx)
        {
            var services = AppContext.Services;
            if (services == null) return;
            if (!services.TryResolve<ISaveService>(out var svc))
            {
                Debug.LogWarning("[JRPG.Exploration] Quicksave: no ISaveService registered.");
                return;
            }
            if (!svc.CanSave())
            {
                Debug.Log("[JRPG.Exploration] Quicksave rejected: not in an allowed state.");
                return;
            }
            bool ok = svc.Save(quicksaveSlot);
            Debug.Log($"[JRPG.Exploration] Quicksave to slot {quicksaveSlot}: {(ok ? "OK" : "FAIL")}");
        }
    }
}
