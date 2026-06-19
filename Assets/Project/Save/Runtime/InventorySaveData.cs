using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public struct InventoryStackDto
    {
        public string itemId;
        public int quantity;
        public string entryId;
    }

    [Serializable]
    public class InventorySaveData : SaveDataBase
    {
        public List<InventoryStackDto> stacks = new();
    }

    [Serializable]
    public sealed class InventoryPayload : SaveDataBase
    {
        public InventorySaveData data = new();
    }
}
