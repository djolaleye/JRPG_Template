namespace JRPG.Data
{
    /// When a battle trigger is evaluated. Evaluation happens only at safe combat transition points
    /// (turn boundaries / after an action fully resolves), never mid-effect-mutation.
    public enum BattleTriggerTiming
    {
        BattleStart,
        TurnStart,
        TurnEnd,
        ActionResolved,
        HpThresholdCrossed,
        PartyMemberDefeated,
        EnemyDefeated,
        RoundReached,
        SpecificActionUsed,
        CombatantActingOrTargeted
    }

    /// Restricts which team a trigger's combatant condition/filter applies to.
    public enum CombatantTeamFilter
    {
        Any,
        Party,
        Enemy
    }
}
