using System.Collections.Generic;

namespace JRPG.Menu
{
    public class MainMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            RowModel.Simple("new_game", "New Game", new CloseAllAction(), Context),
            RowModel.Simple("load_game", "Load Game", new CloseAllAction(), Context, enabled: false),
            RowModel.Simple("settings", "Settings", new CloseMenuAction(), Context, enabled: false),
            RowModel.Simple("exit", "Exit", new CloseMenuAction(), Context)
        };
    }
}
