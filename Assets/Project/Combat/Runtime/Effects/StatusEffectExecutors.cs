using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Inflicts a status on every resolved target.
    ///
    /// The effect carries only the status id: duration, stack rules, stat modifiers, ticks, and
    /// restrictions all come from the referenced StatusEffectData, so a status behaves identically
    /// however it is inflicted.
    public sealed class ApplyStatusEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.ApplyStatus;

        public void Execute(EffectContext ctx) => ApplyStatusTo(ctx, nameof(CombatEffectType.ApplyStatus));

        /// Shared by Buff/Debuff executors
        internal static void ApplyStatusTo(EffectContext ctx, string label)
        {
            if (ctx.targets == null || ctx.status == null || string.IsNullOrEmpty(ctx.effect.statusId)) return;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null || target.IsDefeated) continue;

                bool applied = ctx.status.TryApply(target, ctx.effect.statusId, ctx.actor?.combatantId, out _);

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = applied ? label : "StatusBlocked",
                    statusId = ctx.effect.statusId,
                    hpBefore = target.currentHP,
                    hpAfter = target.currentHP,
                });
            }
        }
    }

    /// Removes a named status, or — when no id is authored — every status in the effect's dispel
    /// category
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
                    removed = ctx.status.RemoveByCategory(target, ctx.effect.dispelCategory);

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

    /// Applies a buff (or debuff) status. Identical to ApplyStatus — the separate effect
    /// types exist so intent is explicit and mis-categorised statuses are caught early.
    public sealed class BuffStatEffectExecutor : IEffectExecutor
    {
        private readonly bool _debuff;

        public BuffStatEffectExecutor(bool debuff) { _debuff = debuff; }

        public CombatEffectType Type => _debuff ? CombatEffectType.DebuffStat : CombatEffectType.BuffStat;

        public void Execute(EffectContext ctx)
        {
            WarnOnCategoryMismatch(ctx);
            ApplyStatusEffectExecutor.ApplyStatusTo(ctx,
                _debuff ? nameof(CombatEffectType.DebuffStat) : nameof(CombatEffectType.BuffStat));
        }

        private void WarnOnCategoryMismatch(EffectContext ctx)
        {
            if (ctx.data == null || string.IsNullOrEmpty(ctx.effect.statusId)) return;
            if (!ctx.data.TryGet<StatusEffectData>(ctx.effect.statusId, out var s) || s == null) return;

            var expected = _debuff ? StatusDispelCategory.Debuff : StatusDispelCategory.Buff;
            
            if (s.dispelCategory != expected)
                Debug.LogWarning($"[JRPG.Combat] Action '{ctx.action?.Id}' applies '{s.Id}' as a " +
                                 $"{expected} but the status is categorised {s.dispelCategory}; " +
                                 "dispel effects will not match it.");
        }
    }

    /// Restores or drains MP/SP/HP immediately.
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
