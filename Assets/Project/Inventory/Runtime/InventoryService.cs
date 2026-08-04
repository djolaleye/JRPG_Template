using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Characters;
using JRPG.Save;

namespace JRPG.Inventory
{
    /// <summary>
    /// Inventory authority. Wraps the container, the item-use resolver, and the filter engine.
    /// Implements both the lean cross-assembly IInventoryService and ISaveable.
    /// </summary>
    public sealed class InventoryService : IInventoryService, ISaveable
    {
        private readonly DataRegistry _registry;
        private readonly InventoryContainer _container = new();
        private readonly ItemUseResolver _resolver;
        private readonly IEventBus _bus;

        public InventoryContainer Container => _container;
        public string SaveKey => "inventory";

        public InventoryService(DataRegistry registry, IEventBus bus)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _resolver = new ItemUseResolver(_registry, _container, _bus);
        }

        // ----- IInventoryService -----

        public int Add(string itemId, int quantity)
        {
            _registry.TryGet<ItemData>(itemId, out var item);
            return _container.Add(itemId, quantity, item);
        }

        public int Remove(string itemId, int quantity) => _container.Remove(itemId, quantity);
        public int GetQuantity(string itemId) => _container.GetQuantity(itemId);
        public bool Has(string itemId, int n = 1) => _container.Has(itemId, n);

        
        /// <summary>
        /// Drops every owned item. The container is the whole of this service's mutable state — the
        /// registry, bus and use-resolver are constructor-fixed collaborators — so clearing it
        /// reproduces the constructor's starting condition exactly.
        ///
        /// Callers are expected to re-run <see cref="StartingInventoryBaker.Bake"/> afterwards; the
        /// baker only bakes into an empty container, so this reset is what re-arms it.
        /// Idempotent and safe to call before anything has happened.
        /// </summary>
        public void ResetForNewGame() => _container.Clear();


        // ----- Richer queries / commands (used from JRPG.Inventory consumers) -----

        public IEnumerable<InventoryStack> OfCategory(ItemCategory category)
            => _container.OfCategory(category, id => _registry.TryGet<ItemData>(id, out var i) ? i : null);

        public IReadOnlyList<InventoryStack> Filter(LayeredState state, ContextualFilterRequest request)
            => ContextualFilterEngine.Filter(_container, _registry, state, request);

        public bool TryUse(string itemId, CharacterRuntimeInstance target, LayeredState state, out string failureReason)
            => _resolver.TryUse(itemId, target, state, out failureReason);

        
        // ----- ISaveable -----

        public SaveDataBase CaptureState()
        {
            var dto = new InventorySaveData { version = SaveSystemCore.CurrentSaveVersion };

            foreach (var stack in _container.Enumerate())
            {
                if (stack.quantity <= 0) continue;

                dto.stacks.Add(new InventoryStackDto
                {
                    itemId = stack.itemId,
                    quantity = stack.quantity,
                    entryId = stack.entryId
                });
            }

            return new InventoryPayload { version = dto.version, data = dto };
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not InventoryPayload invPayload || invPayload.data == null) return;

            var stacks = new List<InventoryStack>(invPayload.data.stacks.Count);
            for (int i = 0; i < invPayload.data.stacks.Count; i++)
            {
                var d = invPayload.data.stacks[i];

                stacks.Add(new InventoryStack(d.itemId, d.quantity, d.entryId));
            }
            
            _container.ReplaceAll(stacks);
        }
    }
}
