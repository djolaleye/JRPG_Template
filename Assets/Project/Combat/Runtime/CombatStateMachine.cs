namespace JRPG.Combat
{
    /// <summary>
    /// Authoritative combat phase. Assigned by <see cref="CombatService"/>,
    /// which owns its own selection flow (command → action → target → confirm) and reaches combat
    /// solely through <c>SubmitAction</c>. A battle walks:
    ///
    ///   CalculateTurnOrder → AwaitPlayerInput | EnemyAI → ExecuteAction → ResolveEffects
    ///                      → TurnTransition → CheckWinLoss → (next turn | EndBattle)
    /// </summary>
    public enum CombatPhase
    {
        None,
        BuildRuntimeCombatants,
        CalculateTurnOrder,
        AwaitPlayerInput,
        EnemyAI,
        ExecuteAction,
        ResolveEffects,
        TurnTransition,
        CheckWinLoss,
        EndBattle
    }
}
