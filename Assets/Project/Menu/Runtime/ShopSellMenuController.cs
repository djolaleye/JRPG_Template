using System.Collections.Generic;
using UnityEngine.InputSystem;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Selling: the player's own stock, priced by this vendor.
    ///
    /// <para><b>Everything sellable is listed, refusals included.</b> A key item the shop will not take
    /// is shown greyed with the reason rather than hidden, so the player can see the vendor's limits
    /// instead of wondering where an item went.</para>
    /// </summary>
    public sealed class ShopSellMenuController : ShopMenuControllerBase
    {
        /// Items backing the current rows, index-aligned with them.
        private readonly List<ItemData> _rowItems = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowItems.Clear();

            var rows = new List<RowModel>();
            var shops = Shops;
            var shopId = ShopId;

            if (shops == null || string.IsNullOrEmpty(shopId)
                || Context?.Services == null
                || !Context.Services.TryResolve<IInventoryService>(out var invSvc)
                || invSvc is not InventoryService inventory)
            {
                rows.Add(RowModel.Simple("no_shop", "They aren't buying.", null, Context, enabled: false));
                return rows;
            }

            var data = AppContext.Data as DataRegistry;
            var state = AppContext.State?.Current ?? default;
            var stacks = inventory.Filter(state, ContextualFilterRequest.ExplorationTab(null));

            if (stacks.Count == 0)
            {
                rows.Add(RowModel.Simple("empty", "You have nothing to sell.", null, Context, enabled: false));
                ShowTotal("Total", 0);
                return rows;
            }

            var focusedItemId = FocusedItemId(stacks);
            ConfigureQuantity(focusedItemId != null ? inventory.GetQuantity(focusedItemId) : 1);

            int quantity = SelectedQuantity();

            for (int i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (data == null || !data.TryGet<ItemData>(stack.itemId, out var item) || item == null) continue;

                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    SelectedItemId = stack.itemId,
                    Payload = Context.Payload,
                };

                // Price the amount the row would sell
                int sellable = UnityEngine.Mathf.Min(quantity, stack.quantity);
                var action = new SellItemAction(shopId, sellable);
                bool allowed = action.CanExecute(rowContext);

                rows.Add(new RowModel
                {
                    id = stack.itemId,
                    label = ItemName(item),
                    icon = item.icon,
                    quantityText = $"× {stack.quantity}",
                    costText = allowed ? Money(shops.GetSellValue(shopId, stack.itemId, sellable)) : null,
                    enabled = allowed,
                    action = action,
                    context = rowContext,
                    disabledReason = allowed ? null : action.GetDisabledReason(rowContext),
                });

                _rowItems.Add(item);
            }

            ShowTotal("Total", focusedItemId != null ? shops.GetSellValue(shopId, focusedItemId, quantity) : 0);

            return rows;
        }

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            var item = index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;

            if (item == null || confirmPrompt == null)
            {
                base.OnSubmit(ctx);
                return;
            }

            var shops = Shops;
            int quantity = SelectedQuantity();

            var rowContext = new MenuContext
            {
                Services = Context.Services,
                Menus = Context.Menus,
                SelectedItemId = item.Id,
                Payload = Context.Payload,
            };

            var action = new SellItemAction(ShopId, quantity);
            if (!action.CanExecute(rowContext)) { base.OnSubmit(ctx); return; }

            int payment = shops.GetSellValue(ShopId, item.Id, quantity);

            confirmPrompt.Ask($"Sell {ItemName(item)} ×{quantity} for {Money(payment)}?",
                              "Sell", "Cancel", () =>
                              {
                                  action.Execute(rowContext);
                                  RebuildAndFocus();
                              });
        }

        // ---- Presentation --------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            var shops = Shops;
            var item = index >= 0 && index < _rowItems.Count ? _rowItems[index] : null;

            if (item != null && Context?.Services != null
                && Context.Services.TryResolve<IInventoryService>(out var inv))
            {
                ConfigureQuantity(inv.GetQuantity(item.Id));
                if (shops != null) ShowTotal("Total", shops.GetSellValue(ShopId, item.Id, SelectedQuantity()));
            }

            if (detailPanel == null) return;

            if (item == null) { detailPanel.Clear(); return; }

            int unit = shops != null ? shops.GetSellValue(ShopId, item.Id, 1) : 0;

            detailPanel.ShowDetail(ItemName(item),
                                   string.IsNullOrEmpty(item.description) ? "No description." : item.description,
                                   item.icon,
                                   unit > 0 ? $"They pay {Money(unit)} each" : "They won't buy this");
        }

        private string FocusedItemId(IReadOnlyList<InventoryStack> stacks)
        {
            int index = HighlightedIndex;
            if (index >= 0 && index < _rowItems.Count) return _rowItems[index].Id;

            return stacks.Count > 0 ? stacks[0].itemId : null;
        }

        private static string ItemName(ItemData item)
            => string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Sell"),
            new InputPrompt("PageL", "Fewer"),
            new InputPrompt("PageR", "More"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
