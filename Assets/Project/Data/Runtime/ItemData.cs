using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    [CreateAssetMenu(menuName = "JRPG/Item Data", fileName = "ItemData")]
    public class ItemData : GameDataBase
    {
        public Sprite icon;
        public ItemCategory category = ItemCategory.Consumable;
        [Min(1)] public int stackLimit = 99;
        public bool canSell = true;
        public bool canDiscard = true;
        [Min(0)] public int basePrice;
        public ItemUsageRule usageRule = new();

        /// <summary>
        /// What the item does when used outside of combat.
        /// </summary>
        public List<ItemEffect> linkedEffects = new();

        // ---- Combat use -------------------------------------------------------------------

        [Header("Combat use")]
        [Tooltip("Full combat effects for this item. Leave EMPTY to auto-derive them from " +
                 "linkedEffects above.")]
        public List<CombatEffect> combatEffects = new();

        [Tooltip("Who the item can be used on in battle.")]
        public TargetRule combatTargetRule = new TargetRule
        {
            team = TargetTeam.Allies,
            selectionMode = TargetSelectionMode.Single,
        };

        /// True when this item should appear as an action in the battle Item menu.
        public bool IsCombatAction =>
            category == ItemCategory.Consumable
            && ItemCombatRules.IsUsableInCombat(this)
            && (combatEffects.Count > 0 || linkedEffects.Count > 0);

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if ((category == ItemCategory.KeyItem || category == ItemCategory.QuestItem) && stackLimit != 1)
                Debug.LogWarning($"[JRPG.Data] '{name}' ({category}) has stackLimit={stackLimit}; key/quest items should cap at 1.", this);
        }
#endif
    }
}
