using System;

namespace JRPG.Data
{
    public enum DialogueCommandType
    {
        SetStoryFlag,            // stringA = flagId, boolA = value
        GiveItem,                // stringA = itemId, intA = quantity
        RemoveItem,              // stringA = itemId, intA = quantity
        RecruitCharacter,        // stringA = characterId
        ChangePartyScope,        // stringA = scopeId (Phase 9 stub)
        StartBattle,             // stringA = encounterId (handled via exit resolution)
        UnlockSkill,             // Phase 9 stub
        ModifyRelationshipValue  // Phase 9 stub
    }

    /// Plain enum-tagged command data run on node enter/exit or choice selection, interpreted by
    /// DialogueCommandExecutor.
    [Serializable]
    public struct DialogueCommand
    {
        public DialogueCommandType type;
        public string stringA;
        public int intA;
        public bool boolA;
    }
}
