namespace JRPG.Data
{
    /// Damage elements
    public enum Element
    {
        Physical,
        Neutral,
        Fire,
        Ice,
        Electric,
        Earth,
        Wind,
        Water,
        Light,
        Dark
    }

    /// How a defender responds to an element. Resolved against the interaction matrix.
    public enum ElementAffinity
    {
        Normal,
        Weak,
        Resist,
        Immune,
        Absorb
    }

    /// Which resource a ResourceChange effect moves.
    public enum CombatResource
    {
        HP,
        MP,
        SP
    }
}
