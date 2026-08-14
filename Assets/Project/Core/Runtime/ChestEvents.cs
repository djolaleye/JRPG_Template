namespace JRPG.Core
{
    /// <summary>
    /// A chest was successfully opened and its reward granted. Notification only — the chest state
    /// service remains the authority on which chests are open, and presentation must not infer state
    /// from the absence of this event (a scene re-entered after a save carries no event at all).
    /// </summary>
    public readonly struct ChestOpened
    {
        public readonly string ChestId;
        public readonly string ItemId;
        public readonly int Quantity;

        public ChestOpened(string chestId, string itemId, int quantity)
        {
            ChestId = chestId;
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
