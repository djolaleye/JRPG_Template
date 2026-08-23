namespace JRPG.Data
{
    /// <summary>
    /// How a shop presents itself, and therefore which screens it opens.
    ///
    /// <para>The two have different flows: a Basic shop is
    /// one catalog the player buys from, while a Specialty shop has sections and may buy
    /// goods back, so it needs a menu in front of the lists.</para>
    /// </summary>
    public enum ShopType
    {
        Basic = 0,
        Specialty = 1,
    }

    /// Whether a requirement list is satisfied by every entry or by any one of them.
    public enum RequirementMode
    {
        All = 0,
        Any = 1,
    }

    /// How a shop prices what it buys from the player.
    public enum SellPriceMode
    {
        /// A fraction of the item's authored basePrice — the ordinary vendor markdown.
        BasePriceMultiplier = 0,

        /// One flat price for anything the shop accepts, regardless of what the item is worth.
        FlatOverride = 1,
    }
}
