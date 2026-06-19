namespace JRPG.Core
{
    public readonly struct MenuOpened
    {
        public readonly string MenuId;
        public MenuOpened(string menuId) { MenuId = menuId; }
    }

    public readonly struct MenuClosed
    {
        public readonly string MenuId;
        public MenuClosed(string menuId) { MenuId = menuId; }
    }
}
