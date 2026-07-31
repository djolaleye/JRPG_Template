using System;
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
        // v2: removed the always-empty "characters" contributor (superseded by "party"/"progression").
        public const int CurrentSaveVersion = 2;

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


        /// <summary>
        /// The single authority on save eligibility. Allowed only from exploration, in one of two
        /// shapes:
        ///   Exploration + None      + (Exploration | Menu)   — overworld / quicksave / a plain menu
        ///   Exploration + PauseMenu + Menu                   — the pause menu's Save route
        /// </summary>
        public bool CanSave()
        {
            var s = _state.Current;
            if (s.Mode != GameMode.Exploration) return false;

            if (s.Overlay == OverlayState.None)
                return s.Input == InputContext.Exploration || s.Input == InputContext.Menu;

            if (s.Overlay == OverlayState.PauseMenu)
                return s.Input == InputContext.Menu;

            return false;
        }

        public bool Save(int slot)
        {
            if (!CanSave())
            {
                Debug.LogWarning($"[JRPG.Save] Save(slot {slot}) rejected: not in an allowed state (current={_state.Current}).");
                return false;
            }

            _bus.Publish(new SaveRequested(slot));

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
            if (dto.party == null)
            {
                Debug.LogError("[JRPG.Save] Load: 'party' payload is null.");
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

        /// <summary>
        /// Sequential forward-only migrator: applies one step per version until the dto reaches
        /// <see cref="CurrentSaveVersion"/>. Each step transforms in place; an unknown step fails the
        /// load rather than silently loading mismatched data. Add a case per future schema bump.
        /// </summary>
        private bool Migrate(GameSaveData dto, int fromVersion)
        {
            int v = fromVersion;
            while (v < CurrentSaveVersion)
            {
                switch (v)
                {
                    case 1:
                        MigrateV1ToV2(dto);
                        break;
                    default:
                        Debug.LogError($"[JRPG.Save] No migration step defined from v{v}.");
                        return false;
                }
                v++;
            }
            dto.version = CurrentSaveVersion;
            Debug.Log($"[JRPG.Save] Migrated save from v{fromVersion} to v{CurrentSaveVersion}.");
            return true;
        }

        /// v1 → v2:  "characters" contributor was removed (it was always empty, superseded by the
        /// party/progression payloads). JsonUtility already drops the now-unknown field on read, so
        /// there is no data to transform. Step remains explicit so version bump is auditable
        /// and sequential pipeline is extended.
        private static void MigrateV1ToV2(GameSaveData dto) { }

        private string SlotPath(int slot)
        {
            var fileName = string.Format(_config.fileNameFormat, slot);
            var dir = string.IsNullOrEmpty(_config.directoryName)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, _config.directoryName);
            return Path.Combine(dir, fileName);
        }

        // Slot a contributor's CaptureState() result into the right GameSaveData field by SaveKey.
        // Each registered contributor's SaveKey needs a case here (and a matching GameSaveData field).
        private static void ApplyPayloadByKey(GameSaveData dto, string key, SaveDataBase payload)
        {
            switch (key)
            {
                case "player":
                    if (payload is PlayerPayload pp) dto.player = pp.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'player': {payload?.GetType().Name}");
                    break;
                case "party":
                    if (payload is PartyPayload partyP) dto.party = partyP.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'party': {payload?.GetType().Name}");
                    break;
                case "inventory":
                    if (payload is InventoryPayload invP) dto.inventory = invP.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'inventory': {payload?.GetType().Name}");
                    break;
                case "equipment":
                    if (payload is EquipmentPayload eqP) dto.equipment = eqP.data;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'equipment': {payload?.GetType().Name}");
                    break;
                case "progression":
                    // ProgressionSaveData is itself a SaveDataBase, so no wrapper payload is needed.
                    if (payload is ProgressionSaveData progP) dto.progression = progP;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'progression': {payload?.GetType().Name}");
                    break;
                case "story":
                    if (payload is StorySaveData storyP) dto.story = storyP;
                    else Debug.LogError($"[JRPG.Save] Unexpected payload type for key 'story': {payload?.GetType().Name}");
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
                case "player":
                    return new PlayerPayload { version = dto.version, data = dto.player };
                case "party":
                    return new PartyPayload { version = dto.version, data = dto.party };
                case "inventory":
                    return new InventoryPayload { version = dto.version, data = dto.inventory };
                case "equipment":
                    return new EquipmentPayload { version = dto.version, data = dto.equipment };
                case "progression":
                    if (dto.progression == null) return null;
                    dto.progression.version = dto.version;
                    return dto.progression;
                case "story":
                    if (dto.story == null) return null;
                    dto.story.version = dto.version;
                    return dto.story;
                default:
                    return null;
            }
        }
    }
}
