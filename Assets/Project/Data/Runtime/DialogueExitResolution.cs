using System;

namespace JRPG.Data
{
    public enum DialogueExitType
    {
        ReturnToExploration,
        StartBattle,      // targetId = encounterId
        OpenMenu,         // Phase 9 stub
        ChangePartyScope, // Phase 9 stub
        None
    }

    /// How a graph ending is resolved after DialogueCompleted.
    [Serializable]
    public struct DialogueExitResolution
    {
        public DialogueExitType exitType;
        public string targetId;
    }
}
