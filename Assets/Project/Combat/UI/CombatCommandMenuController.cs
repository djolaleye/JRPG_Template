using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Top-level battle command menu for the current party actor: Attack / Skill / Item / Guard / Flee.
    /// Attack, Guard, and Flee are concrete baseline actions; Skill and Item open filtered submenus.
    /// Cancel is suppressed — you can't back out of your own turn here.
    public sealed class CombatCommandMenuController : MenuController
    {
        private const string FleeActionId = "action_flee";
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

            AddFleeRow(rows, combat, actorId);
            return rows;
        }

        private void AddFleeRow(List<RowModel> rows, CombatService combat, string actorId)
        {
            var battle = combat.CurrentBattle;
            if (battle == null) return;

            EncounterData encounter = null;
            if (AppContext.Data is DataRegistry data && !string.IsNullOrEmpty(battle.encounterId))
                data.TryGet(battle.encounterId, out encounter);

            var escape = EscapeResolver.Evaluate(battle, encounter);

            rows.Add(new RowModel
            {
                id = "flee",
                label = "Flee",
                costText = escape.allowed ? $"{Mathf.RoundToInt(escape.chance * 100f)}%" : "—",
                enabled = escape.allowed && combat.CanAfford(actorId, FleeActionId),
                action = new ChooseCombatActionAction(FleeActionId),
                context = Context
            });
        }

        protected override void OnCancel(InputAction.CallbackContext ctx)
        {
            // Intentionally no-op: the top-level command menu cannot be cancelled mid-turn.
        }
    }
}
