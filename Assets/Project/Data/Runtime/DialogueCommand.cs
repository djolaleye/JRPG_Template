using System;

namespace JRPG.Data
{
    public enum DialogueCommandType
    {
        SetStoryFlag,            // stringA = flagId, boolA = value
        GiveItem,                // stringA = itemId, intA = quantity
        RemoveItem,              // stringA = itemId, intA = quantity
        RecruitCharacter,        // stringA = characterId — requires Recruitable (no forced pre-walk)
        ChangePartyScope,        // stringA = scopeId (Phase 9 stub)
        StartBattle,             // stringA = encounterId (handled via exit resolution)
        UnlockSkill,             // Phase 9 stub
        ModifyRelationshipValue,  // Phase 9 stub
        StartCutscene,            // Phase 9 stub
        // Recruitment lifecycle (appended — existing serialized indices above must not shift):
        MeetCharacter,           // stringA = characterId (Unmet → Met)
        EvaluateRecruitment,     // stringA = characterId (Met → Recruitable, iff recruitmentFlagIds satisfied)
        SetCharacterGuest,       // stringA = characterId (→ Guest, from Met/Unavailable)
        SetCharacterUnavailable, // stringA = characterId (→ Unavailable)
        // Phase 10 non-terminal combat-integration commands (routed through ICombatInterruptionService):
        QueueCombatAction,       // stringA = combatantId (empty = current actor), stringB = actionId
        SetEnemyActionProfile,   // stringA = profileId (Phase 11 stub)
        SetBattleTrigger         // stringA = triggerId, boolA = active (true = re-enable, false = suppress)
    }

    /// Plain enum-tagged command data run on node enter/exit or choice selection, interpreted by
    /// DialogueCommandExecutor.
    [Serializable]
    public struct DialogueCommand
    {
        public DialogueCommandType type;
        public string stringA;
        public string stringB;   // secondary string arg (appended for Phase 10, e.g. QueueCombatAction actionId)
        public int intA;
        public bool boolA;
    }
}
