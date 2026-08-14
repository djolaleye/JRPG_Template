using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// <summary>
    /// Game-over screen. Responds to <c>DefeatFlowStarted</c> and owns what happens next: retry the
    /// fight, restore the last save, or return to the title screen.
    /// </summary>
    public sealed class DefeatFlowController : MonoBehaviour
    {
        public static DefeatFlowController Current { get; private set; }

        [SerializeField] private string defeatMenuId = "combat_defeat";

        private IEventBus _bus;
        private IMenuService _menus;

        private bool _openPending;
        private bool _retryPending;
        private bool _titlePending;
        private int _loadSlotPending = -1;

        public bool CanRetry { get; private set; }

        private void Awake() => Current = this;

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            AppContext.Services?.TryResolve(out _menus);
            _bus?.Subscribe<DefeatFlowStarted>(OnDefeatFlowStarted);
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<DefeatFlowStarted>(OnDefeatFlowStarted);
            if (Current == this) Current = null;
        }

        private void OnDefeatFlowStarted(DefeatFlowStarted e)
        {
            CanRetry = e.CanRetry;
            _openPending = true;
        }

        private void Update()
        {
            if (_openPending)
            {
                _openPending = false;
                if (_menus == null) AppContext.Services?.TryResolve(out _menus);

                _menus?.Open(defeatMenuId, null);
                return;
            }

            if (_retryPending)
            {
                _retryPending = false;
                if (AppContext.Services != null && AppContext.Services.TryResolve<ICombatService>(out var svc)
                    && svc is CombatService combat)
                {
                    if (!combat.RestartLastBattle())
                    {
                        Debug.LogWarning("[JRPG.Combat] Retry failed — returning to the title screen.");
                        GoToTitle();
                    }
                }
            }
            else if (_loadSlotPending >= 0)
            {
                int slot = _loadSlotPending;
                _loadSlotPending = -1;
                LoadSlot(slot);
            }
            else if (_titlePending)
            {
                _titlePending = false;
                GoToTitle();
            }
        }

        // Each of these is drained on the next frame rather than acted on inline: they are called from
        // inside a menu row's Execute, and starting a scene load there would tear down the menu stack
        // the caller is still standing on.
        public void RequestRetry() => _retryPending = true;
        public void RequestLoadLastSave(int slot) => _loadSlotPending = slot;
        public void RequestReturnToTitle() => _titlePending = true;

        /// <summary>
        /// Restores a save. The scene swap this triggers unloads the Combat scene on its way through
        /// <c>SceneFlowService</c>, so the defeat flow does not tear anything down itself.
        ///
        /// <para>A refusal deliberately leaves the defeat screen up: dropping the player onto a black
        /// screen with no menu would be worse than telling them the load did not happen.</para>
        /// </summary>
        private void LoadSlot(int slot)
        {
            if (AppContext.Services == null || !AppContext.Services.TryResolve<ISessionService>(out var session))
            {
                Debug.LogError("[JRPG.Combat] No ISessionService registered; the save could not be loaded.", this);
                _openPending = true;
                return;
            }

            if (!session.LoadGame(slot))
            {
                Debug.LogWarning($"[JRPG.Combat] Loading slot {slot} failed; the defeat screen stays up.");
                _openPending = true;
            }
        }

        /// The guaranteed exit, and the fallback when a retry or a load cannot proceed.
        private void GoToTitle()
        {
            if (AppContext.Services != null && AppContext.Services.TryResolve<ISessionService>(out var session))
            {
                session.ReturnToTitle();
                return;
            }

            Debug.LogError("[JRPG.Combat] No ISessionService registered; cannot return to the title screen.", this);
        }
    }
}
