using System.Collections.Generic;

namespace JRPG.Menu
{
    public class PauseMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            RowModel.Simple("party", "Party", new OpenSubmenuAction("party"), Context),
            RowModel.Simple("inventory", "Inventory", new OpenSubmenuAction("inventory"), Context),
            RowModel.Simple("system", "System", new CloseMenuAction(), Context, enabled: false),
            RowModel.Simple("exit", "Exit", new CloseAllAction(), Context),
        };
    }
}
