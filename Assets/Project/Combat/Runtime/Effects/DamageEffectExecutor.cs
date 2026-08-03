using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Applies damage to every resolved target by running the DamagePipeline. All damage arithmetic
    /// lives in the pipeline stages — this executor only applies the result and records it.
    public sealed class DamageEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.Damage;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null || ctx.damage == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                var d = ctx.damage.Run(ctx.actor, target, ctx.effect, ctx.action, ctx.rng);

                int before = target.currentHP;
                int applied;

                if (d.absorbed)
                {
                    // Absorb converts the hit into healing, capped at MaxHP.
                    target.currentHP = Mathf.Min(target.MaxHP, target.currentHP + d.finalAmount);
                    applied = target.currentHP - before;
                }
                else
                {
                    target.currentHP = Mathf.Max(0, target.currentHP - d.finalAmount);
                    applied = before - target.currentHP;
                }

                int after = target.currentHP;
                bool defeated = after <= 0;

                // Remember who hurt this combatant, so AI retaliation (LastAttacker) has something to
                // aim at.
                if (!d.missed && !d.absorbed && applied > 0 && ctx.actor != null && ctx.actor != target)
                    target.lastAttackerCombatantId = ctx.actor.combatantId;

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.Damage),
                    amount = d.missed ? 0 : applied,
                    hpBefore = before,
                    hpAfter = after,
                    wasDefeated = defeated,
                    missed = d.missed,
                    critical = d.critical,
                    absorbed = d.absorbed,
                    immune = d.immune,
                    element = d.element,
                });

                if (defeated && !ctx.result.defeatedCombatantIds.Contains(target.combatantId))
                    ctx.result.defeatedCombatantIds.Add(target.combatantId);
            }
        }
    }
}
