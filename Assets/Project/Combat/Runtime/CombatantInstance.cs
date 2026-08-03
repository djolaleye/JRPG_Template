using System.Collections.Generic;
using JRPG.Characters;
using JRPG.Data;

namespace JRPG.Combat
{
    public enum CombatantTeam
    {
        Party,
        Enemy
    }

    /// <summary>
    /// Battle-local runtime participant. Effects mutate these values during battle; party combatants
    /// commit HP/MP/SP back to their CharacterRuntimeInstance at battle end.
    /// </summary>
    public class CombatantInstance
    {
        public string combatantId;
        public string sourceDataId;
        public string sourceRuntimeId;

        public CombatantTeam team;
        public string displayName;

        public StatBlockRuntime stats;

        public int currentHP;
        public int currentMP;
        public int currentSP;

        public bool isGuarding;
        public float guardDamageMultiplier = 1f;

        /// Snapshot of elemental affinities / immunities / passives
        public CombatProfile profile = new();

        public readonly List<StatusEffectInstance> activeStatuses = new();

        /// Taunt-style override: while set, this combatant's single-target actions are redirected to
        /// the named combatant (when it is still a legal target). Battle-local.
        public string forcedTargetCombatantId;

        public bool IsDefeated => currentHP <= 0;

        public int MaxHP => stats.GetFinal(StatType.MaxHP);
        public int MaxMP => stats.GetFinal(StatType.MaxMP);
        public int MaxSP => stats.GetFinal(StatType.MaxSP);
        public int Speed => stats.GetFinal(StatType.Speed);
    }
}
