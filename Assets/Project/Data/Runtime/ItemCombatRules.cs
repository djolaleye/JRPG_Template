namespace JRPG.Data
{
    /// <summary>
    /// The single interpretation of "may this item be used in combat", shared by the combat item menu
    /// (which filters rows before building them), <c>ContextualFilterEngine</c> (which filters stacks),
    /// and <c>CombatActionResolver</c> (which refuses an ineligible submission regardless of how it
    /// arrived).
    /// </summary>
    public static class ItemCombatRules
    {
        public static bool IsUsableInCombat(ItemData item)
            => item != null && item.usageRule != null && item.usageRule.usableInCombat;

        public static bool IsUsableInCombat(DataRegistry registry, string itemId)
            => registry != null
               && !string.IsNullOrEmpty(itemId)
               && registry.TryGet<ItemData>(itemId, out var item)
               && IsUsableInCombat(item);
    }
}
