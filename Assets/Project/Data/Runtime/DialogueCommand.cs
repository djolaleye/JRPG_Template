using System;

namespace JRPG.Data
{
    public enum DialogueCommandType
    {
        SetStoryFlag,            // stringA = flagId, boolA = value
        GiveItem,                // stringA = itemId, intA = quantity
        RemoveItem,              // stringA = itemId, intA = quantity
        RecruitCharacter,        // stringA = characterId — requires Recruitable (no forced pre-walk)
        ChangePartyScope,        // stringA = scopeId (stub)
        StartBattle,             // stringA = encounterId (handled via exit resolution)
        UnlockSkill,             // stub
        ModifyRelationshipValue,  // stub
        StartCutscene,            // tub
        MeetCharacter,           // stringA = characterId (Unmet → Met)
        EvaluateRecruitment,     // stringA = characterId (Met → Recruitable, iff recruitmentFlagIds satisfied)
        SetCharacterGuest,       // stringA = characterId (→ Guest, from Met/Unavailable)
        SetCharacterUnavailable, // stringA = characterId (→ Unavailable)
        QueueCombatAction,       // stringA = combatantId (empty = current actor), stringB = actionId
        SetEnemyActionProfile,   // stringA = profileId (stub)
        SetBattleTrigger,        // stringA = triggerId, boolA = active (true = re-enable, false = suppress)
        SetCharacterActive,      // stringA = characterId — promotes a recruited member into the active party
        DiscoverQuest,           // stringA = questId — Hidden → Available (Main continues to Active)
        AcceptQuest,             // stringA = questId — Available → Active
        CompleteQuest,           // stringA = questId — the hand-in for an autoComplete=false quest
        SetQuestObjectiveComplete, // stringA = questId, stringB = objectiveId
        GrantBondProgress        // stringA = characterId, intA = amount
    }

    /// Plain enum-tagged command data run on node enter/exit or choice selection, interpreted by
    /// DialogueCommandExecutor.
    [Serializable]
    public struct DialogueCommand
    {
        public DialogueCommandType type;
        public string stringA;
        public string stringB;
        public int intA;
        public bool boolA;
    }
}
