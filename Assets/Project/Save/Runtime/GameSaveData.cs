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
    }
}
