using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Converts persistent runtime characters and enemy assets into battle-local combatants.
    /// Kept separate from the state machine so conversion rules are inspectable and testable.
    public sealed class CombatantFactory
    {
        /// Party combatant. Clones the character's stat block so temporary combat modifiers cannot
        /// leak into exploration state. Current HP/MP/SP are copied from the live instance and are
        /// committed back at battle end via sourceRuntimeId.
        public CombatantInstance FromCharacter(CharacterRuntimeInstance character)
        {
            var stats = character.stats.Clone();
            stats.Recalculate();

            return new CombatantInstance
            {
                combatantId = $"party_{character.SourceDataId}",
                sourceDataId = character.SourceDataId,
                sourceRuntimeId = character.InstanceId,
                team = CombatantTeam.Party,
                displayName = character.SourceDataId,
                stats = stats,
                currentHP = character.currentHP,
                currentMP = character.currentMP,
                currentSP = character.currentSP,
            };
        }

        /// Enemy combatant. Battle-local and discarded when combat ends; resources start full.
        public CombatantInstance FromEnemy(EnemyData enemy, int enemyIndex)
        {
            var stats = new StatBlockRuntime();
            for (int i = 0; i < enemy.baseStats.Count; i++)
            {
                var bs = enemy.baseStats[i];
                stats.SetBase(bs.stat, bs.value);
            }
            stats.Recalculate();

            return new CombatantInstance
            {
                combatantId = $"enemy_{enemy.Id}_{enemyIndex}",
                sourceDataId = enemy.Id,
                sourceRuntimeId = string.Empty,
                team = CombatantTeam.Enemy,
                displayName = string.IsNullOrEmpty(enemy.displayName) ? enemy.Id : enemy.displayName,
                stats = stats,
                currentHP = stats.GetFinal(StatType.MaxHP),
                currentMP = stats.GetFinal(StatType.MaxMP),
                currentSP = stats.GetFinal(StatType.MaxSP),
            };
        }
    }
}
