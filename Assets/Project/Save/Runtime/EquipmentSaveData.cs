using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Save
{
    [Serializable]
    public struct SlotEntry
    {
        public EquipmentSlot slot;
        public string itemId;
    }

    [Serializable]
    public class CharacterEquipEntry
    {
        public string charInstanceId;
        public List<SlotEntry> slots = new();
    }

    [Serializable]
    public class EquipmentSaveData : SaveDataBase
    {
        public List<CharacterEquipEntry> entries = new();
    }

    [Serializable]
    public sealed class EquipmentPayload : SaveDataBase
    {
        public EquipmentSaveData data = new();
    }
}
