using JRPG.Data;

namespace JRPG.Combat
{
    /// Raises the actor's guard. Self-only by nature — it ignores the resolved target list. The guard
    /// flag is cleared when the guarding combatant's own next turn begins (CombatService).
    public sealed class GuardEffectExecutor : IEffectExecutor
    {
        public const float DefaultGuardMultiplier = 0.5f;

        public CombatEffectType Type => CombatEffectType.Guard;

        public void Execute(EffectContext ctx)
        {
            var actor = ctx.actor;
            if (actor == null) return;

            actor.isGuarding = true;
            actor.guardDamageMultiplier = ctx.effect.guardMultiplier <= 0f
                ? DefaultGuardMultiplier
                : ctx.effect.guardMultiplier;

            ctx.result.effects.Add(new EffectResult
            {
                targetCombatantId = actor.combatantId,
                effectType = nameof(CombatEffectType.Guard),
                amount = 0,
                hpBefore = actor.currentHP,
                hpAfter = actor.currentHP,
            });
        }
    }
}
