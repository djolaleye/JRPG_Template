using System;

namespace JRPG.Data
{
    public enum DialogueExitType
    {
        ReturnToExploration,
        StartBattle,      // targetId = encounterId
        OpenMenu,         // stub
        ChangePartyScope, // stub
        None,
        ResumeCombat,        // resume the held battle (default when a combat interruption ends)
        EndCombat,           // targetId = outcome ("Victory"/"Defeat"); ends the current battle
        StartFollowUpBattle  // targetId = encounterId; ends the current battle then starts another
    }

    /// How a graph ending is resolved after DialogueCompleted.
    [Serializable]
    public struct DialogueExitResolution
    {
        public DialogueExitType exitType;
        public string targetId;
    }
}
