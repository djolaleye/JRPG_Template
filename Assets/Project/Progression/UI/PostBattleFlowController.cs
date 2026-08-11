using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// The post-battle screens, in order.
    ///
    /// <para>Victory → Results → Level Up → Attributes → New Skill. Every screen after Results is
    /// conditional: no level-ups skips the last three outright, and each of the others appears only
    /// when it has something to say.</para>
    ///
    /// <para><b>Skill outcomes come last, after allocation.</b> They are the final consequence of
    /// levelling, and a skill replacement is the one decision that can be declined — asking for it
    /// before the player has finished spending points buried it mid-flow.</para>
    /// </summary>
    public enum PostBattleFlowState
    {
        Idle,
        VictorySummary,
        Results,
        LevelUpReview,
        AttributeAllocation,
        SkillChoice,
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
                    Transition(PostBattleFlowState.Results, "postbattle_results");
                    break;

                case PostBattleFlowState.Results:
                    // Confirmation point: XP/level-ups/points/skills are applied here, so everything
                    // shown up to now was a projection (preview-first rule).
                    progression.ApplyBattleResult();

                    if (progression.PendingLevelUps.Count > 0)
                        Transition(PostBattleFlowState.LevelUpReview, "postbattle_levelup");
                    else
                        AdvanceAfterLevelUps(progression);
                    break;

                case PostBattleFlowState.LevelUpReview:
                    AdvanceAfterLevelUps(progression);
                    break;

                case PostBattleFlowState.AttributeAllocation:
                    if (progression.HasPendingAttributeAllocations())
                    {
                        Debug.Log("[JRPG.Progression.UI] Cannot continue — unspent attribute points remain.");
                        break;
                    }
                    AdvanceAfterAllocation(progression);
                    break;

                case PostBattleFlowState.SkillChoice:
                    if (progression.HasPendingSkillChoices())
                    {
                        Debug.Log("[JRPG.Progression.UI] Cannot continue — a skill decision is pending.");
                        break;
                    }
                    CompleteFlow();
                    break;
            }
        }

        /// Allocation, then skills, then done — skipping whichever have nothing to show.
        private void AdvanceAfterLevelUps(ProgressionService progression)
        {
            if (progression.HasPendingAttributeAllocations())
                Transition(PostBattleFlowState.AttributeAllocation, "postbattle_allocate");
            else
                AdvanceAfterAllocation(progression);
        }

        private void AdvanceAfterAllocation(ProgressionService progression)
        {
            // Covers both kinds of skill outcome: gained outright (an announcement) and gained at the
            // cap (a decision). One screen handles both, so one check gates it.
            if (progression.HasSkillOutcomes())
                Transition(PostBattleFlowState.SkillChoice, "postbattle_skill");
            else
                CompleteFlow();
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
