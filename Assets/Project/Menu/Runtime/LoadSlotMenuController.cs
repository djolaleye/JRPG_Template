using System.Collections.Generic;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// Slot-select for restoring a save. Empty slots are visible but disabled.
    ///
    /// <para>Loading is confirmed only while a session is live: from the title there is nothing to lose,
    /// but loading from the pause menu discards unsaved progress.</para>
    /// </summary>
    public sealed class LoadSlotMenuController : SaveLoadMenuControllerBase
    {
        protected override IMenuAction CreateSlotAction(SaveSlotInfo info) => new LoadGameAction(info.slot);

        protected override bool IsSlotEnabled(SaveSlotInfo info, ISaveService save) => info.exists;

        protected override bool NeedsConfirmation(SaveSlotInfo info, out string question, out string confirmLabel)
        {
            question = null;
            confirmLabel = null;
            if (!info.exists) return false;

            bool sessionLive = Context?.Services != null
                               && Context.Services.TryResolve<ISessionService>(out var session)
                               && session.IsSessionActive;
            if (!sessionLive) return false;

            question = "Load this save? Unsaved progress will be lost.";
            confirmLabel = "Load";
            return true;
        }

        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Load"),
            new InputPrompt("Cancel", "Back"),
            new InputPrompt("Tab", "Delete"),
        };
    }
}
