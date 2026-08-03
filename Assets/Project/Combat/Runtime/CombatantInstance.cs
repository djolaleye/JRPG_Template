using System.Collections.Generic;
using UnityEngine;
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

        /// Authored per-instance identity from the encounter roster (e.g. "slime_left"), so two copies
        /// of the same EnemyData can be told apart by triggers and AI. Empty for party members.
        public string encounterSlotId;

        /// Remaining cooldown turns per action id.
        public readonly Dictionary<string, int> cooldowns = new();

        public bool IsOnCooldown(string actionId)
            => !string.IsNullOrEmpty(actionId) && cooldowns.TryGetValue(actionId, out var turns) && turns > 0;

        public int GetCooldown(string actionId)
            => !string.IsNullOrEmpty(actionId) && cooldowns.TryGetValue(actionId, out var turns) ? Mathf.Max(0, turns) : 0;

        public void StartCooldown(string actionId, int turns)
        {
            if (string.IsNullOrEmpty(actionId) || turns <= 0) return;

            cooldowns[actionId] = turns;
        }

        public void TickCooldowns()
        {
            if (cooldowns.Count == 0) return;

            var keys = new List<string>(cooldowns.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                int remaining = cooldowns[keys[i]] - 1;
                
                if (remaining <= 0) cooldowns.Remove(keys[i]);
                else cooldowns[keys[i]] = remaining;
            }
        }

        public bool IsDefeated => currentHP <= 0;

        public int MaxHP => stats.GetFinal(StatType.MaxHP);
        public int MaxMP => stats.GetFinal(StatType.MaxMP);
        public int MaxSP => stats.GetFinal(StatType.MaxSP);
        public int Speed => stats.GetFinal(StatType.Speed);
    }
}
