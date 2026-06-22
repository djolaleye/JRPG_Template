using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Returns candidate targets for an action. Does not apply effects or mutate combat state.
    /// Phase 6 supports only Self and Single selection.
    public sealed class TargetingSystem
    {
        public List<CombatantInstance> GetValidTargets(BattleContext ctx, CombatantInstance actor, CombatActionData action)
        {
            var result = new List<CombatantInstance>();
            if (ctx == null || actor == null || action == null) return result;

            var rule = action.targetRule;
            if (rule == null) return result;

            // 1. Determine the team pool relative to the acting combatant.
            var pool = GetPool(ctx, actor, rule.team);

            for (int i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];

                // 2. Remove defeated targets if requireLiving.
                if (rule.requireLiving && candidate.IsDefeated) continue;

                // 3. Self restriction.
                bool isSelf = candidate == actor;
                if (rule.selectionMode == TargetSelectionMode.Self && !isSelf) continue;
                if (isSelf && !rule.allowSelf && rule.team != TargetTeam.Self) continue;

                result.Add(candidate);
            }

            // 4/5. Selection count + stable order. Pool order is already stable (party then enemy lists).
            return result;
        }

        private static List<CombatantInstance> GetPool(BattleContext ctx, CombatantInstance actor, TargetTeam team)
        {
            switch (team)
            {
                case TargetTeam.Self:
                    return new List<CombatantInstance> { actor };

                case TargetTeam.Allies:
                    return actor.team == CombatantTeam.Party ? ctx.partyCombatants : ctx.enemyCombatants;

                case TargetTeam.Enemies:
                    return actor.team == CombatantTeam.Party ? ctx.enemyCombatants : ctx.partyCombatants;

                default:
                    return new List<CombatantInstance>();
            }
        }
    }
}
