using System.Collections.Generic;
using JRPG.Core;
using JRPG.Data;

namespace JRPG.Inventory
{
    /// <summary>
    /// Pure filter: snapshot of inventory + layered state + request → filtered stack list.
    /// No mutation; menus call this instead of touching the container directly.
    /// </summary>
    public static class ContextualFilterEngine
    {
        public static IReadOnlyList<InventoryStack> Filter(
            InventoryContainer container,
            DataRegistry registry,
            LayeredState state,
            ContextualFilterRequest request)
        {
            var result = new List<InventoryStack>();
            if (container == null || registry == null) return result;

            foreach (var stack in container.Enumerate())
            {
                if (stack == null || stack.quantity <= 0) continue;
                if (!registry.TryGet<ItemData>(stack.itemId, out var item) || item == null) continue;
                if (!PassesContext(item, state, request, registry)) continue;
                if (request.categoryFilter.HasValue && item.category != request.categoryFilter.Value) continue;

                result.Add(stack);
            }

            return result;
        }

        private static bool PassesContext(ItemData item, LayeredState state, ContextualFilterRequest request, DataRegistry registry)
        {
            switch (request.context)
            {
                case FilterContext.CombatItemMenu:
                    return item.category == ItemCategory.Consumable
                        && item.usageRule != null
                        && item.usageRule.usableInCombat;

                case FilterContext.ExplorationInventoryTab:
                    // Show everything, key items are visible (their use is just refused).
                    return true;

                case FilterContext.EquipScreenForCharacter:
                    if (item is EquipmentData equip)
                    {
                        // The screen narrows by character via allowedCharacterIds/allowedClassTags;
                            // empty list = any. 
                        if (string.IsNullOrEmpty(request.subjectCharacterId)) return true;
                        if (equip.allowedCharacterIds != null && equip.allowedCharacterIds.Count > 0
                            && !equip.allowedCharacterIds.Contains(request.subjectCharacterId))
                            return false;
                        return true;
                    }
                    return false;
            }
            return true;
        }
    }
}
