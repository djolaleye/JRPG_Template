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
        int GetQuantity(string itemId);
        bool Has(string itemId, int n = 1);
    }
}
