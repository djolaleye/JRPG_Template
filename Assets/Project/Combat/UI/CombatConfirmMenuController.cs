using System.Collections.Generic;
using System.Text;
using JRPG.Menu;

namespace JRPG.Combat.UI
{
    /// Final step before submission: a read-only summary row (action, costs, target names) followed by
    /// Confirm / Cancel. Cancel is the framework's Close, so it pops back to the previous menu — target
    /// selection, or the command menu for self-target actions — with the pending action intact.
    public sealed class CombatConfirmMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            var actionId = flow?.PendingActionId;

            if (combat == null || string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(actionId)) return rows;
            if (!CombatRowFormat.TryGetAction(actionId, out var action)) return rows;

            string targets = TargetNames(combat, flow.PendingTargetIds);

            // Disabled so the framework's disabled-row skipping lands initial focus on Confirm.
            rows.Add(new RowModel
            {
                id = "summary",
                label = string.IsNullOrEmpty(targets)
                    ? CombatRowFormat.Label(action)
                    : $"{CombatRowFormat.Label(action)} → {targets}",
                costText = CombatRowFormat.CostText(action),
                enabled = false,
                context = Context
            });

            rows.Add(RowModel.Simple("confirm", "Confirm", new ConfirmCombatActionAction(), Context));
            rows.Add(RowModel.Simple("cancel", "Cancel", new CloseMenuAction(), Context));
            return rows;
        }

        private static string TargetNames(CombatService combat, IReadOnlyList<string> targetIds)
        {
            var battle = combat.CurrentBattle;
            if (battle == null || targetIds == null || targetIds.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < targetIds.Count; i++)
            {
                var t = battle.FindCombatant(targetIds[i]);
                if (t == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(t.displayName);
            }
            return sb.ToString();
        }
    }
}
