using UnityEngine;

namespace JRPG.Menu
{
    public struct RowModel
    {
        public string id;
        public string label;
        public Sprite icon;
        public string quantityText;
        public string costText;
        public bool enabled;
        public IMenuAction action;
        public MenuContext context;

        public static RowModel Simple(string id, string label, IMenuAction action, MenuContext context, bool enabled = true)
            => new() { id = id, label = label, enabled = enabled, action = action, context = context };
    }
}
