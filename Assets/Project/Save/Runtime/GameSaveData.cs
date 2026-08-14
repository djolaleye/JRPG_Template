using System;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public class GameSaveData : SaveDataBase
    {
        // ---- Slot metadata (v3) -------------------------------------------------------------
        // Written directly by SaveSystemCore.Save() rather than by a contributor, and readable
        // without running any restore — this is what SaveSystemCore.GetSlotInfo() parses so a
        // slot-select screen can show a summary (and learn which scene to load) up front.

        // PlayerSaveData carries the active scene name, which Save() mirrors into sceneId (falling back
        // to the active scene when the player contributor is absent). GetSlotInfo() reads it without
        // running a restore, so the load flow knows which scene to bring up first.
        public string sceneId;

        // [Planned] Checkpoint restoration. Currently - a load resumes from the saved player position rather than a checkpoint.
        public string checkpointId;

        /// Total accumulated play seconds. See SaveSystemCore's play-time accounting comment.
        public float playTime;

        /// DateTime.UtcNow.Ticks at save time. 0 means "unknown" (pre-v3 saves) — UI renders "—".
        public long savedAtUtcTicks;

        /// Stable id of the lead/protagonist character, not a localized display name: the save
        /// format carries no names. Empty when the party payload has no active member.
        public string protagonistName;

        /// Protagonist's level (or the roster's highest, as a fallback). 0 means "unknown".
        public int partyLevel;

        // Player transform contributor.
        public PlayerSaveData player = new();

        // Party roster + scope stack.
        public PartySaveData party = new();

        // Inventory stacks + per-character equipment.
        public InventorySaveData inventory = new();
        public EquipmentSaveData equipment = new();

        // Per-character level/XP/attribute-point progression.
        public ProgressionSaveData progression = new();

        // Story flags + completed dialogue markers.
        public StorySaveData story = new();

        // Encounter lifecycle state. Never records an in-flight battle — see WorldStateService.
        public WorldSaveData world = new();

        // The selected difficulty. The tuning it maps to is authored data, not save state.
        public DifficultySaveData difficulty = new();

        // Which placed chests have been opened. Records opened chests only — see ChestStateService.
        public ChestSaveData chests = new();
    }
}
