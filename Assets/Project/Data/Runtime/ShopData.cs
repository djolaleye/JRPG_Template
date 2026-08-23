using System;
using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;

namespace JRPG.Data
{
    /// <summary>
    /// One material an offering costs, alongside (or instead of) currency. Materials are ordinary
    /// inventory items, so a recipe is a purchase whose price happens to be paid in goods.
    /// </summary>
    [Serializable]
    public struct MaterialCost
    {
        public string itemId;
        [Min(1)] public int quantity;
    }

    /// <summary>
    /// A named group of offerings inside a specialty shop — the "Buy Items" /
    /// "Buy Accessories" rows the player picks between before seeing a list. Basic shops ignore
    /// sections entirely and show one flat catalog.
    /// </summary>
    [Serializable]
    public class ShopSection
    {
        public string sectionId;
        public string displayName = "Buy";

        [Tooltip("Lower sorts first on the shop's main menu.")]
        public int sortOrder;
    }

    /// <summary>
    /// One purchasable line: what the player receives and everything it costs.
    /// </summary>
    [Serializable]
    public class ShopOfferingData
    {
        [Tooltip("Unique within its shop. Used as the row id and in transaction events.")]
        public string offeringId;

        public string outputItemId;
        [Min(1)] public int outputQuantity = 1;

        [Tooltip("Units available before this row sells out. -1 is unlimited.")]
        [Min(-1)] public int stock = -1;

        /// True when this row can run out at all.
        public bool HasLimitedStock => stock >= 0;

        [Min(0)] public int currencyCost;
        public List<MaterialCost> materialCosts = new();

        [Tooltip("Story flags gating this row. Empty means always stocked.")]
        public List<string> requiredStoryFlags = new();
        public RequirementMode requirementMode = RequirementMode.All;

        [Tooltip("Which section of a specialty shop this belongs to. Ignored by basic shops.")]
        public string sectionId;

        public int sortOrder;

        /// True when the offering asks for goods as well as (or instead of) money — a recipe.
        public bool HasMaterialCosts => materialCosts != null && materialCosts.Count > 0;
    }

    /// <summary>
    /// Whether and how a shop buys goods back from the player. The global category prohibitions in
    /// <c>IItemRuleService</c> always apply first, this narrows further, never widens.
    /// [NOTE: When shop stock is tracked, buying back from the player does not increase remaining stock]
    /// </summary>
    [Serializable]
    public class ShopSellRule
    {
        public bool enabled = true;

        [Tooltip("Categories this vendor will buy. Empty means anything the global rules allow.")]
        public List<ItemCategory> acceptedCategories = new();

        public SellPriceMode priceMode = SellPriceMode.BasePriceMultiplier;

        [Tooltip("Fraction of basePrice paid, using BasePriceMultiplier. Negative uses the economy " +
                 "settings' defaultSellMultiplier.")]
        public float sellMultiplier = -1f;

        [Tooltip("Price per unit, for FlatOverride.")]
        [Min(0)] public int flatPrice;
    }

    /// <summary>
    /// A vendor: its catalog, its sections, its buy-back rule and its own availability gate.
    ///
    /// <para><b>Stock is authored, not stateful.</b> A shop holds no runtime data at all and is
    /// therefore not a save contributor — what the player may buy today is a function of the authored
    /// offerings and the story flags, both of which are already persisted elsewhere.</para>
    /// 
    /// [TODO: Stock should be stateful, so shops can 'sell out' of a certain offering. Remaining stock is captured on save.]
    /// </summary>
    [CreateAssetMenu(menuName = "JRPG/Shop Data", fileName = "ShopData")]
    public class ShopData : GameDataBase
    {
        public ShopType shopType = ShopType.Basic;

        [Tooltip("Specialty shops list one main-menu row per section. Basic shops ignore this.")]
        public List<ShopSection> sections = new();

        public List<ShopOfferingData> offerings = new();

        public ShopSellRule sellRule = new();

        [Tooltip("Story flags gating the whole shop. Empty means always open.")]
        public List<string> requiredStoryFlags = new();
        public RequirementMode requirementMode = RequirementMode.All;

        public bool BuysFromPlayer => shopType == ShopType.Specialty && sellRule != null && sellRule.enabled;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Skipped while the asset is still empty
            if (offerings != null && offerings.Count > 0
                && shopType == ShopType.Basic && sellRule != null && sellRule.enabled)
                Debug.LogWarning($"[JRPG.Data] Shop '{name}' is Basic but enables selling; basic shops are " +
                                 "buy-only and the rule will be ignored.", this);
        }
#endif
    }
}
