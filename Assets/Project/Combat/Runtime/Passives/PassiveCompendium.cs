using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{

    // ------------------ DEFENSIVE PASSIVES------------------

    /// Reduces incoming damage of one element by a fixed fraction. Defensive passive
    /// that stacks with (and is independent of) the elemental affinity matrix.
    public sealed class ElementalAffinityPassive : IPassiveModifier
    {
        private readonly Element _element;
        private readonly float _multiplier;

        public ElementalAffinityPassive(string id, Element element, float multiplier)
        {
            Id = id;
            _element = element;
            _multiplier = multiplier;
        }

        public string Id { get; }

        public void ModifyDamage(DamageContext ctx, bool asAttacker)
        {
            if (asAttacker) return;                 // defensive only
            if (ctx.element != _element) return;

            ctx.runningDamage *= _multiplier;
        }
    }

    // ------------------ OFFENSIVE PASSIVES------------------

    /// Scales outgoing damage.
    public sealed class FlatDamageBonusPassive : IPassiveModifier
    {
        private readonly float _multiplier;

        public FlatDamageBonusPassive(string id, float multiplier)
        {
            Id = id;
            _multiplier = multiplier;
        }

        public string Id { get; }

        public void ModifyDamage(DamageContext ctx, bool asAttacker)
        {
            if (!asAttacker) return;                // offensive only

            ctx.runningDamage *= _multiplier;
        }
    }

    // ------------------ HOOK BASED PASSIVES------------------

    /// Restores HP at the end of each of the owner's turns. Reacts to combat events.
    public sealed class RegenPassive : IPassiveHook
    {
        private readonly int _amount;

        public RegenPassive(string id, int amount)
        {
            Id = id;
            _amount = amount;
        }

        public string Id { get; }

        public bool HandlesHook(PassiveHook hook) => hook == PassiveHook.TurnEnd;

        public void OnHook(PassiveHook hook, CombatantInstance owner, BattleContext battle, ActionResult result)
        {
            if (owner == null || owner.IsDefeated) return;

            int before = owner.currentHP;
            owner.currentHP = Mathf.Min(owner.MaxHP, owner.currentHP + _amount);
            if (owner.currentHP == before) return;

            result?.effects.Add(new EffectResult
            {
                targetCombatantId = owner.combatantId,
                effectType = "PassiveRegen",
                amount = owner.currentHP - before,
                hpBefore = before,
                hpAfter = owner.currentHP,
            });
        }
    }
}
