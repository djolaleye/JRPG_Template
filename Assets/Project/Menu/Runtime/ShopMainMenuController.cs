using System.Collections.Generic;
using JRPG.Data;
using JRPG.Economy;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// A specialty vendor's front desk: one row per section, then Sell, then Leave.
    ///
    /// <para><b>A section whose stock is entirely story-locked is not shown at all.</b> The desk lists
    /// what this vendor deals in today. A section appears the moment its gate opens.</para>
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

            // GetSections already drops sections with nothing available, so an empty result means either
            // an unsectioned shop or one whose whole catalog is still locked. The flat catalog row tells
            // those apart: it is shown only when it has something in it.
            var sections = shops.GetSections(shopId);

            if (sections.Count == 0)
            {
                if (shops.GetAvailableOfferingIds(shopId, null).Count > 0)
                    rows.Add(SectionRow("catalog", "Buy", shopId, null, shops));
                else
                    rows.Add(RowModel.Simple("bare", "Nothing on the shelves today.", null, Context, enabled: false));
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
        /// One section row, with its line count as aux text. Every row reaching here has something in
        /// it — an empty section was filtered out upstream — so the only thing that can disable it is a
        /// missing buy screen.
        /// </summary>
        private RowModel SectionRow(string rowId, string label, string shopId, string sectionId, ShopService shops)
        {
            int stocked = shops.GetAvailableOfferingIds(shopId, sectionId).Count;
            var action = new OpenShopScreenAction(BuyMenuId, shopId, sectionId, "No buy screen yet.");

            return new RowModel
            {
                id = rowId,
                label = label,
                auxText = $"{stocked}",
                enabled = action.CanExecute(Context),
                action = action,
                context = Context,
                disabledReason = action.GetDisabledReason(Context),
            };
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Leave"),
        };
    }
}
