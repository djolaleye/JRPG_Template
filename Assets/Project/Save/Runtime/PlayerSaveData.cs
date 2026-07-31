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
    /// Wire-format wrapper for the "player" SaveKey: JsonUtility can't serialize a polymorphic
    /// SaveDataBase, so each contributor wraps its DTO in a sealed per-key payload.
    /// </summary>
    [Serializable]
    public sealed class PlayerPayload : SaveDataBase
    {
        public PlayerSaveData data = new();
    }
}
