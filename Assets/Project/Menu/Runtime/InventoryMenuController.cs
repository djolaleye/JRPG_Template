using System.Collections.Generic;
using JRPG.Core;
using JRPG.Services;
using JRPG.Inventory;
using JRPG.Data;

namespace JRPG.Menu
{
    public class InventoryMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            if (Context?.Services == null) return rows;
            if (!Context.Services.TryResolve<IInventoryService>(out var invSvc) || invSvc is not InventoryService inv) return rows;

            var state = AppContext.State?.Current ?? default;
            var filtered = inv.Filter(state, ContextualFilterRequest.ExplorationTab());

            for (int i = 0; i < filtered.Count; i++)
            {
                var stack = filtered[i];
                inv.Filter(state, ContextualFilterRequest.ExplorationTab()); // no-op kept for clarity
                if (!TryResolveItem(inv, stack.itemId, out var item)) continue;

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    SelectedItemId = stack.itemId
                };

                bool enabled = item.usageRule != null && item.usageRule.usableInExploration;
                var row = new RowModel
                {
                    id = stack.itemId,
                    label = string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName,
                    quantityText = "x" + stack.quantity,
                    enabled = enabled,
                    action = new UseItemAction(),
                    context = rowContext
                };
                
                rows.Add(row);
            }
            return rows;
        }

        private static bool TryResolveItem(InventoryService inv, string id, out ItemData item)
        {
            item = null;
            var data = AppContext.Data as DataRegistry;
            return data != null && data.TryGet<ItemData>(id, out item);
        }
    }
}
