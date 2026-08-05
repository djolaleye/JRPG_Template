using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The in-world pause menu.
    ///
    /// <para><b>Rows explain themselves.</b> Every destination row is built through
    /// <see cref="MenuController.NavigationRow"/>, so a screen that does not exist yet — Progression,
    /// Equipment, Settings — renders greyed with its reason attached.
    /// They light up on their own as those sub-phases register their screens; this controller
    /// needs no edit when they do.</para>
    ///
    /// Exit to Main Menu discards unsaved progress, so it
    /// routes through <see cref="ConfirmPromptController"/> (which defaults to Cancel) before
    /// <see cref="ReturnToTitleAction"/> runs.
    /// </summary>
    public class PauseMenuController : MenuController
    {
        private const string ExitRowId = "exit_to_title";
        [SerializeField] private ConfirmPromptController confirmPrompt;

        protected override bool ModalActive => confirmPrompt != null && confirmPrompt.IsOpen;

        protected override IReadOnlyList<RowModel> BuildRows() => new[]
        {
            NavigationRow("party", "Party", "party"),
            NavigationRow("progression", "Progression", "progression", "No progression screen yet."),
            NavigationRow("inventory", "Inventory", "inventory"),
            NavigationRow("equipment", "Equipment", "equip", "No equipment screen yet."),

            NavigationRow("save", "Save", "save", "No save screen yet.", SaveGate),
            NavigationRow("load", "Load", "load_pause", "No load screen yet.", LoadGate),
            NavigationRow("settings", "Settings", "settings", "No settings screen yet."),

            RowModel.Simple(ExitRowId, "Exit to Main Menu", new ReturnToTitleAction(), Context),
            RowModel.Simple("resume", "Resume", new CloseMenuAction(), Context),
        };

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Resume"),
        };

        // ---- Domain gates ---------------------------------------------------------------------------

        /// Null when saving is allowed here, otherwise the reason. The rule itself lives in the save
        /// service; this only asks.
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

        // ---- Confirmation ---------------------------------------------------------------------------

        protected override void OnSubmit(InputAction.CallbackContext ctx)
        {
            if (ModalActive) return;

            int index = HighlightedIndex;
            if (confirmPrompt == null || !IsRow(index, ExitRowId))
            {
                base.OnSubmit(ctx);
                return;
            }

            // Capture the index: the prompt answers on a later frame, by which point focus may differ.
            int target = index;
            confirmPrompt.Ask("Return to the main menu? Unsaved progress will be lost.",
                              "Exit", "Cancel", () => ExecuteRow(target));
        }

        private bool IsRow(int index, string id)
        {
            if (populator == null || index < 0 || index >= populator.ActiveRows.Count) return false;
            return populator.ActiveRows[index].Model.id == id;
        }
    }
}
