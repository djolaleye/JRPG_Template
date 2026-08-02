using JRPG.Data;

namespace JRPG.Combat
{
    /// Points at which passives are notified.
    public enum PassiveHook
    {
        BattleStart,
        TurnStart,
        BeforeAction,
        AfterAction,
        TurnEnd,
        RoundEnd,
        OnDamageDealt,
        OnDamageTaken,
        BattleEnd
    }

    /// A runtime passive. Sources are open: character traits, equipment, statuses,
    /// encounter rules, and story/difficulty modifiers all register the same way.
    ///
    /// Passives only see combat runtime types.
    public interface IPassive
    {
        /// Stable id (matches passiveEffectIds on characters/enemies/equipment).
        string Id { get; }
    }

    /// A passive that contributes to damage calculation.
    public interface IPassiveModifier : IPassive
    {
        void ModifyDamage(DamageContext ctx, bool asAttacker);
    }

    /// A passive that reacts to combat events rather than damage numbers (regen, on-kill effects, etc.).
    public interface IPassiveHook : IPassive
    {
        bool HandlesHook(PassiveHook hook);
        void OnHook(PassiveHook hook, CombatantInstance owner, BattleContext battle, ActionResult result);
    }
}
