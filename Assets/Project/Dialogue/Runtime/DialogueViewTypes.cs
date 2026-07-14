using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Dialogue
{
    /// Read-only snapshot the UI renders. Speaker/body are already resolved (speaker ref → name,
    /// tokens → values); the presenter never evaluates conditions or runs commands.
    public struct SpeakerViewData
    {
        public string speakerId;
        public string displayName;
        public string portraitId;
    }

    public class DialogueRenderSnapshot
    {
        public string graphId;
        public string nodeId;
        public SpeakerViewData speaker;
        public string body;
        public bool hasChoices;
        public DialogueImportance importance;
    }

    public class DialogueChoiceViewData
    {
        public string choiceId;
        public string text;
        public bool available;
    }
}
