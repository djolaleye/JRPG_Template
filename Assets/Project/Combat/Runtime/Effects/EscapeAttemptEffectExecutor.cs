using JRPG.Data;

namespace JRPG.Combat
{
    /// Attempts to flee the battle. 
    ///
    /// Failure of an attempt consumes the actor's turn.
    public sealed class EscapeAttemptEffectExecutor : IEffectExecutor
    {
        public CombatEffectType Type => CombatEffectType.EscapeAttempt;

        public void Execute(EffectContext ctx)
        {
            var battle = ctx.battle;
            if (battle == null || ctx.actor == null) return;

            EncounterData encounter = null;
            if (ctx.data != null && !string.IsNullOrEmpty(battle.encounterId))
                ctx.data.TryGet(battle.encounterId, out encounter);

            var check = EscapeResolver.Evaluate(battle, encounter);

            if (!check.allowed)
            {
                ctx.result.effects.Add(new EffectResult
                {
                    targetCombatantId = ctx.actor.combatantId,
                    effectType = "EscapeBlocked",
                    hpBefore = ctx.actor.currentHP,
                    hpAfter = ctx.actor.currentHP,
                });
                return;
            }

            bool success = ctx.rng == null || ctx.rng.NextDouble() < check.chance;
            if (success) battle.escapeSucceeded = true;

            ctx.result.effects.Add(new EffectResult
            {
                targetCombatantId = ctx.actor.combatantId,
                effectType = success ? "EscapeSucceeded" : "EscapeFailed",
                // Report the odds (as a percentage).
                amount = UnityEngine.Mathf.RoundToInt(check.chance * 100f),
                hpBefore = ctx.actor.currentHP,
                hpAfter = ctx.actor.currentHP,
            });
        }
    }
}
