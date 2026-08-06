using JRPG.Data;
using UnityEngine;

namespace JRPG.Inventory
{
    public static class StartingInventoryBaker
    {
        /// <summary>
        /// Bake the authored starting inventory into the container. Idempotent — only bakes if the
        /// container is empty (so loading a save followed by a re-bootstrap doesn't double-up items).
        ///
        /// <para>Pass <paramref name="equipment"/> to also apply the authored starting gear. That runs
        /// inside the same emptiness guard: starting equipment is part of "what a new game begins with",
        /// so it must never be re-applied over a restored save, and it must not run at all when the item
        /// bake was skipped — the items it equips would not be there to consume.</para>
        /// </summary>
        public static void Bake(StartingInventoryConfig config, InventoryContainer container, DataRegistry registry,
                                EquipmentManager equipment = null)
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

            BakeEquipment(config, equipment);
        }

        /// <summary>
        /// Equips the authored starting gear. Runs after the item pass because equipping consumes the
        /// item from the container — the gear has to be in the inventory before it can be worn.
        /// </summary>
        private static void BakeEquipment(StartingInventoryConfig config, EquipmentManager equipment)
        {
            if (equipment == null || config.startingEquipment == null) return;

            for (int i = 0; i < config.startingEquipment.Count; i++)
            {
                var e = config.startingEquipment[i];
                if (string.IsNullOrEmpty(e.characterId) || string.IsNullOrEmpty(e.itemId)) continue;

                if (!equipment.EquipById(e.characterId, e.itemId, out var reason))
                    Debug.LogWarning($"[JRPG.Inventory] Starting equipment: could not equip '{e.itemId}' " +
                                     $"on '{e.characterId}' — {reason}");
            }
        }
    }
}
