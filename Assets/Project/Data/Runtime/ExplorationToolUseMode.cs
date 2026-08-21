namespace JRPG.Data
{
    /// <summary>
    /// What using an <see cref="ItemCategory.ExplorationTool"/> costs the player.
    ///
    /// <para>Consumption is per item, not per category. Ex: a grappling hook is used forever, a chest key
    /// is spent on the lock it opens, and a one-shot charge is spent only when it actually got the
    /// player somewhere.</para>
    /// </summary>
    public enum ExplorationToolUseMode
    {
        /// Reusable forever — the tool is a capability.
        NeverConsumed = 0,

        /// One unit per use, whether or not the attempt achieved anything.
        ConsumeOnUse = 1,

        /// One unit only when the traversal it was used for succeeded.
        ConsumeWhenTraversalSucceeds = 2,
    }
}
