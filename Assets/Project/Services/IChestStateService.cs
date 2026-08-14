using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// Runtime authority for which placed chests have been opened. Scene components author the
    /// starting state; this service owns every change after that and is what survives a save.
    ///
    /// <para>Opening is terminal: <see cref="MarkOpened"/> is the only transition that matters, and
    /// nothing but <see cref="ResetChest"/> / <see cref="ResetForNewGame"/> can undo it.</para>
    /// </summary>
    public interface IChestStateService
    {
        /// <summary>
        /// Recorded state for a chest, or <paramref name="authoredDefault"/> when the service has
        /// never heard of it — the absence of a record is "as the designer placed it".
        /// </summary>
        ChestOpenedState GetState(string chestId, ChestOpenedState authoredDefault = ChestOpenedState.Unopened);

        bool IsOpened(string chestId);

        /// <summary>Records the chest as opened. Returns false when it already was.</summary>
        bool MarkOpened(string chestId);

        /// <summary>Debug/authoring path: forget this chest, so its authored state applies again.</summary>
        bool ResetChest(string chestId);

        void ResetForNewGame();
    }
}
