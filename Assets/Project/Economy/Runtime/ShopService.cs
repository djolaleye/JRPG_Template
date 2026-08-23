using System.Collections.Generic;
using UnityEngine;
using JRPG.Core;
using JRPG.Data;
using JRPG.Services;

namespace JRPG.Economy
{
    /// <summary>
    /// Vendors: what is stocked, what it costs, and the exchange itself.
    ///
    /// <para><b>Preflight, then commit.</b> A purchase resolves the offering, checks the story gate,
    /// the inventory room for the whole output, the money and every material — and only then takes
    /// anything. The player can never end up short a material/currency for an item that did not fit.</para>
    ///
    /// <para><b>Selling asks the rule service first.</b> <see cref="IItemRuleService.CanSell"/> is the
    /// global prohibition (key items, quest items, tools, worn gear); a shop's own rule narrows that
    /// further by category. A vendor can refuse what the rules allow, but never the reverse.</para>
    ///
    /// <para><b>TEMP: No state, no save contributor.</b> Stock is authored and availability is derived from
    /// story flags, which are persisted already — so there is nothing here for a save to hold.
    /// [TODO: Shops will track their remaining stock of each offering, determining availability for purchase.]</para>
    /// </summary>
    public sealed class ShopService : IShopService
    {
        private readonly DataRegistry _data;
        private readonly IInventoryService _inventory;
        private readonly ICurrencyService _currency;
        private readonly IItemRuleService _rules;
        private readonly IStoryStateService _story;
        private readonly IEventBus _bus;

        public ShopService(DataRegistry data, IInventoryService inventory, ICurrencyService currency,
                           IItemRuleService rules, IEventBus bus, IStoryStateService story = null)
        {
            _data = data;
            _inventory = inventory;
            _currency = currency;
            _rules = rules;
            _bus = bus;
            _story = story;
        }

        // ---- Catalog  ----------------------------------------

        public ShopData GetShop(string shopId)
            => !string.IsNullOrEmpty(shopId) && _data != null && _data.TryGet<ShopData>(shopId, out var shop)
                ? shop
                : null;

        public ShopOfferingData GetOffering(string shopId, string offeringId)
        {
            var shop = GetShop(shopId);
            if (shop == null || string.IsNullOrEmpty(offeringId)) return null;

            for (int i = 0; i < shop.offerings.Count; i++)
                if (shop.offerings[i] != null && shop.offerings[i].offeringId == offeringId)
                    return shop.offerings[i];

            return null;
        }

        /// <summary>
        /// Sections with at least one authored offering, in sort order. A section whose rows are all
        /// story-locked is still returned — the screen renders it disabled.
        /// </summary>
        public IReadOnlyList<ShopSection> GetSections(string shopId)
        {
            var shop = GetShop(shopId);
            var sections = new List<ShopSection>();

            if (shop == null || shop.sections == null) return sections;

            for (int i = 0; i < shop.sections.Count; i++)
            {
                var section = shop.sections[i];
                if (section == null || string.IsNullOrEmpty(section.sectionId)) continue;

                sections.Add(section);
            }

            sections.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

            return sections;
        }

        /// <summary>
        /// Stocked offerings, optionally narrowed to one section. Sorted by the authored order; a shop
        /// that authors none keeps its list order.
        /// </summary>
        public IReadOnlyList<ShopOfferingData> GetAvailableOfferings(string shopId, string sectionId = null)
        {
            var results = new List<ShopOfferingData>();
            var shop = GetShop(shopId);

            if (shop == null || shop.offerings == null) return results;

            bool narrowing = !string.IsNullOrEmpty(sectionId);

            for (int i = 0; i < shop.offerings.Count; i++)
            {
                var offering = shop.offerings[i];

                if (offering == null || string.IsNullOrEmpty(offering.offeringId)) continue;
                if (narrowing && offering.sectionId != sectionId) continue;
                if (!FlagsSatisfied(offering.requiredStoryFlags, offering.requirementMode)) continue;

                results.Add(offering);
            }

            results.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));

            return results;
        }

        /// <summary>Offerings in a section regardless of their story gate.</summary>
        public int CountOfferingsInSection(string shopId, string sectionId)
        {
            var shop = GetShop(shopId);
            if (shop == null || shop.offerings == null) return 0;

            int count = 0;
            for (int i = 0; i < shop.offerings.Count; i++)
            {
                var offering = shop.offerings[i];
                if (offering != null && offering.sectionId == sectionId) count++;
            }

            return count;
        }

        // ---- IShopService -----------------------------------------------------------------------

        public bool IsShopAvailable(string shopId)
        {
            var shop = GetShop(shopId);

            return shop != null && FlagsSatisfied(shop.requiredStoryFlags, shop.requirementMode);
        }

        public IReadOnlyList<string> GetAvailableOfferingIds(string shopId, string sectionId = null)
        {
            var offerings = GetAvailableOfferings(shopId, sectionId);
            var ids = new List<string>(offerings.Count);

            for (int i = 0; i < offerings.Count; i++) ids.Add(offerings[i].offeringId);

            return ids;
        }

        public bool CanBuy(string shopId, string offeringId, int quantity, out string reason)
        {
            reason = null;

            if (quantity <= 0)
            {
                reason = "Choose an amount first.";
                return false;
            }

            if (!IsShopAvailable(shopId))
            {
                reason = "This shop is closed.";
                return false;
            }

            var offering = GetOffering(shopId, offeringId);
            if (offering == null)
            {
                reason = "Not stocked.";
                return false;
            }

            if (!FlagsSatisfied(offering.requiredStoryFlags, offering.requirementMode))
            {
                reason = "Not for sale yet.";
                return false;
            }

            if (string.IsNullOrEmpty(offering.outputItemId)
                || _data == null || !_data.TryGet<ItemData>(offering.outputItemId, out var output) || output == null)
            {
                reason = "Unknown goods.";
                return false;
            }

            int outputTotal = Mathf.Max(1, offering.outputQuantity) * quantity;
            if (_inventory == null || _inventory.RoomFor(offering.outputItemId) < outputTotal)
            {
                reason = "You can't carry that many.";
                return false;
            }

            int price = offering.currencyCost * quantity;
            if (price > 0 && (_currency == null || !_currency.CanAfford(price)))
            {
                reason = "Not enough money.";
                return false;
            }

            if (!MaterialsAvailable(offering, quantity, out reason)) return false;

            return true;
        }

        public bool TryBuy(string shopId, string offeringId, int quantity)
        {
            if (!CanBuy(shopId, offeringId, quantity, out var reason))
            {
                Debug.Log($"[JRPG.Economy] Buy '{offeringId}' x{quantity} refused: {reason}");
                return false;
            }

            var offering = GetOffering(shopId, offeringId);
            int price = offering.currencyCost * quantity;

            // Money first, as it can be refused atomically by its owner, so a
            // failure here happens before any material has left the bag.
            if (price > 0 && !_currency.TrySpend(price, CurrencyChangeReason.Shop))
            {
                Debug.LogWarning($"[JRPG.Economy] Buy '{offeringId}': the wallet refused {price} after the preflight passed.");
                return false;
            }

            var paidMaterials = new List<MaterialCost>();
            if (!PayMaterials(offering, quantity, paidMaterials))
            {
                // Put back everything that did leave, in the amounts it left in.
                RefundMaterials(paidMaterials);
                if (price > 0) _currency.Add(price, CurrencyChangeReason.Shop);

                Debug.LogWarning($"[JRPG.Economy] Buy '{offeringId}': materials went missing mid-transaction; rolled back.");
                return false;
            }

            int outputTotal = Mathf.Max(1, offering.outputQuantity) * quantity;
            int overflow = _inventory.Add(offering.outputItemId, outputTotal);

            if (overflow > 0)
            {
                // RoomFor promised the space, so this is an inconsistency.
                // Undo the whole exchange rather than charging for goods not received.
                _inventory.Remove(offering.outputItemId, outputTotal - overflow);
                RefundMaterials(paidMaterials);
                if (price > 0) _currency.Add(price, CurrencyChangeReason.Shop);

                Debug.LogError($"[JRPG.Economy] Buy '{offeringId}': {overflow} of '{offering.outputItemId}' did not " +
                               "fit despite a successful preflight; rolled back.");
                return false;
            }

            _bus?.Publish(new ShopTransactionCompleted(shopId, offeringId, ShopTransactionType.Purchase,
                                                       offering.outputItemId, outputTotal, -price));

            return true;
        }

        public int MaxPurchasable(string shopId, string offeringId)
        {
            var offering = GetOffering(shopId, offeringId);
            if (offering == null || !IsShopAvailable(shopId)) return 0;
            if (!FlagsSatisfied(offering.requiredStoryFlags, offering.requirementMode)) return 0;
            if (_inventory == null) return 0;

            int perLot = Mathf.Max(1, offering.outputQuantity);
            int limit = _inventory.RoomFor(offering.outputItemId) / perLot;

            if (offering.currencyCost > 0)
            {
                int affordable = _currency != null ? _currency.Current / offering.currencyCost : 0;
                limit = Mathf.Min(limit, affordable);
            }

            if (offering.HasMaterialCosts)
            {
                for (int i = 0; i < offering.materialCosts.Count; i++)
                {
                    var cost = offering.materialCosts[i];
                    if (string.IsNullOrEmpty(cost.itemId)) continue;

                    int per = Mathf.Max(1, cost.quantity);
                    limit = Mathf.Min(limit, _inventory.GetQuantity(cost.itemId) / per);
                }
            }

            return Mathf.Max(0, limit);
        }

        public bool CanSell(string shopId, string itemId, int quantity, out string reason)
        {
            reason = null;

            var shop = GetShop(shopId);
            if (shop == null || !IsShopAvailable(shopId))
            {
                reason = "This shop is closed.";
                return false;
            }

            if (!shop.BuysFromPlayer)
            {
                reason = "They don't buy anything.";
                return false;
            }

            // The global prohibitions
            if (_rules != null && !_rules.CanSell(itemId, quantity, out reason)) return false;

            if (_data == null || !_data.TryGet<ItemData>(itemId, out var item) || item == null)
            {
                reason = "Unknown item.";
                return false;
            }

            var accepted = shop.sellRule.acceptedCategories;
            if (accepted != null && accepted.Count > 0 && !accepted.Contains(item.category))
            {
                reason = "They don't deal in those.";
                return false;
            }

            if (UnitSellValue(shop, item) <= 0)
            {
                reason = "It's worth nothing to them.";
                return false;
            }

            return true;
        }

        public bool TrySell(string shopId, string itemId, int quantity)
        {
            if (!CanSell(shopId, itemId, quantity, out var reason))
            {
                Debug.Log($"[JRPG.Economy] Sell '{itemId}' x{quantity} refused: {reason}");
                return false;
            }

            int payment = GetSellValue(shopId, itemId, quantity);

            int removed = _inventory.Remove(itemId, quantity);
            if (removed != quantity)
            {
                // Pay only for what was actually handed over and put back nothing
                Debug.LogWarning($"[JRPG.Economy] Sell '{itemId}': asked for {quantity}, took {removed}.");
                if (removed <= 0) return false;

                payment = GetSellValue(shopId, itemId, removed);
            }

            _currency?.Add(payment, CurrencyChangeReason.Shop);

            _bus?.Publish(new ShopTransactionCompleted(shopId, string.Empty, ShopTransactionType.Sale,
                                                       itemId, removed, payment));

            return true;
        }

        public int GetSellValue(string shopId, string itemId, int quantity)
        {
            if (quantity <= 0) return 0;

            var shop = GetShop(shopId);
            if (shop == null || !shop.BuysFromPlayer) return 0;
            if (_data == null || !_data.TryGet<ItemData>(itemId, out var item) || item == null) return 0;

            return UnitSellValue(shop, item) * quantity;
        }

        // ---- Pricing ------------------------------------------------------------------------------

        /// <summary>
        /// What one unit fetches. <see cref="SellPriceMode.BasePriceMultiplier"/> reads the item's
        /// authored worth. Negative multiplier -> defer to the economy settings.
        /// </summary>
        private int UnitSellValue(ShopData shop, ItemData item)
        {
            var rule = shop.sellRule;
            if (rule == null) return 0;

            if (rule.priceMode == SellPriceMode.FlatOverride) return Mathf.Max(0, rule.flatPrice);

            float multiplier = rule.sellMultiplier;
            if (multiplier < 0f)
                multiplier = _data != null && _data.EconomySettings != null
                    ? _data.EconomySettings.defaultSellMultiplier
                    : 0.5f;

            return Mathf.Max(0, Mathf.FloorToInt(item.basePrice * Mathf.Max(0f, multiplier)));
        }

        // ---- Materials ----------------------------------------------------------------------------

        private bool MaterialsAvailable(ShopOfferingData offering, int quantity, out string reason)
        {
            reason = null;
            if (!offering.HasMaterialCosts) return true;

            for (int i = 0; i < offering.materialCosts.Count; i++)
            {
                var cost = offering.materialCosts[i];
                if (string.IsNullOrEmpty(cost.itemId)) continue;

                int needed = Mathf.Max(1, cost.quantity) * quantity;
                if (_inventory.GetQuantity(cost.itemId) >= needed) continue;

                reason = $"You need {needed} × {MaterialName(cost.itemId)}.";
                return false;
            }

            return true;
        }

        private bool PayMaterials(ShopOfferingData offering, int quantity, List<MaterialCost> paid)
        {
            if (!offering.HasMaterialCosts) return true;

            for (int i = 0; i < offering.materialCosts.Count; i++)
            {
                var cost = offering.materialCosts[i];
                if (string.IsNullOrEmpty(cost.itemId)) continue;

                int needed = Mathf.Max(1, cost.quantity) * quantity;
                int removed = _inventory.Remove(cost.itemId, needed);

                paid.Add(new MaterialCost { itemId = cost.itemId, quantity = removed });

                if (removed != needed) return false;
            }

            return true;
        }

        private void RefundMaterials(List<MaterialCost> paid)
        {
            for (int i = 0; i < paid.Count; i++)
                if (paid[i].quantity > 0) _inventory.Add(paid[i].itemId, paid[i].quantity);
        }

        private string MaterialName(string itemId)
        {
            if (_data != null && _data.TryGet<ItemData>(itemId, out var item) && item != null)
                return string.IsNullOrEmpty(item.displayName) ? item.Id : item.displayName;

            return itemId;
        }

        // ---- Story gates --------------------------------------------------------------------------

        /// Empty is an open gate
        private bool FlagsSatisfied(List<string> flags, RequirementMode mode)
        {
            if (flags == null || flags.Count == 0) return true;

            bool anySet = false;
            int considered = 0;

            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (string.IsNullOrEmpty(flag)) continue;   // an empty entry gates nothing

                considered++;
                bool set = _story == null || _story.GetBool(flag);

                if (mode == RequirementMode.All && !set) return false;
                if (set) anySet = true;
            }

            // A list of blanks is an open gate
            if (considered == 0) return true;

            return mode == RequirementMode.All || anySet;
        }
    }
}
