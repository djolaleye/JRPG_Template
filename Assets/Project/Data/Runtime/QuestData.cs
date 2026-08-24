using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// One requirement inside a quest. Definition only — how far along the player is lives in the
    /// quest service's runtime state.
    /// </summary>
    [Serializable]
    public class QuestObjectiveData
    {
        [Tooltip("Unique within this quest.")]
        public string objectiveId;

        public QuestObjectiveType type;

        [Tooltip("Interpreted by type: character id, item id, enemy id, interactable/location id, " +
                 "story flag id, or a custom handler id.")]
        public string targetId;

        [Min(1)] public int requiredAmount = 1;

        [Tooltip("An optional objective never blocks completion, but still tracks and displays.")]
        public bool optional;

        [Tooltip("Player-facing line.")]
        public string description;

        [Tooltip("Extra gating on this objective, reusing the dialogue/story condition model.")]
        public List<DialogueCondition> conditions = new();

        /// True for objective types whose requirement is inherently boolean, where requiredAmount is
        /// ignored and any progress at all completes them.
        public bool IsBoolean =>
            type == QuestObjectiveType.StoryCondition ||
            type == QuestObjectiveType.ReachLocation ||
            type == QuestObjectiveType.TalkToCharacter;

        public int EffectiveRequiredAmount => IsBoolean ? 1 : Mathf.Max(1, requiredAmount);
    }

    /// <summary>
    /// One payout.
    ///
    /// <para><c>id</c> is read per type: an item id, a skill id, a story flag id, a character id for
    /// bond progress, or a handler id for Custom. Experience and Currency ignore it.</para>
    /// </summary>
    [Serializable]
    public class QuestRewardData
    {
        public QuestRewardType type;
        public string id;
        [Min(0)] public int amount = 1;

        [Tooltip("StoryFlag rewards only: the value the flag is set to. Ignored by every other type.")]
        public bool flagValue = true;
    }

    /// <summary>
    /// An authored quest, immutable at runtime.
    ///
    /// <para><b>Discovery and acceptance are separate.</b> <c>availabilityConditions</c> decide when a
    /// quest may be discovered at all; whether discovery then leads straight to Active is a property
    /// of <see cref="questType"/> — Main quests auto-activate from story progression, Side and Party
    /// quests wait for the player to accept.</para>
    ///
    /// <para><c>completionFlag</c> is the bridge back to the narrative layer: quest state answers
    /// "what is the player on", story flags answer "what is true in the world". A quest that needs to
    /// gate later dialogue writes a flag rather than dialogue querying the quest ledger.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Quest Data", fileName = "QuestData")]
    public sealed class QuestData : GameDataBase
    {
        public QuestType questType = QuestType.Side;

        [Tooltip("Player-facing title.")]
        public string title;

        [TextArea] public string summary;

        [Tooltip("Optional authoring reference to the NPC or source that offers this quest. Not the " +
                 "runtime identity of anything.")]
        public string giverId;

        [Tooltip("Required for Party quests: the character whose bond this advances.")]
        public string characterId;

        [Tooltip("Party quests only: the bond level this quest completes. 0 reads the level off the " +
                 "character's BondData entry that names this quest.")]
        [Min(0)] public int bondLevel;

        [Tooltip("Main quests are always mandatory.")]
        public bool mandatory;

        [Tooltip("When the quest may be discovered. Empty means immediately eligible.")]
        public List<DialogueCondition> availabilityConditions = new();

        public List<QuestObjectiveData> objectives = new();
        public List<QuestRewardData> completionRewards = new();

        [Tooltip("Completes itself the moment every required objective is met. Author false for a " +
                 "quest that must be handed in — dialogue then calls CompleteQuest.")]
        public bool autoComplete = true;

        [Tooltip("Discovered through the quest system when this quest completes. Optional.")]
        public string followUpQuestId;

        [Tooltip("Story flag set true on completion.")]
        public string completionFlag;

        public string DisplayTitle => string.IsNullOrEmpty(title) ? displayName : title;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // A Main quest is mandatory by definition — the player is never asked whether to take the
            // story. Normalising here rather than only reporting it keeps the authored asset honest.
            if (questType == QuestType.Main) mandatory = true;
        }
#endif
    }
}
