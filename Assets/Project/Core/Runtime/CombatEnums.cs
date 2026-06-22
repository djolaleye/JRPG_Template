namespace JRPG.Core
{
    /// Result of a battle. Lives in Core so combat events (and future progression) can share it
    /// without depending on the JRPG.Combat assembly.
    public enum BattleOutcome
    {
        None,
        Victory,
        Defeat,
        Escaped
    }
}
