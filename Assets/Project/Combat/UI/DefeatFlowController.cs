using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Game-over screen
    /// responds to DefeatFlowStarted decides what happens next (retry the fight, or return to exploration).
    ///
    public sealed class DefeatFlowController : MonoBehaviour
    {
        public static DefeatFlowController Current { get; private set; }

        [SerializeField] private string defeatMenuId = "combat_defeat";

        private IEventBus _bus;
        private IMenuService _menus;

        private bool _openPending;
        private bool _retryPending;
        private bool _returnPending;

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
                        Debug.LogWarning("[JRPG.Combat] Retry failed — returning to exploration.");
                        GoToExploration();
                    }
                }
            }
            else if (_returnPending)
            {
                _returnPending = false;
                GoToExploration();
            }
        }

        public void RequestRetry() => _retryPending = true;
        public void RequestReturnToExploration() => _returnPending = true;

        /// The defeat flow owns this exit — combat deliberately no longer forces it.
        private void GoToExploration()
        {
            AppContext.State?.SetState(
                new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
        }
    }
}
