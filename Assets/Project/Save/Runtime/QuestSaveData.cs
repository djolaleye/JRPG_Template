using System;
using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public struct QuestObjectiveSaveEntry
    {
        public string objectiveId;
        public int currentAmount;
        public bool complete;
    }

    [Serializable]
    public struct QuestSaveEntry
    {
        public string questId;
        public QuestState state;
        public List<QuestObjectiveSaveEntry> objectives;
        public bool rewardsApplied;
    }

    /// One character's bond standing. Progress is cumulative and never resets, so it is saved
    /// alongside the level rather than derived from it.
    [Serializable]
    public struct BondSaveEntry
    {
        public string characterId;
        public int level;
        public int progress;
    }

    /// <summary>
    /// The quest ledger: which quests the player knows about and how far along each is, plus bond
    /// levels and progress. Records only quests that have left <see cref="QuestState.Hidden"/>, so the
    /// file does not grow with every authored quest.
    /// </summary>
    [Serializable]
    public class QuestSaveData : SaveDataBase
    {
        public List<QuestSaveEntry> quests = new();
        public List<BondSaveEntry> bonds = new();
    }
}
