using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Menu;
using JRPG.Services;

namespace JRPG.Combat.UI
{
    /// Lists Item-category combat actions, filtered to combat-usable items, with quantities.
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

            // Set of item ids that pass the combat-usable filter (usableInCombat consumables).
            var combatUsable = new HashSet<string>();
            if (inv is InventoryService invc)
            {
                var state = AppContext.State?.Current ?? default;
                foreach (var stack in invc.Filter(state, ContextualFilterRequest.CombatItems()))
                    combatUsable.Add(stack.itemId);
            }

            foreach (var action in combat.GetAvailableActions(actorId))
            {
                if (action.category != CombatActionCategory.Item) continue;
                string itemId = CombatRowFormat.FirstItemCostId(action);
                if (string.IsNullOrEmpty(itemId)) continue;
                if (combatUsable.Count > 0 && !combatUsable.Contains(itemId)) continue;

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
