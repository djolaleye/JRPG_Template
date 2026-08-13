using System.Collections.Generic;
using JRPG.Core;

namespace JRPG.Services
{
    /// <summary>
    /// Persistent world progress. Today that is encounter lifecycle state; the service is the place
    /// later world flags (opened chests, cleared rooms) belong.
    ///
    /// <para><b>The re-initiation rule lives here, not in triggers or UI.</b> Every caller asks
    /// <see cref="CanInitiate"/>.</para>
    /// </summary>
    public interface IWorldStateService
    {
        /// Unrecorded encounters read as <see cref="EncounterState.Ready"/>.
        EncounterState GetEncounterState(string encounterId);

        /// True only for Ready.
        bool CanInitiate(string encounterId);

        /// Commits the world to resolving this encounter. Refused unless it is Ready.
        bool MarkInProgress(string encounterId);

        /// Marks the encounter fully resolved.
        bool MarkComplete(string encounterId);

        /// Returns an encounter to Ready so it can be fought again. Used by defeat-and-leave, escape,
        /// and crash recovery.
        bool ResetEncounter(string encounterId);

        /// Every encounter with a recorded state. Ready-by-default encounters are absent.
        IReadOnlyDictionary<string, EncounterState> RecordedEncounters { get; }

        void ResetForNewGame();
    }
}
