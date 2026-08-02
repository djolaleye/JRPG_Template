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

    /// Brings a defeated ally back. Only meaningful against dead targets, which requires the DeadAlly
    /// target mode (Phase 11.5) to select them.
    public sealed class ReviveEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.Revive;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null || !target.IsDefeated) continue; 

                // basePower is a percentage of MaxHP when set, otherwise a flat amount.
                int restored = ctx.effect.basePower > 0
                    ? Mathf.Max(1, Mathf.RoundToInt(target.MaxHP * Mathf.Clamp01(ctx.effect.basePower / 100f)))
                    : 1;

                int before = target.currentHP;
                target.currentHP = Mathf.Clamp(restored, 1, target.MaxHP);

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.Revive),
                    amount = target.currentHP - before,
                    hpBefore = before,
                    hpAfter = target.currentHP,
                });
            }
        }
    }
}
