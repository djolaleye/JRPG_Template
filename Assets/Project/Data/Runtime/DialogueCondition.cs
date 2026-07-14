using System;

namespace JRPG.Data
{
    public enum DialogueConditionType
    {
        HasCharacter,          // stringA = characterId (recruited)
        IsCharacterPresent,    // stringA = characterId (present for dialogue)
        HasItem,               // stringA = itemId, intA = minQuantity
        StoryFlagEquals,       // stringA = flagId, boolA = expected value
        PartyMemberInSlot,     // stringA = characterId, intA = slot index
        PlayerLevelAtLeast,    // stringA = characterId (empty = protagonist), intA = min level
        PreviousChoiceSelected // stringA = choiceId (previously selected this playthrough)
    }

    /// Plain enum-tagged condition data embedded on choices/nodes and interpreted by
    /// DialogueConditionEvaluator.
    [Serializable]
    public struct DialogueCondition
    {
        public DialogueConditionType type;
        public string stringA;
        public string stringB;
        public int intA;
        public bool boolA;
    }
}
