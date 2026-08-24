using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Quest.UI
{
    /// <summary>
    /// Base for the quest verbs. Each one resolves the service and hands it a quest id — no quest
    /// state is read or written on the screen itself.
    /// </summary>
    public abstract class QuestActionBase : IMenuAction
    {
        protected readonly string QuestId;

        protected QuestActionBase(string questId)
        {
            QuestId = questId;
        }

        protected static IQuestService Resolve(MenuContext context)
            => context?.Services != null && context.Services.TryResolve<IQuestService>(out var quests) ? quests : null;

        public abstract bool CanExecute(MenuContext context);
        public abstract void Execute(MenuContext context);
        public abstract string GetDisabledReason(MenuContext context);
    }

    /// Takes on a discovered quest: Available → Active.
    public sealed class AcceptQuestAction : QuestActionBase
    {
        public AcceptQuestAction(string questId) : base(questId) { }

        public override bool CanExecute(MenuContext context)
            => Resolve(context)?.GetState(QuestId) == QuestState.Available;

        public override void Execute(MenuContext context) => Resolve(context)?.TryAccept(QuestId);

        public override string GetDisabledReason(MenuContext context) => "Not available to accept.";
    }

    /// <summary>
    /// Hands in an Active quest whose objectives are met. Only reachable for a quest authored
    /// <c>autoComplete = false</c> — anything else has already completed itself by the time a row
    /// could be pressed.
    /// </summary>
    public sealed class TurnInQuestAction : QuestActionBase
    {
        private readonly bool _objectivesMet;

        public TurnInQuestAction(string questId, bool objectivesMet) : base(questId)
        {
            _objectivesMet = objectivesMet;
        }

        public override bool CanExecute(MenuContext context)
            => _objectivesMet && Resolve(context)?.GetState(QuestId) == QuestState.Active;

        public override void Execute(MenuContext context) => Resolve(context)?.TryComplete(QuestId);

        public override string GetDisabledReason(MenuContext context) => "Objectives are not finished.";
    }

    /// <summary>
    /// Drops an optional quest back to Available. The screen routes this through a confirmation
    /// prompt: objective progress is discarded, and the row alone does not say so.
    /// </summary>
    public sealed class AbandonQuestAction : QuestActionBase
    {
        public AbandonQuestAction(string questId) : base(questId) { }

        public override bool CanExecute(MenuContext context)
            => Resolve(context)?.GetState(QuestId) == QuestState.Active;

        public override void Execute(MenuContext context) => Resolve(context)?.TryAbandon(QuestId);

        public override string GetDisabledReason(MenuContext context) => "Only an active quest can be abandoned.";
    }
}
