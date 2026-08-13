using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Save;
using JRPG.Services;

namespace JRPG.Party
{
    /// <summary>
    /// In-memory encounter ledger, persisted through the save system under the key <c>world</c>.
    ///
    /// <para><b>Crash recovery is a property of the restore, not a separate pass.</b>
    /// <see cref="RestoreState"/> converts every saved <c>InProgress</c> to <c>Ready</c>, so an
    /// encounter cannot be stuck by a crash, a force-quit, or a save file hand-edited into a bad
    /// state. Doing it anywhere else would leave a window where the stuck value is visible.</para>
    ///
    /// <para>Only encounters that have left <c>Ready</c> are recorded; the absence of an entry is the
    /// Ready state, so the save does not grow with every encounter the designer authors.</para>
    /// </summary>
    public sealed class WorldStateService : IWorldStateService, ISaveable
    {
        private readonly IEventBus _bus;
        private readonly Dictionary<string, EncounterState> _encounters = new();

        public WorldStateService(IEventBus bus)
        {
            _bus = bus;
        }

        public IReadOnlyDictionary<string, EncounterState> RecordedEncounters => _encounters;

        // ---- IWorldStateService ---------------------------------------------------------------

        public EncounterState GetEncounterState(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return EncounterState.Ready;
            return _encounters.TryGetValue(encounterId, out var state) ? state : EncounterState.Ready;
        }

        public bool CanInitiate(string encounterId)
            => !string.IsNullOrEmpty(encounterId) && GetEncounterState(encounterId) == EncounterState.Ready;

        public bool MarkInProgress(string encounterId)
        {
            if (!CanInitiate(encounterId))
            {
                Debug.LogWarning($"[JRPG.World] Encounter '{encounterId}' cannot start — it is " +
                                 $"{GetEncounterState(encounterId)}.");
                return false;
            }

            return Set(encounterId, EncounterState.InProgress);
        }

        public bool MarkComplete(string encounterId)
        {
            // Completing from Ready would mean the encounter was resolved without ever being started.
            if (GetEncounterState(encounterId) != EncounterState.InProgress)
            {
                Debug.LogWarning($"[JRPG.World] Encounter '{encounterId}' cannot complete from " +
                                 $"{GetEncounterState(encounterId)}.");
                return false;
            }

            return Set(encounterId, EncounterState.Complete);
        }

        public bool ResetEncounter(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return false;
            return Set(encounterId, EncounterState.Ready);
        }

        public void ResetForNewGame() => _encounters.Clear();

        private bool Set(string encounterId, EncounterState next)
        {
            var previous = GetEncounterState(encounterId);
            if (previous == next) return true;

            // Ready is the default, so recording it would only bloat the save.
            if (next == EncounterState.Ready) _encounters.Remove(encounterId);
            else _encounters[encounterId] = next;

            _bus?.Publish(new EncounterStateChanged(encounterId, previous, next));
            return true;
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "world";

        public SaveDataBase CaptureState()
        {
            var payload = new WorldSaveData { version = SaveSystemCore.CurrentSaveVersion };

            foreach (var kv in _encounters)
                payload.encounters.Add(new EncounterStateEntry { encounterId = kv.Key, state = kv.Value });

            return payload;
        }

        public void RestoreState(SaveDataBase state)
        {
            if (state is not WorldSaveData payload) return;

            _encounters.Clear();
            if (payload.encounters == null) return;

            int recovered = 0;

            for (int i = 0; i < payload.encounters.Count; i++)
            {
                var entry = payload.encounters[i];
                if (string.IsNullOrEmpty(entry.encounterId)) continue;

                // The whole of the crash-recovery rule. A save can only have been written mid-battle
                // by a crash or a hand-edit — CanSave() refuses to write during GameMode.Combat — and
                // either way the battle is gone, so the encounter is fightable again.
                if (entry.state == EncounterState.InProgress)
                {
                    recovered++;
                    continue;
                }

                if (entry.state == EncounterState.Ready) continue;

                _encounters[entry.encounterId] = entry.state;
            }

            if (recovered > 0)
                Debug.Log($"[JRPG.World] Recovered {recovered} interrupted encounter(s) to Ready.");
        }
    }
}
