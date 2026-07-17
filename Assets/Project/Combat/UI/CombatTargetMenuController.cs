using System.Collections.Generic;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Lists valid targets for the pending action (respecting the action's TargetRule via the engine).
    public sealed class CombatTargetMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            var actionId = flow?.PendingActionId;
            if (combat == null || string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(actionId)) return rows;

            foreach (var t in combat.GetValidTargets(actorId, actionId))
            {
                rows.Add(new RowModel
                {
                    id = t.combatantId,
                    label = $"{t.displayName}  HP {t.currentHP}/{t.MaxHP}",
                    enabled = true,
                    action = new ChooseCombatTargetAction(t.combatantId),
                    context = Context
                });
            }
            return rows;
        }
    }
}
