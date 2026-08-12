using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Progression
{
    /// Mutable per-character progression coordination state. The live CharacterRuntimeInstance
    /// remains the authority for HP/MP/SP and the stat block; this tracks growth bookkeeping only.
    public class CharacterProgressRuntime
    {
        public string characterId;
        public int currentLevel = 1;
        public int currentXp;
        public int unspentAttributePoints;
        public LevelUpMode levelUpMode = LevelUpMode.FixedGrowth;
        public Dictionary<StatType, int> manuallyAllocatedPoints = new();
    }

    /// One level threshold crossing, produced by the engine and applied by LevelUpApplier.
    public class LevelUpResult
    {
        public string characterId;
        public int oldLevel;
        public int newLevel;
        public List<StatGrowthEntry> statIncreases = new();
        public int pointsGranted;
    }

    public class PendingAttributeAllocation
    {
        public string characterId;
        public int pointsRemaining;
    }

    public class PendingSkillChoice
    {
        public string characterId;
        public string newSkillId;
        public List<string> currentSkillIds = new();
    }

    /// <summary>
    /// A skill gained outright during this post-battle flow — no decision needed, only an
    /// announcement. The counterpart to <see cref="PendingSkillChoice"/>, which is what the same
    /// unlock becomes when the character's skill list is already full.
    /// </summary>
    public class LearnedSkill
    {
        public string characterId;
        public string skillId;
        public int atLevel;
    }

    /// Non-mutating projection of what applying a battle result would do. Shown on the XP preview
    /// screen before the player confirms.
    public class ProgressionPreview
    {
        public string battleId;
        public int totalXp;
        public List<CharacterXpPreview> characters = new();
    }

    public class CharacterXpPreview
    {
        public string characterId;
        public string displayName;
        public int currentLevel;
        public int currentXp;
        public int xpGained;
        public int projectedLevel;
    }

    /// Rewards resolved from the battle result + encounter reward table. Drop rolls are made once
    /// (seeded) when the flow begins; items are granted only when the result is applied.
    public class ResolvedRewards
    {
        public int totalXp;
        public int currency; // Currently display only 
        public List<GrantedDrop> drops = new();
    }

    public struct GrantedDrop
    {
        public string itemId;
        public int quantity;
    }
}
