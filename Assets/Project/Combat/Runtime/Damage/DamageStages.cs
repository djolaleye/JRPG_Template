using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    // The parity stages formula :
    //     max(1, round(basePower + attackStat * statScale − defenseStat * 0.5)) × guardMultiplier
    // decomposed into ordered, individually replaceable steps.

    /// 1. The action's authored flat power.
    public sealed class BasePowerStage : IDamageStage
    {
        public string Name => "base";
        public void Apply(DamageContext ctx) => ctx.runningDamage += ctx.effect.basePower;
    }

    /// 2. Attacker's offensive stat contribution.
    public sealed class AttackerStatStage : IDamageStage
    {
        public string Name => "atk";
        public void Apply(DamageContext ctx)
        {
            if (ctx.actor?.stats == null) return;

            ctx.runningDamage += ctx.actor.stats.GetFinal(ctx.effect.attackStat) * ctx.effect.statScale;
        }
    }

    /// 5. Target's defensive stat.
    public sealed class DefenseStage : IDamageStage
    {
        public const float DefenseCoefficient = 0.5f;
        public string Name => "def";
        public void Apply(DamageContext ctx)
        {
            if (ctx.target?.stats == null) return;

            ctx.runningDamage -= ctx.target.stats.GetFinal(ctx.effect.defenseStat) * DefenseCoefficient;
        }
    }

    /// 11. Guard and defensive reactions. Reads the target's guard multiplier, matching prior behavior
    public sealed class GuardStage : IDamageStage
    {
        public string Name => "guard";
        public void Apply(DamageContext ctx)
        {
            if (ctx.target == null || !ctx.target.isGuarding) return;

            ctx.runningDamage = Mathf.Max(1, Mathf.RoundToInt(ctx.runningDamage * ctx.target.guardDamageMultiplier));
        }
    }

    /// 12. Final clamp and rounding. Immune, Absorbed hits skip the >= 1 floor.
    public sealed class ClampRoundStage : IDamageStage
    {
        public string Name => "clamp";
        public void Apply(DamageContext ctx)
        {
            if (ctx.immune) { ctx.runningDamage = 0f; ctx.finalAmount = 0; return; }
            if (ctx.missed) { ctx.finalAmount = 0; return; }

            int rounded = Mathf.RoundToInt(ctx.runningDamage);
            ctx.finalAmount = ctx.absorbed ? Mathf.Max(0, rounded) : Mathf.Max(1, rounded);
        }
    }

    // ---- Placeholder stages -------------------------------------------------------------------
    // These hold their slot in the ordering and are swapped for live implementations by later
    // sub-phases (DamagePipelineFactory documents which). They must not alter the value.

    /// 3. Equipment contribution — activated in 11.4 (equipment passives).
    public sealed class EquipmentStage : IDamageStage
    {
        public string Name => "equip";
        public void Apply(DamageContext ctx) { }
    }

    /// 4. Accuracy vs evasion — activated in 11.3+ (needs the RNG + status accuracy modifiers).
    public sealed class AccuracyEvasionStage : IDamageStage
    {
        public string Name => "acc";
        public void Apply(DamageContext ctx) { }
    }

    /// 6. Elemental interaction — activated in 11.2.
    public sealed class ElementalStage : IDamageStage
    {
        public string Name => "elem";
        public void Apply(DamageContext ctx) { }
    }

    /// 7. Critical hits — activated in 11.3+.
    public sealed class CriticalStage : IDamageStage
    {
        public string Name => "crit";
        public void Apply(DamageContext ctx) { }
    }

    /// 8. Status-driven modifiers — activated in 11.3.
    public sealed class StatusModifierStage : IDamageStage
    {
        public string Name => "status";
        public void Apply(DamageContext ctx) { }
    }

    /// 9. Passive modifiers — activated in 11.4.
    public sealed class PassiveModifierStage : IDamageStage
    {
        public string Name => "passive";
        public void Apply(DamageContext ctx) { }
    }

    /// 10. Encounter/difficulty scaling — authored hook, inert until content uses it.
    public sealed class DifficultyStage : IDamageStage
    {
        public string Name => "diff";
        public void Apply(DamageContext ctx) { }
    }
}
