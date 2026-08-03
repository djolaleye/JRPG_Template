using UnityEngine;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Decides whether a flee attempt succeeds.
    ///
    /// Chance = base + (partyAvgSpeed − enemyAvgSpeed) × speedWeight, divided by the encounter's
    /// escapeDifficulty, then clamped. Boss and non-escapable encounters refuse outright.
    public static class EscapeResolver
    {
        public const float BaseChance = 0.5f;
        public const float SpeedWeight = 0.05f;
        public const float MinChance = 0.10f;
        public const float MaxChance = 1.0f;

        public struct EscapeCheck
        {
            public bool allowed;
            public float chance;
            public string blockedReason;
        }

        /// Evaluates the odds without rolling — used by the UI and by the executor.
        public static EscapeCheck Evaluate(BattleContext battle, EncounterData encounter)
        {
            if (battle == null)
                return new EscapeCheck { allowed = false, blockedReason = "No active battle." };

            if (encounter != null && encounter.isBoss)
                return new EscapeCheck { allowed = false, blockedReason = "You cannot flee from this battle!" };

            if (!battle.escapable)
                return new EscapeCheck { allowed = false, blockedReason = "There is no escape from this fight." };

            float partySpeed = AverageSpeed(battle, CombatantTeam.Party);
            float enemySpeed = AverageSpeed(battle, CombatantTeam.Enemy);
            float difficulty = encounter != null ? Mathf.Max(0.1f, encounter.escapeDifficulty) : 1f;

            float chance = (BaseChance + (partySpeed - enemySpeed) * SpeedWeight) / difficulty;
            
            return new EscapeCheck { allowed = true, chance = Mathf.Clamp(chance, MinChance, MaxChance) };
        }

        public static float AverageSpeed(BattleContext battle, CombatantTeam team)
        {
            var list = team == CombatantTeam.Party ? battle.partyCombatants : battle.enemyCombatants;
            int total = 0, count = 0;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsDefeated) continue;

                total += list[i].Speed;
                count++;
            }

            return count == 0 ? 0f : (float)total / count;
        }
    }
}
