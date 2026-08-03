using UnityEngine;
using JRPG.Core;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Replays the encounter that was just lost, restoring the party to the state it entered with.
    public sealed class RetryBattleAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
        {
            var flow = DefeatFlowController.Current;
            return flow != null && flow.CanRetry;
        }

        public void Execute(MenuContext c)
        {
            // Close the defeat screen first: the retry starts a battle synchronously, and the combat UI
            // must open its command menu onto a clean stack.
            c.Menus?.CloseAll();
            DefeatFlowController.Current?.RequestRetry();
        }

        public string GetDisabledReason(MenuContext c) => "This battle cannot be retried.";
    }

    /// Abandons the fight and hands control back to exploration.
    public sealed class ReturnToExplorationAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => true;

        public void Execute(MenuContext c)
        {
            c.Menus?.CloseAll();
            DefeatFlowController.Current?.RequestReturnToExploration();
        }

        public string GetDisabledReason(MenuContext c) => "";
    }
}
