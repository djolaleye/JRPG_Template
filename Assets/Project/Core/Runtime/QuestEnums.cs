namespace JRPG.Core
{
    /// <summary>
    /// The three quest families. The player-facing labels differ from these names: Main renders as
    /// "Story", Side as "Missions", Party as "Bonds". The code names avoid colliding with the
    /// existing story-state domain.
    ///
    /// <para>Persisted (quest runtime state records the family it came from), so this is append-only.</para>
    /// </summary>
    public enum QuestType
    {
        Main,
        Side,
        Party
    }

    /// <summary>
    /// A single quest instance's lifecycle. Persisted, so the order is part of the save format.
    ///
    /// <para>"Hidden" means the player has no knowledge of the quest and it appears in no
    /// screen. "Available" means discovered but not accepted.
    /// "Complete" is terminal.</para>
    /// </summary>
    public enum QuestState
    {
        Hidden,
        Available,
        Active,
        Complete,
        Failed
    }

    /// <summary>
    /// What an objective asks for. <paramref name="targetId"/> on the objective is interpreted per
    /// type: a character id, item id, enemy id, interactable/location id, or a custom handler id.
    ///
    /// <para><see cref="Custom"/> is the extension seam — a project routes it through its own handler
    /// rather than the quest system growing a case.</para>
    /// </summary>
    public enum QuestObjectiveType
    {
        StoryCondition,
        TalkToCharacter,
        ReachLocation,
        DefeatEnemy,
        CollectItem,
        UseItem,
        Interact,
        Custom
    }

    /// <summary>
    /// What a completed quest (or a bond level) pays out. Every member routes through an existing
    /// domain service; the quest system never mutates inventory, currency or character state itself.
    /// </summary>
    public enum QuestRewardType
    {
        Experience,
        Currency,
        Item,
        Skill,
        StoryFlag,
        BondProgress,
        Custom
    }

    /// <summary>
    /// Query filter for quest listings.
    /// </summary>
    public enum QuestStateFilter
    {
        /// Everything the player could know about: Available, Active, Complete and Failed.
        Known,

        Available,
        Active,
        Complete,
        Failed,

        /// Active and Available together 
        Open,

        /// No filtering at all, Hidden included. For tooling.
        All
    }

    /// <summary>
    /// Where a grant of bond progress came from. Recorded on the event rather than in the ledger:
    /// the ledger holds a number, this explains one movement of it.
    /// </summary>
    public enum BondProgressSource
    {
        Unspecified,
        Dialogue,
        QuestReward,
        BattleParticipation,
        Debug
    }
}
