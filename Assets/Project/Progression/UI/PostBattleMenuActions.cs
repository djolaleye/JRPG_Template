using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Progression.UI
{
    /// Advances the post-battle flow. On the allocation screen it is gated by the blocking rule:
    /// all attribute points must be spent before the flow can finish.
    public sealed class ContinuePostBattleAction : IMenuAction
    {
        private readonly bool _requiresCompletable;
        public ContinuePostBattleAction(bool requiresCompletable = false) { _requiresCompletable = requiresCompletable; }

        public bool CanExecute(MenuContext c)
        {
            if (PostBattleFlowController.Current == null) return false;
            if (!_requiresCompletable) return true;
            return AppContext.Services != null
                && AppContext.Services.TryResolve<IProgressionService>(out var svc)
                && svc.CanCompletePostBattleFlow();
        }

        public void Execute(MenuContext c) => PostBattleFlowController.Current?.Continue();

        public string GetDisabledReason(MenuContext c) => "Unspent attribute points remain.";
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
