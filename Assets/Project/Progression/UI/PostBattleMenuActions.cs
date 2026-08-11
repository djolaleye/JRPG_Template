using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// <summary>
    /// What a screen's Continue row demands before it will fire.
    ///
    /// <para>The blocking rules are per-screen, not global. <c>CanCompletePostBattleFlow</c> requires
    /// <i>everything</i> resolved — points spent AND skill decisions answered — so using it as the gate
    /// on an intermediate screen deadlocks the flow: with the skill screen last, the attributes screen
    /// would refuse to advance because of a decision the player has not been shown yet.</para>
    /// </summary>
    public enum PostBattleGate
    {
        /// No precondition — a purely informational screen.
        None,

        /// Every granted attribute point must be spent. Used by the allocation screen.
        AttributePointsSpent,

        /// Every pending skill replacement must be answered. Used by the skill screen, which is last,
        /// so this is also the point where the whole flow becomes completable.
        SkillChoicesResolved,
    }

    /// Advances the post-battle flow, subject to the owning screen's gate.
    public sealed class ContinuePostBattleAction : IMenuAction
    {
        private readonly PostBattleGate _gate;

        public ContinuePostBattleAction(PostBattleGate gate = PostBattleGate.None) { _gate = gate; }

        private static ProgressionService Progression
            => AppContext.Services != null && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        public bool CanExecute(MenuContext c)
        {
            if (PostBattleFlowController.Current == null) return false;

            var progression = Progression;
            if (progression == null) return _gate == PostBattleGate.None;

            return _gate switch
            {
                PostBattleGate.AttributePointsSpent => !progression.HasPendingAttributeAllocations(),
                PostBattleGate.SkillChoicesResolved => !progression.HasPendingSkillChoices(),
                _ => true,
            };
        }

        public void Execute(MenuContext c) => PostBattleFlowController.Current?.Continue();

        public string GetDisabledReason(MenuContext c) => _gate switch
        {
            PostBattleGate.AttributePointsSpent => "Spend all attribute points first.",
            PostBattleGate.SkillChoicesResolved => "Choose a skill to replace, or decline the new one.",
            _ => string.Empty,
        };
    }

    /// Spends one attribute point on a stat for a character via ProgressionService.
    public sealed class AllocatePointAction : IMenuAction
    {
        private readonly string _characterId;
        private readonly StatType _stat;

        public AllocatePointAction(string characterId, StatType stat)
        {
            _characterId = characterId;
            _stat = stat;
        }

        private static ProgressionService Progression
            => AppContext.Services != null && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        public bool CanExecute(MenuContext c)
        {
            var progression = Progression;
            if (progression == null) return false;
            return progression.GetProgressForCharacter(_characterId).unspentAttributePoints > 0;
        }

        public void Execute(MenuContext c) => Progression?.TryAllocateAttributePoint(_characterId, _stat);

        public string GetDisabledReason(MenuContext c) => "No unspent points for this character.";
    }

    /// Answers a pending skill-learn choice by forgetting one skill. Passing the NEW skill's id
    /// declines it instead — both are valid answers, so this single action covers the whole screen.
    public sealed class ResolveSkillChoiceAction : IMenuAction
    {
        private readonly string _characterId;
        private readonly string _discardSkillId;

        public ResolveSkillChoiceAction(string characterId, string discardSkillId)
        {
            _characterId = characterId;
            _discardSkillId = discardSkillId;
        }

        private static ProgressionService Progression
            => AppContext.Services != null && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                ? svc as ProgressionService : null;

        public bool CanExecute(MenuContext c) => Progression?.NextPendingSkillChoice() != null;

        public void Execute(MenuContext c) => Progression?.ResolveSkillChoice(_characterId, _discardSkillId);

        public string GetDisabledReason(MenuContext c) => "No skill decision is pending.";
    }
}
