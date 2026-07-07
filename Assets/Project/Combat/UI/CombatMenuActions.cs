using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Chooses a combat action for the current actor. Self-target actions (e.g. Guard) submit
    /// immediately; targeted actions open the target selector. Never mutates combat directly — it
    /// routes through CombatService (which the menu framework requires).
    public sealed class ChooseCombatActionAction : IMenuAction
    {
        private readonly string _actionId;
        public ChooseCombatActionAction(string actionId) { _actionId = actionId; }

        public bool CanExecute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return false;
            return combat.CanAfford(actorId, _actionId);
        }

        public void Execute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return;

            flow.PendingActionId = _actionId;

            if (IsSelfTarget(_actionId))
            {
                combat.SubmitAction(actorId, _actionId, null); // Self mode resolves target = actor.
                flow.PendingActionId = null;
                c.Menus?.CloseAll();
            }
            else
            {
                c.Menus?.Open("combat_target", null);
            }
        }

        public string GetDisabledReason(MenuContext c) => "Action unavailable or unaffordable.";

        private static bool IsSelfTarget(string actionId)
        {
            return AppContext.Data is DataRegistry data
                && data.TryGet<CombatActionData>(actionId, out var action)
                && action.targetRule != null
                && action.targetRule.selectionMode == TargetSelectionMode.Self;
        }
    }

    /// Submits the pending action (chosen in the command/skill/item menu) against the selected target,
    /// then closes the combat menus so the turn loop can continue.
    public sealed class SubmitCombatTargetAction : IMenuAction
    {
        private readonly string _targetCombatantId;
        public SubmitCombatTargetAction(string targetCombatantId) { _targetCombatantId = targetCombatantId; }

        public bool CanExecute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            return flow?.Combat != null
                && !string.IsNullOrEmpty(flow.CurrentActorId)
                && !string.IsNullOrEmpty(flow.PendingActionId)
                && !string.IsNullOrEmpty(_targetCombatantId);
        }

        public void Execute(MenuContext c)
        {
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            if (combat == null) return;

            combat.SubmitAction(flow.CurrentActorId, flow.PendingActionId, new List<string> { _targetCombatantId });
            flow.PendingActionId = null;
            c.Menus?.CloseAll();
        }

        public string GetDisabledReason(MenuContext c) => "No pending action or target.";
    }
}
