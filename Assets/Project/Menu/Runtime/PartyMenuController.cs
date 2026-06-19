using System.Collections.Generic;
using JRPG.Services;
using JRPG.Party;

namespace JRPG.Menu
{
    public class PartyMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>();
            if (Context?.Services == null) return rows;
            if (!Context.Services.TryResolve<IPartyService>(out var partySvc)) return rows;

            foreach (var id in partySvc.GetActivePartyIds())
            {
                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    SelectedItemId = id
                };
                rows.Add(new RowModel
                {
                    id = id,
                    label = id + "  [Active]",
                    enabled = true,
                    action = new SelectPartyMemberAction(),
                    context = rowContext
                });
            }
            foreach (var id in partySvc.GetReservePartyIds())
            {
                var rowContext = new MenuContext
                {
                    Services = Context.Services,
                    Menus = Context.Menus,
                    SelectedItemId = id
                };
                rows.Add(new RowModel
                {
                    id = id,
                    label = id + "  [Reserve]",
                    enabled = true,
                    action = new SelectPartyMemberAction(),
                    context = rowContext
                });
            }
            return rows;
        }
    }
}
