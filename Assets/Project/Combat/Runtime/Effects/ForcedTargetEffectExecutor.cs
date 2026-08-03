using JRPG.Data;

namespace JRPG.Combat
{
    /// Taunt / redirect. Forces the resolved targets to aim their single-target actions at the actor
    ///
    /// The redirect lives on the affected combatant and is honoured by TargetingSystem.
    public sealed class ForcedTargetEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.ForcedTargetChange;

        public void Execute(EffectContext ctx)
        {
            if (ctx.targets == null || ctx.actor == null) return;

            // stringArg may name an explicit combatant; otherwise the actor draws the aggro.
            string forcedTo = string.IsNullOrEmpty(ctx.effect.stringArg)
                ? ctx.actor.combatantId
                : ctx.effect.stringArg;

            bool clearing = ctx.effect.boolArg;

            for (int i = 0; i < ctx.targets.Count; i++)
            {
                var target = ctx.targets[i];
                if (target == null) continue;

                target.forcedTargetCombatantId = clearing ? null : forcedTo;

                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = target.combatantId,
                    effectType = nameof(CombatEffectType.ForcedTargetChange),
                    hpBefore = target.currentHP,
                    hpAfter = target.currentHP,
                });
            }
        }
    }
}
