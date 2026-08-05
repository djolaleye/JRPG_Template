using System.Collections.Generic;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The title screen's menu state: the row stack overlaid on the same 3D background the attract
    /// state uses (see <see cref="TitleScreenController"/>).
    ///
    /// <para><b>Every row is a service call.</b> New Game / Continue route through
    /// <see cref="ISessionService"/>; Load Game and Settings navigate to registered screens. This
    /// controller decides only what to <i>show</i> — never what a new session is, nor whether a slot
    /// is loadable.</para>
    ///
    /// <para><b>Continue is hidden, not disabled, when no save exists.</b> A permanently greyed row on
    /// a brand-new install is noise; the player has nothing to continue and no action to take. Load
    /// Game stays visible and disabled, because that one becomes available later in the same session.</para>
    /// </summary>
    public class MainMenuController : MenuController
    {
        protected override IReadOnlyList<RowModel> BuildRows()
        {
            var rows = new List<RowModel>(5);

            rows.Add(RowModel.Simple("new_game", "New Game", new NewGameAction(), Context));

            int recent = MostRecentSlot(out var recentInfo);
            if (recent >= 0)
            {
                // Name the slot on the row: "Continue" alone gives no clue which save is about to load.
                rows.Add(RowModel.Simple("continue", ContinueLabel(recentInfo),
                                         new LoadGameAction(recent), Context));
            }

            rows.Add(RowModel.Simple("load_game", "Load Game",
                                     new OpenSubmenuAction("load", "No load screen is available."), Context,
                                     enabled: HasAnySave() && MenuExists("load")));

            rows.Add(RowModel.Simple("settings", "Settings",
                                     new OpenSubmenuAction("settings", "No settings screen is available."), Context,
                                     enabled: MenuExists("settings")));

            rows.Add(RowModel.Simple("exit", "Exit", new QuitGameAction(), Context));

            return rows;
        }

        /// Cancel keeps the inherited behaviour — close. This is the bottom frame of the title stack, so
        /// closing empties it and TitleScreenController, watching MenuClosed, drops back to the attract
        /// state.
        public override IReadOnlyList<InputPrompt> Prompts { get; } = new[]
        {
            new InputPrompt("Submit", "Select"),
            new InputPrompt("Cancel", "Back"),
        };

        // ---- Save queries -------------------------------------------------------------------------

        /// The slot the player most likely wants back: newest by save timestamp, falling back to the
        /// lowest occupied index when timestamps are unavailable (legacy pre-v3 saves record none).
        private int MostRecentSlot(out SaveSlotInfo info)
        {
            info = default;
            if (Context?.Services == null || !Context.Services.TryResolve<ISaveService>(out var save)) return -1;

            var slots = save.ListSlots();
            if (slots == null) return -1;

            int best = -1;
            long bestTicks = -1L;

            for (int i = 0; i < slots.Count; i++)
            {
                var candidate = slots[i];
                if (!candidate.exists) continue;

                if (best < 0 || candidate.savedAtUtcTicks > bestTicks)
                {
                    best = candidate.slot;
                    bestTicks = candidate.savedAtUtcTicks;
                    info = candidate;
                }
            }

            return best;
        }

        private static string ContinueLabel(SaveSlotInfo info)
        {
            var label = $"Continue — Slot {info.slot + 1}";
            if (info.partyLevel > 0) label += $"  Lv.{info.partyLevel}";
            if (!string.IsNullOrEmpty(info.sceneId)) label += $"  {info.sceneId}";

            return label;
        }

        private bool HasAnySave() => MostRecentSlot(out _) >= 0;

        private bool MenuExists(string menuId) => Context?.Menus != null && Context.Menus.HasMenu(menuId);
    }
}
