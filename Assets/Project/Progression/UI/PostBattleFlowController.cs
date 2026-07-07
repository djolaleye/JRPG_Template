using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    public enum PostBattleFlowState
    {
        Idle,
        VictorySummary,
        RewardReview,
        XpPreview,
        LevelUpReview,
        AttributeAllocation,
        Complete,
    }

    /// Sequences the post-battle placeholder screens. Owns ordering only — all math lives in
    /// ProgressionService. Menus replace each other (Close current + Open next); on completion the
    /// controller closes everything, restores exploration state, and lets the service commit.
    public sealed class PostBattleFlowController : MonoBehaviour
    {
        public static PostBattleFlowController Current { get; private set; }

        public PostBattleFlowState CurrentState { get; private set; } = PostBattleFlowState.Idle;

        private IEventBus _bus;
        private IMenuService _menus;
        private bool _startPending;

        private ProgressionService Progression
            => AppContext.Services != null && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        private void Awake() => Current = this;

        private void OnEnable()
        {
            _bus = AppContext.Bus;
            if (AppContext.Services != null) AppContext.Services.TryResolve(out _menus);
            _bus?.Subscribe<PostBattleFlowStarted>(OnFlowStarted);
        }

        private void OnDisable()
        {
            _bus?.Unsubscribe<PostBattleFlowStarted>(OnFlowStarted);
            if (Current == this) Current = null;
        }

        // Defer out of the event handler: PostBattleFlowStarted is published synchronously from
        // inside CombatService.EndBattle, and opening menus mid-battle-teardown would reenter it.
        private void OnFlowStarted(PostBattleFlowStarted e) => _startPending = true;

        private void Update()
        {
            if (!_startPending) return;
            _startPending = false;

            if (Progression == null || !Progression.IsPostBattleFlowActive) return;
            CurrentState = PostBattleFlowState.VictorySummary;
            _menus?.Open("postbattle_victory", null);
        }

        /// Advances to the next screen. Called by the Continue/Confirm menu rows.
        public void Continue()
        {
            var progression = Progression;
            if (progression == null || _menus == null) return;

            switch (CurrentState)
            {
                case PostBattleFlowState.VictorySummary:
                    Transition(PostBattleFlowState.RewardReview, "postbattle_rewards");
                    break;

                case PostBattleFlowState.RewardReview:
                    Transition(PostBattleFlowState.XpPreview, "postbattle_xp");
                    break;

                case PostBattleFlowState.XpPreview:
                    // Confirmation point: XP/level-ups/points are applied here (preview-first rule).
                    progression.ApplyBattleResult();
                    if (progression.PendingLevelUps.Count > 0)
                        Transition(PostBattleFlowState.LevelUpReview, "postbattle_levelup");
                    else
                        CompleteFlow();
                    break;

                case PostBattleFlowState.LevelUpReview:
                    if (progression.HasPendingAttributeAllocations())
                        Transition(PostBattleFlowState.AttributeAllocation, "postbattle_allocate");
                    else
                        CompleteFlow();
                    break;

                case PostBattleFlowState.AttributeAllocation:
                    if (progression.CanCompletePostBattleFlow()) CompleteFlow();
                    else Debug.Log("[JRPG.Progression.UI] Cannot finish — unspent attribute points remain.");
                    break;
            }
        }

        private void Transition(PostBattleFlowState next, string menuId)
        {
            CurrentState = next;
            _menus.Close();
            _menus.Open(menuId, null);
        }

        private void CompleteFlow()
        {
            CurrentState = PostBattleFlowState.Complete;
            _menus.CloseAll();
            Progression?.CompletePostBattleFlow();

            // The flow owns the exit back to exploration (combat no longer forces it on victory).
            AppContext.State?.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            CurrentState = PostBattleFlowState.Idle;
        }
    }
}
