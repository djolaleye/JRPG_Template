using UnityEngine;
using JRPG.Core;
using JRPG.Services;
using JRPG.Inventory;
using JRPG.Party;

namespace JRPG.Menu
{
    public sealed class OpenSubmenuAction : IMenuAction
    {
        private readonly string _menuId;
        public OpenSubmenuAction(string menuId) { _menuId = menuId; }
        public bool CanExecute(MenuContext c) => !string.IsNullOrEmpty(_menuId);
        public void Execute(MenuContext c) => c.Menus.Open(_menuId, c);
        public string GetDisabledReason(MenuContext c) => "Menu id empty.";
    }

    public sealed class CloseMenuAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => true;
        public void Execute(MenuContext c) => c.Menus.Close();
        public string GetDisabledReason(MenuContext c) => "";
    }

    public sealed class CloseAllAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => true;
        public void Execute(MenuContext c) => c.Menus.CloseAll();
        public string GetDisabledReason(MenuContext c) => "";
    }

    /// <summary>
    /// Writes a save to <see cref="_slot"/> through <see cref="ISaveService"/>. Gated by the service's
    /// own <c>CanSave</c> — the single authority — so the row disables itself wherever saving is
    /// disallowed.
    /// </summary>
    public sealed class SaveGameAction : IMenuAction
    {
        private readonly int _slot;
        public SaveGameAction(int slot) { _slot = slot; }

        public bool CanExecute(MenuContext c)
            => c.Services != null && c.Services.TryResolve<ISaveService>(out var save) && save.CanSave();

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<ISaveService>(out var save)) return;
            
            bool ok = save.Save(_slot);
            Debug.Log($"[JRPG.Menu] Save to slot {_slot}: {(ok ? "OK" : "rejected")}");
        }

        public string GetDisabledReason(MenuContext c)
            => c.Services != null && c.Services.TryResolve<ISaveService>(out _)
                ? "Can't save here."
                : "No save service.";
    }

    /// <summary>
    /// Use the item identified by <see cref="MenuContext.SelectedItemId"/> on the active subject
    /// (defaults to the first active party member).
    /// </summary>
    public sealed class UseItemAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
        {
            if (string.IsNullOrEmpty(c.SelectedItemId)) return false;
            return c.Services.TryResolve<IInventoryService>(out var inv) && inv.GetQuantity(c.SelectedItemId) > 0;
        }

        public void Execute(MenuContext c)
        {
            if (!c.Services.TryResolve<IInventoryService>(out var invSvc) || invSvc is not InventoryService inv) return;

            var subject = c.Subject;
            if (subject == null
                && c.Services.TryResolve<IPartyService>(out var partySvc)
                && partySvc is IPartyRuntimeQueries pq)
            {
                var active = pq.GetActiveCombatParty();
                if (active != null && active.Count > 0) subject = active[0];
            }
            if (subject == null) { Debug.LogWarning("[JRPG.Menu] UseItemAction: no subject."); return; }

            var state = AppContext.State?.Current ?? default;
            inv.TryUse(c.SelectedItemId, subject, state, out _);
        }

        public string GetDisabledReason(MenuContext c) => "Out of stock or no inventory service.";
    }

    public sealed class SelectPartyMemberAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => !string.IsNullOrEmpty(c.SelectedItemId);
        public void Execute(MenuContext c)
        {
            Debug.Log($"[JRPG.Menu] Selected party member: {c.SelectedItemId}");
            if (!c.Services.TryResolve<IPartyService>(out var partySvc) || partySvc is not IPartyRuntimeQueries pq) return;
            var active = pq.GetActiveCombatParty();
            for (int i = 0; i < active.Count; i++)
                if (active[i].SourceDataId == c.SelectedItemId) { c.Subject = active[i]; return; }
        }
        public string GetDisabledReason(MenuContext c) => "No selected id.";
    }

    public sealed class EquipItemAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => c.Subject != null && !string.IsNullOrEmpty(c.SelectedItemId);
        public void Execute(MenuContext c)
        {
            if (!c.Services.TryResolve<IEquipmentService>(out var eqSvc) || eqSvc is not EquipmentManager equip) return;
            equip.Equip(c.Subject, c.SelectedItemId, out _);
        }
        public string GetDisabledReason(MenuContext c) =>
            c.Subject == null ? "Pick a party member first." : "No item selected.";
    }
}
