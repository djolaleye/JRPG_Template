using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// The post-battle screens, in order.
    ///
    /// <para>Victory → Results → Level Up → Attributes → Skill Unlocked → New Skill. Every screen
    /// after Results is conditional: no level-ups skips the rest outright, and each of the others
    /// appears only when it has something to say.</para>
    ///
    /// <para><b>Skill outcomes come last, after allocation.</b> They are the final consequence of
    /// levelling.</para>
    ///
    /// <para><b>Unlocks precede the replacement decision</b>, and repeat once per skill offered —
    /// including the ones the character has no room for. Being shown what was earned and then being
    /// asked what to give up are two moments, in that order.</para>
    /// </summary>
    public enum PostBattleFlowState
    {
        Idle,
        VictorySummary,
        Results,
        LevelUpReview,
        AttributeAllocation,
        SkillUnlocked,
        SkillChoice,
        Complete,
    }

    /// <summary>
    /// One skill to announce on the unlock screen, whether or not the character can keep it.
    ///
    /// <para><see cref="RequiresChoice"/> distinguishes the two: false means it was already added,
    /// true means the skill screen is about to ask what to forget in exchange. The screen says so,
    /// rather than presenting an unavailable skill as though it had been gained.</para>
    /// </summary>
    public readonly struct SkillAnnouncement
    {
        public readonly string CharacterId;
        public readonly string SkillId;

        public readonly int AtLevel;

        public readonly bool RequiresChoice;

        public SkillAnnouncement(string characterId, string skillId, int atLevel, bool requiresChoice)
        {
            CharacterId = characterId;
            SkillId = skillId;
            AtLevel = atLevel;
            RequiresChoice = requiresChoice;
        }
    }

    /// <summary>
    /// Sequences the post-battle screens (see <see cref="PostBattleFlowState"/> for the order and why).
    /// Owns ordering only — all math lives in ProgressionService. Menus replace each other
    /// (Close current + Open next); on completion the controller closes everything, restores exploration
    /// state, and lets the service commit.
    ///
    /// <para><see cref="Continue"/> is the authority on which screen follows which, including the skips.</para>
    /// </summary>
    public sealed class PostBattleFlowController : MonoBehaviour
    {
        public static PostBattleFlowController Current { get; private set; }

        public PostBattleFlowState CurrentState { get; private set; } = PostBattleFlowState.Idle;

        private readonly List<SkillAnnouncement> _announcements = new();
        
        public int SkillAnnouncementIndex { get; private set; } = -1;

        /// The skill the unlock screen should be showing, or null when there is none.
        public SkillAnnouncement? CurrentSkillAnnouncement
            => SkillAnnouncementIndex >= 0 && SkillAnnouncementIndex < _announcements.Count
                ? _announcements[SkillAnnouncementIndex]
                : null;

        public int SkillAnnouncementCount => _announcements.Count;

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

                case PostBattleFlowState.SkillUnlocked:
                    AdvanceAfterUnlock(progression);
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

        /// <summary>
        /// Announce every skill on offer, then ask about the ones that need a decision.
        ///
        /// <para>A full skill list does not skip the announcement: the player is shown the skill
        /// that was earned before being asked what to give up for it.</para>
        /// </summary>
        private void AdvanceAfterAllocation(ProgressionService progression)
        {
            BuildAnnouncements(progression);

            if (_announcements.Count > 0)
            {
                SkillAnnouncementIndex = 0;
                Transition(PostBattleFlowState.SkillUnlocked, "postbattle_skill_unlocked");
                return;
            }

            AdvanceAfterUnlocks(progression);
        }

        private void BuildAnnouncements(ProgressionService progression)
        {
            _announcements.Clear();

            var learned = progression.LearnedSkills;
            for (int i = 0; i < learned.Count; i++)
                _announcements.Add(new SkillAnnouncement(learned[i].characterId, learned[i].skillId,
                                                         learned[i].atLevel, requiresChoice: false));

            // Offered but blocked. PendingSkillChoice records no level, so these announce without one.
            var pending = progression.PendingSkillChoices;
            for (int i = 0; i < pending.Count; i++)
                _announcements.Add(new SkillAnnouncement(pending[i].characterId, pending[i].newSkillId,
                                                         atLevel: 0, requiresChoice: true));
        }

        /// Steps to the next announcement, or past the stage entirely once they run out.
        private void AdvanceAfterUnlock(ProgressionService progression)
        {
            int next = SkillAnnouncementIndex + 1;

            if (next < _announcements.Count)
            {
                SkillAnnouncementIndex = next;

                // Same screen again with a new subject: close and reopen so the rows rebuild from
                // the moved cursor, exactly as a transition between two different screens would.
                Transition(PostBattleFlowState.SkillUnlocked, "postbattle_skill_unlocked");
                return;
            }

            SkillAnnouncementIndex = -1;
            AdvanceAfterUnlocks(progression);
        }

        private void AdvanceAfterUnlocks(ProgressionService progression)
        {
            if (progression.HasPendingSkillChoices())
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
            SkillAnnouncementIndex = -1;
            _announcements.Clear();
            _menus.CloseAll();
            Progression?.CompletePostBattleFlow();

            // The flow owns the exit back to exploration (combat no longer forces it on victory).
            AppContext.State?.SetState(new LayeredState(GameMode.Exploration, OverlayState.None, InputContext.Exploration));
            CurrentState = PostBattleFlowState.Idle;
        }
    }
}
