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

    /// Battle-local runtime participant. Effects mutate these values during battle; party combatants
    /// commit HP/MP/SP back to their CharacterRuntimeInstance at battle end. Enemy combatants are
    /// discarded. No asset is ever modified.
    public class CombatantInstance
    {
        public string combatantId;     // unique within battle, e.g. "party_char_hero" or "enemy_slime_0"
        public string sourceDataId;    // CharacterData.Id or EnemyData.Id
        public string sourceRuntimeId; // CharacterRuntimeInstance.InstanceId for party; empty for enemies

        public CombatantTeam team;
        public string displayName;

        public StatBlockRuntime stats;

        public int currentHP;
        public int currentMP;
        public int currentSP;

        public bool isGuarding;
        public float guardDamageMultiplier = 1f; // applied to incoming damage while guarding
        public bool hasActedThisRound;

        public bool IsDefeated => currentHP <= 0;

        public List<StatModifier> temporaryModifiers = new();

        public int MaxHP => stats.GetFinal(StatType.MaxHP);
        public int MaxMP => stats.GetFinal(StatType.MaxMP);
        public int MaxSP => stats.GetFinal(StatType.MaxSP);
        public int Speed => stats.GetFinal(StatType.Speed);
    }
}
