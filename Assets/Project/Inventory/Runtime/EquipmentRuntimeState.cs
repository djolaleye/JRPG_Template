using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Inventory
{
    public sealed class EquipmentRuntimeState
    {
        public readonly Dictionary<EquipmentSlot, string> slotToItemId = new();
    }
}
