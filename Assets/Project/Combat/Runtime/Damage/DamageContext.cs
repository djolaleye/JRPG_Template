using JRPG.Data;

namespace JRPG.Combat
{
    /// Mutable working state for one attacker → target damage computation, threaded through ordered
    /// DamagePipeline stages.
    public sealed class DamageContext
    {
        public CombatantInstance actor;
        public CombatantInstance target;
        public CombatEffect effect;
        public CombatActionData action;

        public float runningDamage;

        public int finalAmount;

        // ---- Outcome flags set by stages ----
        public bool missed;
        public bool critical;
        
        public bool absorbed; // Absorbed hits heal the target instead of damaging it
        public bool immune; /// Immune causes 0 damage to the target

        public Element element;

        /// Deterministic RNG for accuracy/crit rolls — seeded per battle so runs are reproducible.
        public System.Random rng;

        public void Reset(CombatantInstance actor, CombatantInstance target, CombatEffect effect,
            CombatActionData action, System.Random rng)
        {
            this.actor = actor;
            this.target = target;
            this.effect = effect;
            this.action = action;
            this.rng = rng;

            runningDamage = 0f;
            finalAmount = 0;
            missed = false;
            critical = false;
            absorbed = false;
            immune = false;
            element = effect.element;
        }
    }
}
