using System.Collections.Generic;

namespace JRPG.Combat
{
    /// <summary>
    /// The per-target detail of an action that just resolved: damage and heal amounts, crits, misses,
    /// absorbs, immunities, elements, status applications, and downs.
    ///
    /// <para><b>Why this is separate from <c>BattleActionResolved</c>.</b> That event lives in
    /// <c>JRPG.Core</c> and deliberately carries only primitives, so assemblies that know nothing about
    /// combat can subscribe. It therefore cannot carry <see cref="EffectResult"/> — and the resolver
    /// populates that richly (amount, critical, missed, absorbed, immune, element) only for it all to be
    /// dropped. Damage numbers, hit reactions and element feedback need that data.</para>
    ///
    /// <para>This event lives in <c>JRPG.Combat</c> instead, because its only consumer is
    /// <c>JRPG.Combat.UI</c>, which already references this assembly. The bus only requires
    /// <c>where T : struct</c>; it does not require the event to live in Core.</para>
    ///
    /// <para><b>Published for every action, from either team.</b> Enemy turns resolve through the same
    /// <c>SubmitAction</c> path, so an enemy hit produces floaters exactly like a party hit.</para>
    /// </summary>
    public readonly struct BattleEffectsResolved
    {
        public readonly string BattleId;
        public readonly string ActorCombatantId;
        public readonly string ActionId;
        public readonly bool Success;

        /// <summary>
        /// The resolved per-target effects, in resolution order. Never null; empty when the action
        /// produced none (a failed submission, or an action whose whole effect was a status that did
        /// not land).
        /// </summary>
        public readonly IReadOnlyList<EffectResult> Effects;
        public readonly IReadOnlyList<string> DefeatedCombatantIds;

        public BattleEffectsResolved(string battleId, string actorCombatantId, string actionId,
                                     bool success, IReadOnlyList<EffectResult> effects,
                                     IReadOnlyList<string> defeatedCombatantIds)
        {
            BattleId = battleId;
            ActorCombatantId = actorCombatantId;
            ActionId = actionId;
            Success = success;
            Effects = effects ?? System.Array.Empty<EffectResult>();
            DefeatedCombatantIds = defeatedCombatantIds ?? System.Array.Empty<string>();
        }
    }
}
