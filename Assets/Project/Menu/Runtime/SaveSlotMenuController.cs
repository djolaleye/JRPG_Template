using System.Collections.Generic;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Slot-select for writing a save. Every row is a candidate; occupied ones confirm first.
    ///
    /// <para>Row availability is <see cref="ISaveService.CanSave"/>'s call, not this screen's — the
    /// service is the single authority on save eligibility, and it is state-sensitive (mid-battle and
    /// the title screen are both refusals).</para>
    /// </summary>
    public sealed class SaveSlotMenuController : SaveLoadMenuControllerBase
    {
        protected override IMenuAction CreateSlotAction(SaveSlotInfo info) => new SaveGameAction(info.slot);

        protected override bool IsSlotEnabled(SaveSlotInfo info, ISaveService save) => save.CanSave();

        protected override bool NeedsConfirmation(SaveSlotInfo info, out string question, out string confirmLabel)
        {
            question = null;
            confirmLabel = null;
            if (!info.exists) return false;   // writing into an empty slot destroys nothing

            question = $"Overwrite the save in slot {info.slot + 1}?";
            confirmLabel = "Overwrite";
            return true;
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Save"),
            new InputPrompt("Cancel", "Back"),
            new InputPrompt("Tab", "Delete"),
        };
    }
}
