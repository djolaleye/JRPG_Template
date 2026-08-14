using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    /// Persistent chest ledger: stable chest ids and enum values only. Only chests that have left
    /// their authored default are recorded — see ChestStateService.
    [Serializable]
    public class ChestSaveData : SaveDataBase
    {
        public List<ChestStateEntry> entries = new();
    }

    [Serializable]
    public struct ChestStateEntry
    {
        public string chestId;
        public ChestOpenedState state;
    }
}
