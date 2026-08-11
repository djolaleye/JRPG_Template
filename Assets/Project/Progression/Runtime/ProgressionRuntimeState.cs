using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Progression
{
    /// Aggregate runtime state owned by ProgressionService. Never serialized directly — the save
    /// payload is ProgressionSaveData (primitives and stable ids only).
    public class ProgressionRuntimeState
    {
        public Dictionary<string, CharacterProgressRuntime> charactersById = new();
        public List<LevelUpResult> pendingLevelUps = new();
        public List<PendingAttributeAllocation> pendingAllocations = new();
        public List<PendingSkillChoice> pendingSkillChoices = new();

        /// <summary>
        /// Skills gained outright this post-battle flow — learned with room to spare, so unlike
        /// <see cref="pendingSkillChoices"/> they need no decision from the player.
        ///
        /// <para>Recorded because they are otherwise invisible: the learn already happened inside
        /// <c>ApplyLevelUpLearning</c>, and without this the post-battle screen has nothing to announce
        /// and the player finds new skills only by opening a menu later.</para>
        /// </summary>
        public List<LearnedSkill> learnedSkills = new();
        public BattleResultData lastProcessedBattleResult;
        public bool postBattleFlowActive;
    }
}
