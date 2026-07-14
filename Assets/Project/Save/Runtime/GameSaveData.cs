using System;
using System.Collections.Generic;
using JRPG.Core;
using JRPG.Characters;

namespace JRPG.Save
{
    [Serializable]
    public class GameSaveData : SaveDataBase
    {
        public string sceneId;
        public string checkpointId;
        public float playTime;

        // Phase 3 proof-of-structure payload. Later phases add party, inventory, progression, story, world.
        public List<CharacterSaveData> characters = new();

        // Phase 4: player transform contributor.
        public PlayerSaveData player = new();

        // Phase 5: party roster + scope stack.
        public PartySaveData party = new();

        // Phase 6: inventory stacks + per-character equipment.
        public InventorySaveData inventory = new();
        public EquipmentSaveData equipment = new();

        // Phase 8: per-character level/XP/attribute-point progression.
        public ProgressionSaveData progression = new();

        // Phase 9: story flags + completed dialogue markers.
        public StorySaveData story = new();
    }
}
