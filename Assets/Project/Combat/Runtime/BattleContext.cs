using System.Collections.Generic;
using JRPG.Characters;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Combat
{
    /// Active battle snapshot owned by CombatService. 
    public class BattleContext
    {
        public string battleId;
        public string encounterId;
        // [Planned — Phase 11] Escape rules. Plumbed from EncounterData.escapable but not yet read;
        // BattleOutcome.Escaped is likewise unreachable until the escape action exists.
        public bool escapable;

        public List<CombatantInstance> partyCombatants = new();
        public List<CombatantInstance> enemyCombatants = new();

        public Queue<string> turnQueue = new();
        public int roundNumber = 1;

        public CombatPhase phase = CombatPhase.None;

        public CombatantInstance currentActor;

        public bool isBattleOver;
        public BattleOutcome outcome = BattleOutcome.None;

        /// Maps a party combatantId to the persistent runtime instance it was built from, so HP/MP/SP
        /// can be committed back at battle end.
        public Dictionary<string, CharacterRuntimeInstance> partySources = new();

        public IEnumerable<CombatantInstance> AllCombatants()
        {
            for (int i = 0; i < partyCombatants.Count; i++) yield return partyCombatants[i];
            for (int i = 0; i < enemyCombatants.Count; i++) yield return enemyCombatants[i];
        }

        public CombatantInstance FindCombatant(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId)) return null;

            for (int i = 0; i < partyCombatants.Count; i++)
                if (partyCombatants[i].combatantId == combatantId) return partyCombatants[i];
            
            for (int i = 0; i < enemyCombatants.Count; i++)
                if (enemyCombatants[i].combatantId == combatantId) return enemyCombatants[i];
            return null;
        }
    }
}
