using System.Collections.Generic;

namespace JRPG.Services
{
    /// Lean cross-assembly surface: primitive APIs only (ids/bools), mirroring IPartyService.
    /// Rich types (BattleContext, ActionResult, CombatActionData) are exposed by the concrete
    /// CombatService in JRPG.Combat for the sandbox runner and future progression phase.
    public interface ICombatService
    {
        bool IsInBattle { get; }

        void StartBattleFromActiveParty(string encounterId);
        void StartBattle(string encounterId, IReadOnlyList<string> partyIds, IReadOnlyList<string> enemyIds);

        bool SubmitAction(string combatantId, string actionId, IReadOnlyList<string> targetIds);

        void AdvanceEnemyTurn();
        void EndBattle();
    }
}
