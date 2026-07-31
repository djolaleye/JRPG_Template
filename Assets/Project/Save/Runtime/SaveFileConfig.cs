using UnityEngine;

namespace JRPG.Save
{
    [CreateAssetMenu(menuName = "JRPG/Save File Config", fileName = "SaveFileConfig")]
    public class SaveFileConfig : ScriptableObject
    {
        // [Planned — Phase 14] Slot-count enforcement. Declarative today; SaveSystemCore accepts any slot.
        public int maxSlots = 5;
        public string directoryName = "saves";
        public string fileNameFormat = "slot_{0}.json";
    }
}
