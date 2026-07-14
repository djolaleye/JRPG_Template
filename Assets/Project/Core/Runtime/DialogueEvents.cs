namespace JRPG.Core
{

    public readonly struct DialogueStarted
    {
        public readonly string GraphId;
        public readonly string SessionId;
        public DialogueStarted(string graphId, string sessionId) { GraphId = graphId; SessionId = sessionId; }
    }

    public readonly struct DialogueNodeEntered
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public DialogueNodeEntered(string graphId, string nodeId) { GraphId = graphId; NodeId = nodeId; }
    }

    public readonly struct DialogueChoiceSelected
    {
        public readonly string GraphId;
        public readonly string NodeId;
        public readonly string ChoiceId;
        public DialogueChoiceSelected(string graphId, string nodeId, string choiceId)
        {
            GraphId = graphId;
            NodeId = nodeId;
            ChoiceId = choiceId;
        }
    }

    public readonly struct DialogueCommandExecuted
    {
        public readonly string GraphId;
        public readonly string CommandType; /// DialogueCommandType.ToString() — kept as a string so JRPG.Core doesn't reference JRPG.Data.
        public DialogueCommandExecuted(string graphId, string commandType) { GraphId = graphId; CommandType = commandType; }
    }

    public readonly struct DialogueCompleted
    {
        public readonly string GraphId;
        public readonly string SessionId;
        public readonly string ExitType; /// DialogueExitType.ToString() and its target (e.g. encounterId for StartBattle).
        public readonly string TargetId;
        public DialogueCompleted(string graphId, string sessionId, string exitType, string targetId)
        {
            GraphId = graphId;
            SessionId = sessionId;
            ExitType = exitType;
            TargetId = targetId;
        }
    }

    public readonly struct StoryFlagChanged
    {
        public readonly string FlagId;
        public StoryFlagChanged(string flagId) { FlagId = flagId; }
    }
}
