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

            var actor = combat.CurrentBattle?.FindCombatant(actorId);

            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Skill) continue;

                // A cooling-down skill stays visible but disabled, showing the turns remaining, so the
                // player can see what is coming back rather than watching rows vanish.
                int cooldown = actor?.GetCooldown(action.Id) ?? 0;
                string cost = CombatRowFormat.CostText(action);
                if (cooldown > 0) cost = string.IsNullOrEmpty(cost) ? $"CD {cooldown}" : $"{cost}   CD {cooldown}";

                rows.Add(new RowModel
                {
                    id = action.Id,
                    label = CombatRowFormat.Label(action),
                    costText = cost,
                    enabled = combat.CanAfford(actorId, action.Id),
                    action = new ChooseCombatActionAction(action.Id),
                    context = Context
                });
            }
            return rows;
        }
    }
}
