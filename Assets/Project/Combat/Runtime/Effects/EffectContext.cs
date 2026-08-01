using System.Collections.Generic;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Combat
{
    /// Everything an effect executor needs to do its job. Passed by reference for one effect of one
    /// action; executors must not cache it.
    public sealed class EffectContext
    {
        // ---- Per-effect data ----
        public CombatantInstance actor;
        public CombatEffect effect;
        public CombatActionData action;
        public List<CombatantInstance> targets;
        public ActionResult result;

        /// The battle this action belongs to. Null in isolated unit tests.
        public BattleContext battle;

        // ---- Shared services (assigned once by the resolver) ----
        public DataRegistry data;
        public IInventoryService inventory;
        public DamagePipeline damage;
        public System.Random rng;

        /// Deterministic roll helper for chance-gated effects.
        public bool Roll(float chance)
        {
            if (chance >= 1f) return true;
            if (chance <= 0f) return true;   // unauthored (0) means "always"

            return rng == null || rng.NextDouble() < chance;
        }
    }
}
