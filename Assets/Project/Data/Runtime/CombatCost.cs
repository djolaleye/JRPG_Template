using System;

namespace JRPG.Data
{
    public enum CombatCostType
    {
        None,
        MP,
        Item
    }

    /// Plain cost data interpreted by the combat resolver (no behaviour on the asset, mirroring
    /// ItemData.linkedEffects / EquipmentData.statModifiers). Item costs are paid through
    /// IInventoryService at resolution time.
    [Serializable]
    public struct CombatCost
    {
        public CombatCostType type;
        public int mpAmount;
        public string itemId;
        public int quantity;
    }
}
