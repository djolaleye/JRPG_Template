namespace JRPG.Core
{
    /// Progression events carry primitives only (matching the CombatEvents precedent) so any
    /// assembly can subscribe without referencing JRPG.Progression. Rich preview/result objects are
    /// exposed directly by ProgressionService.

    public readonly struct PostBattleFlowStarted
    {
        public readonly string BattleId;
        public PostBattleFlowStarted(string battleId) { BattleId = battleId; }
    }

    public readonly struct XpApplied
    {
        public readonly string CharacterId;
        public readonly int XpGained;
        public readonly int NewTotalXp;
        public XpApplied(string characterId, int xpGained, int newTotalXp)
        {
            CharacterId = characterId;
            XpGained = xpGained;
            NewTotalXp = newTotalXp;
        }
    }

    public readonly struct LevelUpOccurred
    {
        public readonly string CharacterId;
        public readonly int OldLevel;
        public readonly int NewLevel;
        public LevelUpOccurred(string characterId, int oldLevel, int newLevel)
        {
            CharacterId = characterId;
            OldLevel = oldLevel;
            NewLevel = newLevel;
        }
    }

    public readonly struct SkillLearned
    {
        public readonly string CharacterId;
        public readonly string LearnedSkillId;
        public readonly string DiscardedSkillId;
        public SkillLearned(string characterId, string learnedSkillId, string discardedSkillId)
        {
            CharacterId = characterId;
            LearnedSkillId = learnedSkillId;
            DiscardedSkillId = discardedSkillId;
        }
    }

    public readonly struct AttributePointsAssigned
    {
        public readonly string CharacterId;
        /// StatType.ToString() — kept as a string so JRPG.Core does not reference JRPG.Data.
        public readonly string StatId;
        public readonly int Points;
        public AttributePointsAssigned(string characterId, string statId, int points)
        {
            CharacterId = characterId;
            StatId = statId;
            Points = points;
        }
    }

    public readonly struct ProgressionCommitted
    {
        public readonly string BattleId;
        public ProgressionCommitted(string battleId) { BattleId = battleId; }
    }

    public readonly struct PostBattleFlowCompleted
    {
        public readonly string BattleId;
        public PostBattleFlowCompleted(string battleId) { BattleId = battleId; }
    }
}
