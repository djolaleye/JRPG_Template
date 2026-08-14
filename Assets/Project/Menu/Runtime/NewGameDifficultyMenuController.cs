using JRPG.Core;
using JRPG.Services;

namespace JRPG.Menu
{
    /// <summary>
    /// The difficulty picker shown when starting a new game. Confirming begins the game.
    /// </summary>
    public sealed class NewGameDifficultyMenuController : DifficultyMenuControllerBase
    {
        /// Nothing is "current" before the game exists; every row is an equal choice.
        protected override bool ShowsCurrent => false;

        public override void Select(Difficulty difficulty)
        {
            if (confirmPrompt == null) { Begin(difficulty); return; }

            confirmPrompt.Ask($"Do you wish to begin the game with {DisplayName(difficulty)} difficulty?",
                              "Yes", "No", () => Begin(difficulty), defaultToCancel: false);
        }

        /// <summary>
        /// Difficulty is set <i>before</i> the session starts, because
        /// <c>SessionService.NewGame</c> resets every service — including this one — as its first act.
        /// Applying afterwards would be silently undone.
        /// </summary>
        private void Begin(Difficulty difficulty)
        {
            if (Context?.Services == null || !Context.Services.TryResolve<ISessionService>(out var session)) return;

            // Close the menu stack first, then set: NewGame's own reset runs during the call below and
            // would overwrite a value applied ahead of it, so the order here is load-bearing.
            Context.Menus?.CloseAll();

            session.NewGame();
            Apply(difficulty);
        }
    }
}
