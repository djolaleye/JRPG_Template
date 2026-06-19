using JRPG.Core;
using JRPG.Services;
using JRPG.Characters;

namespace JRPG.Menu
{
    /// <summary>
    /// Generic payload passed to a row's <see cref="IMenuAction"/>. Screens populate the fields they care about.
    /// </summary>
    public class MenuContext
    {
        public IServiceRegistry Services;
        public IMenuService Menus;
        public string SelectedItemId;
        public CharacterRuntimeInstance Subject;
        public object Payload;
    }
}
