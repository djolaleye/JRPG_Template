namespace JRPG.Combat
{
    /// Global knobs for stat-driven parts of the damage pipeline.
    ///
    /// The defaults are ZERO-IMPACT: stat-derived evasion and criticals are switched off,
    /// so existing authored content resolves exactly as before and the damage-parity guarantee
    /// still holds. Statuses can still grant accuracy/evasion/crit modifiers (those always apply) — this
    /// only controls how much the Evasion and Luck *stats* contribute. Content that wants stat-based
    /// dodging or crits raises these.
    public sealed class CombatTuning
    {
        /// Dodge chance contributed per point of the target's Evasion stat.
        public float evasionPerPoint = 0f;

        /// Critical chance contributed per point of the attacker's Luck stat.
        public float critChancePerLuck = 0f;

        /// Damage multiplier applied on a critical hit.
        public float critMultiplier = 1.5f;

        public static CombatTuning Default => new();
    }
}
