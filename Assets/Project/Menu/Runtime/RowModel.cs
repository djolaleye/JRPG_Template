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

        /// <summary>
        /// Secondary line/strip, rendered in the row's <c>AuxLabel</c> slot.
        ///
        /// <para>Unlike <see cref="quantityText"/> and <see cref="costText"/>, which are narrow
        /// fixed-width value columns, the aux slot is flexible — so it is the right home for anything of
        /// variable length, such as the party screen's "Lv 1  HP 100/100  SP 30/30" strip. Putting that
        /// in the quantity column overflowed it straight across the cost column.</para>
        /// </summary>
        public string auxText;
        public bool enabled;
        public IMenuAction action;
        public MenuContext context;
        public string disabledReason;

        public static RowModel Simple(string id, string label, IMenuAction action, MenuContext context,
                                      bool enabled = true, string disabledReason = null)
            => new()
            {
                id = id,
                label = label,
                enabled = enabled,
                action = action,
                context = context,
                disabledReason = disabledReason,
            };
    }
}
