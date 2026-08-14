using System.Collections.Generic;
using JRPG.Core;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Party
{
    /// <summary>
    /// In-memory chest ledger, persisted through the save system under the key <c>chests</c>. Sibling
    /// of <see cref="WorldStateService"/> and built on the same two rules.
    ///
    /// <para><b>Only opened chests are recorded.</b> A chest the player has not touched has no entry,
    /// so the save does not grow with every chest the designer places, and an authored
    /// <c>Unopened</c>/<c>Locked</c> chest reads its state from the scene where it belongs.</para>
    ///
    /// <para><b>Opened is irreversible for the lifetime of the save.</b> The ledger stores nothing but
    /// <c>Opened</c>, so there is no value a restore could write that would re-close a chest, and
    /// <see cref="MarkOpened"/> is a one-way door. Reopening is a debug/New Game affordance only.</para>
    /// </summary>
    public sealed class ChestStateService : IChestStateService, ISaveable
    {
        private readonly HashSet<string> _opened = new();

        /// <remarks>
        /// No event bus: the ledger records the outcome of a transaction it does not run. The
        /// <see cref="ChestOpened"/> notification is published by the chest component, which is the
        /// only place that knows what was granted.
        /// </remarks>
        public IReadOnlyCollection<string> OpenedChests => _opened;

        // ---- IChestStateService ----------------------------------------------------------------

        public ChestOpenedState GetState(string chestId, ChestOpenedState authoredDefault = ChestOpenedState.Unopened)
        {
            if (string.IsNullOrEmpty(chestId)) return authoredDefault;

            // A recorded chest is open; anything else defers to how the scene authored it. Saved
            // state can therefore only ever win *upwards*, which is the whole invariant.
            return _opened.Contains(chestId) ? ChestOpenedState.Opened : authoredDefault;
        }

        public bool IsOpened(string chestId)
            => !string.IsNullOrEmpty(chestId) && _opened.Contains(chestId);

        public bool MarkOpened(string chestId)
        {
            if (string.IsNullOrEmpty(chestId)) return false;

            return _opened.Add(chestId);
        }

        public bool ResetChest(string chestId)
        {
            if (string.IsNullOrEmpty(chestId)) return false;

            return _opened.Remove(chestId);
        }

        public void ResetForNewGame() => _opened.Clear();

        // ---- ISaveable -------------------------------------------------------------------------

        public string SaveKey => "chests";

        public SaveDataBase CaptureState()
        {
            var payload = new ChestSaveData { version = SaveSystemCore.CurrentSaveVersion };

            foreach (var chestId in _opened)
                payload.entries.Add(new ChestStateEntry { chestId = chestId, state = ChestOpenedState.Opened });

            return payload;
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not ChestSaveData payload) return;

            _opened.Clear();
            if (payload.entries == null) return;

            for (int i = 0; i < payload.entries.Count; i++)
            {
                var entry = payload.entries[i];
                if (string.IsNullOrEmpty(entry.chestId)) continue;

                // Defensive: a hand-edited or future save could carry a non-Opened row. The ledger
                // has no way to express "closed", so such a row is simply the absence of a record.
                if (entry.state != ChestOpenedState.Opened) continue;

                _opened.Add(entry.chestId);
            }
        }
    }
}
