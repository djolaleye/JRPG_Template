using JRPG.Data;

namespace JRPG.Inventory
{
    public enum FilterContext
    {
        CombatItemMenu,
        ExplorationInventoryTab,
        EquipScreenForCharacter
    }

    public struct ContextualFilterRequest
    {
        public FilterContext context;
        public string subjectCharacterId;     // for EquipScreenForCharacter
        public ItemCategory? categoryFilter;  // optional override for tabbed UIs

        public static ContextualFilterRequest ExplorationTab(ItemCategory? cat = null)
            => new() { context = FilterContext.ExplorationInventoryTab, categoryFilter = cat };

        public static ContextualFilterRequest CombatItems()
            => new() { context = FilterContext.CombatItemMenu };

        public static ContextualFilterRequest EquipScreen(string subjectCharacterId)
            => new() { context = FilterContext.EquipScreenForCharacter, subjectCharacterId = subjectCharacterId };
    }
}
