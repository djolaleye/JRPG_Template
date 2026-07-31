using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Equipment Data", fileName = "EquipmentData")]
    public class EquipmentData : ItemData
    {
        public EquipmentSlot slot = EquipmentSlot.MeleeWeapon;
        public List<StatModifier> statModifiers = new();
        public List<string> allowedCharacterIds = new();
        [Min(1)] public int requiredLevel = 1;

        // [Planned — Phase 11] Combat integration for these is not built yet; no system reads them.
        // (statusImmunities is typed List<StatType> — TEMP, to be revisited when the
        // status model lands; a status immunity should key off a status id, not a stat.)
        public List<string> allowedClassTags = new();
        public List<string> passiveEffectIds = new();
        public List<StatType> statusImmunities = new();
        public List<string> actionUnlockIds = new();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            // Force category to Equipment so it can't drift to something else by accident.
            category = ItemCategory.Equipment;
            // Equipment instances always stack at 1 in the prototype.
            stackLimit = 1;
            base.OnValidate();
        }
#endif
    }
}
