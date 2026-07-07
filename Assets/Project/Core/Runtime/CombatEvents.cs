namespace JRPG.Core
{
    /// Combat events carry primitives only (matching the GameStateChanged/PartyChanged precedent) so
    /// any assembly can subscribe without referencing JRPG.Combat. The full ActionResult/BattleResultData
    /// are returned directly by CombatService methods and exposed via CombatService.LastResult.

    public readonly struct BattleStarted
    {
        public readonly string BattleId;
        public readonly string EncounterId;
        public BattleStarted(string battleId, string encounterId)
        {
            BattleId = battleId;
            EncounterId = encounterId;
        }
    }

    public readonly struct TurnStarted
    {
        public readonly string CombatantId;
        public TurnStarted(string combatantId) { CombatantId = combatantId; }
    }

    public readonly struct BattleActionResolved
    {
        public readonly string BattleId;
        public readonly string ActorCombatantId;
        public readonly string ActionId;
        public readonly bool Success;
        public BattleActionResolved(string battleId, string actorCombatantId, string actionId, bool success)
        {
            BattleId = battleId;
            ActorCombatantId = actorCombatantId;
            ActionId = actionId;
            Success = success;
        }
    }

    public readonly struct BattleEnded
    {
        public readonly string BattleId;
        public readonly BattleOutcome Outcome;
        public BattleEnded(string battleId, BattleOutcome outcome)
        {
            BattleId = battleId;
            Outcome = outcome;
        }
    }

    public readonly struct BattleResultPackaged
    {
        public readonly string BattleId;
        public readonly BattleOutcome Outcome;
        public BattleResultPackaged(string battleId, BattleOutcome outcome)
        {
            BattleId = battleId;
            Outcome = outcome;
        }
    }

    /// Published when something (e.g. the exploration Attack input) requests a battle to begin.
    /// Lives in Core so exploration can publish it and the combat UI can subscribe without a direct
    /// assembly reference between those domains.
    public readonly struct CombatInitiationRequested
    {
        public readonly string Reason;
        public CombatInitiationRequested(string reason) { Reason = reason; }
    }
}
