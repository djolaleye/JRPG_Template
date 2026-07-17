using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Progression
{
    /// Aggregate runtime state owned by ProgressionService. Never serialized directly — the save
    /// payload is ProgressionSaveData (primitives and stable ids only).
    public class ProgressionRuntimeState
    {
        public Dictionary<string, CharacterProgressRuntime> charactersById = new();
        public List<LevelUpResult> pendingLevelUps = new();
        public List<PendingAttributeAllocation> pendingAllocations = new();
        public BattleResultData lastProcessedBattleResult;
        public bool postBattleFlowActive;
    }
}
