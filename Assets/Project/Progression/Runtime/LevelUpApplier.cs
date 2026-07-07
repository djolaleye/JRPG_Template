using UnityEngine;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Progression
{
    /// Applies one evaluated level-up to a runtime character: permanent growth modifiers, the level
    /// bump, and resource top-ups. Mutates CharacterRuntimeInstance only — never the growth assets.
    public sealed class LevelUpApplier
    {
        /// SourceId format for fixed growth: level_growth:{characterId}:{level}
        public static string GrowthSourceId(string characterId, int level) => $"level_growth:{characterId}:{level}";

        public void ApplyLevel(CharacterRuntimeInstance character, LevelUpResult levelUp)
        {
            int oldMaxHp = character.stats.GetFinal(StatType.MaxHP);
            int oldMaxMp = character.stats.GetFinal(StatType.MaxMP);
            int oldMaxSp = character.stats.GetFinal(StatType.MaxSP);

            string sourceId = GrowthSourceId(levelUp.characterId, levelUp.newLevel);
            for (int i = 0; i < levelUp.statIncreases.Count; i++)
            {
                var entry = levelUp.statIncreases[i];
                character.stats.AddModifier(new StatModifier(
                    entry.stat, ModifierType.Flat, entry.value, sourceId, isPermanent: true));
            }

            character.level = levelUp.newLevel;
            character.stats.Recalculate();

            // Max-resource increases raise current resources by the same delta
            character.currentHP += Mathf.Max(0, character.stats.GetFinal(StatType.MaxHP) - oldMaxHp);
            character.currentMP += Mathf.Max(0, character.stats.GetFinal(StatType.MaxMP) - oldMaxMp);
            character.currentSP += Mathf.Max(0, character.stats.GetFinal(StatType.MaxSP) - oldMaxSp);
            character.Recalculate();
        }
    }
}
