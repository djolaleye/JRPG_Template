namespace JRPG.Combat
{
    /// Strict but small combat phase model. Phase 6 drives SelectCombatAction/SelectTargets from debug
    /// code; Phase 7 replaces those with command menus and target-selection UI.
    public enum CombatPhase
    {
        None,
        InitBattle,
        BuildRuntimeCombatants,
        CalculateTurnOrder,
        AwaitPlayerInput,
        EnemyAI,
        SelectCombatAction,
        SelectTargets,
        ExecuteAction,
        ResolveEffects,
        CheckWinLoss,
        EndBattle
    }
}
