using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Restores HP to every resolved target, capped at MaxHP. Defeated combatants are not revived by a
    /// heal — that is the Revive effect's job (Phase 11.3).
    public sealed class HealEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.Heal;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                int amount = Mathf.RoundToInt(
                    ctx.effect.basePower + (ctx.actor?.stats?.GetFinal(ctx.effect.scalingStat) ?? 0) * ctx.effect.statScale);

                int before = target.currentHP;
                target.currentHP = Mathf.Min(target.currentHP + amount, target.MaxHP);
                int after = target.currentHP;

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.Heal),
                    amount = after - before,
                    hpBefore = before,
                    hpAfter = after,
                });
            }
        }
    }
}
