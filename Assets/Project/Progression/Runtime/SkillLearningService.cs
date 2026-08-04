using System.Collections.Generic;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Progression
{
    /// The single entry point for a character gaining a skill, whatever the source.
    ///
    /// When the character is at the skill cap the skill is not dropped, but becomes a pending choice
    /// so the player decides what to discard (including declining the new skill).
    public sealed class SkillLearningService
    {
        private readonly DataRegistry _data;

        public SkillLearningService(DataRegistry data)
        {
            _data = data;
        }

        public enum LearnOutcome
        {
            Learned,
            AlreadyKnown,
            NeedsReplacement,   // at the cap. player chooses what to discard
            UnknownSkill,
        }

        /// Ensures the instance's skill list is seeded from authored defaults + everything its growth
        /// table says it should already know at <paramref name="atLevel"/> (defaulting to the
        /// character's current level). Called when an instance is built or restored, so a loaded
        /// character is never missing skills it earned.
        ///
        /// <para><paramref name="atLevel"/> exists for <see cref="ApplyLevelUpLearning"/>, which must
        /// seed against the level the character had *before* the level-up — seeding at the new level
        /// would pre-learn the very skills the level-up is about to grant, and they would then be
        /// reported as AlreadyKnown instead of appearing in the "learned!" list.</para>
        public void SeedSkills(CharacterRuntimeInstance character, int? atLevel = null)
        {
            if (character == null) return;

            if (_data.TryGet<CharacterData>(character.SourceDataId, out var charData) && charData != null)
                for (int i = 0; i < charData.defaultSkillIds.Count; i++)
                    character.TryLearnSkill(charData.defaultSkillIds[i]);

            var growth = FindGrowth(character.SourceDataId);
            if (growth == null) return;

            var earned = growth.SkillsUpToLevel(atLevel ?? character.level);
            for (int i = 0; i < earned.Count; i++) character.TryLearnSkill(earned[i]);
        }

        /// Skills unlocked by a level-up. Returns those that could NOT be added because the list is
        /// full — each becomes a pending player choice.
        public List<string> ApplyLevelUpLearning(CharacterRuntimeInstance character, int oldLevel, int newLevel,
            List<string> learnedNow)
        {
            var needsChoice = new List<string>();
            var growth = FindGrowth(character?.SourceDataId);
            if (character == null || growth == null) return needsChoice;

            SeedSkills(character, oldLevel);

            var unlocked = growth.SkillsLearnedBetween(oldLevel, newLevel);
            for (int i = 0; i < unlocked.Count; i++)
            {
                switch (TryLearn(character, unlocked[i]))
                {
                    case LearnOutcome.Learned:
                        learnedNow?.Add(unlocked[i]);
                        break;
                    case LearnOutcome.NeedsReplacement:
                        needsChoice.Add(unlocked[i]);
                        break;
                }
            }
            return needsChoice;
        }

        /// Generic learn attempt
        public LearnOutcome TryLearn(CharacterRuntimeInstance character, string skillId)
        {
            if (character == null || string.IsNullOrEmpty(skillId)) return LearnOutcome.UnknownSkill;
            if (!_data.TryGet<CombatActionData>(skillId, out var action) || action == null)
                return LearnOutcome.UnknownSkill;

            if (character.KnowsSkill(skillId)) return LearnOutcome.AlreadyKnown;

            return character.TryLearnSkill(skillId) ? LearnOutcome.Learned : LearnOutcome.NeedsReplacement;
        }

        /// Resolves a pending choice: swap `discardSkillId` for `newSkillId`. Passing the new skill as
        /// the one to discard means "decline it", which is a legal answer.
        public bool ResolveReplacement(CharacterRuntimeInstance character, string newSkillId, string discardSkillId)
        {
            if (character == null || string.IsNullOrEmpty(newSkillId)) return false;
            if (discardSkillId == newSkillId) return true;              // declined — nothing changes

            return character.ReplaceSkill(discardSkillId, newSkillId);
        }

        private CharacterGrowthData FindGrowth(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;

            // Growth assets are keyed by their own id but carry the character they belong to.
            foreach (var kv in _data.CharacterGrowthById)
                if (kv.Value != null && kv.Value.characterId == characterId) return kv.Value;
                
            return null;
        }
    }
}
