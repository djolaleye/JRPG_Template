using UnityEngine;
using JRPG.Data;
using System;

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

    /// <summary>
    /// 2b. Basic attack of the weapon in hand.
    ///
    /// <para><b>Weapon stats, ranged only.</b> <see cref="AttackerStatStage"/> added the full stat
    /// total, which includes both weapons because equipment modifiers all land on one stat block.
    /// This trades the melee weapon's share for the ranged weapon's, so a bow shot is scored by the bow
    /// rather than by the sword plus the bow.</para>
    ///
    /// <para>Inert for every other category, and for enemies, which carry no equipment.</para>
    /// </summary>
    public sealed class WeaponSlotStage : IDamageStage
    {
        public string Name => "weapon";

        public void Apply(DamageContext ctx)
        {
            if (ctx.action == null) return;

            bool isRanged = ctx.action.category == CombatActionCategory.Ranged;
            bool isMelee = ctx.action.category == CombatActionCategory.Melee;
            if (!isRanged && !isMelee) return;

            var profile = ctx.actor?.profile;
            if (profile == null) return;

            // Base power: replace the action's authored value with the weapon's, when it has one.
            if (profile.HasWeaponBasePower(isRanged))
                ctx.runningDamage += profile.WeaponBasePower(isRanged) - ctx.effect.basePower;

            if (!isRanged || !profile.HasRangedWeapon) return;

            float melee = profile.WeaponStat(isRanged: false, ctx.effect.attackStat);
            float ranged = profile.WeaponStat(isRanged: true, ctx.effect.attackStat);

            // Scaled the same way AttackerStatStage scaled the total it is correcting.
            ctx.runningDamage += (ranged - melee) * ctx.effect.statScale;
        }
    }

    /// 3. Accuracy vs evasion.
    public sealed class AccuracyEvasionStage : IDamageStage
    {
        private readonly StatusProcessor _status;
        private readonly CombatTuning _tuning;

        public AccuracyEvasionStage(StatusProcessor status, CombatTuning tuning)
        {
            _status = status;
            _tuning = tuning ?? CombatTuning.Default;
        }

        public string Name => "acc";

        public void Apply(DamageContext ctx)
        {
            if (ctx.action == null) return;
            if (ctx.target == null || ctx.rng == null
                || (ctx.action.targetRule.team != TargetTeam.Enemies && ctx.action.targetRule.team != TargetTeam.All)) return;

            float hitChance = ctx.effect.hitChance <= 0f ? 1f : ctx.effect.hitChance;

            if (_status != null)
            {
                hitChance += _status.GetAccuracyModifier(ctx.actor);
                hitChance -= _status.GetEvasionModifier(ctx.target);
            }

            if (_tuning.evasionPerPoint > 0f && ctx.target.stats != null)
                hitChance -= ctx.target.stats.GetFinal(StatType.Evasion) * _tuning.evasionPerPoint;

            if (hitChance >= 1f) return;             // can't miss == skip the roll

            if (ctx.isPreview) return;

            if (ctx.rng.NextDouble() >= hitChance) ctx.missed = true;
        }
    }

    /// 4. Target's defensive stat.
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

    /// 5. Elemental interaction.
    public sealed class ElementalStage : IDamageStage
    {
        private readonly ElementInteractionMatrix _matrix;

        public ElementalStage(ElementInteractionMatrix matrix)
        {
            _matrix = matrix;
        }

        public string Name => "elem";

        public void Apply(DamageContext ctx)
        {
            if (ctx.target?.profile == null) return;

            var affinity = ctx.target.profile.GetAffinity(ctx.element);

            if (affinity == ElementAffinity.Immune)
            {
                ctx.immune = true;
                ctx.runningDamage = 0f;
                return;
            }

            if (affinity == ElementAffinity.Absorb) ctx.absorbed = true;

            float multiplier = _matrix != null
                ? _matrix.GetMultiplier(ctx.element, affinity)
                : 1f;

            ctx.runningDamage *= multiplier;
        }
    }

    /// 6. Critical hits. Chance comes from the attacker's Luck stat (off by default via CombatTuning)
    /// plus any status crit modifiers; a crit scales damage by tuning.critMultiplier.
    public sealed class CriticalStage : IDamageStage
    {
        private readonly StatusProcessor _status;
        private readonly CombatTuning _tuning;

        public CriticalStage(StatusProcessor status, CombatTuning tuning)
        {
            _status = status;
            _tuning = tuning ?? CombatTuning.Default;
        }

        public string Name => "crit";

        public void Apply(DamageContext ctx)
        {
            if (ctx.rng == null || ctx.actor == null
                || ctx.effect.type != CombatEffectType.Damage
                || ctx.effect.element != Element.Physical) return;

            float critChance = 0f;

            if (_tuning.critChancePerLuck > 0f && ctx.actor.stats != null)
                critChance += ctx.actor.stats.GetFinal(StatType.Luck) * _tuning.critChancePerLuck;
            if (_status != null) critChance += _status.GetCritChanceModifier(ctx.actor);

            if (critChance <= 0f) return;

            // Preview shows the non-critical baseline, for the same reason accuracy does not roll.
            if (ctx.isPreview) return;

            if (ctx.rng.NextDouble() >= critChance) return;

            ctx.critical = true;
            ctx.runningDamage *= _tuning.critMultiplier;
        }
    }

    /// 7. Status-driven damage modifiers: what the attacker's statuses do to damage dealt, and what the
    /// target's statuses do to damage taken.
    public sealed class StatusModifierStage : IDamageStage
    {
        private readonly StatusProcessor _status;

        public StatusModifierStage(StatusProcessor status)
        {
            _status = status;
        }

        public string Name => "status";

        public void Apply(DamageContext ctx)
        {
            if (_status == null) return;

            ctx.runningDamage *= _status.GetDamageDealtMultiplier(ctx.actor);
            ctx.runningDamage *= _status.GetDamageTakenMultiplier(ctx.target);
        }
    }

    /// 8. Passive modifiers. Applies the attacker's offensive passives and the defender's defensive
    /// ones. Resolved from each combatant's CombatProfile.
    public sealed class PassiveModifierStage : IDamageStage
    {
        private readonly PassiveRegistry _passives;

        public PassiveModifierStage(PassiveRegistry passives)
        {
            _passives = passives;
        }

        public string Name => "passive";

        public void Apply(DamageContext ctx)
        {
            if (_passives == null) return;
            
            _passives.ModifyDamage(ctx.actor, ctx, asAttacker: true);
            _passives.ModifyDamage(ctx.target, ctx, asAttacker: false);
        }
    }

    /// 9. Encounter/difficulty scaling — hook, inert until needed.
    public sealed class DifficultyStage : IDamageStage
    {
        public string Name => "diff";
        public void Apply(DamageContext ctx) { }
    }

    /// 10. Guard and defensive reactions. Reads the target's guard multiplier, matching prior behavior
    public sealed class GuardStage : IDamageStage
    {
        public string Name => "guard";
        public void Apply(DamageContext ctx)
        {
            if (ctx.target == null || !ctx.target.isGuarding) return;

            ctx.runningDamage = Mathf.Max(1, Mathf.RoundToInt(ctx.runningDamage * ctx.target.guardDamageMultiplier));
        }
    }

    /// 11. Final clamp and rounding. Immune, Absorbed hits skip the >= 1 floor.
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
}
