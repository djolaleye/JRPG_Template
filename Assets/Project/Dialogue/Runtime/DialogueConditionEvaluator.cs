using UnityEngine;
using JRPG.Characters;
using JRPG.Core;
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
        private readonly DataRegistry _data;

        private readonly IServiceRegistry _services;
        private IQuestService _quests;

        /// <param name="services">
        /// Optional. Only used to reach <see cref="IQuestService"/> for the quest/bond conditions, and
        /// resolved lazily because the quest service is constructed after dialogue.
        /// </param>
        /// <param name="quests">
        /// Optional direct handle, for a caller that owns the quest service and cannot go through the
        /// registry — the quest service itself evaluates conditions while still in its constructor.
        /// </param>
        public DialogueConditionEvaluator(IPartyService party, IPartyRuntimeQueries partyRuntime,
            IInventoryService inventory, IStoryStateService story, DataRegistry data = null,
            IServiceRegistry services = null, IQuestService quests = null)
        {
            _party = party;
            _partyRuntime = partyRuntime;
            _inventory = inventory;
            _story = story;

            // Optional: only used to turn ids into authored names for player-facing reasons.
            _data = data;

            _services = services;
            _quests = quests;
        }

        private IQuestService Quests
        {
            get
            {
                if (_quests == null && _services != null) _services.TryResolve(out _quests);
                return _quests;
            }
        }

        /// True only when every condition passes (empty list ⇒ true).
        public bool EvaluateAll(System.Collections.Generic.List<DialogueCondition> conditions, DialogueSessionRuntime session)
            => EvaluateAll(conditions, session, out _);

        /// <summary>
        /// As <see cref="EvaluateAll(System.Collections.Generic.List{DialogueCondition}, DialogueSessionRuntime)"/>,
        /// additionally reporting why it failed.
        ///
        /// <para>The reason names the <b>first</b> unmet condition rather than all of them: a choice
        /// gated on three things should tell the player the one thing to go and do, not hand them a
        /// checklist. <paramref name="failureReason"/> is null when the result is true.</para>
        /// </summary>
        public bool EvaluateAll(System.Collections.Generic.List<DialogueCondition> conditions,
                                DialogueSessionRuntime session, out string failureReason)
        {
            failureReason = null;
            if (conditions == null) return true;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (Evaluate(conditions[i], session)) continue;

                failureReason = DescribeUnmet(conditions[i]);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Player-facing sentence for a condition that did not pass.
        ///
        /// <para>Vague about story flags: "you have not done the thing yet".</para>
        /// </summary>
        private string DescribeUnmet(DialogueCondition c) => c.type switch
        {
            DialogueConditionType.HasCharacter => $"Requires {CharacterName(c.stringA)} in your party.",
            DialogueConditionType.IsCharacterPresent => $"{CharacterName(c.stringA)} is not here.",
            DialogueConditionType.HasItem => Mathf.Max(1, c.intA) > 1
                ? $"Requires {Mathf.Max(1, c.intA)} × {ItemName(c.stringA)}."
                : $"Requires {ItemName(c.stringA)}.",
            DialogueConditionType.PartyMemberInSlot => $"Requires {CharacterName(c.stringA)} in slot {c.intA + 1}.",
            DialogueConditionType.PlayerLevelAtLeast => $"Requires level {c.intA}.",
            DialogueConditionType.PreviousChoiceSelected => "Something earlier went differently.",
            DialogueConditionType.StoryFlagEquals => "Not available yet.",
            DialogueConditionType.QuestStateIs => DescribeQuestState((QuestState)c.intA, c.stringA),
            DialogueConditionType.BondLevelAtLeast => $"Requires bond level {c.intA} with {CharacterName(c.stringA)}.",
            _ => "Not available.",
        };

        /// Names the quest where one is authored, because "finish Find the Heir first" is actionable
        /// and "not available yet" is not.
        private string DescribeQuestState(QuestState wanted, string questId)
        {
            string title = questId;
            if (_data != null && _data.TryGet<QuestData>(questId, out var quest) && quest != null)
                title = quest.DisplayTitle;

            return wanted switch
            {
                QuestState.Complete => $"Requires '{title}' to be completed.",
                QuestState.Active => $"Requires '{title}' to be under way.",
                QuestState.Available => $"Requires '{title}' to have been found.",
                _ => "Not available yet.",
            };
        }

        private string CharacterName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "someone";
            if (_data != null && _data.TryGet<CharacterData>(id, out var cd) && cd != null
                && !string.IsNullOrEmpty(cd.displayName))
                return cd.displayName;

            return id;
        }

        private string ItemName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "an item";
            if (_data != null && _data.TryGet<ItemData>(id, out var item) && item != null
                && !string.IsNullOrEmpty(item.displayName))
                return item.displayName;

            return id;
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

                // Fails closed with no quest service: a gate that cannot be checked stays shut.
                case DialogueConditionType.QuestStateIs:
                    return Quests != null && Quests.GetState(c.stringA) == (QuestState)c.intA;

                case DialogueConditionType.BondLevelAtLeast:
                    return Quests != null && Quests.GetBondLevel(c.stringA) >= c.intA;

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
