using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Resolves passive ids to runtime passive implementations and dispatches hooks.
    ///
    /// Lookup is by id against a registry populated at bootstrap
    public sealed class PassiveRegistry
    {
        private readonly Dictionary<string, IPassive> _passives = new();

        public PassiveRegistry Register(IPassive passive)
        {
            if (passive != null && !string.IsNullOrEmpty(passive.Id)) _passives[passive.Id] = passive;

            return this;
        }

        public bool TryGet(string id, out IPassive passive) => _passives.TryGetValue(id, out passive);

        public int Count => _passives.Count;

        /// Applies every damage-modifying passive owned by `owner`.
        public void ModifyDamage(CombatantInstance owner, DamageContext ctx, bool asAttacker)
        {
            if (owner?.profile == null) return;

            var ids = owner.profile.passiveEffectIds;

            for (int i = 0; i < ids.Count; i++) 
            {
                if (_passives.TryGetValue(ids[i], out var p) && p is IPassiveModifier mod)
                {
                    mod.ModifyDamage(ctx, asAttacker);
                }
            }
        }

        /// Fires a hook for every passive owned by `owner` that handles it.
        public void Dispatch(PassiveHook hook, CombatantInstance owner, BattleContext battle, ActionResult result = null)
        {
            if (owner?.profile == null) return;

            var ids = owner.profile.passiveEffectIds;

            for (int i = 0; i < ids.Count; i++)
            {
                if (_passives.TryGetValue(ids[i], out var p) && p is IPassiveHook h && h.HandlesHook(hook))
                {
                    h.OnHook(hook, owner, battle, result);
                }
            }
        }

        /// Fires a hook for every combatant in the battle.
        public void DispatchAll(PassiveHook hook, BattleContext battle, ActionResult result = null)
        {
            if (battle == null) return;
            
            foreach (var c in battle.AllCombatants()) Dispatch(hook, c, battle, result);
        }

        /// The built-in sample passives. Content can register more (or override these ids) at bootstrap.
        public static PassiveRegistry CreateStandard()
        {
            return new PassiveRegistry()
                .Register(new ElementalAffinityPassive("passive_fire_ward", Element.Fire, 0.5f))
                .Register(new FlatDamageBonusPassive("passive_power_strike", 1.25f))
                .Register(new RegenPassive("passive_regen", 3));
        }
    }
}
