using System;

namespace JRPG.Inventory
{
    [Serializable]
    public class InventoryStack
    {
        public string itemId;
        public int quantity;
        /// <summary>
        /// Reserved for unique items / unique equipment instances (e.g. equipment with rolled mods).
        /// Empty for the standard stackable case; the prototype keeps it empty everywhere.
        /// </summary>
        public string entryId;

        public InventoryStack() { }
        public InventoryStack(string itemId, int quantity, string entryId = "")
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.entryId = entryId;
        }
    }
}
