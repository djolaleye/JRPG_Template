using JRPG.Data;

namespace JRPG.Combat
{
    /// Executes one kind of CombatEffect. Implementations must:
    ///  - validate their own targets (an executor with nothing valid to hit is a no-op),
    ///  - record structured EffectResult data on ctx.result,
    ///  - never mutate ScriptableObject assets (runtime combatants only),
    ///  - work identically for player, enemy, item, and scripted actions,
    ///  - stay deterministic given ctx.rng.
    public interface IEffectExecutor
    {
        CombatEffectType Type { get; }
        void Execute(EffectContext ctx);
    }
}
