using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Dialogue.UI
{
    /// Row action that selects a dialogue choice through IDialogueService. Never mutates dialogue
    /// state directly — the service owns condition evaluation and command execution.
    public sealed class ChooseDialogueAction : IMenuAction
    {
        private readonly string _choiceId;
        public ChooseDialogueAction(string choiceId) { _choiceId = choiceId; }

        public bool CanExecute(MenuContext c)
            => AppContext.Services != null && AppContext.Services.TryResolve<IDialogueService>(out var d) && d.IsDialogueActive;

        public void Execute(MenuContext c)
        {
            if (AppContext.Services != null && AppContext.Services.TryResolve<IDialogueService>(out var d))
                d.Choose(_choiceId);
        }

        public string GetDisabledReason(MenuContext c) => "Dialogue not active.";
    }
}
