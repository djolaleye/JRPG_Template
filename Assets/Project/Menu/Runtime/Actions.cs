using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;
using JRPG.Inventory;
using JRPG.Party;

namespace JRPG.Menu
{
    /// <summary>
    /// Navigates to another registered screen.
    ///
    /// <para><see cref="CanExecute"/> asks the menu service whether the destination actually resolves,
    /// so a row pointing at a screen that has not been built yet greys itself out with a reason rather
    /// than opening nothing and logging an error — and lights up by itself the moment that screen is
    /// registered. The registry is the single authority for what exists; no row keeps its own list.</para>
    /// </summary>
    public sealed class OpenSubmenuAction : IMenuAction
    {
        private readonly string _menuId;
        private readonly string _unavailableReason;
        private readonly System.Func<MenuContext, string> _gate;

        /// <param name="unavailableReason">Shown when the destination is not registered. Null uses a
        /// generic message.</param>
        /// <param name="gate">
        /// Optional extra condition owned by the domain, evaluated after the destination is known to
        /// exist. Returns null when navigation is allowed, or the player-facing reason it is not — e.g.
        /// the pause menu's Save row asks <c>ISaveService.CanSave()</c>, so the row reflects the save
        /// system's own rule instead of re-deriving when saving is legal.
        /// </param>
        public OpenSubmenuAction(string menuId, string unavailableReason = null,
                                 System.Func<MenuContext, string> gate = null)
        {
            _menuId = menuId;
            _unavailableReason = unavailableReason;
            _gate = gate;
        }

        public bool CanExecute(MenuContext c)
            => !string.IsNullOrEmpty(_menuId)
               && c.Menus != null
               && c.Menus.HasMenu(_menuId)
               && (_gate == null || string.IsNullOrEmpty(_gate(c)));

        public void Execute(MenuContext c) => c.Menus.Open(_menuId, c);

        public string GetDisabledReason(MenuContext c)
        {
            if (string.IsNullOrEmpty(_menuId)) return "Menu id empty.";
            if (c.Menus == null) return "No menu service.";
            if (!c.Menus.HasMenu(_menuId)) return _unavailableReason ?? "Not available yet.";

            // Reaching here means the screen exists but the domain refused; that reason is the useful one.
            return _gate?.Invoke(c) ?? _unavailableReason ?? "Not available yet.";
        }
    }

    // ---- Session lifecycle ------------------------------------------------------------------------
    //
    // Each of these is a thin call into ISessionService, which owns the ordering of the service resets
    // and the scene choreography. Nothing here decides what "a new game" means.

    public sealed class NewGameAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
            => c.Services != null && c.Services.TryResolve<ISessionService>(out _);

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<ISessionService>(out var session)) return;

            // Close the menu stack first. NewGame sets the exploration state once the content scene is
            // live, and a menu closing afterwards would restore its captured priorState over the top.
            c.Menus?.CloseAll();
            session.NewGame();
        }

        public string GetDisabledReason(MenuContext c) => "No session service.";
    }

    /// <summary>
    /// Loads a specific slot. Used by the load screen's per-slot rows and, with the most recent slot
    /// pre-resolved, by the main menu's Continue row.
    /// </summary>
    public sealed class LoadGameAction : IMenuAction
    {
        private readonly int _slot;
        public LoadGameAction(int slot) { _slot = slot; }

        public bool CanExecute(MenuContext c)
            => _slot >= 0
               && c.Services != null
               && c.Services.TryResolve<ISessionService>(out _)
               && c.Services.TryResolve<ISaveService>(out var save)
               && save.SlotExists(_slot);

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<ISessionService>(out var session)) return;

            // Same reason as NewGameAction: the load sets state from its scene-ready callback.
            c.Menus?.CloseAll();

            if (!session.LoadGame(_slot))
                Debug.LogWarning($"[JRPG.Menu] Load of slot {_slot} was rejected.");
        }

        public string GetDisabledReason(MenuContext c)
        {
            if (_slot < 0) return "No save to continue from.";
            if (c.Services == null || !c.Services.TryResolve<ISaveService>(out _)) return "No save service.";

            return "This slot is empty.";
        }
    }

    /// <summary>
    /// Quits to desktop. In the editor there is no application to quit, so it stops play mode instead —
    /// otherwise this row does nothing at all during development.
    /// </summary>
    public sealed class QuitGameAction : IMenuAction
    {
        public bool CanExecute(MenuContext c) => true;

        public void Execute(MenuContext c)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public string GetDisabledReason(MenuContext c) => "";
    }

    public sealed class ReturnToTitleAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
            => c.Services != null && c.Services.TryResolve<ISessionService>(out _);

        public void Execute(MenuContext c)
        {
            if (c.Services == null || !c.Services.TryResolve<ISessionService>(out var session)) return;

            // ReturnToTitle closes the menu stack itself, in the right order relative to the state change.
            session.ReturnToTitle();
        }

        public string GetDisabledReason(MenuContext c) => "No session service.";
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

    /// <summary>
    /// Equips <see cref="MenuContext.SelectedItemId"/> onto <see cref="MenuContext.Subject"/>.
    ///
    /// <para>Both the gate and the explanation come from <see cref="EquipmentManager.CanEquip"/> 
    /// so the row can never offer something the domain refuses,
    /// and the refusal the player reads is the domain's own wording.</para>
    /// </summary>
    public sealed class EquipItemAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
        {
            if (!TryResolve(c, out var equip)) return false;
            return equip.CanEquip(c.Subject, c.SelectedItemId, out _);
        }

        public void Execute(MenuContext c)
        {
            if (!TryResolve(c, out var equip)) return;

            if (!equip.Equip(c.Subject, c.SelectedItemId, out var reason))
                Debug.LogWarning($"[JRPG.Menu] Equip refused: {reason}");
        }

        public string GetDisabledReason(MenuContext c)
        {
            if (c.Subject == null) return "Pick a party member first.";
            if (string.IsNullOrEmpty(c.SelectedItemId)) return "No item selected.";
            if (!TryResolve(c, out var equip)) return "No equipment service.";

            equip.CanEquip(c.Subject, c.SelectedItemId, out var reason);
            return reason;
        }

        private static bool TryResolve(MenuContext c, out EquipmentManager equipment)
        {
            equipment = null;
            if (c?.Subject == null || string.IsNullOrEmpty(c.SelectedItemId)) return false;
            if (c.Services == null || !c.Services.TryResolve<IEquipmentService>(out var svc)) return false;

            equipment = svc as EquipmentManager;
            return equipment != null;
        }
    }

    /// <summary>
    /// Unequips the slot carried in <see cref="MenuContext.Payload"/>. Paired with
    /// <see cref="EquipItemAction"/> so both directions are menu actions rather than one being a
    /// controller-side special case.
    /// </summary>
    public sealed class UnequipItemAction : IMenuAction
    {
        public bool CanExecute(MenuContext c)
            => c?.Subject != null
               && c.Payload is EquipmentSlot slot
               && c.Services != null
               && c.Services.TryResolve<IEquipmentService>(out var svc)
               && svc is EquipmentManager equipment
               && !string.IsNullOrEmpty(equipment.GetEquipped(c.Subject.SourceDataId, slot));

        public void Execute(MenuContext c)
        {
            if (c.Payload is not EquipmentSlot slot) return;
            if (!c.Services.TryResolve<IEquipmentService>(out var svc) || svc is not EquipmentManager equipment) return;

            if (!equipment.Unequip(c.Subject, slot, out var reason))
                Debug.LogWarning($"[JRPG.Menu] Unequip refused: {reason}");
        }

        public string GetDisabledReason(MenuContext c)
            => c?.Subject == null ? "Pick a party member first." : "That slot is already empty.";
    }
}
