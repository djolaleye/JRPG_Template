using System.Collections.Generic;

namespace JRPG.Menu
{
    /// <summary>
    /// The pause menu's "System" submenu: routes to the slot-select save/load screens plus Settings and
    /// Back.
    ///
    /// Load uses the <c>load_pause</c> registry id rather than <c>load</c>.
    ///
    /// <para>12.5 restructures the pause menus wholesale; these rows exist so the 12.4 screens are
    /// reachable in the meantime.</para>
    /// </summary>
    public sealed class SystemMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            RowModel.Simple("save", "Save", new OpenSubmenuAction("save", "No save screen is available."), Context),
            RowModel.Simple("load", "Load", new OpenSubmenuAction("load_pause", "No load screen is available."), Context),
            RowModel.Simple("settings", "Settings", new OpenSubmenuAction("settings", "No settings screen is available."), Context),
            RowModel.Simple("back", "Back", new CloseMenuAction(), Context),
        };
    }
}
