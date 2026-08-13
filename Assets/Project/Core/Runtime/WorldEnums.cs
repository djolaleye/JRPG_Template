namespace JRPG.Core
{
    /// <summary>
    /// Lifecycle of a world encounter.
    ///
    /// <para><c>InProgress</c> means the world has committed to resolving this encounter — not that
    /// the Combat scene finished loading. <c>Complete</c> means it was fully resolved, which only
    /// victory plus the whole post-battle flow achieves.</para>
    ///
    /// <para>The values are persisted, so their order is part of the save format.</para>
    /// </summary>
    public enum EncounterState
    {
        Ready = 0,
        InProgress = 1,
        Complete = 2,
    }
}
