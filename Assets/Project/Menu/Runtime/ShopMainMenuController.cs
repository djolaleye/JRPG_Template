using System.Collections.Generic;
using JRPG.Data;
using JRPG.Economy;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// A specialty vendor's front desk: one row per section, then Sell, then Leave.
    ///
    /// <para><b>A section whose stock is all story-locked is disabled, not hidden.</b> The player can
    /// see the vendor deals in accessories even before any are on the shelf, serving
    /// as a tell to the player to "come back later".</para>
    /// </summary>
    public sealed class ShopMainMenuController : MenuController
    {
        private const string BuyMenuId = "shop_buy";
        private const string SellMenuId = "shop_sell";

        private string ShopId => ShopMenuPayload.From(Context)?.ShopId;

        private ShopService Shops
            => Context?.Services != null && Context.Services.TryResolve<IShopService>(out var svc)
                ? svc as ShopService
                : null;

        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            var shops = Shops;
            var shopId = ShopId;
            var shop = shops?.GetShop(shopId);

            if (shop == null)
            {
                rows.Add(RowModel.Simple("no_shop", "This shop is closed.", null, Context, enabled: false));
                rows.Add(RowModel.Simple("leave", "Leave", new CloseAllAction(), Context));
                return rows;
            }

            var sections = shops.GetSections(shopId);

            if (sections.Count == 0)
            {
                // A specialty shop authored without sections still has a catalog; show it as one row
                rows.Add(SectionRow("catalog", "Buy", shopId, null, shops));
            }
            else
            {
                for (int i = 0; i < sections.Count; i++)
                    rows.Add(SectionRow(sections[i].sectionId, sections[i].displayName,
                                        shopId, sections[i].sectionId, shops));
            }

            if (shop.BuysFromPlayer)
            {
                rows.Add(RowModel.Simple("sell", "Sell",
                                         new OpenShopScreenAction(SellMenuId, shopId, null, "No sell screen yet."),
                                         Context));
            }

            rows.Add(RowModel.Simple("leave", "Leave", new CloseAllAction(), Context));

            return rows;
        }

        /// <summary>
        /// One section row, enabled only when it currently has something to sell — with the count as
        /// its aux text so an empty section reads as empty rather than broken.
        /// </summary>
        private RowModel SectionRow(string rowId, string label, string shopId, string sectionId, ShopService shops)
        {
            int stocked = shops.GetAvailableOfferingIds(shopId, sectionId).Count;
            var action = new OpenShopScreenAction(BuyMenuId, shopId, sectionId, "No buy screen yet.");

            return new RowModel
            {
                id = rowId,
                label = label,
                auxText = stocked > 0 ? $"{stocked}" : null,
                enabled = stocked > 0 && action.CanExecute(Context),
                action = action,
                context = Context,
                disabledReason = stocked > 0 ? action.GetDisabledReason(Context) : "Nothing in stock right now.",
            };
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Leave"),
        };
    }
}
