using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Lists Item-category combat actions whose item is authored usable-in-combat, with quantities.
    /// Eligibility ("may I use this here?") and ownership ("do I have any?") are separate questions:
    /// the first decides whether the row exists at all, the second only whether it is selectable.
    public sealed class CombatItemListController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var flow = CombatFlowController.Current;
            var combat = flow?.Combat;
            var actorId = flow?.CurrentActorId;
            if (combat == null || string.IsNullOrEmpty(actorId) || Context?.Services == null) return rows;

            Context.Services.TryResolve<IInventoryService>(out var inv);
            var data = AppContext.Data as DataRegistry;

            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Item) continue;

                string itemId = CombatRowFormat.FirstItemCostId(action);
                if (string.IsNullOrEmpty(itemId)) continue;

                // Checked before the row is built, and fails closed: an unresolvable registry or item
                // hides the row rather than listing everything. CombatActionResolver enforces the same
                // rule at submission, so a row that slips through still cannot execute.
                if (!ItemCombatRules.IsUsableInCombat(data, itemId)) continue;

                int qty = inv?.GetQuantity(itemId) ?? 0;
                rows.Add(new RowModel
                {
                    id = action.Id,
                    label = CombatRowFormat.Label(action),
                    quantityText = "x" + qty,
                    enabled = qty > 0 && combat.CanAfford(actorId, action.Id),
                    action = new ChooseCombatActionAction(action.Id),
                    context = Context
                });
            }
            return rows;
        }
    }
}
