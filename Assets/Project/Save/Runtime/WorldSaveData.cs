using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    /// Persistent world progress: encounter lifecycle state, as stable ids and enum values only.
    [Serializable]
    public class WorldSaveData : SaveDataBase
    {
        public List<EncounterStateEntry> encounters = new();
    }

    [Serializable]
    public struct EncounterStateEntry
    {
        public string encounterId;
        public EncounterState state;
    }
}
