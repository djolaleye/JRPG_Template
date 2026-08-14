namespace JRPG.Combat
{
    /// <summary>
    /// Global knobs for the stat-driven and randomised parts of the damage pipeline.
    ///
    /// <para><b>Stat contributions default to zero-impact.</b> Stat-derived evasion and criticals are
    /// switched off, so authored content resolves the same with or without them. Statuses can still
    /// grant accuracy/evasion/crit modifiers — those always apply; these knobs only control how much
    /// the Evasion and Luck <i>stats</i> contribute. Content that wants stat-based dodging or crits
    /// raises them.</para>
    ///
    /// <para><b><see cref="damageVariance"/> is the exception, and is deliberately non-zero.</b></para>
    /// </summary>
    public sealed class CombatTuning
    {
        /// Dodge chance contributed per point of the target's Evasion stat.
        public float evasionPerPoint = 0f;

        /// Critical chance contributed per point of the attacker's Luck stat.
        public float critChancePerLuck = 0f;

        /// Damage multiplier applied on a critical hit.
        public float critMultiplier = 1.5f;

        /// Fraction the final damage figure may swing either way. 0 restores fully deterministic damage.
        public float damageVariance = 0.15f;

        public static CombatTuning Default => new();
    }
}
