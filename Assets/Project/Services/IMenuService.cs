namespace JRPG.Services
{
    /// <summary>
    /// Menu navigation surface. Rich types (MenuContext) live on the concrete MenuService in JRPG.Menu;
    /// this lean interface lets non-Menu assemblies open menus by id.
    /// </summary>
    public interface IMenuService
    {
        void Open(string menuId, object context);
        void Close();
        void CloseAll();
        string ActiveMenuId { get; }
        int Depth { get; }

        /// <summary>
        /// True when <paramref name="menuId"/> resolves to a registered canvas, i.e. <see cref="Open"/>
        /// would succeed. Lets a row that navigates to another screen disable itself with a reason
        /// instead of opening nothing and logging an error — and lets it light up on its own as later
        /// phases register the screens it points at.
        /// </summary>
        bool HasMenu(string menuId);
    }
}
