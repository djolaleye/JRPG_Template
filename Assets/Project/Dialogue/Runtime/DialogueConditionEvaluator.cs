using UnityEngine;
using JRPG.Characters;
using JRPG.Data;
using JRPG.Party;
using JRPG.Services;

namespace JRPG.Dialogue
{
    /// Interprets DialogueCondition data against the live services. Stateless per call.
    public sealed class DialogueConditionEvaluator
    {
        private readonly IPartyService _party;
        private readonly IPartyRuntimeQueries _partyRuntime;
        private readonly IInventoryService _inventory;
        private readonly IStoryStateService _story;

        public DialogueConditionEvaluator(IPartyService party, IPartyRuntimeQueries partyRuntime,
            IInventoryService inventory, IStoryStateService story)
        {
            _party = party;
            _partyRuntime = partyRuntime;
            _inventory = inventory;
            _story = story;
        }

        /// True only when every condition passes (empty list ⇒ true).
        public bool EvaluateAll(System.Collections.Generic.List<DialogueCondition> conditions, DialogueSessionRuntime session)
        {
            if (conditions == null) return true;

            for (int i = 0; i < conditions.Count; i++)
                if (!Evaluate(conditions[i], session)) return false;

            return true;
        }

        public bool Evaluate(DialogueCondition c, DialogueSessionRuntime session)
        {
            switch (c.type)
            {
                case DialogueConditionType.HasCharacter:
                    return _party != null && _party.IsRecruited(c.stringA);

                case DialogueConditionType.IsCharacterPresent:
                    return _party != null && _party.IsCharacterPresentForDialogue(c.stringA);

                case DialogueConditionType.HasItem:
                    return _inventory != null && _inventory.GetQuantity(c.stringA) >= Mathf.Max(1, c.intA);

                case DialogueConditionType.StoryFlagEquals:
                    return _story != null && _story.GetBool(c.stringA) == c.boolA;

                case DialogueConditionType.PartyMemberInSlot:
                    var slotInst = _partyRuntime?.GetPartyMemberInSlot(c.intA);
                    return slotInst != null && slotInst.SourceDataId == c.stringA;

                case DialogueConditionType.PlayerLevelAtLeast:
                    var inst = ResolveCharacter(c.stringA);
                    return inst != null && inst.level >= c.intA;

                case DialogueConditionType.PreviousChoiceSelected:
                    return (session != null && session.selectedChoiceIds.Contains(c.stringA))
                        || (_story != null && _story.WasChoiceSelected(c.stringA));

                default:
                    Debug.LogWarning($"[JRPG.Dialogue] Unhandled condition type {c.type}.");
                    return false;
            }
        }

        /// Empty id = protagonist (active slot 0); otherwise resolve by character id.
        private CharacterRuntimeInstance ResolveCharacter(string characterId)
        {
            if (_partyRuntime == null) return null;
            
            return string.IsNullOrEmpty(characterId)
                ? _partyRuntime.GetPartyMemberInSlot(0)
                : _partyRuntime.ResolveInstanceById(characterId);
        }
    }
}
