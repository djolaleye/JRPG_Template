using JRPG.Data;

namespace JRPG.Inventory
{
    public readonly struct ItemUsed
    {
        public readonly string ItemId;
        public readonly string TargetInstanceId;
        public ItemUsed(string itemId, string targetInstanceId)
        {
            ItemId = itemId;
            TargetInstanceId = targetInstanceId;
        }
    }

    /// <summary>
    /// A stack was destroyed by the player rather than spent, sold or used. Carries the amount removed
    /// so a notice can say what was thrown away without re-querying a stack that may now be gone.
    /// </summary>
    public readonly struct ItemDiscarded
    {
        public readonly string ItemId;
        public readonly int Quantity;

        public ItemDiscarded(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }

    public readonly struct EquipmentChanged
    {
        public readonly string CharInstanceId;
        public readonly EquipmentSlot Slot;
        /// <summary>Item id newly equipped in the slot; null if the slot was just cleared.</summary>
        public readonly string EquipItemId;

        public EquipmentChanged(string charInstanceId, EquipmentSlot slot, string equipItemId)
        {
            CharInstanceId = charInstanceId;
            Slot = slot;
            EquipItemId = equipItemId;
        }
    }
}
