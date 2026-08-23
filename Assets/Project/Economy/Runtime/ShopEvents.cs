namespace JRPG.Economy
{
    public enum ShopTransactionType
    {
        Purchase = 0,
        Sale = 1,
    }

    /// <summary>
    /// A shop transaction completed.
    ///
    /// <para>Published after the exchange has fully landed, so a subscriber reads the wallet or
    /// inventory in its finished state.</para>
    /// </summary>
    public readonly struct ShopTransactionCompleted
    {
        public readonly string ShopId;

        /// Offering bought. Empty for a sale, which is priced per item rather than per offering.
        public readonly string OfferingId;
        public readonly ShopTransactionType Type;
        public readonly string ItemId;
        public readonly int Quantity;
        public readonly int CurrencyDelta;

        public ShopTransactionCompleted(string shopId, string offeringId, ShopTransactionType type,
                                        string itemId, int quantity, int currencyDelta)
        {
            ShopId = shopId;
            OfferingId = offeringId;
            Type = type;
            ItemId = itemId;
            Quantity = quantity;
            CurrencyDelta = currencyDelta;
        }
    }
}
