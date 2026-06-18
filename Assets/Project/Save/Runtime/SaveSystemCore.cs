using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Save
{

    /// File-backed ISaveService. Iterates the SaveRegistry to build/restore GameSaveData.
    /// CanSave gates writes against the layered game state.
    public sealed class SaveSystemCore : ISaveService
    {
        public const int CurrentSaveVersion = 1;

        private readonly SaveRegistry _registry;
        private readonly SaveFileConfig _config;
        private readonly IEventBus _bus;
        private readonly GameStateController _state;

        public SaveSystemCore(SaveRegistry registry, SaveFileConfig config, IEventBus bus, GameStateController state)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }


        /// Saving is allowed only in safe states: Exploration + None overlay, with Exploration or Menu input.
        public bool CanSave()
        {
            var currentState = _state.Current;

            if (currentState.Mode != GameMode.Exploration) return false;
            if (currentState.Overlay != OverlayState.None) return false;

            return currentState.Input == InputContext.Exploration || currentState.Input == InputContext.Menu;
        }

        public bool Save(int slot)
        {
            _bus.Publish(new SaveRequested(slot));

            if (!CanSave())
            {
                Debug.LogWarning($"[JRPG.Save] Save(slot {slot}) rejected: not in an allowed state (current={_state.Current}).");
                return false;
            }

            var dto = new GameSaveData { version = CurrentSaveVersion };
            foreach (var kv in _registry.Contributors)
            {
                var payload = kv.Value.CaptureState();
                ApplyPayloadByKey(dto, kv.Key, payload);
            }

            var path = SlotPath(slot);
            var tempPath = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                var json = JsonUtility.ToJson(dto, true);

                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                
                File.WriteAllText(tempPath, json);
                if (File.Exists(path)) File.Replace(tempPath, path, null);
                else File.Move(tempPath, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JRPG.Save] Save(slot {slot}) IO failure: {e.Message}");
                
                if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { /* swallow */ }
                return false;
            }

            _bus.Publish(new GameSaved(slot));
            return true;
        }

        public bool Load(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[JRPG.Save] Load(slot {slot}): no file at '{path}'.");
                return false;
            }

            string json;
            GameSaveData dto;
            try
            {
                json = File.ReadAllText(path);
                dto = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JRPG.Save] Load(slot {slot}) parse failure: {e.Message}");
                return false;
            }

            if (dto == null)
            {
                Debug.LogError("[JRPG.Save] Load: deserialized payload is null.");
                return false;
            }
            if (dto.version <= 0)
            {
                Debug.LogError("[JRPG.Save] Load: missing/invalid version field.");
                return false;
            }
            if (dto.version > CurrentSaveVersion)
            {
                Debug.LogError($"[JRPG.Save] Load: save version {dto.version} is newer than supported ({CurrentSaveVersion}).");
                return false;
            }
            if (dto.version < CurrentSaveVersion)
            {
                if (!Migrate(dto, dto.version))
                {
                    Debug.LogError($"[JRPG.Save] Load: migration from v{dto.version} to v{CurrentSaveVersion} failed.");
                    return false;
                }
            }
            if (dto.characters == null)
            {
                Debug.LogError("[JRPG.Save] Load: 'characters' list is null.");
                return false;
            }

            foreach (var kv in _registry.Contributors)
            {
                var payload = ExtractPayloadByKey(dto, kv.Key);
                if (payload == null)
                {
                    Debug.LogWarning($"[JRPG.Save] Load: no payload for SaveKey '{kv.Key}'.");
                    continue;
                }
                kv.Value.RestoreState(payload);
            }

            _bus.Publish(new GameLoaded(slot));
            return true;
        }

        /// Minimal forward-only migration hook. Override per phase as the schema evolves.
        /// Returns true if the dto is upgraded to CurrentSaveVersion (or already compatible).
        private bool Migrate(GameSaveData dto, int fromVersion)
        {
            Debug.Log($"[JRPG.Save] Migrate: from v{fromVersion} to v{CurrentSaveVersion} (no-op stub).");
            dto.version = CurrentSaveVersion;
            return true;
        }

        private string SlotPath(int slot)
        {
            var fileName = string.Format(_config.fileNameFormat, slot);
            var dir = string.IsNullOrEmpty(_config.directoryName)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, _config.directoryName);
            return Path.Combine(dir, fileName);
        }

        // Slot a contributor's CaptureState() result into the right GameSaveData field by SaveKey.
        // Phase 3 only knows about the "characters" key; later phases extend this.
        private static void ApplyPayloadByKey(GameSaveData dto, string key, SaveDataBase payload)
        {
            switch (key)
            {
                case "characters":
                    if (payload is CharactersPayload cp) dto.characters = cp.entries;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'characters': {payload?.GetType().Name}");
                    break;
                case "player":
                    if (payload is PlayerPayload pp) dto.player = pp.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'player': {payload?.GetType().Name}");
                    break;
                case "party":
                    if (payload is PartyPayload partyP) dto.party = partyP.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'party': {payload?.GetType().Name}");
                    break;
                default:
                    Debug.LogWarning($"[JRPG.Save] Unknown SaveKey '{key}' (no field in GameSaveData).");
                    break;
            }
        }

        private static SaveDataBase ExtractPayloadByKey(GameSaveData dto, string key)
        {
            switch (key)
            {
                case "characters":
                    return new CharactersPayload { version = dto.version, entries = dto.characters };
                case "player":
                    return new PlayerPayload { version = dto.version, data = dto.player };
                case "party":
                    return new PartyPayload { version = dto.version, data = dto.party };
                default:
                    return null;
            }
        }
    }


    /// Wire format wrapper so a contributor (CharacterHolder) can hand a list of CharacterSaveData
    /// to the save core via the non-generic ISaveable contract.
    [Serializable]
    public sealed class CharactersPayload : SaveDataBase
    {
        public List<JRPG.Characters.CharacterSaveData> entries = new();
    }
}
