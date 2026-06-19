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
        public List<string> allowedClassTags = new();
        [Min(1)] public int requiredLevel = 1;

        // Placeholder lists honored when the relevant systems exist.
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
