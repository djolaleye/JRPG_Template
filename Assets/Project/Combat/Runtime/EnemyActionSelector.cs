using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    public struct EnemyActionChoice
    {
        public string actionId;
        public List<string> targetCombatantIds;
    }

    public interface IEnemyActionSelector
    {
        EnemyActionChoice ChooseAction(CombatantInstance enemy, BattleContext context);
    }

    /// TEMP AI: melee the first living party combatant.
    public sealed class SimpleEnemyActionSelector : IEnemyActionSelector
    {
        public const string MeleeActionId = "attack_melee";

        public EnemyActionChoice ChooseAction(CombatantInstance enemy, BattleContext context)
        {
            var targets = new List<string>();
            for (int i = 0; i < context.partyCombatants.Count; i++)
            {
                if (!context.partyCombatants[i].IsDefeated)
                {
                    targets.Add(context.partyCombatants[i].combatantId);
                    break;
                }
            }

            return new EnemyActionChoice
            {
                actionId = MeleeActionId,
                targetCombatantIds = targets,
            };
        }
    }
}
