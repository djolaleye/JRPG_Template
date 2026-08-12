using UnityEngine;

namespace JRPG.Save
{
    [CreateAssetMenu(menuName = "JRPG/Save File Config", fileName = "SaveFileConfig")]
    public class SaveFileConfig : ScriptableObject
    {
        // SaveSystemCore rejects Save/Load/DeleteSlot outside 0..maxSlots-1,
        // and ListSlots() enumerates exactly this many slots for the save/load screens.
        public int maxSlots = 5;
        public string directoryName = "saves";
        public string fileNameFormat = "slot_{0}.json";
    }
}
