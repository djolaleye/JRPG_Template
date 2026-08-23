using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    /// <summary>
    /// What every vendor has sold. Ids and counts only — the catalog itself is authored data.
    ///
    /// <para>The recorded <see cref="ShopStockEntry.catalogSignature"/> is what makes a restock
    /// automatic: when the shop's current signature no longer matches the saved one, the sold counts
    /// are stale by definition and are dropped.</para>
    /// </summary>
    [Serializable]
    public class ShopSaveData : SaveDataBase
    {
        public List<ShopStockEntry> shops = new();
    }

    [Serializable]
    public class ShopStockEntry
    {
        public string shopId;

        /// Which version of the catalog these counts belong to. See ShopService.CatalogSignature.
        public string catalogSignature;

        public List<OfferingSoldEntry> sold = new();
    }

    [Serializable]
    public struct OfferingSoldEntry
    {
        public string offeringId;

        /// Output units sold so far, matching how ShopOfferingData.stock is counted.
        public int soldUnits;
    }
}
