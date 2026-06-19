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
