using JRPG.Data;

namespace JRPG.Inventory
{
    public static class StartingInventoryBaker
    {
        /// <summary>
        /// Bake the authored starting inventory into the container. Idempotent — only bakes if the
        /// container is empty (so loading a save followed by a re-bootstrap doesn't double-up items).
        /// </summary>
        public static void Bake(StartingInventoryConfig config, InventoryContainer container, DataRegistry registry)
        {
            if (config == null || container == null || registry == null) return;
            if (container.Count > 0) return;

            for (int i = 0; i < config.entries.Count; i++)
            {
                var e = config.entries[i];
                if (string.IsNullOrEmpty(e.itemId) || e.quantity <= 0) continue;
                
                registry.TryGet<ItemData>(e.itemId, out var item);
                container.Add(e.itemId, e.quantity, item);
            }
        }
    }
}
