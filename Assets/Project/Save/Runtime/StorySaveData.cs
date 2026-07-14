using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    /// Persistent story state: flags, completed dialogue graphs, and selected choices — primitives and
    /// stable ids only, no Unity object references.
    [Serializable]
    public class StorySaveData : SaveDataBase
    {
        public List<BoolFlagEntry> boolFlags = new();
        public List<IntFlagEntry> intFlags = new();
        public List<string> completedDialogueGraphs = new();
        public List<string> selectedChoiceIds = new();
    }

    [Serializable]
    public struct BoolFlagEntry
    {
        public string id;
        public bool value;
    }

    [Serializable]
    public struct IntFlagEntry
    {
        public string id;
        public int value;
    }
}
