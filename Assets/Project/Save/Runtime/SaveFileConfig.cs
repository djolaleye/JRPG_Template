using UnityEngine;

namespace JRPG.Save
{
    [CreateAssetMenu(menuName = "JRPG/Save File Config", fileName = "SaveFileConfig")]
    public class SaveFileConfig : ScriptableObject
    {
        public int maxSlots = 5;
        public string directoryName = "saves";
        public string fileNameFormat = "slot_{0}.json";

        // No-op feature flags for later. Wiring exists; behavior is intentionally minimal in the foundation.
        public bool useCompression;
        public bool useEncryption;
        public bool keepBackup;
    }
}
