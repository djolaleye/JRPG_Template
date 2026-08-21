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
        /// <param name="storyFlagIsSet">
        /// Resolves <see cref="ItemUsageRule.requiredStoryFlags"/>. Null means "no story access", which
        /// passes every item — the pre-12.6 behaviour, and the right answer for callers such as the
        /// editor harnesses that have no story service.
        ///
        /// A delegate rather than an <c>IStoryStateService</c> parameter so this class stays a pure
        /// static utility with no service dependency.
        /// </param>
        public static IReadOnlyList<InventoryStack> Filter(
            InventoryContainer container,
            DataRegistry registry,
            LayeredState state,
            ContextualFilterRequest request,
            System.Func<string, bool> storyFlagIsSet = null)
        {
            var result = new List<InventoryStack>();
            if (container == null || registry == null) return result;

            foreach (var stack in container.Enumerate())
            {
                if (stack == null || stack.quantity <= 0) continue;
                if (!registry.TryGet<ItemData>(stack.itemId, out var item) || item == null) continue;
                if (!PassesStoryGate(item, storyFlagIsSet)) continue;
                if (!PassesContext(item, state, request)) continue;
                if (request.categoryFilter.HasValue && item.category != request.categoryFilter.Value) continue;

                result.Add(stack);
            }

            return result;
        }

        /// <summary>
        /// An item with an unmet entry in <see cref="ItemUsageRule.requiredStoryFlags"/> stays out of
        /// every list. This is the seam <see cref="ItemUsageRule"/> reserved: the flags are a visibility
        /// gate, so they are evaluated before the per-context rules rather than alongside them.
        /// </summary>
        private static bool PassesStoryGate(ItemData item, System.Func<string, bool> storyFlagIsSet)
        {
            var flags = item.usageRule?.requiredStoryFlags;
            if (flags == null || flags.Count == 0) return true;
            if (storyFlagIsSet == null) return true;

            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (string.IsNullOrEmpty(flag)) continue;   // an empty entry gates nothing

                if (!storyFlagIsSet(flag)) return false;
            }

            return true;
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
