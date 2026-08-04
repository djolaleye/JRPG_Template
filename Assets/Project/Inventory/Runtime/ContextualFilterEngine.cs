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
                if (!PassesContext(item, state, request)) continue;
                if (request.categoryFilter.HasValue && item.category != request.categoryFilter.Value) continue;

                result.Add(stack);
            }

            return result;
        }

        // `state` gates the exploration tab by the mode the game is actually in: the pause/inventory
        // screen can legitimately be opened mid-battle, and there it must not list items that combat
        // would refuse. So when state.Mode == Combat, an ExplorationInventoryTab request drops every
        // item whose usageRule.usableInCombat is false (same predicate as ItemCombatRules, which
        // CombatActionResolver enforces at submission). 
        private static bool PassesContext(ItemData item, LayeredState state, ContextualFilterRequest request)
        {
            switch (request.context)
            {
                case FilterContext.CombatItemMenu:
                    // Combat usability is the shared rule (see ItemCombatRules — CombatActionResolver
                    // enforces the same one); the category narrowing is this screen's own concern.
                    return item.category == ItemCategory.Consumable
                        && ItemCombatRules.IsUsableInCombat(item);

                case FilterContext.ExplorationInventoryTab:
                    // Outside combat: show everything, key items included (their use is just refused).
                    // Inside combat: hide what combat can't use at all.
                    if (state.Mode == GameMode.Combat && !ItemCombatRules.IsUsableInCombat(item))
                        return false;
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
