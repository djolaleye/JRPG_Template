using System;

namespace JRPG.Data
{
    public enum CombatCostType
    {
        None,
        MP,
        SP,
        HP,
        Item
    }

    /// Plain cost data interpreted by the combat resolver. Item costs are paid through
    /// IInventoryService at resolution time.
    [Serializable]
    public struct CombatCost
    {
        public CombatCostType type;
        public int costAmount;
        public string itemId;
        public int quantity;
    }
}
