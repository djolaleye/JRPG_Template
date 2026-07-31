using System.Collections.Generic;

namespace JRPG.Menu
{
    /// The pause menu's "System" submenu: Save (to the quicksave slot) plus disabled Load/Settings
    /// stubs and Back. A full slot-select save/load screen is Phase 12.
    public sealed class SystemMenuController : MenuController
    {
        private const int QuickSaveSlot = 0;

        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            RowModel.Simple("save", "Save", new SaveGameAction(QuickSaveSlot), Context),
            RowModel.Simple("load", "Load", new CloseMenuAction(), Context, enabled: false),
            RowModel.Simple("settings", "Settings", new CloseMenuAction(), Context, enabled: false),
            RowModel.Simple("back", "Back", new CloseMenuAction(), Context),
        };
    }
}
