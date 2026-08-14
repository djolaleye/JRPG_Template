using System;
using System.Collections.Generic;
using JRPG.Data;

namespace JRPG.Inventory
{
    /// <summary>
    /// Central runtime authority for item ownership. Keyed by itemId for stackables;
    /// reserves the entryId path for unique instances (not exercised in prototype).
    /// </summary>
    public sealed class InventoryContainer
    {
        private readonly Dictionary<string, InventoryStack> _itemsById = new();

        public IReadOnlyDictionary<string, InventoryStack> Snapshot() => _itemsById;
        public IEnumerable<InventoryStack> Enumerate() => _itemsById.Values;
        public int Count => _itemsById.Count;

        /// <summary>
        /// Adds <paramref name="qty"/> of <paramref name="itemId"/>, clamped to the item's stackLimit.
        /// Returns the overflow that did not fit (0 if it all went in).
        /// </summary>
        public int Add(string itemId, int qty, ItemData item)
        {
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("itemId is null/empty.", nameof(itemId));
            if (qty <= 0) return 0;

            int cap = item != null ? Math.Max(1, item.stackLimit) : 99;

            if (!_itemsById.TryGetValue(itemId, out var stack))
            {
                stack = new InventoryStack(itemId, 0);
                _itemsById[itemId] = stack;
            }

            int room = cap - stack.quantity;
            int taken = Math.Min(room, qty);

            stack.quantity += taken;
            return qty - taken; // overflow
        }

        /// <summary>
        /// How many more of <paramref name="itemId"/> would fit right now — the preflight half of
        /// <see cref="Add"/>, using the identical cap rule (the item's stackLimit, or 99 when the item
        /// is unknown). A transaction that must not partially consume its costs asks this first, rather
        /// than adding and reacting to the overflow it gets back.
        /// </summary>
        public int RoomFor(string itemId, ItemData item)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int cap = item != null ? Math.Max(1, item.stackLimit) : 99;
            int held = GetQuantity(itemId);

            return Math.Max(0, cap - held);
        }

        /// <summary>
        /// Removes up to <paramref name="qty"/> of <paramref name="itemId"/>. Returns the amount actually removed.
        /// Drops the stack entry when quantity reaches 0 so queries stay clean.
        /// </summary>
        public int Remove(string itemId, int qty)
        {
            if (string.IsNullOrEmpty(itemId) || qty <= 0) return 0;
            if (!_itemsById.TryGetValue(itemId, out var stack)) return 0;

            int removed = Math.Min(stack.quantity, qty);

            stack.quantity -= removed;
            if (stack.quantity <= 0) _itemsById.Remove(itemId);

            return removed;
        }

        public int GetQuantity(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId) && _itemsById.TryGetValue(itemId, out var s))
            { 
                return s.quantity;
            }
            
            return 0;
        }

        public bool Has(string itemId, int n = 1) => GetQuantity(itemId) >= n;

        /// <summary>
        /// Enumerate stacks whose authored category matches the predicate. Caller passes a registry
        /// resolver so we don't take a hard reference on DataRegistry here.
        /// </summary>
        public IEnumerable<InventoryStack> OfCategory(ItemCategory category, Func<string, ItemData> resolveItem)
        {
            if (resolveItem == null) yield break;

            foreach (var stack in _itemsById.Values)
            {
                var item = resolveItem(stack.itemId);
                if (item != null && item.category == category) yield return stack;
            }
        }

        public void Clear() => _itemsById.Clear();

        /// <summary>
        /// Replace the entire dictionary with the given stacks. Used by save restore.
        /// </summary>
        public void ReplaceAll(IEnumerable<InventoryStack> stacks)
        {
            _itemsById.Clear();
            if (stacks == null) return;

            foreach (var s in stacks)
            {
                if (s == null || string.IsNullOrEmpty(s.itemId) || s.quantity <= 0) continue;
                
                _itemsById[s.itemId] = new InventoryStack(s.itemId, s.quantity, s.entryId);
            }
        }
    }
}
