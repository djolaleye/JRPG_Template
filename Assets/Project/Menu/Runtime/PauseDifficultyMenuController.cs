using JRPG.Core;

namespace JRPG.Menu
{
    /// <summary>
    /// The difficulty picker reached from the pause menu. Applies immediately and closes.
    ///
    /// <para><b>Only raising to Hard asks.</b> Every other change is reversible from this same screen a
    /// moment later. Hard is the setting that takes something
    /// away — a lost battle can no longer be retried — and that is worth saying out loud before it
    /// applies.</para>
    ///
    /// <para>Pause is exploration-only, so a change made here can never land mid-battle.</para>
    /// </summary>
    public sealed class PauseDifficultyMenuController : DifficultyMenuControllerBase
    {
        public override void Select(Difficulty difficulty)
        {
            if (!TryGetService(out var service)) return;

            if (service.Current == difficulty) { Context.Menus?.Close(); return; }

            bool warns = difficulty == Difficulty.Hard && confirmPrompt != null;
            if (!warns) { Commit(difficulty); return; }

            confirmPrompt.Ask("Switch to Hard? Lost battles cannot be retried.",
                              "Switch", "Cancel", () => Commit(difficulty));
        }

        private void Commit(Difficulty difficulty)
        {
            Apply(difficulty);

            Context.Menus?.Close();
        }
    }
}
