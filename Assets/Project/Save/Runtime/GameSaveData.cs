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
    }
}
