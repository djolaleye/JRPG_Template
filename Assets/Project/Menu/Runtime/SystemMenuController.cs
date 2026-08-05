using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The system submenu: Settings, Save, Load, Quit.
    ///
    /// <para>The pause menu reaches these destinations directly now, so this
    /// screen is no longer on the pause route. It is kept, and kept correct, because <c>system</c> is a
    /// live registry entry and this is the natural host wherever a compact system list is wanted.</para>
    /// </summary>
    public sealed class SystemMenuController : MenuController
    {
        private const string QuitRowId = "quit";

        [Tooltip("Confirmation modal for Quit. Optional: without it quitting happens immediately.")]
        [SerializeField] private ConfirmPromptController confirmPrompt;

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            NavigationRow("settings", "Settings", "settings", "No settings screen yet."),
            NavigationRow("save", "Save", "save", "No save screen yet.", SaveGate),
            NavigationRow("load", "Load", "load_pause", "No load screen yet.", LoadGate),

            RowModel.Simple(QuitRowId, "Quit to Desktop", new QuitGameAction(), Context),
            RowModel.Simple("back", "Back", new CloseMenuAction(), Context),
        };

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Back"),
        };

        private static string SaveGate(MenuContext c)
        {
            if (c?.Services == null || !c.Services.TryResolve<ISaveService>(out var save) || save == null)
                return "Save service unavailable.";

            return save.CanSave() ? null : "Can't save here.";
        }

        private static string LoadGate(MenuContext c)
        {
            if (c?.Services == null || !c.Services.TryResolve<ISaveService>(out var save) || save == null)
                return "Save service unavailable.";

            var slots = save.ListSlots();
            if (slots != null)
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i].exists) return null;

            return "No saved games.";
        }

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            if (confirmPrompt == null || !IsRow(index, QuitRowId))
            {
                base.OnSubmit(ctx);
                return;
            }

            int target = index;
            confirmPrompt.Ask("Quit to desktop? Unsaved progress will be lost.",
                              "Quit", "Cancel", () => ExecuteRow(target));
        }

        private bool IsRow(int index, string id)
        {
            if (populator == null || index < 0 || index >= populator.ActiveRows.Count) return false;
            return populator.ActiveRows[index].Model.id == id;
        }
    }
}
