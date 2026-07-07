using System.Collections.Generic;
using UnityEngine.InputSystem;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Top-level battle command menu for the current party actor: Attack / Skill / Item / Guard.
    /// Attack and Guard are concrete baseline actions; Skill and Item open filtered submenus.
    /// Cancel is suppressed — you can't back out of your own turn here.
    public sealed class CombatCommandMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId)) return rows;

            rows.Add(new RowModel
            {
                id = "attack", label = "Attack",
                enabled = combat.CanAfford(actorId, "attack_melee"),
                action = new ChooseCombatActionAction("attack_melee"),
                context = Context
            });
            rows.Add(RowModel.Simple("skill", "Skill", new OpenSubmenuAction("combat_skills"), Context));
            rows.Add(RowModel.Simple("item", "Item", new OpenSubmenuAction("combat_items"), Context));
            rows.Add(new RowModel
            {
                id = "guard", label = "Guard",
                enabled = combat.CanAfford(actorId, "guard"),
                action = new ChooseCombatActionAction("guard"),
                context = Context
            });
            return rows;
        }

        protected override void OnCancel(InputAction.CallbackContext ctx)
        {
            // Intentionally no-op: the top-level command menu cannot be cancelled mid-turn.
        }
    }
}
