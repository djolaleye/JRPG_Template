using System;
using JRPG.Core;

namespace JRPG.Save
{
    [Serializable]
    public class PlayerSaveData : SaveDataBase
    {
        public float posX, posY, posZ;
        public float yaw;
        public string sceneId;
    }

    /// <summary>
    /// Wire format wrapper for the "player" SaveKey, mirroring the CharactersPayload pattern.
    /// </summary>
    [Serializable]
    public sealed class PlayerPayload : SaveDataBase
    {
        public PlayerSaveData data = new();
    }
}
