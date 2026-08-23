using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Data;
using JRPG.Economy;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The catalog: one row per stocked offering, with its price, and a quantity the player sets before
    /// confirming.
    ///
    /// <para><b>One screen serves both shop shapes.</b> A basic shop opens it directly and it lists the
    /// whole catalog; a specialty shop opens it from a section row and the payload narrows it. The only
    /// difference is the filter, so there is one controller.</para>
    ///
    /// <para><b>Rows state what they cost and why they are refused.</b> Price, material requirements and
    /// the disabled reason all come from <see cref="IShopService"/>, so a row is buyable exactly when
    /// the purchase would succeed.</para>
    /// </summary>
    public class ShopBuyMenuController : ShopMenuControllerBase
    {
        private const string SoldOutText = "Sold Out";
        private const string SoldOutReason = "They're out of stock.";

        /// Offerings backing the current rows, index-aligned with them.
        private readonly List<ShopOfferingData> _rowOfferings = new();

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            _rowOfferings.Clear();

            var rows = new List<RowModel>();
            var shops = Shops;
            var shopId = ShopId;

            if (shops == null || string.IsNullOrEmpty(shopId))
            {
                rows.Add(RowModel.Simple("no_shop", "Nothing for sale.", null, Context, enabled: false));
                return rows;
            }

            var offerings = shops.GetAvailableOfferings(shopId, SectionId);
            if (offerings.Count == 0)
            {
                rows.Add(RowModel.Simple("empty", "The shelves are bare.", null, Context, enabled: false));
                ShowTotal("Total", 0);
                return rows;
            }

            // The stepper is bound to the focused row, so its ceiling is that offering's limit.
            var focused = FocusedOffering(offerings);
            ConfigureQuantity(focused != null ? shops.MaxPurchasable(shopId, focused.offeringId) : 1);

            int quantity = SelectedQuantity();

            for (int i = 0; i < offerings.Count; i++)
            {
                var offering = offerings[i];
                var action = new BuyOfferingAction(shopId, offering.offeringId, quantity);

                // A sold-out row stays on the shelf, priced "Sold Out".
                bool soldOut = shops.IsSoldOut(shopId, offering.offeringId);
                bool allowed = !soldOut && action.CanExecute(Context);

                rows.Add(new RowModel
                {
                    id = offering.offeringId,
                    label = OfferingLabel(offering),
                    icon = OutputItem(offering)?.icon,
                    costText = soldOut ? SoldOutText : CostText(offering),
                    quantityText = OwnedText(offering),
                    auxText = StockText(shops, shopId, offering, soldOut),
                    enabled = allowed,
                    action = action,
                    context = Context,
                    disabledReason = allowed ? null : (soldOut ? SoldOutReason : action.GetDisabledReason(Context)),
                });

                _rowOfferings.Add(offering);
            }

            ShowTotal("Total", focused != null ? focused.currencyCost * quantity : 0);

            return rows;
        }

        /// <summary>
        /// Confirms before spending. The player has already chosen a quantity, so the modal states what
        /// leaves the wallet.
        /// </summary>
        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            var offering = index >= 0 && index < _rowOfferings.Count ? _rowOfferings[index] : null;

            if (offering == null || confirmPrompt == null)
            {
                base.OnSubmit(ctx);
                return;
            }

            int quantity = SelectedQuantity();
            var action = new BuyOfferingAction(ShopId, offering.offeringId, quantity);
            if (!action.CanExecute(Context)) { base.OnSubmit(ctx); return; }

            int price = offering.currencyCost * quantity;
            var question = price > 0
                ? $"Buy {OfferingLabel(offering)} ×{quantity} for {Money(price)}?"
                : $"Trade for {OfferingLabel(offering)} ×{quantity}?";

            confirmPrompt.Ask(question, "Buy", "Cancel", () =>
            {
                action.Execute(Context);
                RebuildAndFocus();
            });
        }

        // ---- Row text ------------------------------------------------------------------------------

        private ShopOfferingData FocusedOffering(IReadOnlyList<ShopOfferingData> offerings)
        {
            int index = HighlightedIndex;

            return index >= 0 && index < offerings.Count ? offerings[index] : offerings[0];
        }

        private ItemData OutputItem(ShopOfferingData offering)
        {
            var data = JRPG.Core.AppContext.Data as DataRegistry;

            return data != null && data.TryGet<ItemData>(offering.outputItemId, out var item) ? item : null;
        }

        private string OfferingLabel(ShopOfferingData offering)
        {
            var item = OutputItem(offering);
            var name = item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : offering.outputItemId;

            return offering.outputQuantity > 1 ? $"{name} ×{offering.outputQuantity}" : name;
        }

        /// Price, materials, or both — whatever this offering asks for.
        private string CostText(ShopOfferingData offering)
        {
            var parts = new StringBuilder();

            if (offering.currencyCost > 0) parts.Append(Money(offering.currencyCost));

            if (offering.HasMaterialCosts)
            {
                for (int i = 0; i < offering.materialCosts.Count; i++)
                {
                    var cost = offering.materialCosts[i];
                    if (string.IsNullOrEmpty(cost.itemId)) continue;

                    if (parts.Length > 0) parts.Append(" + ");
                    parts.Append($"{MaterialName(cost.itemId)} ×{Mathf.Max(1, cost.quantity)}");
                }
            }

            return parts.Length > 0 ? parts.ToString() : "Free";
        }

        /// <summary>
        /// "N left" for a row that can run out, so the player can see a supply shrinking before it is
        /// gone.
        /// </summary>
        private static string StockText(ShopService shops, string shopId, ShopOfferingData offering, bool soldOut)
        {
            if (soldOut || !offering.HasLimitedStock) return null;

            int remaining = shops.RemainingStock(shopId, offering.offeringId);

            return remaining >= 0 ? $"{remaining} left" : null;
        }

        /// "Have: N" — quantity the player already carries of this item.
        private string OwnedText(ShopOfferingData offering)
        {
            if (Context?.Services == null || !Context.Services.TryResolve<IInventoryService>(out var inv)) return null;

            return $"Have {inv.GetQuantity(offering.outputItemId)}";
        }

        private string MaterialName(string itemId)
        {
            var data = JRPG.Core.AppContext.Data as DataRegistry;

            if (data != null && data.TryGet<ItemData>(itemId, out var item) && item != null)
                return string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

            return itemId;
        }

        // ---- Detail panel --------------------------------------------------------------------------

        protected override void OnHighlightChanged(int index, RowModel model)
        {
            // The stepper's ceiling belongs to the focused offering, so a focus change is also a
            // quantity-range change.
            RebuildQuantityForFocus(index);

            if (detailPanel == null) return;

            var offering = index >= 0 && index < _rowOfferings.Count ? _rowOfferings[index] : null;
            if (offering == null) { detailPanel.Clear(); return; }

            var item = OutputItem(offering);
            var shops = Shops;
            bool soldOut = shops != null && shops.IsSoldOut(ShopId, offering.offeringId);

            var footer = $"{(soldOut ? SoldOutText : CostText(offering))}   ·   {OwnedText(offering)}";
            var stock = shops != null ? StockText(shops, ShopId, offering, soldOut) : null;
            if (!string.IsNullOrEmpty(stock)) footer += $"   ·   {stock}";

            detailPanel.ShowDetail(OfferingLabel(offering),
                                   item != null && !string.IsNullOrEmpty(item.description)
                                       ? item.description
                                       : "No description.",
                                   item != null ? item.icon : null,
                                   footer);
        }

        private void RebuildQuantityForFocus(int index)
        {
            var shops = Shops;
            if (shops == null || index < 0 || index >= _rowOfferings.Count) return;

            ConfigureQuantity(shops.MaxPurchasable(ShopId, _rowOfferings[index].offeringId));
            ShowTotal("Total", _rowOfferings[index].currencyCost * SelectedQuantity());
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Buy"),
            new InputPrompt("PageL", "Fewer"),
            new InputPrompt("PageR", "More"),
            new InputPrompt("Cancel", "Back"),
        };
    }
}
