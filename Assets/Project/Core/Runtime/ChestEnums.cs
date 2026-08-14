namespace JRPG.Core
{
    /// <summary>
    /// Lifecycle of a placed world chest.
    ///
    /// <para><c>Opened</c> is terminal for the lifetime of a save: no interaction, restore or scene
    /// reload may move a chest back to <c>Locked</c> or <c>Unopened</c>. <c>Locked</c> differs from
    /// <c>Unopened</c> only in requiring a chest key to be consumed on the successful open.</para>
    ///
    /// <para>The values are persisted, so their order is part of the save format.</para>
    /// </summary>
    public enum ChestOpenedState
    {
        Unopened = 0,
        Locked = 1,
        Opened = 2,
    }
}
