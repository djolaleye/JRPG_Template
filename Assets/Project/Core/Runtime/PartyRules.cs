namespace JRPG.Core
{
    /// <summary>
    /// The party's structural limits, in one place.
    ///
    /// <para><b>Why a constant rather than a field per system.</b> These numbers are read by the
    /// roster, by story scopes, by arena spawn-formation authoring and by the arena validator. Written
    /// separately in each, a change means finding all of them — and the one that gets missed fails
    /// late and quietly: an arena that cannot seat a full party only refuses the day someone actually
    /// fields one.</para>
    ///
    /// <para><b>These are defaults, not hard ceilings.</b> A pushed <c>PartyScope</c> may impose a
    /// lower cap for a story section; <c>PartyService.EffectiveMaxActiveMembers()</c> resolves
    /// scope-over-roster. <see cref="MaxActiveMembers"/> is what an unrestricted party is capped at.
    /// </para>
    ///
    /// </summary>
    public static class PartyRules
    {
        /// <summary>
        /// Combatants the party can field at once.
        ///
        /// <para>Raising this requires authoring a matching spawn formation in every arena prefab —
        /// <c>ArenaRoot.Validate</c> and <c>CombatContext.Validate</c> refuse the battle otherwise,
        /// so the omission surfaces as an error rather than as overlapping bodies.</para>
        /// </summary>
        public const int MaxActiveMembers = 4;

        /// Total roster size, active plus reserve. Stored and saved, but not currently enforced.
        public const int MaxTotalParty = 8;
    }
}
