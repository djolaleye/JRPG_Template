using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Inflicts a status on every resolved target.
    public sealed class ApplyStatusEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.ApplyStatus;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null || ctx.status == null || string.IsNullOrEmpty(ctx.effect.statusId)) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null || target.IsDefeated) continue;

                bool applied = ctx.status.TryApply(target, ctx.effect.statusId, ctx.actor?.combatantId,
                    ctx.effect.duration, Mathf.Max(1, ctx.effect.stacks), out _);

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = applied ? nameof(CombatEffectType.ApplyStatus) : "StatusBlocked",
                    statusId = ctx.effect.statusId,
                    hpBefore = target.currentHP,
                    hpAfter = target.currentHP,
                });
            }
        }
    }

    /// Removes a specific status, or every status in a dispel category when no id is given.
    public sealed class RemoveStatusEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.RemoveStatus;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null || ctx.status == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                int removed;
                if (!string.IsNullOrEmpty(ctx.effect.statusId))
                    removed = ctx.status.Remove(target, ctx.effect.statusId) ? 1 : 0;
                else
                    removed = ctx.status.RemoveByCategory(target, StatusDispelCategory.Ailment);

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.RemoveStatus),
                    statusId = ctx.effect.statusId,
                    amount = removed,
                    hpBefore = target.currentHP,
                    hpAfter = target.currentHP,
                });
            }
        }
    }

    /// Direct stat buff/debuff for the rest of the battle, without an authored status asset. Uses the
    /// same modifier+sourceId convention so it reverses cleanly and never leaks out of combat.
    public sealed class BuffStatEffectExecutor : IEffectExecutor
    {
        private readonly bool _debuff;
        public BuffStatEffectExecutor(bool debuff) { _debuff = debuff; }

        public CombatEffectType Type => _debuff ? CombatEffectType.DebuffStat : CombatEffectType.BuffStat;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                float value = _debuff ? -Mathf.Abs(ctx.effect.buffValue) : Mathf.Abs(ctx.effect.buffValue);
                // Distinct per action so repeated casts stack rather than overwrite each other.
                string sourceId = $"combatbuff:{target.combatantId}:{ctx.action?.Id}:{ctx.effect.buffStat}";

                target.stats.RemoveModifiersFrom(sourceId);
                target.stats.AddModifier(new StatModifier(
                    ctx.effect.buffStat, ctx.effect.buffModifierType, value, sourceId, isPermanent: false));
                target.stats.Recalculate();

                target.currentHP = Mathf.Clamp(target.currentHP, 0, target.MaxHP);
                target.currentMP = Mathf.Clamp(target.currentMP, 0, target.MaxMP);
                target.currentSP = Mathf.Clamp(target.currentSP, 0, target.MaxSP);

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = _debuff ? nameof(CombatEffectType.DebuffStat) : nameof(CombatEffectType.BuffStat),
                    amount = Mathf.RoundToInt(value),
                    hpBefore = target.currentHP,
                    hpAfter = target.currentHP,
                });
            }
        }
    }

    /// Restores or drains MP/SP/HP directly (ethers, drains, self-costs).
    public sealed class ResourceChangeEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.ResourceChange;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                int before = target.currentHP;
                int delta = ctx.effect.resourceDelta;

                switch (ctx.effect.resourceType)
                {
                    case CombatResource.HP: target.currentHP = Mathf.Clamp(target.currentHP + delta, 0, target.MaxHP); break;
                    case CombatResource.MP: target.currentMP = Mathf.Clamp(target.currentMP + delta, 0, target.MaxMP); break;
                    case CombatResource.SP: target.currentSP = Mathf.Clamp(target.currentSP + delta, 0, target.MaxSP); break;
                }

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.ResourceChange),
                    amount = delta,
                    hpBefore = before,
                    hpAfter = target.currentHP,
                });
            }
        }
    }

}
