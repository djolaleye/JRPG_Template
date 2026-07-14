using System;
using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    /// One authored dialogue node. Linear when it has a nextNodeId and no choices; a choice node when
    /// it has choices. Static content only
    [Serializable]
    public class DialogueNodeData
    {
        public string nodeId;

        /// Speaker reference resolved at render time (e.g. protagonist, party_slot_1, current_speaker,
        /// character:char_hero, npc:npc_blacksmith, system).
        public string speakerRef;

        [TextArea(2, 6)] public string text;

        public DialogueImportance importance = DialogueImportance.Interactive;

        public List<DialogueCommand> onEnterCommands = new();
        public List<DialogueCommand> onExitCommands = new();

        public string nextNodeId;
        public List<DialogueChoiceData> choices = new();

        public bool endGraphAfterThisNode;
        public DialogueExitResolution exitResolution;
    }

    [Serializable]
    public class DialogueChoiceData
    {
        public string choiceId;
        public string displayText;
        public string nextNodeId;

        /// All must pass for the choice to be available. Empty = always available.
        public List<DialogueCondition> conditions = new();

        /// Commands run when this choice is selected (before advancing to nextNodeId).
        public List<DialogueCommand> commands = new();
    }
}
