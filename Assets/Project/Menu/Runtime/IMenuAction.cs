namespace JRPG.Menu
{
    public interface IMenuAction
    {
        bool CanExecute(MenuContext context);
        void Execute(MenuContext context);
        string GetDisabledReason(MenuContext context);
    }
}
