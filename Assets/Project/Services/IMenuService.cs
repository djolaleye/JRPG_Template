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
    }
}
