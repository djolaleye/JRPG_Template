using UnityEngine;
using UnityEngine.InputSystem;
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

            // Visible health log so we can tell at a glance whether AppContext got initialized
            // (i.e., whether Bootstrap ran before this scene). Without it, Pause/QuickSave/Attack
            // handlers will silently return because they have no services to talk to.
            if (AppContext.Services == null)
                Debug.LogWarning("[JRPG.Exploration] AppContext.Services is null — GameBootstrap hasn't run. " +
                                 "Pause/QuickSave/Attack handlers will still log receipt but cannot publish events or save.", this);
            else
                Debug.Log("[JRPG.Exploration] Input bridge enabled; AppContext OK.", this);

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
            WarnIfLookMissing();
            _sprintAction    = _map.FindAction("Sprint",    throwIfNotFound: false);
            _recenterAction  = _map.FindAction("Recenter",  throwIfNotFound: false);
            _pauseAction     = _map.FindAction("Pause",     throwIfNotFound: false);
            _interactAction  = _map.FindAction("Interact",  throwIfNotFound: false);
            _attackAction    = _map.FindAction("Attack",    throwIfNotFound: false);
            _quicksaveAction = _map.FindAction("QuickSave", throwIfNotFound: false);
            return true;
        }

        // Look is consumed by the Cinemachine InputAxisController directly; we don't need a reference here.
        // The method exists so a missing 'Look' action surfaces clearly during development.
        private void WarnIfLookMissing()
        {
            if (_map.FindAction("Look", throwIfNotFound: false) == null)
                Debug.LogWarning("[JRPG.Exploration] Action 'Look' is missing on the asset — camera orbit input won't work.", this);
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
            if (state == null) { _map.Enable(); return; }
            if (state.Current.Input == InputContext.Exploration) _map.Enable();
            else _map.Disable();
        }

        private void OnRecenter(InputAction.CallbackContext ctx) => cameraRig?.RecenterBehindPlayer();

        private void OnPause(InputAction.CallbackContext ctx)
        {
            var state = AppContext.State;
            if (state == null) return;

            var currentState = state.Current;
            if (currentState.Mode == GameMode.Exploration && currentState.Overlay == OverlayState.None)
            {
                state.SetState(new LayeredState(GameMode.Exploration, OverlayState.PauseMenu, InputContext.Menu));
                if (AppContext.Services != null && AppContext.Services.TryResolve<IEventBus>(out var bus))
                    bus.Publish(new MenuOpened("pause"));
            }
            else if (currentState.Mode == GameMode.Exploration && currentState.Overlay == OverlayState.PauseMenu)
            {
                state.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            }
        }

        private void OnInteract(InputAction.CallbackContext ctx) => interactor?.TryInteract();

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
