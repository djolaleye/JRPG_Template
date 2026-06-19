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
        public ItemUsageRule usageRule = new();

        /// <summary>
        /// Minimal effect list for the Heal/RestoreMP/RestoreSP prototype path. When the full
        /// combat effect pipeline lands, this is superseded by a richer `linkedEffectIds` lookup.
        /// </summary>
        public List<ItemEffect> linkedEffects = new();

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
