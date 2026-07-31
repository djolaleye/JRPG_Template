using System;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public class GameSaveData : SaveDataBase
    {
        // [Planned — Phase 14] Scene/checkpoint restoration.
        // No Scene/Checkpoint Manager yer. PlayerSaveData carries the active scene
        // name in the meantime.
        public string sceneId;
        public string checkpointId;
        public float playTime;

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
    }
}
