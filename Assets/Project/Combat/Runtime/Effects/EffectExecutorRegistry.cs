using System.Collections.Generic;
using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Maps CombatEffectType → executor .Adding an effect kind
    /// means registering an executor, not editing action execution.
    public sealed class EffectExecutorRegistry
    {
        private readonly Dictionary<CombatEffectType, IEffectExecutor> _executors = new();

        public EffectExecutorRegistry Register(IEffectExecutor executor)
        {
            if (executor == null) return this;

            _executors[executor.Type] = executor;   // last registration wins (allows overriding)

            return this;
        }

        public bool TryGet(CombatEffectType type, out IEffectExecutor executor)
            => _executors.TryGetValue(type, out executor);

        public void Execute(EffectContext ctx)
        {
            if (!_executors.TryGetValue(ctx.effect.type, out var executor))
            {
                Debug.LogWarning($"[JRPG.Combat] No executor registered for effect '{ctx.effect.type}' — skipped.");
                return;
            }
            executor.Execute(ctx);
        }

        public static EffectExecutorRegistry CreateStandard()
        {
            return new EffectExecutorRegistry()
                .Register(new DamageEffectExecutor())
                .Register(new HealEffectExecutor())
                .Register(new GuardEffectExecutor());
        }
    }
}
