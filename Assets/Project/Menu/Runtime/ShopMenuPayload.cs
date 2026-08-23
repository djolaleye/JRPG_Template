namespace JRPG.Menu
{
    /// <summary>
    /// What a shop screen needs to know that a menu id cannot express: which vendor, and which of its
    /// sections. Travels in <see cref="MenuContext.Payload"/>.
    ///
    /// <para>One payload type for all four shop screens, because they are one flow — the main menu
    /// hands the same object down with a section filled in, so nothing has to re-resolve the shop from
    /// the world object that opened it.</para>
    /// </summary>
    public sealed class ShopMenuPayload
    {
        public string ShopId;

        /// Section of a specialty shop being browsed. Empty means "the whole catalog".
        public string SectionId;

        public ShopMenuPayload(string shopId, string sectionId = null)
        {
            ShopId = shopId;
            SectionId = sectionId;
        }

        /// <summary>
        /// Reads the payload off a context, tolerating a context that carries none.
        ///
        /// <para>A bare shop id is accepted too: <c>ShopInteractable</c> lives in JRPG.Exploration, which
        /// cannot see this type, so it passes the id as a string through the lean menu interface. Both
        /// forms mean the same thing — browse this vendor's whole catalog.</para>
        /// </summary>
        public static ShopMenuPayload From(MenuContext context)
        {
            var payload = context?.Payload;

            if (payload is ShopMenuPayload typed) return typed;
            if (payload is string shopId && !string.IsNullOrEmpty(shopId)) return new ShopMenuPayload(shopId);

            return null;
        }
    }
}
