using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Inventory;
using JRPG.Services;

namespace JRPG.Economy
{
    /// <summary>
    /// The item-category rule matrix, in one place.
    ///
    /// <para><b>Category prohibitions beat authored flags.</b> <c>canSell</c> / <c>canDiscard</c> are
    /// consulted, but a key or quest item is refused whatever the asset says — the flags express a
    /// designer's preference within a category, not permission to leave it.</para>
    /// </summary>
    public sealed class ItemRuleService : IItemRuleService
    {
        private readonly DataRegistry _data;
        private readonly IInventoryService _inventory;
        private readonly IStoryStateService _story;
        private readonly EquipmentManager _equipment;
        private readonly IEventBus _bus;

        public ItemRuleService(DataRegistry data, IInventoryService inventory, IEventBus bus,
                               IStoryStateService story = null, EquipmentManager equipment = null)
        {
            _data = data;
            _inventory = inventory;
            _bus = bus;
            _story = story;
            _equipment = equipment;
        }

        // ---- Selling ------------------------------------------------------------------------------

        public bool CanSell(string itemId, int quantity, out string reason)
        {
            if (!Resolve(itemId, quantity, out var item, out reason)) return false;

            if (!OwnedUnequipped(itemId, quantity, out reason)) return false;

            switch (item.category)
            {
                case ItemCategory.KeyItem:
                    reason = "Key items can't be sold.";
                    return false;
                case ItemCategory.QuestItem:
                    reason = "Quest items can't be sold.";
                    return false;
                case ItemCategory.ExplorationTool:
                    reason = "You'll need this out there.";
                    return false;
            }

            // Materials are sellable in principle; whether a given vendor buys them is the shop's rule,
            // applied on top of this one.
            if (!item.canSell)
            {
                reason = "No one will buy this.";
                return false;
            }

            return true;
        }

        // ---- Discarding ---------------------------------------------------------------------------

        public bool CanDiscard(string itemId, int quantity, out string reason)
        {
            if (!Resolve(itemId, quantity, out var item, out reason)) return false;

            if (!OwnedUnequipped(itemId, quantity, out reason)) return false;

            switch (item.category)
            {
                case ItemCategory.KeyItem:
                    reason = "Key items can't be thrown away.";
                    return false;
                case ItemCategory.QuestItem:
                    reason = "Quest items can't be thrown away.";
                    return false;
            }

            if (!item.canDiscard)
            {
                reason = "This can't be thrown away.";
                return false;
            }

            return true;
        }

        public bool TryDiscard(string itemId, int quantity)
        {
            if (!CanDiscard(itemId, quantity, out var reason))
            {
                Debug.Log($"[JRPG.Economy] Discard '{itemId}' x{quantity} refused: {reason}");
                return false;
            }

            int removed = _inventory.Remove(itemId, quantity);
            if (removed != quantity)
            {
                // CanDiscard checked the quantity a moment ago; a short removal means something else
                // moved the stack. Report it rather than pretending the requested amount went.
                Debug.LogWarning($"[JRPG.Economy] Discard '{itemId}': asked for {quantity}, removed {removed}.");
            }

            if (removed > 0) _bus?.Publish(new ItemDiscarded(itemId, removed));

            return removed > 0;
        }

        // ---- Key items ----------------------------------------------------------------------------

        public bool CanUseKeyItem(string itemId, out string reason)
        {
            if (!ResolveOwned(itemId, 1, out var item, out reason)) return false;

            if (item.category != ItemCategory.KeyItem)
            {
                reason = "Not a key item.";
                return false;
            }

            return StoryGateSatisfied(item, out reason);
        }

        // ---- Quest items --------------------------------------------------------------------------

        public bool CanUseQuestItem(string itemId, string questId, out string reason)
        {
            if (!ResolveOwned(itemId, 1, out var item, out reason)) return false;

            if (item.category != ItemCategory.QuestItem)
            {
                reason = "Not a quest item.";
                return false;
            }

            var rule = item.usageRule;
            var required = rule != null ? rule.requiredQuestId : null;

            if (!string.IsNullOrEmpty(required) && required != questId)
            {
                reason = "That's for something else.";
                return false;
            }

            return StoryGateSatisfied(item, out reason);
        }

        public bool ConsumeQuestItem(string itemId, int quantity, string questId)
        {
            if (quantity <= 0) return false;
            if (!CanUseQuestItem(itemId, questId, out var reason))
            {
                Debug.Log($"[JRPG.Economy] Quest use of '{itemId}' refused: {reason}");
                return false;
            }

            if (_inventory.GetQuantity(itemId) < quantity) return false;

            return _inventory.Remove(itemId, quantity) == quantity;
        }

        // ---- Exploration tools --------------------------------------------------------------------

        public bool CanUseExplorationTool(string itemId, out string reason)
        {
            if (!ResolveOwned(itemId, 1, out var item, out reason)) return false;

            if (item.category != ItemCategory.ExplorationTool)
            {
                reason = "Not an exploration tool.";
                return false;
            }

            return StoryGateSatisfied(item, out reason);
        }

        public bool TryUseExplorationTool(string itemId, ExplorationToolUseContext context)
        {
            if (!CanUseExplorationTool(itemId, out var reason))
            {
                Debug.Log($"[JRPG.Economy] Tool '{itemId}' refused: {reason}");
                return false;
            }

            _data.TryGet<ItemData>(itemId, out var item);
            var mode = item.usageRule != null
                ? item.usageRule.explorationToolUseMode
                : ExplorationToolUseMode.NeverConsumed;

            bool consume = mode == ExplorationToolUseMode.ConsumeOnUse
                           || (mode == ExplorationToolUseMode.ConsumeWhenTraversalSucceeds && context.TraversalSucceeded);

            // A reusable tool returns true having taken nothing — "used" and "spent" are separate facts.
            if (consume) _inventory.Remove(itemId, 1);

            return true;
        }

        // ---- Shared checks ------------------------------------------------------------------------

        /// Resolves the item and sanity-checks the amount. Split from <see cref="Owned"/> so a caller
        /// can slot its own checks between the two and word the refusal for the case that actually
        /// applies.
        private bool Resolve(string itemId, int quantity, out ItemData item, out string reason)
        {
            item = null;
            reason = null;

            if (quantity <= 0)
            {
                reason = "Choose an amount first.";
                return false;
            }

            if (string.IsNullOrEmpty(itemId) || _data == null || !_data.TryGet(itemId, out item) || item == null)
            {
                reason = "Unknown item.";
                return false;
            }

            return true;
        }

        private bool Owned(string itemId, int quantity, out string reason)
        {
            reason = null;

            if (_inventory != null && _inventory.GetQuantity(itemId) >= quantity) return true;

            reason = "You don't have that many.";
            return false;
        }

        /// <summary>
        /// Ownership of a potentially worn item.
        ///
        /// <para><b>The inventory only ever holds unequipped copies.</b> Equipping moves an item out of
        /// it, so a stack the player can see is by definition not worn, and a spare sword
        /// is sellable while the one in use is not.</para>
        /// </summary>
        private bool OwnedUnequipped(string itemId, int quantity, out string reason)
        {
            if (Owned(itemId, quantity, out reason)) return true;   // In Inventory

            if (IsEquippedByAnyone(itemId)) reason = "Someone is wearing that.";

            return false;
        }

        /// The common case: resolve, then require ownership, in that order.
        private bool ResolveOwned(string itemId, int quantity, out ItemData item, out string reason)
            => Resolve(itemId, quantity, out item, out reason) && Owned(itemId, quantity, out reason);

        /// <summary>
        /// The item's story gate: every flag in the AND list. An empty list is satisfied.
        /// </summary>
        private bool StoryGateSatisfied(ItemData item, out string reason)
        {
            reason = null;

            var rule = item.usageRule;
            if (rule == null) return true;

            var flags = rule.requiredStoryFlags;
            if (flags == null) return true;

            for (int i = 0; i < flags.Count; i++)
            {
                if (FlagSet(flags[i])) continue;

                reason = "Not yet.";
                return false;
            }

            return true;
        }

        private bool FlagSet(string flagId)
        {
            if (string.IsNullOrEmpty(flagId)) return true;

            // No story service means no flags can ever be set - treat the gate as open 
            return _story == null || _story.GetBool(flagId);
        }

        private bool IsEquippedByAnyone(string itemId)
        {
            if (_equipment == null) return false;

            foreach (KeyValuePair<string, EquipmentRuntimeState> entry in _equipment.All)
            {
                var slots = entry.Value?.slotToItemId;
                if (slots == null) continue;

                foreach (var slot in slots)
                    if (slot.Value == itemId) return true;
            }

            return false;
        }
    }
}
