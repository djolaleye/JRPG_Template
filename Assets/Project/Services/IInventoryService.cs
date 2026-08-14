namespace JRPG.Services
{
    /// <summary>
    /// Lean cross-assembly surface: primitive APIs only. Richer queries that return
    /// InventoryStack / use ItemCategory live on IInventoryQueries in JRPG.Inventory.
    /// </summary>
    public interface IInventoryService
    {
        int Add(string itemId, int quantity);          // returns overflow that did not fit
        int Remove(string itemId, int quantity);       // returns amount removed

        /// <summary>
        /// How many more of this item would fit. The preflight for any transaction that must not
        /// consume a cost (a key, currency, materials) unless the whole output can be received.
        /// </summary>
        int RoomFor(string itemId);

        int GetQuantity(string itemId);
        bool Has(string itemId, int n = 1);
    }
}
