namespace JRPG.Services
{
    /// <summary>
    /// What the traversal attempt an exploration tool was used for actually did. Primitives only, so
    /// the item never learns about scene objects and the world never learns about item rules.
    /// </summary>
    public readonly struct ExplorationToolUseContext
    {
        /// Stable id of the obstacle the tool was used on. May be empty for a free use.
        public readonly string TargetId;

        /// Whether the traversal this use was for succeeded. Read only by
        /// <c>ExplorationToolUseMode.ConsumeWhenTraversalSucceeds</c>.
        public readonly bool TraversalSucceeded;

        public ExplorationToolUseContext(string targetId, bool traversalSucceeded)
        {
            TargetId = targetId;
            TraversalSucceeded = traversalSucceeded;
        }
    }

    /// <summary>
    /// The single home for the item-category rules: what may be sold, what may be discarded, and what
    /// the four special categories are allowed to do.
    ///
    /// <para><b>Domain.</b> <c>ItemData.canSell</c> and <c>canDiscard</c> are read
    /// here and nowhere else, and every category prohibition overrides them — a key item authored
    /// <c>canDiscard = true</c> still cannot be discarded. Menus ask these questions rather than
    /// re-deriving the answers, so a row is enabled when the operation would succeed and the
    /// greyed-out explanation is the domain's own wording.</para>
    /// </summary>
    public interface IItemRuleService
    {
        // ---- Selling (shop applies its own rules on top) ---------------------

        bool CanSell(string itemId, int quantity, out string reason);

        // ---- Discarding -------------------------------------------------------------------------

        bool CanDiscard(string itemId, int quantity, out string reason);

        /// <summary>Removes exactly <paramref name="quantity"/>. Returns false and changes nothing when refused.</summary>
        bool TryDiscard(string itemId, int quantity);

        // ---- Key items --------------------------------------------------------------------------

        /// <summary>
        /// Whether a key item's story gate is satisfied. A key item is never consumed, so there is no
        /// paired Try.
        /// </summary>
        bool CanUseKeyItem(string itemId, out string reason);

        // ---- Quest items ------------------------------------------------------------------------

        bool CanUseQuestItem(string itemId, string questId, out string reason);

        /// <summary>
        /// Consumes quest items on a successful quest step. Called by the quest flow, never by the
        /// inventory screen. A failed or abandoned interaction must not spend the item.
        /// </summary>
        bool ConsumeQuestItem(string itemId, int quantity, string questId);

        // ---- Exploration tools -------------------------------------------------------------------

        bool CanUseExplorationTool(string itemId, out string reason);

        /// <summary>
        /// Registers a tool use and applies the item's consumption mode. Returns false when the tool
        /// could not be used at all. A successful call may still consume nothing in the case of a reusable tool.
        /// </summary>
        bool TryUseExplorationTool(string itemId, ExplorationToolUseContext context);
    }
}
