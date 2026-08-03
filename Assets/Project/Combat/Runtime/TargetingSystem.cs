using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Resolves who an action may hit. Returns candidates and the final
    /// target set.
    public sealed class TargetingSystem
    {
        /// Every combatant eligible under the rule's team + filters. For Single mode this is the list
        /// the player picks from; for auto-resolved modes it is the pool ResolveTargets draws from.
        public List<CombatantInstance> GetValidTargets(BattleContext ctx, CombatantInstance actor, CombatActionData action)
        {
            var result = new List<CombatantInstance>();
            if (ctx == null || actor == null || action == null) return result;

            var rule = action.targetRule;
            if (rule == null) return result;

            var pool = GetPool(ctx, actor, rule.team);

            for (int i = 0; i < pool.Count; i++)
            {
                var candidate = pool[i];
                if (candidate == null) continue;

                // Living / defeated filters. requireDefeated wins (revive-style actions).
                if (rule.requireDefeated)
                {
                    if (!candidate.IsDefeated) continue;
                }
                else if (rule.requireLiving && candidate.IsDefeated) continue;

                bool isSelf = candidate == actor;
                if (rule.selectionMode == TargetSelectionMode.Self && !isSelf) continue;
                if (isSelf && !rule.allowSelf && rule.team != TargetTeam.Self) continue;

                // Conditional restrictions.
                if (!string.IsNullOrEmpty(rule.requiredStatusId) && !HasStatus(candidate, rule.requiredStatusId)) continue;
                if (rule.maxHpFraction > 0f)
                {
                    int max = candidate.MaxHP;
                    if (max > 0 && candidate.currentHP > max * rule.maxHpFraction) continue;
                }

                result.Add(candidate);
            }

            return result;
        }

        /// The final target set passed to the effects.
        ///
        public List<CombatantInstance> ResolveTargets(BattleContext ctx, CombatantInstance actor,
            CombatActionData action, IReadOnlyList<string> requestedIds, System.Random rng)
        {
            var chosen = new List<CombatantInstance>();
            var candidates = GetValidTargets(ctx, actor, action);
            var rule = action?.targetRule;
            if (rule == null || candidates.Count == 0) return chosen;

            switch (rule.selectionMode)
            {
                case TargetSelectionMode.Self:
                    chosen.Add(candidates[0]);
                    break;

                case TargetSelectionMode.All:
                    chosen.AddRange(candidates);
                    break;

                case TargetSelectionMode.Random:
                {
                    // Seeded
                    var pool = new List<CombatantInstance>(candidates);
                    int take = UnityEngine.Mathf.Clamp(rule.maxTargets, 1, pool.Count);
                    
                    for (int i = 0; i < take; i++)
                    {
                        int idx = rng != null ? rng.Next(pool.Count) : 0;
                        chosen.Add(pool[idx]);
                        pool.RemoveAt(idx);
                    }
                    break;
                }

                default: // Single
                {
                    var forced = GetForcedTarget(actor, candidates);
                    if (forced != null) { chosen.Add(forced); break; }

                    if (requestedIds != null)
                    {
                        for (int i = 0; i < requestedIds.Count && chosen.Count < 1; i++)
                        {
                            var t = ctx.FindCombatant(requestedIds[i]);
                            if (t != null && candidates.Contains(t)) chosen.Add(t);
                        }
                    }
                    break;
                }
            }

            return chosen;
        }

        /// Taunt-style redirection: while a combatant has a forced target set, its single-target
        /// actions must hit that combatant (when still eligible).
        private static CombatantInstance GetForcedTarget(CombatantInstance actor, List<CombatantInstance> candidates)
        {
            if (actor == null || string.IsNullOrEmpty(actor.forcedTargetCombatantId)) return null;

            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].combatantId == actor.forcedTargetCombatantId) return candidates[i];
            
            return null;
        }

        private static bool HasStatus(CombatantInstance c, string statusId)
        {
            for (int i = 0; i < c.activeStatuses.Count; i++)
                if (c.activeStatuses[i].statusId == statusId) return true;

            return false;
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

                case TargetTeam.All:
                {
                    var all = new List<CombatantInstance>();
                    all.AddRange(ctx.partyCombatants);
                    all.AddRange(ctx.enemyCombatants);
                    return all;
                }

                default:
                    return new List<CombatantInstance>();
            }
        }
    }
}
