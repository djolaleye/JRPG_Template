using System.Collections.Generic;
using UnityEngine;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Equipment Data", fileName = "EquipmentData")]
    public class EquipmentData : ItemData
    {
        [Header("Equip rules")]
        public EquipmentSlot slot = EquipmentSlot.MeleeWeapon;
        public List<string> allowedCharacterIds = new();
        [Min(1)] public int requiredLevel = 1;

        [Tooltip("Applied to the wearer's stats")]
        public List<StatModifier> statModifiers = new();

        [Header("Weapon")]
        /// <summary>
        /// Flat damage this weapon contributes to a basic attack, replacing the base power authored on
        /// the attack action itself.
        ///
        /// <para>Only read for the two weapon slots, and only by
        /// <see cref="CombatActionCategory.Melee"/> / <see cref="CombatActionCategory.Ranged"/> actions.
        /// Skills keep their own authored base power.</para>
        ///
        /// <para>Negative means "unset": the action's authored base power is used instead. 0 is a
        /// legitimate value (a weapon that adds only stats).</para>
        /// </summary>
        [Tooltip("Base damage this weapon gives a basic attack. " +
                 "Negative = unset, action's authored value is used. Weapon slots only.")]
        public int weaponBasePower = -1;

        /// <summary>True when this item occupies a weapon slot and authored a base power to contribute.</summary>
        public bool HasWeaponBasePower
            => weaponBasePower >= 0 && (slot == EquipmentSlot.MeleeWeapon || slot == EquipmentSlot.RangedWeapon);

        [Header("Combat integration")]
        [Tooltip("Elemental responses granted by wearing this.")]
        public List<ElementAffinityEntry> elementAffinities = new();

        [Tooltip("Statuses this equipment grants immunity to.")]
        public List<string> statusImmunityIds = new();

        [Tooltip("Passives granted while equipped.")]
        public List<string> passiveEffectIds = new();

        [Tooltip("Combat actions this equipment unlocks for the wearer, even if the action's own " +
                 "team flags would exclude them.")]
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
