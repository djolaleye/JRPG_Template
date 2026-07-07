using JRPG.Core;

namespace JRPG.Combat
{
    public interface ICombatOutcomeEvaluator
    {
        BattleOutcome Evaluate(BattleContext context);
    }

    public sealed class CombatOutcomeEvaluator : ICombatOutcomeEvaluator
    {
        public BattleOutcome Evaluate(BattleContext context)
        {
            if (AllDefeated(context, CombatantTeam.Enemy)) return BattleOutcome.Victory;
            if (AllDefeated(context, CombatantTeam.Party)) return BattleOutcome.Defeat;
            return BattleOutcome.None;
        }

        private static bool AllDefeated(BattleContext ctx, CombatantTeam team)
        {
            var list = team == CombatantTeam.Party ? ctx.partyCombatants : ctx.enemyCombatants;
            
            if (list.Count == 0) return true;
            for (int i = 0; i < list.Count; i++)
                if (!list[i].IsDefeated) return false;
            return true;
        }
    }
}
