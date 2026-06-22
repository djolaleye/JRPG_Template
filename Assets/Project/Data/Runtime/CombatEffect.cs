using System;

namespace JRPG.Data
{
    public enum CombatActionCategory
    {
        Melee,
        Guard,
        Item,
        Skill
    }

    public enum CombatEffectType
    {
        Damage,
        Heal,
        Guard
    }

    /// Plain effect data interpreted by the combat resolver. The fields form a small union — the
    /// resolver reads only those relevant to <see cref="type"/> (mirrors the ItemEffect convention).
    [Serializable]
    public struct CombatEffect
    {
        public CombatEffectType type;

        // Damage / Heal shared.
        public int basePower;
        public float statScale;

        // Damage.
        public StatType attackStat;
        public StatType defenseStat;

        // Heal.
        public StatType scalingStat;

        // Guard.
        public float guardMultiplier;
    }
}
