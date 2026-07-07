using System.Collections.Generic;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Lists the current actor's Skill-category actions, greying out unaffordable ones.
    public sealed class CombatSkillListController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return rows;

            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Skill) continue;
                rows.Add(new RowModel
                {
                    id = action.Id,
                    label = CombatRowFormat.Label(action),
                    costText = CombatRowFormat.CostText(action),
                    enabled = combat.CanAfford(actorId, action.Id),
                    action = new ChooseCombatActionAction(action.Id),
                    context = Context
                });
            }
            return rows;
        }
    }
}
