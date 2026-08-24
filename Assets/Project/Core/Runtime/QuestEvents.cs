namespace JRPG.Core
{
    /// A quest became known to the player, moved out of Hidden. Main quests publish this
    /// immediately before QuestAccepted, since story progression accepts them in the same step.
    public readonly struct QuestDiscovered
    {
        public readonly string QuestId;
        public readonly QuestType Type;

        public QuestDiscovered(string questId, QuestType type)
        {
            QuestId = questId;
            Type = type;
        }
    }

    /// A quest became Active. For Side and Party quests this is the player's explicit acceptance;
    /// for Main quests it follows discovery automatically.
    public readonly struct QuestAccepted
    {
        public readonly string QuestId;
        public readonly QuestType Type;

        public QuestAccepted(string questId, QuestType type)
        {
            QuestId = questId;
            Type = type;
        }
    }

    /// One objective's progress moved.
    public readonly struct QuestObjectiveUpdated
    {
        public readonly string QuestId;
        public readonly string ObjectiveId;
        public readonly int CurrentAmount;
        public readonly int RequiredAmount;
        public readonly bool Complete;

        public QuestObjectiveUpdated(string questId, string objectiveId, int currentAmount,
                                     int requiredAmount, bool complete)
        {
            QuestId = questId;
            ObjectiveId = objectiveId;
            CurrentAmount = currentAmount;
            RequiredAmount = requiredAmount;
            Complete = complete;
        }
    }

    /// A quest completed and its rewards were applied. Published once per quest instance.
    public readonly struct QuestCompleted
    {
        public readonly string QuestId;
        public readonly QuestType Type;

        public QuestCompleted(string questId, QuestType type)
        {
            QuestId = questId;
            Type = type;
        }
    }

    /// A quest was abandoned by the player and returned to Available. Never fires for Main quests.
    public readonly struct QuestAbandoned
    {
        public readonly string QuestId;

        public QuestAbandoned(string questId)
        {
            QuestId = questId;
        }
    }

    /// Bond progress moved for one character. Unlocked is true when this movement crossed the next
    /// level's threshold — the point at which that level's bond quest becomes available and further
    /// progress for the character is refused until it is completed.
    public readonly struct BondProgressChanged
    {
        public readonly string CharacterId;
        public readonly int Current;
        public readonly int Delta;
        public readonly BondProgressSource Source;
        public readonly bool Unlocked;

        public BondProgressChanged(string characterId, int current, int delta,
                                   BondProgressSource source, bool unlocked)
        {
            CharacterId = characterId;
            Current = current;
            Delta = delta;
            Source = source;
            Unlocked = unlocked;
        }
    }

    /// A character's bond level advanced. Levels never skip, so NewLevel is always PreviousLevel + 1.
    public readonly struct BondLevelChanged
    {
        public readonly string CharacterId;
        public readonly int PreviousLevel;
        public readonly int NewLevel;

        public BondLevelChanged(string characterId, int previousLevel, int newLevel)
        {
            CharacterId = characterId;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
        }
    }
}
