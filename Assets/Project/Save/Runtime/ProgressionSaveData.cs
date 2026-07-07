using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    /// Progression contributor payload: per-character level/XP/attribute-point state as primitives
    /// and stable ids only. Level/XP here are the restore-time authority — PartyService lazily
    /// rebuilds runtime instances at level 1, and ProgressionService re-applies this data (plus
    /// growth modifiers) on top after party restore.
    [Serializable]
    public class ProgressionSaveData : SaveDataBase
    {
        public List<CharacterProgressEntry> characters = new();
    }

    [Serializable]
    public class CharacterProgressEntry
    {
        public string characterId;
        public int level;
        public int currentXp;
        public int unspentAttributePoints;
        public List<StatPointEntry> manuallyAllocatedPoints = new();
    }

    [Serializable]
    public struct StatPointEntry
    {
        /// StatType.ToString() — stored as a string so this assembly stays decoupled from JRPG.Data.
        public string statId;
        public int points;
    }
}
