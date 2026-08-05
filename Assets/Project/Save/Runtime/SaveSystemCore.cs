using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using JRPG.Core;
using JRPG.Services;

namespace JRPG.Save
{

    /// File-backed ISaveService. Iterates the SaveRegistry to build/restore GameSaveData.
    /// CanSave gates writes against the layered game state.
    public sealed class SaveSystemCore : ISaveService
    {
        // v2: removed the always-empty "characters" contributor (superseded by "party"/"progression").
        // v3: added slot metadata (savedAtUtcTicks / protagonistName / partyLevel) and started
        //     actually populating sceneId + playTime, so slots can be summarized without a restore.
        // v4: party payload carries each character's current HP/MP/SP. Before this, restore rebuilt
        //     instances and progression refilled every pool, so loading was a silent full heal.
        public const int CurrentSaveVersion = 4;

        private readonly SaveRegistry _registry;
        private readonly SaveFileConfig _config;
        private readonly IEventBus _bus;
        private readonly GameStateController _state;

        // ---- Play-time accounting -----------------------------------------------------------
        // Total playTime = whatever the last loaded save
        // reported (_accumulatedPlayTime) + wall-clock seconds since that point
        // (_sessionStartRealtime). A fresh service starts both at "now / zero", so a brand-new game
        // counts from construction. Load() adopts the loaded file's playTime and restarts the
        // session clock, which carries the total across load. Save()
        // reads the running total — so repeated saves keep counting monotonically.
        // Limitation: realtimeSinceStartup keeps running while the game is paused or the
        // app is backgrounded. Swap in a per-frame accumulator later for "active play time"
        private float _sessionStartRealtime;
        private float _accumulatedPlayTime;

        public SaveSystemCore(SaveRegistry registry, SaveFileConfig config, IEventBus bus, GameStateController state)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _state = state ?? throw new ArgumentNullException(nameof(state));

            _sessionStartRealtime = Time.realtimeSinceStartup;
            _accumulatedPlayTime = 0f;
        }

        /// Seconds of play time this service would write right now
        private float CurrentPlayTime()
            => _accumulatedPlayTime + Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStartRealtime);

        /// <summary>
        /// Zeroes the play-time clock for a New Game.
        /// Called by SessionService.NewGame(); not part of ISaveService because it is session
        /// lifecycle, not save I/O.
        /// </summary>
        public void ResetPlayTime()
        {
            _accumulatedPlayTime = 0f;
            _sessionStartRealtime = Time.realtimeSinceStartup;
        }


        /// <summary>
        /// The single authority on save eligibility. Allowed only from exploration, in one of three
        /// shapes:
        ///   Exploration + None      + (Exploration | Menu)   — overworld / quicksave / a plain menu
        ///   Exploration + PauseMenu + Menu                   — the pause menu's Save route
        ///   Exploration + SaveMenu  + Menu                   — a dedicated save/slot-select screen
        /// </summary>
        public bool CanSave()
        {
            var s = _state.Current;
            if (s.Mode != GameMode.Exploration) return false;

            if (s.Overlay == OverlayState.None)
                return s.Input == InputContext.Exploration || s.Input == InputContext.Menu;

            if (s.Overlay == OverlayState.PauseMenu || s.Overlay == OverlayState.SaveMenu)
                return s.Input == InputContext.Menu;

            return false;
        }

        public bool Save(int slot)
        {
            if (!IsValidSlot(slot, nameof(Save))) return false;

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

            PopulateMetadata(dto);

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
            if (!IsValidSlot(slot, nameof(Load))) return false;

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

            // Adopt the loaded file's play time and restart the session clock, so the counter
            // continues from where this save left off rather than from app start.
            _accumulatedPlayTime = Mathf.Max(0f, dto.playTime);
            _sessionStartRealtime = Time.realtimeSinceStartup;

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

            // Second pass, after every contributor has restored.
            foreach (var kv in _registry.Contributors)
            {
                if (kv.Value is ISaveablePostRestore post) post.PostRestore();
            }

            _bus.Publish(new GameLoaded(slot));
            return true;
        }

        // ---- Slot metadata API ---------------------------------------------------------------

        public bool SlotExists(int slot)
        {
            if (slot < 0 || slot >= _config.maxSlots) return false;

            try { return File.Exists(SlotPath(slot)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[JRPG.Save] SlotExists(slot {slot}) IO failure: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses only the metadata header of a slot.
        /// Out-of-range, missing, unreadable and corrupt files all come back
        /// as <see cref="SaveSlotInfo.Empty"/> rather than throwing.
        /// </summary>
        public SaveSlotInfo GetSlotInfo(int slot)
        {
            var empty = SaveSlotInfo.Empty(slot);
            if (slot < 0 || slot >= _config.maxSlots) return empty;

            SaveHeader header;
            try
            {
                var path = SlotPath(slot);
                if (!File.Exists(path)) return empty;
                // JsonUtility ignores fields the target type does not declare, so deserializing
                // into SaveHeader skips every contributor payload except the player's sceneId.
                header = JsonUtility.FromJson<SaveHeader>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JRPG.Save] GetSlotInfo(slot {slot}) unreadable: {e.Message}");
                return empty;
            }

            // A file that parses but carries no usable version is treated as corrupt.
            if (header == null || header.version <= 0)
            {
                Debug.LogWarning($"[JRPG.Save] GetSlotInfo(slot {slot}): missing/invalid version field.");
                return empty;
            }

            // Legacy (pre-v3) saves never wrote the top-level sceneId; fall back to the player
            // payload's copy, which has been written since v1.
            var sceneId = string.IsNullOrEmpty(header.sceneId)
                ? (header.player != null ? header.player.sceneId : null)
                : header.sceneId;

            // Mirror of the save-time fallback, for slots written before it existed: a header that
            // names a lead character but records level 0 was written by a party that had simply never
            // levelled. The header carries no roster, so the lead's name is the signal that one exists.
            var partyLevel = header.partyLevel;
            if (partyLevel == 0 && !string.IsNullOrEmpty(header.protagonistName)) partyLevel = 1;

            return new SaveSlotInfo
            {
                slot = slot,
                exists = true,
                sceneId = sceneId ?? string.Empty,
                playTime = header.playTime,
                savedAtUtcTicks = header.savedAtUtcTicks,   // 0 for pre-v3 saves == "unknown"
                protagonistName = header.protagonistName ?? string.Empty,
                partyLevel = partyLevel
            };
        }

        public IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            int count = Mathf.Max(0, _config.maxSlots);
            var list = new List<SaveSlotInfo>(count);

            for (int i = 0; i < count; i++) list.Add(GetSlotInfo(i));

            return list;
        }

        public bool DeleteSlot(int slot)
        {
            if (!IsValidSlot(slot, nameof(DeleteSlot))) return false;

            try
            {
                var path = SlotPath(slot);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[JRPG.Save] DeleteSlot(slot {slot}): no file at '{path}'.");
                    return false;
                }
                File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JRPG.Save] DeleteSlot(slot {slot}) IO failure: {e.Message}");
                return false;
            }
        }

        /// Shared slot-range guard. maxSlots is authored on the SaveFileConfig asset (currently 8).
        private bool IsValidSlot(int slot, string caller)
        {
            if (slot >= 0 && slot < _config.maxSlots) return true;
            Debug.LogWarning(
                $"[JRPG.Save] {caller}(slot {slot}) rejected: slot out of range (valid 0..{_config.maxSlots - 1}).");
            
            return false;
        }

        /// <summary>
        /// Fills the non-contributor metadata fields after the contributor loop has run.
        /// </summary>
        private void PopulateMetadata(GameSaveData dto)
        {
            var playerScene = dto.player != null ? dto.player.sceneId : null;
            dto.sceneId = !string.IsNullOrEmpty(playerScene)
                ? playerScene
                : SceneManager.GetActiveScene().name;

            dto.playTime = CurrentPlayTime();
            dto.savedAtUtcTicks = DateTime.UtcNow.Ticks;

            string leadId = null;
            if (dto.party != null && dto.party.activeOrder != null)
            {
                for (int i = 0; i < dto.party.activeOrder.Count; i++)
                {
                    if (!string.IsNullOrEmpty(dto.party.activeOrder[i])) { leadId = dto.party.activeOrder[i]; break; }
                }
            }

            dto.protagonistName = leadId ?? string.Empty;

            int level = 0;
            if (dto.progression != null && dto.progression.characters != null)
            {
                int best = 0;
                for (int i = 0; i < dto.progression.characters.Count; i++)
                {
                    var entry = dto.progression.characters[i];
                    if (entry == null) continue;
                    if (!string.IsNullOrEmpty(leadId) && entry.characterId == leadId) { level = entry.level; break; }
                    if (entry.level > best) best = entry.level;
                }

                if (level == 0) level = best;
            }

            // A character who has never levelled has no progression record at all — ProgressionService
            // only tracks characters it has awarded something to. Reporting 0 ("unknown") for that case
            // blanked the Lv column on every early save, when the answer is simply 1: instances are
            // always built at level 1. 0 is now reserved for a save with no roster to speak of.
            if (level == 0 && dto.party?.roster != null && dto.party.roster.Count > 0) level = 1;

            dto.partyLevel = level;
        }

        /// <summary>
        /// Metadata-only view of the save file used by <see cref="GetSlotInfo"/>. JsonUtility drops
        /// every field the target type does not declare, so parsing into this skips the inventory /
        /// party / progression / story payloads entirely. <c>player</c> is declared (sceneId only)
        /// purely so legacy saves without a top-level sceneId can still report their scene.
        /// </summary>
        [Serializable]
        private sealed class SaveHeader
        {
            public int version;
            public string sceneId;
            public float playTime;
            public long savedAtUtcTicks;
            public string protagonistName;
            public int partyLevel;
            public HeaderPlayer player;

            [Serializable]
            public sealed class HeaderPlayer
            {
                public string sceneId;
            }
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
                    case 2:
                        MigrateV2ToV3(dto);
                        break;
                    case 3:
                        MigrateV3ToV4(dto);
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

        /// v2 → v3:  added slot metadata (savedAtUtcTicks / protagonistName / partyLevel) and began
        /// populating the previously-declared-but-never-written sceneId and playTime. Every new
        /// field is additive and JsonUtility already defaults it on read, so there is nothing to
        /// transform — the step stays explicit so the version bump is auditable.
        ///
        /// Semantics of the defaults for a legacy save, which UI must honour:
        ///   savedAtUtcTicks == 0  — save time is UNKNOWN, not the DateTime epoch. Render "—".
        ///   playTime        == 0  — unknown; the counter restarts from this load.
        ///   protagonistName == "" / partyLevel == 0 — unknown; omit them from the slot row.
        ///   sceneId         == "" — GetSlotInfo falls back to the player payload's sceneId.
        private static void MigrateV2ToV3(GameSaveData dto)
        {
            dto.savedAtUtcTicks = 0L;
        }

        /// v3 → v4:  the party payload gained per-character current HP/MP/SP. A legacy save has no such
        /// record, and there is nothing to infer it from — the pools are session state, not a function
        /// of level or gear. Leaving the list empty is the honest outcome: PartyService.PostRestore
        /// finds no entry and leaves the rebuilt-at-full-health defaults in place, which is exactly the
        /// behaviour that save was written under. Explicit so the version bump stays auditable.
        private static void MigrateV3ToV4(GameSaveData dto)
        {
            if (dto.party != null) dto.party.resources ??= new List<CharacterResourceEntry>();
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
