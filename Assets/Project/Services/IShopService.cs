using System.Collections.Generic;

namespace JRPG.Services
{
    /// <summary>
    /// Buying and selling. The single authority on whether a transaction may happen and the only place
    /// one is carried out.
    ///
    /// <para><b>Every purchase is atomic.</b> Currency, materials and inventory room are all checked
    /// before anything is taken, so a refused transaction leaves the player exactly as they were.</para>
    ///
    /// <para>Ids only: the authored <c>ShopData</c> shape lives
    /// in JRPG.Data, which this assembly does not reference. Screens that need the catalog itself
    /// resolve the concrete service.</para>
    /// </summary>
    public interface IShopService
    {
        /// <summary>Whether the shop exists and its story gate is satisfied.</summary>
        bool IsShopAvailable(string shopId);

        /// <summary>
        /// Offering ids currently stocked, in authored order. <paramref name="sectionId"/> narrows to
        /// one section of a specialty shop; null or empty returns the whole catalog.
        /// </summary>
        IReadOnlyList<string> GetAvailableOfferingIds(string shopId, string sectionId = null);

        bool CanBuy(string shopId, string offeringId, int quantity, out string reason);

        /// <summary>Buys <paramref name="quantity"/> lots. Returns false and changes nothing when refused.</summary>
        bool TryBuy(string shopId, string offeringId, int quantity);

        /// <summary>
        /// Largest quantity the player could buy right now, limited by money, materials and inventory
        /// room. Zero when they cannot buy one — the quantity picker's upper bound.
        /// [TODO: limited by shop's remaining stock of that offering]
        /// </summary>
        int MaxPurchasable(string shopId, string offeringId);

        bool CanSell(string shopId, string itemId, int quantity, out string reason);

        /// <summary>Sells <paramref name="quantity"/> units. Returns false and changes nothing when refused.</summary>
        bool TrySell(string shopId, string itemId, int quantity);

        /// <summary>What this shop pays for that many units. Zero when it will not buy them at all.</summary>
        int GetSellValue(string shopId, string itemId, int quantity);
    }
}
